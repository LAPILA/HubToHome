using System;

public enum ActionExecutionEventType
{
    SessionStarted,
    SessionCompleted,
    SessionFailed,
    SessionCanceled,
    SequenceStarted,
    SequenceCompleted,
    SequenceFailed,
    SequenceCanceled,
    BlockStarted,
    BlockCompleted,
    BlockFailed,
    BlockCanceled,
    BlockSkipped,
    Paused,
    Resumed,
    StepRequested
}

public enum ActionBlockExecutionStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Canceled,
    Skipped
}

public sealed class ActionExecutionEvent
{
    public ActionExecutionEvent(
        long order,
        ActionExecutionEventType eventType,
        string sequenceId = "",
        string blockId = "",
        string parentBlockId = "",
        string actionId = "",
        string message = "")
    {
        Order = order;
        EventType = eventType;
        SequenceId = sequenceId ?? string.Empty;
        BlockId = blockId ?? string.Empty;
        ParentBlockId = parentBlockId ?? string.Empty;
        ActionId = actionId ?? string.Empty;
        Message = message ?? string.Empty;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public long Order { get; }
    public ActionExecutionEventType EventType { get; }
    public string SequenceId { get; }
    public string BlockId { get; }
    public string ParentBlockId { get; }
    public string ActionId { get; }
    public string Message { get; }
    public DateTime OccurredAtUtc { get; }
}
