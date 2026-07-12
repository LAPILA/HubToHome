using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class ActionDirector
{
    public const string ParallelActionId = "flow.parallel";

    private readonly ActionAdapterRegistry _registry;

    public ActionDirector(ActionAdapterRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IEnumerator Play(ActionSequenceAsset sequence, ActionExecutionContext context)
    {
        context = context ?? new ActionExecutionContext();
        ActionExecutionSession session = context.GetService<ActionExecutionSession>()
            ?? new ActionExecutionSession();
        var request = new ActionPlayRequest(sequence)
        {
            ParentBlockId = context.ExecutionBlockId
        };
        IEnumerator routine = Play(request, context, session);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    public IEnumerator Play(
        ActionPlayRequest request,
        ActionExecutionContext context,
        ActionExecutionSession session)
    {
        context = context ?? new ActionExecutionContext();
        session = session ?? new ActionExecutionSession();
        context.SetService(session);
        ActionExecutionHandle handle = context.Handle;
        handle.MarkRunning();
        bool rootRun = session.BeginRun(request, handle);
        ActionSequenceAsset sequence = request?.Sequence;
        string sequenceId = sequence != null ? Normalize(sequence.SequenceId) : string.Empty;
        bool sequenceStarted = false;

        try
        {
            if (sequence == null)
            {
                handle.Fail("Action Sequence is missing.");
                yield break;
            }

            session.BeginSequence(sequenceId);
            sequenceStarted = true;
            if (sequence.Actions == null)
            {
                handle.Fail("Action Sequence actions list is missing.");
                yield break;
            }

            ScenarioBlockIdentity.EnsureUnique(sequence.Actions, sequenceId);
            string startBlockId = Normalize(request.StartBlockId);
            if (!string.IsNullOrEmpty(startBlockId) && !ContainsBlock(sequence.Actions, startBlockId))
            {
                handle.Fail("Start Block ID was not found: " + startBlockId);
                yield break;
            }

            if (!SequenceInputBinder.TryEnsureInputs(
                    sequence.Contract != null ? sequence.Contract.Inputs : null,
                    context,
                    out string inputError))
            {
                handle.Fail("Action Sequence input validation failed: " + inputError);
                yield break;
            }

            var gate = new StartBlockGate(startBlockId);
            for (int i = 0; i < sequence.Actions.Count; i++)
            {
                if (handle.IsDone || handle.IsCancellationRequested)
                {
                    break;
                }

                IEnumerator actionRoutine = PlayAction(
                    sequence.Actions[i],
                    context,
                    session,
                    gate,
                    sequenceId,
                    request.ParentBlockId);
                while (actionRoutine.MoveNext())
                {
                    yield return actionRoutine.Current;
                }
            }

            if (handle.IsCancellationRequested)
            {
                handle.MarkCanceled("Action Sequence was canceled.");
            }
            else if (!handle.IsDone)
            {
                handle.MarkSucceeded();
            }
        }
        finally
        {
            if (sequenceStarted)
            {
                session.EndSequence(sequenceId, handle);
            }

            session.EndRun(rootRun, handle);
        }
    }

    private IEnumerator PlayAction(
        ScenarioActionData action,
        ActionExecutionContext context,
        ActionExecutionSession session,
        StartBlockGate gate,
        string sequenceId,
        string parentBlockId)
    {
        ActionExecutionHandle handle = context.Handle;
        if (handle.IsCancellationRequested)
        {
            yield break;
        }

        if (action == null)
        {
            handle.Fail("Scenario action is missing.");
            yield break;
        }

        string blockId = Normalize(action.BlockId);
        string actionId = Normalize(action.ActionId);
        if (!gate.ShouldEnter(action))
        {
            session.SkipBlock(sequenceId, blockId, parentBlockId, actionId, "before_start_block");
            yield break;
        }

        if (action.Disabled)
        {
            session.SkipBlock(sequenceId, blockId, parentBlockId, actionId, "disabled");
            yield break;
        }

        while (!handle.IsDone
            && !handle.IsCancellationRequested
            && !session.CanBeginBlock(parentBlockId))
        {
            yield return null;
        }

        if (handle.IsDone || handle.IsCancellationRequested)
        {
            yield break;
        }

        session.BeginBlock(sequenceId, blockId, parentBlockId, actionId);
        string previousExecutionBlockId = context.ExecutionBlockId;
        context.ExecutionBlockId = blockId;
        if (string.IsNullOrEmpty(actionId))
        {
            handle.Fail("ActionId is required.");
        }
        else if (actionId == ParallelActionId)
        {
            IEnumerator parallelRoutine = PlayParallel(
                action,
                context,
                session,
                gate,
                sequenceId,
                blockId);
            while (parallelRoutine.MoveNext())
            {
                yield return parallelRoutine.Current;
            }
        }
        else if (!_registry.TryGet(actionId, out IActionAdapter adapter))
        {
            handle.Fail("Unknown action id: " + actionId);
        }
        else if (!ScenarioValueResolver.TryResolveAction(
                     action,
                     context,
                     out ScenarioActionData resolvedAction,
                     out string bindingError))
        {
            handle.Fail(bindingError);
        }
        else
        {
            IEnumerator adapterRoutine = adapter.Execute(resolvedAction, context);
            while (adapterRoutine != null
                && !handle.IsDone
                && !handle.IsCancellationRequested)
            {
                while (!handle.IsDone
                    && !handle.IsCancellationRequested
                    && !session.CanAdvanceBlock(blockId))
                {
                    yield return null;
                }

                if (handle.IsDone || handle.IsCancellationRequested)
                {
                    break;
                }

                bool moved;
                try
                {
                    moved = adapterRoutine.MoveNext();
                }
                catch (Exception exception)
                {
                    handle.Fail("Action adapter threw: " + actionId, exception);
                    break;
                }

                if (!moved)
                {
                    break;
                }

                yield return adapterRoutine.Current;
            }
        }

        ActionBlockExecutionStatus status = BlockStatus(handle);
        session.CompleteBlock(
            sequenceId,
            blockId,
            parentBlockId,
            actionId,
            status,
            status == ActionBlockExecutionStatus.Completed ? string.Empty : handle.Result.Message);
        context.ExecutionBlockId = previousExecutionBlockId;
    }

    private IEnumerator PlayParallel(
        ScenarioActionData action,
        ActionExecutionContext context,
        ActionExecutionSession session,
        StartBlockGate gate,
        string sequenceId,
        string parentBlockId)
    {
        ActionExecutionHandle parentHandle = context.Handle;
        if (!TryReadParallelPolicy(action, out ActionParallelPolicy policy, out string policyError))
        {
            parentHandle.Fail(policyError);
            yield break;
        }

        if (action.Children == null || action.Children.Count == 0)
        {
            yield break;
        }

        var routines = new List<ParallelRoutine>();
        for (int i = 0; i < action.Children.Count; i++)
        {
            ScenarioActionData child = action.Children[i];
            if (child == null)
            {
                continue;
            }

            var childHandle = new ActionExecutionHandle(
                context.Handle.ExecutionId + ":parallel:" + Normalize(child.BlockId));
            ActionExecutionContext childContext = context.CreateChild(childHandle);
            IEnumerator childRoutine = PlayParallelChild(
                child,
                childContext,
                session,
                gate,
                sequenceId,
                parentBlockId);
            routines.Add(new ParallelRoutine(childRoutine, childHandle, child.BlockId, session));
        }

        string firstFailure = string.Empty;
        while (routines.Count > 0 && !parentHandle.IsDone && !parentHandle.IsCancellationRequested)
        {
            for (int i = 0; i < routines.Count;)
            {
                ParallelRoutine routine = routines[i];
                if (routine.MoveNext())
                {
                    i++;
                    continue;
                }

                routines.RemoveAt(i);
                if (routine.WasSkipped)
                {
                    continue;
                }

                ActionExecutionStatus childStatus = routine.Handle.Status;
                if (policy == ActionParallelPolicy.All)
                {
                    if (childStatus == ActionExecutionStatus.Failed)
                    {
                        CancelAndDrain(routines, "Parallel sibling failed.");
                        parentHandle.Fail(
                            "Parallel child failed: " + routine.Handle.Result.Message,
                            routine.Handle.Result.Exception);
                        yield break;
                    }

                    if (childStatus == ActionExecutionStatus.Canceled)
                    {
                        CancelAndDrain(routines, "Parallel sibling canceled.");
                        parentHandle.Cancel("Parallel child was canceled: " + routine.Handle.Result.Message);
                        yield break;
                    }

                    continue;
                }

                if (policy == ActionParallelPolicy.Any)
                {
                    if (childStatus == ActionExecutionStatus.Succeeded)
                    {
                        CancelAndDrain(routines, "Parallel any policy completed.");
                        yield break;
                    }

                    if (string.IsNullOrEmpty(firstFailure))
                    {
                        firstFailure = routine.Handle.Result.Message;
                    }

                    continue;
                }

                CancelAndDrain(routines, "Parallel race completed.");
                if (childStatus == ActionExecutionStatus.Failed)
                {
                    parentHandle.Fail(
                        "Parallel race winner failed: " + routine.Handle.Result.Message,
                        routine.Handle.Result.Exception);
                }
                else if (childStatus == ActionExecutionStatus.Canceled)
                {
                    parentHandle.Cancel("Parallel race winner was canceled: " + routine.Handle.Result.Message);
                }

                yield break;
            }

            if (routines.Count > 0)
            {
                yield return null;
            }
        }

        if (parentHandle.IsDone || parentHandle.IsCancellationRequested)
        {
            CancelAndDrain(routines, "Parent parallel action ended.");
            yield break;
        }

        if (policy == ActionParallelPolicy.Any)
        {
            parentHandle.Fail(
                string.IsNullOrWhiteSpace(firstFailure)
                    ? "Parallel any policy had no successful child."
                    : "Parallel any policy had no successful child: " + firstFailure);
        }
    }

    private IEnumerator PlayParallelChild(
        ScenarioActionData action,
        ActionExecutionContext context,
        ActionExecutionSession session,
        StartBlockGate gate,
        string sequenceId,
        string parentBlockId)
    {
        IEnumerator routine = PlayAction(
            action,
            context,
            session,
            gate,
            sequenceId,
            parentBlockId);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        if (!context.Handle.IsDone)
        {
            context.Handle.MarkSucceeded();
        }
    }

    private static void CancelAndDrain(List<ParallelRoutine> routines, string message)
    {
        for (int i = 0; i < routines.Count; i++)
        {
            routines[i].Handle.Cancel(message);
            routines[i].Drain();
        }

        routines.Clear();
    }

    private static bool TryReadParallelPolicy(
        ScenarioActionData action,
        out ActionParallelPolicy policy,
        out string error)
    {
        policy = ActionParallelPolicy.All;
        error = string.Empty;
        JObject parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(action?.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
        }
        catch (Exception exception)
        {
            error = "Parallel action parameters must be a JSON object: " + exception.Message;
            return false;
        }

        string value = parameters.Value<string>("policy")?.Trim().ToLowerInvariant() ?? "all";
        switch (value)
        {
            case "all": policy = ActionParallelPolicy.All; return true;
            case "any": policy = ActionParallelPolicy.Any; return true;
            case "race": policy = ActionParallelPolicy.Race; return true;
            default:
                error = "Unknown parallel completion policy: " + value;
                return false;
        }
    }

    private static ActionBlockExecutionStatus BlockStatus(ActionExecutionHandle handle)
    {
        if (handle == null)
        {
            return ActionBlockExecutionStatus.Failed;
        }

        switch (handle.Status)
        {
            case ActionExecutionStatus.Failed: return ActionBlockExecutionStatus.Failed;
            case ActionExecutionStatus.Canceled: return ActionBlockExecutionStatus.Canceled;
            default: return ActionBlockExecutionStatus.Completed;
        }
    }

    private static bool ContainsBlock(IList<ScenarioActionData> actions, string blockId)
    {
        if (actions == null)
        {
            return false;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            if (ContainsBlock(actions[i], blockId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsBlock(ScenarioActionData action, string blockId)
    {
        if (action == null)
        {
            return false;
        }

        if (Normalize(action.BlockId) == blockId)
        {
            return true;
        }

        return ContainsBlock(action.Children, blockId);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class StartBlockGate
    {
        private readonly string _targetBlockId;

        public StartBlockGate(string targetBlockId)
        {
            _targetBlockId = Normalize(targetBlockId);
            Started = string.IsNullOrEmpty(_targetBlockId);
        }

        public bool Started { get; private set; }

        public bool ShouldEnter(ScenarioActionData action)
        {
            if (Started)
            {
                return true;
            }

            if (Normalize(action?.BlockId) == _targetBlockId)
            {
                Started = true;
                return true;
            }

            return ContainsBlock(action?.Children, _targetBlockId);
        }
    }

    private sealed class ParallelRoutine
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private readonly string _blockId;
        private readonly ActionExecutionSession _session;

        public ParallelRoutine(
            IEnumerator root,
            ActionExecutionHandle handle,
            string blockId,
            ActionExecutionSession session)
        {
            Handle = handle;
            _blockId = Normalize(blockId);
            _session = session;
            if (root != null)
            {
                _stack.Push(root);
            }
        }

        public ActionExecutionHandle Handle { get; }

        public bool WasSkipped
        {
            get
            {
                return _session != null
                    && _session.TryGetBlockStatus(_blockId, out ActionBlockExecutionStatus status)
                    && status == ActionBlockExecutionStatus.Skipped;
            }
        }

        public bool MoveNext()
        {
            try
            {
                return MoveNextCore();
            }
            catch (Exception exception)
            {
                Handle.Fail("Parallel action child threw.", exception);
                return false;
            }
        }

        public void Drain()
        {
            int guard = 0;
            while (MoveNext() && guard++ < 1024)
            {
            }
        }

        private bool MoveNextCore()
        {
            while (_stack.Count > 0)
            {
                IEnumerator current = _stack.Peek();
                if (!current.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    _stack.Push(nested);
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
