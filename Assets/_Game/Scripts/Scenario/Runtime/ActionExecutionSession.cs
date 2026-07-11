using System;
using System.Collections.Generic;

public sealed class ActionExecutionSession
{
    private readonly List<ActionExecutionEvent> _events = new List<ActionExecutionEvent>();
    private readonly Dictionary<string, ActionBlockExecutionStatus> _blockStates =
        new Dictionary<string, ActionBlockExecutionStatus>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _activeParents =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<string> _activeOrder = new List<string>();
    private readonly List<string> _activeSequences = new List<string>();
    private long _nextOrder;
    private int _stepBudget;
    private string _activeStepRootId = string.Empty;
    private bool _rootEntered;

    public event Action<ActionExecutionEvent> EventRaised;

    public IReadOnlyList<ActionExecutionEvent> Events => _events;
    public bool IsPaused { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public ActionExecutionHandle RootHandle { get; private set; }

    public string CurrentBlockId
    {
        get { return _activeOrder.Count > 0 ? _activeOrder[_activeOrder.Count - 1] : string.Empty; }
    }

    public string CurrentSequenceId
    {
        get { return _activeSequences.Count > 0 ? _activeSequences[_activeSequences.Count - 1] : string.Empty; }
    }

    public void Pause()
    {
        if (IsPaused || IsCompleted)
        {
            return;
        }

        IsPaused = true;
        Raise(ActionExecutionEventType.Paused, message: "Execution paused.");
    }

    public void Resume()
    {
        if (!IsPaused || IsCompleted)
        {
            return;
        }

        IsPaused = false;
        _stepBudget = 0;
        _activeStepRootId = string.Empty;
        Raise(ActionExecutionEventType.Resumed, message: "Execution resumed.");
    }

    public void Step()
    {
        if (IsCompleted)
        {
            return;
        }

        IsPaused = true;
        string current = CurrentBlockId;
        if (!string.IsNullOrEmpty(current))
        {
            _activeStepRootId = FindActiveRoot(current);
        }
        else
        {
            _stepBudget++;
        }

        Raise(
            ActionExecutionEventType.StepRequested,
            blockId: _activeStepRootId,
            message: "One block step requested.");
    }

    public void Cancel(string message = "Execution canceled by user.")
    {
        RootHandle?.Cancel(message);
    }

    public bool TryGetBlockStatus(string blockId, out ActionBlockExecutionStatus status)
    {
        return _blockStates.TryGetValue(Normalize(blockId), out status);
    }

    internal bool BeginRun(ActionPlayRequest request, ActionExecutionHandle handle)
    {
        bool isRoot = !_rootEntered;
        if (!isRoot)
        {
            return false;
        }

        _rootEntered = true;
        RootHandle = handle;
        IsRunning = true;
        IsCompleted = false;
        Raise(
            ActionExecutionEventType.SessionStarted,
            sequenceId: request?.Sequence?.SequenceId,
            message: request?.Label);
        return true;
    }

    internal void EndRun(bool wasRoot, ActionExecutionHandle handle)
    {
        if (!wasRoot)
        {
            return;
        }

        IsRunning = false;
        IsCompleted = true;
        ActionExecutionStatus status = handle != null ? handle.Status : ActionExecutionStatus.Failed;
        ActionExecutionEventType eventType;
        switch (status)
        {
            case ActionExecutionStatus.Succeeded:
                eventType = ActionExecutionEventType.SessionCompleted;
                break;
            case ActionExecutionStatus.Canceled:
                eventType = ActionExecutionEventType.SessionCanceled;
                break;
            default:
                eventType = ActionExecutionEventType.SessionFailed;
                break;
        }

        Raise(eventType, message: handle?.Result?.Message);
    }

    internal void BeginSequence(string sequenceId)
    {
        string normalized = Normalize(sequenceId);
        _activeSequences.Add(normalized);
        Raise(ActionExecutionEventType.SequenceStarted, sequenceId: normalized);
    }

    internal void EndSequence(string sequenceId, ActionExecutionHandle handle)
    {
        string normalized = Normalize(sequenceId);
        for (int i = _activeSequences.Count - 1; i >= 0; i--)
        {
            if (_activeSequences[i] == normalized)
            {
                _activeSequences.RemoveAt(i);
                break;
            }
        }

        ActionExecutionEventType eventType;
        switch (handle != null ? handle.Status : ActionExecutionStatus.Failed)
        {
            case ActionExecutionStatus.Succeeded:
                eventType = ActionExecutionEventType.SequenceCompleted;
                break;
            case ActionExecutionStatus.Canceled:
                eventType = ActionExecutionEventType.SequenceCanceled;
                break;
            default:
                eventType = ActionExecutionEventType.SequenceFailed;
                break;
        }

        Raise(eventType, sequenceId: normalized, message: handle?.Result?.Message);
    }

    internal bool CanBeginBlock(string parentBlockId)
    {
        if (!IsPaused)
        {
            return true;
        }

        if (_stepBudget > 0)
        {
            return true;
        }

        return IsInsideStepScope(parentBlockId);
    }

    internal void BeginBlock(
        string sequenceId,
        string blockId,
        string parentBlockId,
        string actionId)
    {
        string normalized = Normalize(blockId);
        string parent = Normalize(parentBlockId);
        if (IsPaused && _stepBudget > 0 && string.IsNullOrEmpty(_activeStepRootId))
        {
            _stepBudget--;
            _activeStepRootId = normalized;
        }

        _blockStates[normalized] = ActionBlockExecutionStatus.Running;
        _activeParents[normalized] = parent;
        _activeOrder.Remove(normalized);
        _activeOrder.Add(normalized);
        Raise(ActionExecutionEventType.BlockStarted, sequenceId, normalized, parent, actionId);
    }

    internal bool CanAdvanceBlock(string blockId)
    {
        return !IsPaused || IsInsideStepScope(blockId);
    }

    internal void CompleteBlock(
        string sequenceId,
        string blockId,
        string parentBlockId,
        string actionId,
        ActionBlockExecutionStatus status,
        string message = "")
    {
        string normalized = Normalize(blockId);
        _blockStates[normalized] = status;
        _activeParents.Remove(normalized);
        _activeOrder.Remove(normalized);
        ActionExecutionEventType eventType;
        switch (status)
        {
            case ActionBlockExecutionStatus.Failed:
                eventType = ActionExecutionEventType.BlockFailed;
                break;
            case ActionBlockExecutionStatus.Canceled:
                eventType = ActionExecutionEventType.BlockCanceled;
                break;
            case ActionBlockExecutionStatus.Skipped:
                eventType = ActionExecutionEventType.BlockSkipped;
                break;
            default:
                eventType = ActionExecutionEventType.BlockCompleted;
                break;
        }

        Raise(eventType, sequenceId, normalized, parentBlockId, actionId, message);
        if (_activeStepRootId == normalized)
        {
            _activeStepRootId = string.Empty;
        }
    }

    internal void SkipBlock(
        string sequenceId,
        string blockId,
        string parentBlockId,
        string actionId,
        string reason)
    {
        string normalized = Normalize(blockId);
        _blockStates[normalized] = ActionBlockExecutionStatus.Skipped;
        Raise(
            ActionExecutionEventType.BlockSkipped,
            sequenceId,
            normalized,
            parentBlockId,
            actionId,
            reason);
    }

    private bool IsInsideStepScope(string blockId)
    {
        string current = Normalize(blockId);
        while (!string.IsNullOrEmpty(current))
        {
            if (current == _activeStepRootId)
            {
                return true;
            }

            if (!_activeParents.TryGetValue(current, out current))
            {
                return false;
            }
        }

        return false;
    }

    private string FindActiveRoot(string blockId)
    {
        string current = Normalize(blockId);
        while (_activeParents.TryGetValue(current, out string parent)
            && !string.IsNullOrEmpty(parent)
            && _activeParents.ContainsKey(parent))
        {
            current = parent;
        }

        return current;
    }

    private void Raise(
        ActionExecutionEventType eventType,
        string sequenceId = "",
        string blockId = "",
        string parentBlockId = "",
        string actionId = "",
        string message = "")
    {
        var executionEvent = new ActionExecutionEvent(
            ++_nextOrder,
            eventType,
            sequenceId,
            blockId,
            parentBlockId,
            actionId,
            message);
        _events.Add(executionEvent);
        EventRaised?.Invoke(executionEvent);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
