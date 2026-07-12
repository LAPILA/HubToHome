using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum SequencePlaybackKind
{
    SafePreview,
    LiveTest
}

public enum SequencePlaybackState
{
    Idle,
    Preparing,
    Running,
    Paused,
    WaitingForInput,
    Succeeded,
    Blocked,
    Failed,
    Canceled
}

public sealed class SequencePlaybackTraceEntry
{
    public SequencePlaybackTraceEntry(
        long order,
        string phase,
        string blockId,
        string actionId,
        string status,
        string message)
    {
        Order = order;
        Phase = phase ?? string.Empty;
        BlockId = blockId ?? string.Empty;
        ActionId = actionId ?? string.Empty;
        Status = status ?? string.Empty;
        Message = message ?? string.Empty;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public long Order { get; }
    public string Phase { get; }
    public string BlockId { get; }
    public string ActionId { get; }
    public string Status { get; }
    public string Message { get; }
    public DateTime OccurredAtUtc { get; }
}

public sealed class SequencePlaybackController : IDisposable
{
    private readonly SequenceLiveContextRegistry _liveContexts;
    private readonly List<SequencePlaybackTraceEntry> _trace =
        new List<SequencePlaybackTraceEntry>();
    private IEnumerator _safeRoutine;
    private PreparationRun _preparation;
    private EditorPreviewStateScope _previewScope;
    private ActionSequenceAsset _temporarySequence;
    private ActionExecutionSession _session;
    private Coroutine _liveCoroutine;
    private MonoBehaviour _liveHost;
    private int _reportedPreparationSteps;
    private bool _safePaused;
    private bool _safeStepRequested;
    private long _nextTraceOrder;
    private bool _disposed;

