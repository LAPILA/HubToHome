using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum SequencePuzzleInputStatus
{
    InvalidConfiguration,
    Advanced,
    Incorrect,
    ResetPending,
    Completed,
    AlreadyCompleted
}

public readonly struct SequencePuzzleInputResult
{
    public SequencePuzzleInputResult(
        SequencePuzzleInputStatus status,
        int currentStep,
        int totalSteps,
        int resetGeneration)
    {
        Status = status;
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
        ResetGeneration = resetGeneration;
    }

    public SequencePuzzleInputStatus Status { get; }
    public int CurrentStep { get; }
    public int TotalSteps { get; }
    public int ResetGeneration { get; }
}

/// <summary>
/// Pure sequence state. Delayed work is owned by SequencePuzzleController.
/// </summary>
public sealed class SequencePuzzleProgress
{
    private readonly string[] _orderedNodeIds;
    private int _resetGeneration;

    public SequencePuzzleProgress(IReadOnlyList<string> orderedNodeIds)
    {
        if (orderedNodeIds == null)
        {
            _orderedNodeIds = Array.Empty<string>();
            return;
        }

        _orderedNodeIds = new string[orderedNodeIds.Count];
        for (int i = 0; i < orderedNodeIds.Count; i++)
            _orderedNodeIds[i] = Normalize(orderedNodeIds[i]);
    }

    public int CurrentStep { get; private set; }
    public int TotalSteps => _orderedNodeIds.Length;
    public int ResetGeneration => _resetGeneration;
    public bool IsResetPending { get; private set; }
    public bool IsCompleted { get; private set; }

    public SequencePuzzleInputResult Submit(string nodeId)
    {
        if (_orderedNodeIds.Length == 0)
            return Result(SequencePuzzleInputStatus.InvalidConfiguration);
        if (IsCompleted)
            return Result(SequencePuzzleInputStatus.AlreadyCompleted);
        if (IsResetPending)
            return Result(SequencePuzzleInputStatus.ResetPending);

        if (string.Equals(
            Normalize(nodeId),
            _orderedNodeIds[CurrentStep],
            StringComparison.Ordinal))
        {
            CurrentStep++;
            if (CurrentStep >= _orderedNodeIds.Length)
            {
                IsCompleted = true;
                IsResetPending = false;
                _resetGeneration++;
                return Result(SequencePuzzleInputStatus.Completed);
            }

            return Result(SequencePuzzleInputStatus.Advanced);
        }

        IsResetPending = true;
        _resetGeneration++;
        return Result(SequencePuzzleInputStatus.Incorrect);
    }

    public bool TryApplyScheduledReset(int generation)
    {
        if (!IsResetPending || generation != _resetGeneration || IsCompleted)
            return false;

        CurrentStep = 0;
        IsResetPending = false;
        _resetGeneration++;
        return true;
    }

    public void RestoreCompleted()
    {
        _resetGeneration++;
        IsResetPending = false;
        IsCompleted = true;
        CurrentStep = _orderedNodeIds.Length;
    }

    public void Restart()
    {
        _resetGeneration++;
        CurrentStep = 0;
        IsResetPending = false;
        IsCompleted = false;
    }

    public void CancelPendingReset(bool resetProgress)
    {
        _resetGeneration++;
        IsResetPending = false;
        if (resetProgress && !IsCompleted)
            CurrentStep = 0;
    }

