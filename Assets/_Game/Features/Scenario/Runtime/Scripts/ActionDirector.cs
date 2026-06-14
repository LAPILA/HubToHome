using System;
using System.Collections;
using System.Collections.Generic;

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
        ActionExecutionHandle handle = context.Handle;
        handle.MarkRunning();

        if (sequence == null)
        {
            handle.Fail("Action Sequence is missing.");
            yield break;
        }

        if (sequence.Actions == null)
        {
            handle.Fail("Action Sequence actions list is missing.");
            yield break;
        }

        for (int i = 0; i < sequence.Actions.Count; i++)
        {
            if (handle.IsDone || handle.IsCancellationRequested)
            {
                break;
            }

            IEnumerator routine = PlayAction(sequence.Actions[i], context);
            while (!handle.IsDone && routine.MoveNext())
            {
                yield return routine.Current;
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

    private IEnumerator PlayAction(ScenarioActionData action, ActionExecutionContext context)
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

        if (action.Disabled)
        {
            yield break;
        }

        string actionId = Normalize(action.ActionId);
        if (string.IsNullOrEmpty(actionId))
        {
            handle.Fail("ActionId is required.");
            yield break;
        }

        if (actionId == ParallelActionId)
        {
            IEnumerator parallelRoutine = PlayParallel(action, context);
            while (!handle.IsDone && parallelRoutine.MoveNext())
            {
                yield return parallelRoutine.Current;
            }

            yield break;
        }

        IActionAdapter adapter;
        if (!_registry.TryGet(actionId, out adapter))
        {
            handle.Fail("Unknown action id: " + actionId);
            yield break;
        }

        IEnumerator adapterRoutine = adapter.Execute(action, context);
        if (adapterRoutine == null)
        {
            yield break;
        }

        while (!handle.IsDone && !handle.IsCancellationRequested)
        {
            bool moved;
            try
            {
                moved = adapterRoutine.MoveNext();
            }
            catch (Exception exception)
            {
                handle.Fail("Action adapter threw: " + actionId, exception);
                yield break;
            }

            if (!moved)
            {
                yield break;
            }

            yield return adapterRoutine.Current;
        }
    }

    private IEnumerator PlayParallel(ScenarioActionData action, ActionExecutionContext context)
    {
        ActionExecutionHandle handle = context.Handle;
        if (action.Children == null || action.Children.Count == 0)
        {
            yield break;
        }

        var routines = new List<ParallelRoutine>();
        for (int i = 0; i < action.Children.Count; i++)
        {
            ScenarioActionData child = action.Children[i];
            if (child == null || child.Disabled)
            {
                continue;
            }

            routines.Add(new ParallelRoutine(PlayAction(child, context)));
        }

        while (routines.Count > 0 && !handle.IsDone && !handle.IsCancellationRequested)
        {
            for (int i = 0; i < routines.Count;)
            {
                if (!routines[i].MoveNext(handle))
                {
                    routines.RemoveAt(i);
                    if (handle.IsDone)
                    {
                        break;
                    }

                    continue;
                }

                i++;
            }

            yield return null;
        }
    }

    private static string Normalize(string actionId)
    {
        return string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
    }

    private sealed class ParallelRoutine
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();

        public ParallelRoutine(IEnumerator root)
        {
            if (root != null)
            {
                _stack.Push(root);
            }
        }

        public bool MoveNext(ActionExecutionHandle handle)
        {
            try
            {
                return MoveNextCore();
            }
            catch (Exception exception)
            {
                handle.Fail("Parallel action child threw.", exception);
                return false;
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

                IEnumerator nested = current.Current as IEnumerator;
                if (nested != null)
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