    public SequencePlaybackController(SequenceLiveContextRegistry liveContexts = null)
    {
        _liveContexts = liveContexts ?? new SequenceLiveContextRegistry();
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public event Action Changed;
    public event Action TraceChanged;

    public SequencePlaybackKind Kind { get; private set; }
    public SequencePlaybackState State { get; private set; } = SequencePlaybackState.Idle;
    public string StatusMessage { get; private set; } = string.Empty;
    public string CurrentBlockId { get; private set; } = string.Empty;
    public IReadOnlyList<SequencePlaybackTraceEntry> Trace => _trace;
    public PreparationInputRequest PendingInput => _preparation?.Result?.PendingInput;
    public bool IsActive => State == SequencePlaybackState.Preparing
        || State == SequencePlaybackState.Running
        || State == SequencePlaybackState.Paused
        || State == SequencePlaybackState.WaitingForInput;
    public bool CanPause => IsActive && State != SequencePlaybackState.WaitingForInput;
    public bool CanStep => State == SequencePlaybackState.Paused;
    public bool CanStop => State != SequencePlaybackState.Idle;

    public bool StartSafePreview(
        BattleScenarioData battle,
        ActionSequenceAsset sequence,
        ActionCatalogAsset catalog,
        string throughBlockId = "")
    {
        Stop();
        ClearTrace();
        if (sequence == null || catalog == null)
        {
            Fail("안전 미리보기에 필요한 시퀀스 또는 Action Library가 없습니다.");
            return false;
        }

        _temporarySequence = BuildPreparationSequence(
            sequence,
            throughBlockId,
            includeTarget: true,
            out string sentinelId);
        if (_temporarySequence == null)
        {
            Fail("선택 블록을 시퀀스에서 찾지 못했습니다: " + throughBlockId);
            return false;
        }

        var sourceContext = new ActionExecutionContext();
        if (battle?.Sequences != null)
        {
            sourceContext.SetService<IActionSequenceResolver>(
                new ActionSequenceListResolver(battle.Sequences));
        }
        else
        {
            sourceContext.SetService<IActionSequenceResolver>(
                new ActionSequenceListResolver(new[] { sequence }));
        }

        ActionExecutionContext context = PreviewActionExecutionContextFactory.Create(sourceContext);
        context.ScenarioId = battle != null ? battle.ScenarioId : sequence.SequenceId;
        context.PrimaryMode = battle != null ? battle.PrimaryMode : "overworld";
        _previewScope = new EditorPreviewStateScope(true);
        _preparation = new PreparationRun(catalog, ActionPreparationRegistry.CreateDefault());
        _safeRoutine = _preparation.PrepareBefore(
            _temporarySequence,
            sentinelId,
            context,
            _previewScope);
        _reportedPreparationSteps = 0;
        _safePaused = false;
        _safeStepRequested = false;
        Kind = SequencePlaybackKind.SafePreview;
        SetState(SequencePlaybackState.Preparing, "안전 미리보기 준비 중");
        AddTrace("preview", string.Empty, string.Empty, "start", "복구 가능한 준비 실행 시작");
        EditorApplication.update += TickSafePreview;
        return true;
    }

    public bool StartLiveTest(
        BattleScenarioData battle,
        ActionSequenceAsset sequence,
        ActionCatalogAsset catalog,
        string startBlockId = "")
    {
        Stop();
        ClearTrace();
        if (!_liveContexts.TryCreate(battle, sequence, out SequenceLiveContext live, out string error))
        {
            Fail(error);
            return false;
        }

        var handle = new ActionExecutionHandle("sequence-maker-live-test");
        ActionExecutionContext context = live.ExecutionContext.CreateExecutionScope(handle);
        _session = new ActionExecutionSession();
        _session.EventRaised += OnExecutionEvent;
        var request = new ActionPlayRequest(sequence)
        {
            StartBlockId = startBlockId ?? string.Empty,
            Label = "Sequence Maker: " + live.Label
        };
        Kind = SequencePlaybackKind.LiveTest;
        _liveHost = live.CoroutineHost;
        IEnumerator liveRoutine = live.Director.Play(request, context, _session);
        if (!string.IsNullOrWhiteSpace(startBlockId))
        {
            if (catalog == null)
            {
                Fail("선택 블록 준비 실행에 필요한 Action Library가 없습니다.");
                CleanupExecution();
                return false;
            }
            _temporarySequence = BuildPreparationSequence(
                sequence,
                startBlockId,
                includeTarget: false,
                out string sentinelId);
            if (_temporarySequence == null)
            {
                Fail("선택 블록을 시퀀스에서 찾지 못했습니다: " + startBlockId);
                CleanupExecution();
                return false;
            }
            _previewScope = new EditorPreviewStateScope(false);
            _preparation = new PreparationRun(catalog, ActionPreparationRegistry.CreateDefault());
            IEnumerator prepareRoutine = _preparation.PrepareBefore(
                _temporarySequence,
                sentinelId,
                context,
                _previewScope);
            _reportedPreparationSteps = 0;
            SetState(SequencePlaybackState.Preparing, "선택 블록 이전 상태를 빠르게 준비 중");
            _liveCoroutine = _liveHost.StartCoroutine(RunLiveAfterPreparation(
                prepareRoutine,
                liveRoutine,
                live.Label));
        }
        else
        {
            SetState(SequencePlaybackState.Running, live.Label + "에서 실동작 테스트 중");
            _liveCoroutine = _liveHost.StartCoroutine(RunLive(liveRoutine));
        }
        return true;
    }

    public void PauseOrResume()
    {
        if (!IsActive)
        {
            return;
        }
        if (Kind == SequencePlaybackKind.LiveTest)
        {
            if (_session == null)
            {
                return;
            }
            if (_session.IsPaused)
            {
                _session.Resume();
                SetState(SequencePlaybackState.Running, "실동작 테스트 계속");
            }
            else
            {
                _session.Pause();
                SetState(SequencePlaybackState.Paused, "실동작 테스트 일시정지");
            }
            return;
        }

        _safePaused = !_safePaused;
        SetState(
            _safePaused ? SequencePlaybackState.Paused : SequencePlaybackState.Preparing,
            _safePaused ? "안전 미리보기 일시정지" : "안전 미리보기 계속");
    }

    public void Step()
    {
        if (State != SequencePlaybackState.Paused)
        {
            return;
        }
        if (Kind == SequencePlaybackKind.LiveTest)
        {
            _session?.Step();
            return;
        }
        _safeStepRequested = true;
    }

    public bool ProvidePreviewInput(Newtonsoft.Json.Linq.JToken value)
    {
        bool accepted = _preparation != null && _preparation.TryProvideInput(value);
        if (accepted)
        {
            SetState(SequencePlaybackState.Preparing, "입력값을 적용해 미리보기 계속");
        }
        return accepted;
    }

    public void Stop()
    {
        EditorApplication.update -= TickSafePreview;
        _preparation?.Cancel();
        _session?.Cancel("Sequence Maker에서 중지했습니다.");
        if (_liveHost != null && _liveCoroutine != null)
        {
            _liveHost.StopCoroutine(_liveCoroutine);
        }
        CleanupExecution();
        if (State != SequencePlaybackState.Idle)
        {
            SetState(SequencePlaybackState.Idle, string.Empty);
        }
    }

    public void ClearTrace()
    {
        _trace.Clear();
        _nextTraceOrder = 0;
        TraceChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Stop();
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        _disposed = true;
    }

    private void TickSafePreview()
    {
        if (_safeRoutine == null || (_safePaused && !_safeStepRequested))
        {
            return;
        }
        _safeStepRequested = false;
        bool moved;
        try
        {
            moved = _safeRoutine.MoveNext();
        }
        catch (Exception exception)
        {
            AddTrace("preview", CurrentBlockId, string.Empty, "failed", exception.Message);
            FinishSafe(SequencePlaybackState.Failed, exception.Message, true);
            return;
        }
        ReportPreparationSteps();
        if (!moved)
        {
            CompleteSafeFromResult();
            return;
        }
        if (_preparation.Result.Status == PreparationRunStatus.RequiresInput)
        {
            SetState(SequencePlaybackState.WaitingForInput, _preparation.Result.PendingInput?.Prompt);
        }
        else if (_safePaused)
        {
            SetState(SequencePlaybackState.Paused, "한 단계 실행 완료");
        }
    }

    private IEnumerator RunLive(IEnumerator routine)
    {
        while (routine != null)
        {
            bool moved;
            try
            {
                moved = routine.MoveNext();
            }
            catch (Exception exception)
            {
                AddTrace("live", CurrentBlockId, string.Empty, "failed", exception.Message);
                SetState(SequencePlaybackState.Failed, exception.Message);
                CleanupExecution(false);
                yield break;
            }
            if (!moved)
            {
                break;
            }
            yield return routine.Current;
        }

        ActionExecutionStatus status = _session?.RootHandle?.Status
            ?? ActionExecutionStatus.Failed;
        SetState(
            status == ActionExecutionStatus.Succeeded
                ? SequencePlaybackState.Succeeded
                : status == ActionExecutionStatus.Canceled
                    ? SequencePlaybackState.Canceled
                    : SequencePlaybackState.Failed,
            _session?.RootHandle?.Result?.Message);
        CleanupExecution(false);
    }

    private IEnumerator RunLiveAfterPreparation(
        IEnumerator preparationRoutine,
        IEnumerator liveRoutine,
        string label)
    {
        while (preparationRoutine != null)
        {
            bool moved;
            try
            {
                moved = preparationRoutine.MoveNext();
            }
            catch (Exception exception)
            {
                AddTrace("preparation", CurrentBlockId, string.Empty, "failed", exception.Message);
                SetState(SequencePlaybackState.Failed, exception.Message);
                CleanupExecution();
                yield break;
            }
            if (!moved)
            {
                break;
            }
            ReportPreparationSteps();
            yield return preparationRoutine.Current;
        }
        ReportPreparationSteps();
        PreparationRunStatus status = _preparation?.Result?.Status
            ?? PreparationRunStatus.Failed;
        if (status != PreparationRunStatus.Succeeded)
        {
            SequencePlaybackState state = status == PreparationRunStatus.Blocked
                ? SequencePlaybackState.Blocked
                : status == PreparationRunStatus.Canceled
                    ? SequencePlaybackState.Canceled
                    : SequencePlaybackState.Failed;
            SetState(state, _preparation?.Result?.Message);
            CleanupExecution();
            yield break;
        }

        DestroyTemporarySequence();
        _preparation = null;
        _previewScope?.Dispose();
        _previewScope = null;
        SetState(SequencePlaybackState.Running, label + "에서 선택 블록 실동작 테스트 중");
        IEnumerator run = RunLive(liveRoutine);
        while (run.MoveNext())
        {
            yield return run.Current;
        }
    }

    private void OnExecutionEvent(ActionExecutionEvent executionEvent)
    {
        if (executionEvent == null)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(executionEvent.BlockId))
        {
            CurrentBlockId = executionEvent.BlockId;
        }
        AddTrace(
            "live",
            executionEvent.BlockId,
            executionEvent.ActionId,
            executionEvent.EventType.ToString(),
            executionEvent.Message);
        Changed?.Invoke();
    }