    private SequencePuzzleInputResult Result(SequencePuzzleInputStatus status)
    {
        return new SequencePuzzleInputResult(
            status,
            CurrentStep,
            TotalSteps,
            _resetGeneration);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[Serializable]
public sealed class SequencePuzzleProgressEvent : UnityEvent<int, int>
{
}

[DisallowMultipleComponent]
public sealed class SequencePuzzleController : MonoBehaviour, IPuzzleRuntime
{
    [TitleGroup("퍼즐 데이터")]
    [SerializeField, Required, LabelText("Sequence Puzzle Definition")]
    private SequencePuzzleDefinition _definition;

    [TitleGroup("완료 시 환경")]
    [SerializeField, LabelText("활성화할 대상")]
    private GameObject[] _activateOnComplete;

    [TitleGroup("완료 시 환경")]
    [SerializeField, LabelText("비활성화할 대상")]
    private GameObject[] _deactivateOnComplete;

    [TitleGroup("이벤트")]
    [SerializeField, LabelText("정답 진행")]
    private SequencePuzzleProgressEvent _onProgress = new SequencePuzzleProgressEvent();

    [TitleGroup("이벤트")]
    [SerializeField, LabelText("오답")]
    private UnityEvent _onIncorrect = new UnityEvent();

    [TitleGroup("이벤트")]
    [SerializeField, LabelText("초기화")]
    private UnityEvent _onReset = new UnityEvent();

    [TitleGroup("이벤트")]
    [SerializeField, LabelText("완료")]
    private UnityEvent _onCompleted = new UnityEvent();

    private SequencePuzzleProgress _progress;
    private Coroutine _resetCoroutine;
    private GlobalDataManager _subscribedGlobal;
    private GlobalDataManager _globalDataSource;

    public SequencePuzzleDefinition Definition => _definition;
    public string PuzzleId => _definition != null ? _definition.PuzzleId : string.Empty;
    public int CurrentStep => _progress?.CurrentStep ?? 0;
    public int TotalSteps => _progress?.TotalSteps ?? 0;
    public int ResetGeneration => _progress?.ResetGeneration ?? 0;
    public bool IsResetPending => _progress != null && _progress.IsResetPending;
    public bool IsCompleted => _progress != null && _progress.IsCompleted;

    public bool CanInteract(PlayerController player)
    {
        return _definition != null;
    }

    public bool TryHandleMarkerInteraction(PlayerController player)
    {
        return false;
    }

    public void Configure(SequencePuzzleDefinition definition)
    {
        _definition = definition;
        RebuildProgress();
        RefreshFromStoredFlag();
    }

    public void SetGlobalDataSource(GlobalDataManager globalData)
    {
        UnsubscribeFromFlags();
        _globalDataSource = globalData;
        SubscribeToFlags();
        RefreshFromStoredFlag();
    }

    public SequencePuzzleInputResult Submit(string nodeId)
    {
        if (!EnsureProgress(out string error))
        {
            Debug.LogWarning($"[SequencePuzzleController] 입력 거부: {error}", this);
            return new SequencePuzzleInputResult(
                SequencePuzzleInputStatus.InvalidConfiguration,
                0,
                0,
                0);
        }

        SequencePuzzleInputResult result = _progress.Submit(nodeId);
        switch (result.Status)
        {
            case SequencePuzzleInputStatus.Advanced:
                _onProgress?.Invoke(result.CurrentStep, result.TotalSteps);
                break;

            case SequencePuzzleInputStatus.Incorrect:
                _onIncorrect?.Invoke();
                ScheduleReset(result.ResetGeneration);
                break;

            case SequencePuzzleInputStatus.Completed:
                CancelResetCoroutine();
                PersistCompletion();
                ApplyCompletionTargets(true);
                _onProgress?.Invoke(result.CurrentStep, result.TotalSteps);
                _onCompleted?.Invoke();
                break;
        }

        return result;
    }

    public bool CompleteScheduledReset(int generation)
    {
        if (_progress == null || !_progress.TryApplyScheduledReset(generation))
            return false;

        _resetCoroutine = null;
        _onReset?.Invoke();
        return true;
    }

    public void RestartProgress()
    {
        CancelResetCoroutine();
        if (_progress == null)
            RebuildProgress();
        _progress?.Restart();
        ApplyCompletionTargets(false);
    }

    public void RefreshFromStoredFlag()
    {
        if (!EnsureProgress(out _))
            return;

        GlobalDataManager global = ResolveGlobalData();
        bool completed = global != null
            && !string.IsNullOrEmpty(_definition.CompletionFlag)
            && global.GetFlag(_definition.CompletionFlag, 0) != 0;

        if (completed)
        {
            CancelResetCoroutine();
            _progress.RestoreCompleted();
            ApplyCompletionTargets(true);
        }
        else if (_progress.IsCompleted)
        {
            _progress.Restart();
            ApplyCompletionTargets(false);
        }
    }

    public bool TryValidate(out string error)
    {
        if (_definition == null)
        {
            error = "SequencePuzzleDefinition이 지정되지 않았습니다.";
            return false;
        }

        if (!_definition.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        SubscribeToFlags();
        if (_progress == null)
            RebuildProgress();
        RefreshFromStoredFlag();
    }

    private void OnDisable()
    {
        StopRuntime();
    }

    public void StopRuntime()
    {
        UnsubscribeFromFlags();
        CancelResetCoroutine();
        _progress?.CancelPendingReset(true);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && _definition != null && !_definition.TryValidate(out string error))
            Debug.LogWarning($"[SequencePuzzleController] {error}", this);
    }

    [TitleGroup("검증")]
    [Button("Controller 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log($"[SequencePuzzleController] 검증 통과: {_definition.PuzzleId}", this);
        else
            Debug.LogError($"[SequencePuzzleController] {error}", this);
    }

    private bool EnsureProgress(out string error)
    {
        if (!TryValidate(out error))
            return false;
        if (_progress == null || _progress.TotalSteps != _definition.OrderedNodeIds.Count)
            RebuildProgress();
        return _progress != null;
    }

    private void RebuildProgress()
    {
        CancelResetCoroutine();
        _progress = _definition != null && _definition.TryValidate(out _)
            ? new SequencePuzzleProgress(_definition.OrderedNodeIds)
            : null;
    }

    private void ScheduleReset(int generation)
    {
        CancelResetCoroutine();
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        _resetCoroutine = StartCoroutine(ResetAfterDelay(
            generation,
            _definition != null ? _definition.IncorrectResetDelay : 0f));
    }

    private IEnumerator ResetAfterDelay(int generation, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return null;

        CompleteScheduledReset(generation);
    }

    private void CancelResetCoroutine()
    {
        if (_resetCoroutine == null)
            return;

        StopCoroutine(_resetCoroutine);
        _resetCoroutine = null;
    }

    private void PersistCompletion()
    {
        GlobalDataManager global = ResolveGlobalData();
        if (global != null && _definition != null && !string.IsNullOrEmpty(_definition.CompletionFlag))
            global.SetFlag(_definition.CompletionFlag, 1);
    }

    private void ApplyCompletionTargets(bool completed)
    {
        SetTargetsActive(_activateOnComplete, completed);
        SetTargetsActive(_deactivateOnComplete, !completed);
    }

    private static void SetTargetsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && targets[i].activeSelf != active)
                targets[i].SetActive(active);
        }
    }

    private void SubscribeToFlags()
    {
        GlobalDataManager global = ResolveGlobalData();
        if (global == null || _subscribedGlobal == global)
            return;

        UnsubscribeFromFlags();
        _subscribedGlobal = global;
        _subscribedGlobal.FlagChanged += HandleFlagChanged;
    }

    private void UnsubscribeFromFlags()
    {
        if (_subscribedGlobal != null)
            _subscribedGlobal.FlagChanged -= HandleFlagChanged;
        _subscribedGlobal = null;
    }

    private GlobalDataManager ResolveGlobalData()
    {
        return _globalDataSource != null ? _globalDataSource : GlobalDataManager.Instance;
    }
    private void HandleFlagChanged(string key, int oldValue, int newValue)
    {
        if (_definition == null
            || !string.Equals(key, _definition.CompletionFlag, StringComparison.Ordinal))
        {
            return;
        }

        if (newValue != 0)
        {
            CancelResetCoroutine();
            _progress?.RestoreCompleted();
            ApplyCompletionTargets(true);
        }
        else
        {
            RestartProgress();
        }
    }
}