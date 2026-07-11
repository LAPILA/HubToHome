using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class PreparationRun
{
    private readonly ActionCatalogAsset _catalog;
    private readonly ActionPreparationRegistry _registry;
    private readonly HashSet<ActionSequenceAsset> _activeSequences = new HashSet<ActionSequenceAsset>();
    private readonly List<string> _sequencePath = new List<string>();
    private bool _isExecuting;
    private bool _cancelRequested;
    private bool _inputProvided;
    private JToken _providedInput;
    private IPreviewStateScope _scope;
    private ActionExecutionContext _rootContext;

    public PreparationRun(ActionCatalogAsset catalog, ActionPreparationRegistry registry)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public PreparationRunResult Result { get; private set; } = new PreparationRunResult();

    public bool TryProvideInput(JToken value)
    {
        if (!_isExecuting
            || Result.Status != PreparationRunStatus.RequiresInput
            || Result.PendingInput == null)
        {
            return false;
        }

        _providedInput = value == null ? JValue.CreateNull() : value.DeepClone();
        _inputProvided = true;
        return true;
    }

    public void Cancel()
    {
        if (_isExecuting)
        {
            _cancelRequested = true;
        }
    }

    public IEnumerator PrepareBefore(
        ActionSequenceAsset sequence,
        string startBlockId,
        ActionExecutionContext context,
        IPreviewStateScope scope)
    {
        if (_isExecuting)
        {
            throw new InvalidOperationException("Preparation Run is already executing.");
        }

        Result = new PreparationRunResult
        {
            Status = PreparationRunStatus.Running
        };
        _isExecuting = true;
        _cancelRequested = false;
        _inputProvided = false;
        _providedInput = null;
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _rootContext = context ?? new ActionExecutionContext();
        _activeSequences.Clear();
        _sequencePath.Clear();

        var preparationHandle = new ActionExecutionHandle("preparation:" + Normalize(sequence?.SequenceId));
        ActionExecutionContext preparationContext = _rootContext.CreateExecutionScope(preparationHandle);
        try
        {
            if (_scope.IsSafePreview
                && !_rootContext.TryGetService(out IPreviewExecutionContextMarker _))
            {
                Stop(
                    PreparationRunStatus.Blocked,
                    "Safe Preview requires a detached preview execution context.");
                yield break;
            }

            if (sequence == null)
            {
                Stop(PreparationRunStatus.Failed, "Preparation Run requires an Action Sequence.");
                yield break;
            }

            if (sequence.Actions == null)
            {
                Stop(PreparationRunStatus.Failed, "Action Sequence actions list is missing.");
                yield break;
            }

            string targetBlockId = Normalize(startBlockId);
            if (string.IsNullOrEmpty(targetBlockId))
            {
                Result.Status = PreparationRunStatus.Succeeded;
                yield break;
            }

            if (!TryValidateBlockIds(sequence.Actions, out string blockIdError))
            {
                Stop(PreparationRunStatus.Failed, blockIdError);
                yield break;
            }

            if (!ContainsBlock(sequence.Actions, targetBlockId))
            {
                Stop(
                    PreparationRunStatus.Failed,
                    "Preparation start Block ID was not found: " + targetBlockId);
                yield break;
            }

            if (!SequenceInputBinder.TryEnsureInputs(
                    sequence.Contract != null ? sequence.Contract.Inputs : null,
                    preparationContext,
                    out string inputError))
            {
                Stop(
                    PreparationRunStatus.Failed,
                    "Preparation input validation failed: " + inputError);
                yield break;
            }

            EnterSequence(sequence);
            for (int i = 0; i < sequence.Actions.Count; i++)
            {
                ScenarioActionData action = sequence.Actions[i];
                if (ContainsBlock(action, targetBlockId))
                {
                    break;
                }

                IEnumerator routine = PrepareAction(action, preparationContext);
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }

                if (IsTerminal())
                {
                    yield break;
                }
            }

            if (_cancelRequested)
            {
                Stop(PreparationRunStatus.Canceled, "Preparation Run was canceled.");
                yield break;
            }

            _rootContext.ModuleId = preparationContext.ModuleId;
            Result.Status = PreparationRunStatus.Succeeded;
            Result.Message = string.Empty;
        }
        finally
        {
            if (Result.Status == PreparationRunStatus.Running
                || Result.Status == PreparationRunStatus.RequiresInput)
            {
                Stop(
                    PreparationRunStatus.Canceled,
                    "Preparation Run was interrupted before completion.");
            }

            if (sequence != null)
            {
                ExitSequence(sequence);
            }

            _isExecuting = false;
            Result.PendingInput = null;
        }
    }

    private IEnumerator PrepareActions(
        IList<ScenarioActionData> actions,
        ActionExecutionContext context)
    {
        if (actions == null)
        {
            yield break;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            IEnumerator routine = PrepareAction(actions[i], context);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }

            if (IsTerminal())
            {
                yield break;
            }
        }
    }

    private IEnumerator PrepareNestedSequence(
        ActionSequenceAsset sequence,
        ActionExecutionContext context)
    {
        if (sequence == null)
        {
            Stop(PreparationRunStatus.Failed, "Preparation sequence call target is missing.");
            yield break;
        }

        if (_activeSequences.Contains(sequence))
        {
            string sequenceId = Normalize(sequence.SequenceId);
            var path = new List<string>(_sequencePath) { sequenceId };
            Stop(
                PreparationRunStatus.Failed,
                "Preparation sequence call cycle detected: " + string.Join(" -> ", path));
            yield break;
        }

        if (sequence.Actions == null)
        {
            Stop(
                PreparationRunStatus.Failed,
                "Called Action Sequence actions list is missing: " + Normalize(sequence.SequenceId));
            yield break;
        }

        if (!TryValidateBlockIds(sequence.Actions, out string blockIdError))
        {
            Stop(
                PreparationRunStatus.Failed,
                "Called Action Sequence has invalid Block IDs: " + blockIdError);
            yield break;
        }

        if (!SequenceInputBinder.TryEnsureInputs(
                sequence.Contract != null ? sequence.Contract.Inputs : null,
                context,
                out string inputError))
        {
            Stop(
                PreparationRunStatus.Failed,
                "Called Action Sequence input validation failed: " + inputError);
            yield break;
        }

        EnterSequence(sequence);
        try
        {
            IEnumerator routine = PrepareActions(sequence.Actions, context);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
        }
        finally
        {
            ExitSequence(sequence);
        }
    }

    private IEnumerator PrepareAction(
        ScenarioActionData action,
        ActionExecutionContext context)
    {
        if (_cancelRequested)
        {
            Stop(PreparationRunStatus.Canceled, "Preparation Run was canceled.");
            yield break;
        }

        if (action == null)
        {
            Stop(PreparationRunStatus.Failed, "Preparation encountered a missing Action block.");
            yield break;
        }

        string blockId = Normalize(action.BlockId);
        string actionId = Normalize(action.ActionId);
        if (action.Disabled)
        {
            Result.AddStep(new PreparationStepResult(
                blockId,
                actionId,
                ActionPreparationPolicy.SkipPresentation,
                PreparationStepStatus.Skipped,
                "Disabled block."));
            yield break;
        }

        ActionCatalogEntry entry = _catalog.FindById(actionId);
        if (entry == null)
        {
            Block(
                blockId,
                actionId,
                ActionPreparationPolicy.Unsupported,
                "Action Library entry is missing for block '" + blockId + "': " + actionId);
            yield break;
        }

        if (!ScenarioValueResolver.TryResolveAction(
                action,
                context,
                out ScenarioActionData resolvedAction,
                out string bindingError))
        {
            Fail(blockId, actionId, entry.PreparationPolicy, bindingError);
            yield break;
        }

        ActionPreparationPolicy policy = entry.PreparationPolicy;
        if (policy == ActionPreparationPolicy.Unsupported)
        {
            Block(
                blockId,
                actionId,
                policy,
                "Block '" + blockId + "' does not support Preparation Run: " + actionId);
            yield break;
        }

        if (policy == ActionPreparationPolicy.SkipPresentation)
        {
            Result.AddStep(new PreparationStepResult(
                blockId,
                actionId,
                policy,
                PreparationStepStatus.Skipped,
                "Presentation skipped."));
            yield break;
        }

        if (!_registry.TryGet(actionId, out IActionPreparationAdapter adapter))
        {
            if (policy == ActionPreparationPolicy.RequireInput)
            {
                IEnumerator inputRoutine = ResolveRequiredInput(resolvedAction, context, policy);
                while (inputRoutine.MoveNext())
                {
                    yield return inputRoutine.Current;
                }

                yield break;
            }

            Block(
                blockId,
                actionId,
                policy,
                "Preparation adapter is missing for block '" + blockId + "': " + actionId);
            yield break;
        }

        if (!_scope.TryAuthorize(adapter.SideEffects, blockId, actionId, out string authorizationError))
        {
            Block(blockId, actionId, policy, authorizationError);
            yield break;
        }

        var preparationContext = new ActionPreparationContext(
            context,
            _scope,
            PrepareActions,
            PrepareNestedSequence);
        IEnumerator adapterRoutine;
        try
        {
            adapterRoutine = adapter.Prepare(resolvedAction, preparationContext);
        }
        catch (Exception exception)
        {
            Fail(
                blockId,
                actionId,
                policy,
                "Preparation adapter failed to start: " + exception.Message);
            yield break;
        }

        while (adapterRoutine != null && !_cancelRequested)
        {
            bool moved;
            try
            {
                moved = adapterRoutine.MoveNext();
            }
            catch (Exception exception)
            {
                Fail(
                    blockId,
                    actionId,
                    policy,
                    "Preparation adapter threw: " + exception.Message);
                yield break;
            }

            if (!moved)
            {
                break;
            }

            yield return adapterRoutine.Current;
        }

        if (_cancelRequested)
        {
            Result.AddStep(new PreparationStepResult(
                blockId,
                actionId,
                policy,
                PreparationStepStatus.Canceled,
                "Preparation canceled."));
            Stop(PreparationRunStatus.Canceled, "Preparation Run was canceled.");
            yield break;
        }

        if (IsTerminal())
        {
            yield break;
        }

        if (preparationContext.IsBlocked)
        {
            Block(blockId, actionId, policy, preparationContext.Message);
            yield break;
        }

        if (preparationContext.HasFailed || context.Handle.Status == ActionExecutionStatus.Failed)
        {
            string message = preparationContext.HasFailed
                ? preparationContext.Message
                : context.Handle.Result.Message;
            Fail(blockId, actionId, policy, message);
            yield break;
        }

        Result.AddStep(new PreparationStepResult(
            blockId,
            actionId,
            policy,
            preparationContext.WasSkipped
                ? PreparationStepStatus.Skipped
                : PreparationStepStatus.Applied,
            preparationContext.Message));
    }

    private IEnumerator ResolveRequiredInput(
        ScenarioActionData action,
        ActionExecutionContext context,
        ActionPreparationPolicy policy)
    {
        string blockId = Normalize(action.BlockId);
        string actionId = Normalize(action.ActionId);
        JObject parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(action.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
        }
        catch (Exception exception)
        {
            Fail(
                blockId,
                actionId,
                policy,
                "Preparation input parameters are invalid: " + exception.Message);
            yield break;
        }

        string valuePath = "preview.input." + blockId;
        JToken previewDefault = parameters["previewDefault"];
        if (previewDefault != null)
        {
            context.SetValue(valuePath, previewDefault);
            Result.AddStep(new PreparationStepResult(
                blockId,
                actionId,
                policy,
                PreparationStepStatus.InputResolved,
                "Preview default used."));
            yield break;
        }

        _inputProvided = false;
        _providedInput = null;
        Result.PendingInput = new PreparationInputRequest(
            blockId,
            actionId,
            "Preparation Run requires a preview value for '" + actionId + "'.",
            valuePath);
        Result.Status = PreparationRunStatus.RequiresInput;
        while (!_inputProvided && !_cancelRequested)
        {
            yield return null;
        }

        if (_cancelRequested)
        {
            Stop(PreparationRunStatus.Canceled, "Preparation Run was canceled while waiting for input.");
            yield break;
        }

        context.SetValue(valuePath, _providedInput);
        Result.AddStep(new PreparationStepResult(
            blockId,
            actionId,
            policy,
            PreparationStepStatus.InputResolved,
            "Preview input supplied."));
        Result.PendingInput = null;
        Result.Status = PreparationRunStatus.Running;
    }

    private void Block(
        string blockId,
        string actionId,
        ActionPreparationPolicy policy,
        string message)
    {
        Result.AddStep(new PreparationStepResult(
            blockId,
            actionId,
            policy,
            PreparationStepStatus.Blocked,
            message));
        Stop(PreparationRunStatus.Blocked, message);
    }

    private void Fail(
        string blockId,
        string actionId,
        ActionPreparationPolicy policy,
        string message)
    {
        Result.AddStep(new PreparationStepResult(
            blockId,
            actionId,
            policy,
            PreparationStepStatus.Failed,
            message));
        Stop(PreparationRunStatus.Failed, message);
    }

    private void Stop(PreparationRunStatus status, string message)
    {
        if (IsTerminal())
        {
            return;
        }

        Result.Status = status;
        Result.Message = message ?? string.Empty;
        if (status == PreparationRunStatus.Blocked
            || status == PreparationRunStatus.Failed
            || status == PreparationRunStatus.Canceled)
        {
            _scope?.Restore();
        }
    }

    private bool IsTerminal()
    {
        return Result.Status == PreparationRunStatus.Blocked
            || Result.Status == PreparationRunStatus.Failed
            || Result.Status == PreparationRunStatus.Canceled;
    }

    private void EnterSequence(ActionSequenceAsset sequence)
    {
        if (sequence == null || _activeSequences.Contains(sequence))
        {
            return;
        }

        _activeSequences.Add(sequence);
        _sequencePath.Add(Normalize(sequence.SequenceId));
    }

    private void ExitSequence(ActionSequenceAsset sequence)
    {
        if (sequence == null || !_activeSequences.Remove(sequence))
        {
            return;
        }

        if (_sequencePath.Count > 0)
        {
            _sequencePath.RemoveAt(_sequencePath.Count - 1);
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

    private static bool TryValidateBlockIds(
        IList<ScenarioActionData> actions,
        out string error)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        return TryValidateBlockIds(actions, ids, "actions", out error);
    }

    private static bool TryValidateBlockIds(
        IList<ScenarioActionData> actions,
        HashSet<string> ids,
        string path,
        out string error)
    {
        if (actions == null)
        {
            error = string.Empty;
            return true;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            string actionPath = path + "[" + i + "]";
            if (action == null)
            {
                error = "Preparation encountered a missing Action block at " + actionPath + ".";
                return false;
            }

            string blockId = Normalize(action.BlockId);
            if (string.IsNullOrEmpty(blockId))
            {
                error = "Preparation requires an existing Block ID at " + actionPath + ".";
                return false;
            }

            if (!ids.Add(blockId))
            {
                error = "Preparation found a duplicate Block ID: " + blockId;
                return false;
            }

            if (!TryValidateBlockIds(action.Children, ids, actionPath + ".children", out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ContainsBlock(ScenarioActionData action, string blockId)
    {
        if (action == null)
        {
            return false;
        }

        return Normalize(action.BlockId) == blockId
            || ContainsBlock(action.Children, blockId);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