    private void ReportPreparationSteps()
    {
        IReadOnlyList<PreparationStepResult> steps = _preparation?.Result?.Steps;
        if (steps == null)
        {
            return;
        }
        while (_reportedPreparationSteps < steps.Count)
        {
            PreparationStepResult step = steps[_reportedPreparationSteps++];
            CurrentBlockId = step.BlockId;
            AddTrace(
                "preview",
                step.BlockId,
                step.ActionId,
                step.Status.ToString(),
                step.Message);
        }
    }

    private void CompleteSafeFromResult()
    {
        PreparationRunResult result = _preparation?.Result;
        SequencePlaybackState state;
        switch (result?.Status ?? PreparationRunStatus.Failed)
        {
            case PreparationRunStatus.Succeeded: state = SequencePlaybackState.Succeeded; break;
            case PreparationRunStatus.Blocked: state = SequencePlaybackState.Blocked; break;
            case PreparationRunStatus.Canceled: state = SequencePlaybackState.Canceled; break;
            default: state = SequencePlaybackState.Failed; break;
        }
        FinishSafe(state, result?.Message, state != SequencePlaybackState.Succeeded);
    }

    private void FinishSafe(SequencePlaybackState state, string message, bool restore)
    {
        EditorApplication.update -= TickSafePreview;
        _safeRoutine = null;
        if (restore)
        {
            _previewScope?.Restore();
        }
        DestroyTemporarySequence();
        SetState(state, message);
    }

