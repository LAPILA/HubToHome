using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public enum PreparationRunStatus
{
    NotStarted,
    Running,
    RequiresInput,
    Succeeded,
    Blocked,
    Failed,
    Canceled
}

public enum PreparationStepStatus
{
    Applied,
    Skipped,
    InputResolved,
    Blocked,
    Failed,
    Canceled
}

public sealed class PreparationStepResult
{
    public PreparationStepResult(
        string blockId,
        string actionId,
        ActionPreparationPolicy policy,
        PreparationStepStatus status,
        string message = "")
    {
        BlockId = blockId ?? string.Empty;
        ActionId = actionId ?? string.Empty;
        Policy = policy;
        Status = status;
        Message = message ?? string.Empty;
    }

    public string BlockId { get; }
    public string ActionId { get; }
    public ActionPreparationPolicy Policy { get; }
    public PreparationStepStatus Status { get; }
    public string Message { get; }
}

public sealed class PreparationInputRequest
{
    public PreparationInputRequest(
        string blockId,
        string actionId,
        string prompt,
        string valuePath)
    {
        BlockId = blockId ?? string.Empty;
        ActionId = actionId ?? string.Empty;
        Prompt = prompt ?? string.Empty;
        ValuePath = valuePath ?? string.Empty;
    }

    public string BlockId { get; }
    public string ActionId { get; }
    public string Prompt { get; }
    public string ValuePath { get; }
}

public sealed class PreparationRunResult
{
    private readonly List<PreparationStepResult> _steps = new List<PreparationStepResult>();

    public PreparationRunStatus Status { get; internal set; } = PreparationRunStatus.NotStarted;
    public string Message { get; internal set; } = string.Empty;
    public PreparationInputRequest PendingInput { get; internal set; }
    public IReadOnlyList<PreparationStepResult> Steps => _steps;

    internal void AddStep(PreparationStepResult step)
    {
        if (step != null)
        {
            _steps.Add(step);
        }
    }
}

public interface IActionPreparationAdapter
{
    string ActionId { get; }

    PreviewSideEffect SideEffects { get; }

    IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context);
}

public sealed class ActionPreparationContext
{
    private readonly Func<IList<ScenarioActionData>, ActionExecutionContext, IEnumerator> _prepareChildren;
    private readonly Func<ActionSequenceAsset, ActionExecutionContext, IEnumerator> _prepareSequence;

    internal ActionPreparationContext(
        ActionExecutionContext executionContext,
        IPreviewStateScope stateScope,
        Func<IList<ScenarioActionData>, ActionExecutionContext, IEnumerator> prepareChildren,
        Func<ActionSequenceAsset, ActionExecutionContext, IEnumerator> prepareSequence)
    {
        ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        StateScope = stateScope ?? throw new ArgumentNullException(nameof(stateScope));
        _prepareChildren = prepareChildren;
        _prepareSequence = prepareSequence;
    }

    public ActionExecutionContext ExecutionContext { get; }
    public IPreviewStateScope StateScope { get; }
    public bool HasFailed { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool WasSkipped { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public void Fail(string message)
    {
        HasFailed = true;
        Message = string.IsNullOrWhiteSpace(message) ? "Preparation adapter failed." : message.Trim();
    }

    public void Block(string message)
    {
        IsBlocked = true;
        Message = string.IsNullOrWhiteSpace(message) ? "Preparation adapter was blocked." : message.Trim();
    }

    public void Skip(string message = "Presentation skipped during Preparation Run.")
    {
        WasSkipped = true;
        Message = message ?? string.Empty;
    }

    public bool TryTrackState(string key, object participant)
    {
        if (!StateScope.IsSafePreview)
        {
            return true;
        }

        if (!(participant is IPreviewStateParticipant previewParticipant))
        {
            Fail("Safe Preview requires restorable state for '" + key + "'.");
            return false;
        }

        if (!StateScope.TryCapture(key, previewParticipant, out string error))
        {
            Fail(error);
            return false;
        }

        return true;
    }

    public IEnumerator PrepareChildren(IList<ScenarioActionData> actions)
    {
        return _prepareChildren != null
            ? _prepareChildren(actions, ExecutionContext)
            : Empty();
    }

    public IEnumerator PrepareSequence(ActionSequenceAsset sequence, ActionExecutionContext context)
    {
        return _prepareSequence != null
            ? _prepareSequence(sequence, context ?? ExecutionContext)
            : Empty();
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}