    private void CleanupExecution(bool restorePreview = true)
    {
        if (_session != null)
        {
            _session.EventRaised -= OnExecutionEvent;
        }
        _session = null;
        _liveCoroutine = null;
        _liveHost = null;
        _safeRoutine = null;
        _preparation = null;
        if (restorePreview)
        {
            _previewScope?.Dispose();
            _previewScope = null;
        }
        DestroyTemporarySequence();
        CurrentBlockId = string.Empty;
    }

    private void DestroyTemporarySequence()
    {
        if (_temporarySequence != null)
        {
            UnityEngine.Object.DestroyImmediate(_temporarySequence);
            _temporarySequence = null;
        }
    }

    private void Fail(string message)
    {
        AddTrace("system", string.Empty, string.Empty, "failed", message);
        SetState(SequencePlaybackState.Failed, message);
    }

    private void SetState(SequencePlaybackState state, string message)
    {
        State = state;
        StatusMessage = message ?? string.Empty;
        Changed?.Invoke();
    }

    private void AddTrace(
        string phase,
        string blockId,
        string actionId,
        string status,
        string message)
    {
        _trace.Add(new SequencePlaybackTraceEntry(
            ++_nextTraceOrder,
            phase,
            blockId,
            actionId,
            status,
            message));
        TraceChanged?.Invoke();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.ExitingPlayMode)
        {
            Stop();
        }
    }

    internal void TickSafePreviewForTests()
    {
        TickSafePreview();
    }

    internal static ActionSequenceAsset BuildPreparationSequence(
        ActionSequenceAsset source,
        string throughBlockId,
        bool includeTarget,
        out string sentinelId)
    {
        sentinelId = "preview.sentinel." + Guid.NewGuid().ToString("N");
        var actions = new List<ScenarioActionData>();
        if (string.IsNullOrWhiteSpace(throughBlockId))
        {
            CloneAll(source.Actions, actions);
        }
        else if (!(includeTarget
            ? TryCloneThrough(source.Actions, throughBlockId.Trim(), actions)
            : TryCloneBefore(source.Actions, throughBlockId.Trim(), actions)))
        {
            return null;
        }
        actions.Add(new ScenarioActionData
        {
            BlockId = sentinelId,
            ActionId = "preview.sentinel",
            Disabled = true
        });

        ActionSequenceAsset temporary = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.SequenceId = source.SequenceId + ".preview";
        temporary.DisplayNameKo = source.DisplayNameKo;
        temporary.Contract = ActionSequenceContractData.CopyOf(source.Contract);
        temporary.Actions = actions;
        return temporary;
    }

    private static void CloneAll(
        IList<ScenarioActionData> source,
        List<ScenarioActionData> destination)
    {
        if (source == null)
        {
            return;
        }
        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(ScenarioBlockIdentity.ClonePreservingIds(source[i]));
        }
    }

    private static bool TryCloneThrough(
        IList<ScenarioActionData> source,
        string targetBlockId,
        List<ScenarioActionData> destination)
    {
        if (source == null)
        {
            return false;
        }
        for (int i = 0; i < source.Count; i++)
        {
            ScenarioActionData item = source[i];
            if (item == null)
            {
                destination.Add(null);
                continue;
            }
            if (string.Equals(item.BlockId, targetBlockId, StringComparison.Ordinal))
            {
                destination.Add(ScenarioBlockIdentity.ClonePreservingIds(item));
                return true;
            }
            var childPrefix = new List<ScenarioActionData>();
            if (TryCloneThrough(item.Children, targetBlockId, childPrefix))
            {
                ScenarioActionData parent = ScenarioBlockIdentity.ClonePreservingIds(item);
                parent.Children = childPrefix;
                destination.Add(parent);
                return true;
            }
            destination.Add(ScenarioBlockIdentity.ClonePreservingIds(item));
        }
        return false;
    }

    private static bool TryCloneBefore(
        IList<ScenarioActionData> source,
        string targetBlockId,
        List<ScenarioActionData> destination)
    {
        if (source == null)
        {
            return false;
        }
        for (int i = 0; i < source.Count; i++)
        {
            ScenarioActionData item = source[i];
            if (item == null)
            {
                destination.Add(null);
                continue;
            }
            if (string.Equals(item.BlockId, targetBlockId, StringComparison.Ordinal))
            {
                return true;
            }
            var childPrefix = new List<ScenarioActionData>();
            if (TryCloneBefore(item.Children, targetBlockId, childPrefix))
            {
                if (childPrefix.Count > 0)
                {
                    ScenarioActionData parent = ScenarioBlockIdentity.ClonePreservingIds(item);
                    parent.Children = childPrefix;
                    destination.Add(parent);
                }
                return true;
            }
            destination.Add(ScenarioBlockIdentity.ClonePreservingIds(item));
        }
        return false;
    }
}
