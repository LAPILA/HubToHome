using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum QteTermination
{
    Running,
    Completed,
    TimedOut,
    Cancelled,
    Failed
}

public sealed class QteExecution
{
    public QteTermination Termination { get; private set; } = QteTermination.Running;
    public bool IsDone => Termination != QteTermination.Running;

    internal void Complete(QteTermination termination)
    {
        if (IsDone)
            return;

        Termination = termination;
    }
}

/// <summary>
/// 방어 및 스킬 QTE의 단일 실행 소유자입니다.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.4f)] private float _greatWindow = 0.22f;

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.6f)] private float _goodWindow = 0.40f;

    [BoxGroup("시간제 방어"), LabelText("Z 통합 방어 사용")]
    [SerializeField] private bool _useTimedGuard = true;

    [BoxGroup("시간제 방어"), LabelText("최대 유지 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _guardDuration = 0.4f;

    [BoxGroup("시간제 방어"), LabelText("방어 시 받는 피해 배율")]
    [SerializeField, Range(0.01f, 1f)] private float _guardDamageMultiplier = 0.5f;

    public bool IsActive { get; private set; }
    public bool UseTimedGuard => _useTimedGuard;
    public DefenseTimingProfile DefaultDefenseTimingProfile =>
        new DefenseTimingProfile(_perfectWindow, _greatWindow, _goodWindow);

    public event Action<DefenseQteRequest> DefenseWindowOpened;
    public event Action<DefenseQteResult> DefenseResolved;
    public event Action DefenseWindowClosed;

    private Coroutine _activeCoroutine;
    private QteExecution _activeExecution;
    private bool _activeIsSequence;
    private int _lastGuardPressFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDefenseQTE(
        float attackDelay,
        float difficultyMult,
        Action<DefenseInput, QTEGrade> onResult)
    {
        StartDefenseQTEWithResult(attackDelay, difficultyMult, onResult);
    }

    public QteExecution StartDefenseQTEWithResult(
        float attackDelay,
        float difficultyMult,
        Action<DefenseInput, QTEGrade> onResult)
    {
        DefenseQteRequest request = CreateDefenseRequest(
            attackDelay,
            difficultyMult,
            DefenseRequirement.Any);
        return StartDefenseQTEWithResult(
            request,
            result => onResult?.Invoke(result.Input, result.Grade));
    }

    public DefenseQteRequest CreateDefenseRequest(
        float attackDelay,
        float difficultyMult,
        DefenseRequirement requirement,
        bool allowNearSuccess = true)
    {
        return new DefenseQteRequest(
            attackDelay,
            difficultyMult,
            requirement,
            DefaultDefenseTimingProfile,
            allowNearSuccess,
            _useTimedGuard,
            _guardDuration,
            _guardDamageMultiplier);
    }

    public QteExecution StartDefenseQTEWithResult(
        DefenseQteRequest request,
        Action<DefenseQteResult> onResult)
    {
        return StartDefenseQTEWithResult(request, null, onResult);
    }

    public QteExecution StartDefenseQTEWithResult(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult)
    {
        CancelActiveQTE();

        // 개별 스킬의 판정 구간도 보존하면서, 모든 진입 경로에 같은 기본 방어 방식을 적용합니다.
        request = new DefenseQteRequest(request.Duration, request.DifficultyMultiplier,
            request.Requirement, request.TimingProfile, request.AllowNearSuccess,
            _useTimedGuard, _guardDuration, _guardDamageMultiplier);
        var execution = new QteExecution();
        _activeExecution = execution;
        _activeIsSequence = false;

        Coroutine coroutine = StartCoroutine(DefenseQTERoutine(request, inputSource, onResult, execution));
        if (ReferenceEquals(execution, _activeExecution) && !execution.IsDone)
            _activeCoroutine = coroutine;

        return execution;
    }

    private IEnumerator DefenseQTERoutine(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult,
        QteExecution execution)
    {
        IsActive = true;
        float startedAt = Time.realtimeSinceStartup;
        float impactAt = startedAt + request.Duration;
        DefenseInputReadStatus inputStatus = DefenseInputReadStatus.None;
        DefenseInput input = DefenseInput.None;
        float inputTime = impactAt;
        var guardAttempt = new TimedGuardAttempt();
        IDefenseInputSource controller = inputSource ?? ResolveDefenseInputSource();

        InvokePresentation(
            () => BattleUIController.Instance?.ShowDefenseQTE(request),
            "show defense QTE");
        InvokeSafely(DefenseWindowOpened, request, nameof(DefenseWindowOpened));

        while (!execution.IsDone)
        {
            float now = Time.realtimeSinceStartup;
            // 지정한 대상이 사라졌을 때 다른 파티원의 입력으로 대체하지 않습니다.
            if (controller != null && !IsInputSourceAvailable(controller))
            {
                Cancel(execution);
                yield break;
            }

            if (request.UseTimedGuard)
            {
                bool held = controller is ITimedGuardInputSource guardSource
                    ? guardSource.IsGuardHeld : GameInput.QTEZHeld;
                guardAttempt.ObserveHeld(held);
                if (now >= impactAt)
                    break;

                if (!guardAttempt.HasAttempt)
                {
                    bool hasBuffered = controller != null
                        && controller.TryConsumeBufferedDefenseInput(out input, out inputTime);
                    if (hasBuffered && input == DefenseInput.Parry)
                    {
                        guardAttempt.TryPress(inputTime, startedAt, impactAt);
                    }
                    else if (GameInput.QTEZPressed && _lastGuardPressFrame != Time.frameCount)
                    {
                        if (guardAttempt.TryPress(now, startedAt, impactAt))
                            controller?.PreviewDefenseInput(DefenseInput.Parry);
                    }

                    if (guardAttempt.HasAttempt)
                    {
                        _lastGuardPressFrame = Time.frameCount;
                        guardAttempt.ObserveHeld(held);
                    }
                }

                // UI는 상태가 변할 때만 라벨을 갱신하며, 프레임마다 문자열을 생성하지 않습니다.
                BattleUIController.Instance?.UpdateDefenseGuard(
                    guardAttempt.RemainingAt(now, request.GuardDuration), guardAttempt.HasAttempt);
                yield return null;
                continue;
            }

            if (now >= impactAt)
                break;

            if (controller != null
                && controller.TryConsumeBufferedDefenseInput(out input, out float bufferedInputTime))
            {
                inputStatus = DefenseInputReadStatus.Valid;
                inputTime = bufferedInputTime;
                break;
            }

            inputStatus = GameInput.ReadDefenseInputThisFrame(out input);
            if (inputStatus != DefenseInputReadStatus.None)
            {
                inputTime = Time.realtimeSinceStartup;
                if (inputTime >= impactAt)
                {
                    inputStatus = DefenseInputReadStatus.None;
                    input = DefenseInput.None;
                    break;
                }

                if (inputStatus == DefenseInputReadStatus.Valid)
                    controller?.PreviewDefenseInput(input);
                break;
            }

            yield return null;
        }

        if (execution.IsDone)
            yield break;

        float secondsBeforeImpact = inputStatus == DefenseInputReadStatus.None
            ? 0f
            : Mathf.Clamp(impactAt - inputTime, 0f, request.Duration);
        DefenseQteResult result = request.UseTimedGuard
            ? DefenseJudgementPolicy.EvaluateTimedGuard(request, guardAttempt, impactAt)
            : DefenseJudgementPolicy.Evaluate(request, inputStatus, input, secondsBeforeImpact);
        QteTermination termination = result.InputStatus == DefenseInputReadStatus.None
            ? QteTermination.TimedOut
            : QteTermination.Completed;

        CompleteExecution(execution, termination);
        InvokePresentation(
            () => BattleUIController.Instance?.ShowDefenseQTEResult(result),
            "show defense result");
        InvokeSafely(DefenseResolved, result, nameof(DefenseResolved));
        InvokeSafely(onResult, result, "defense result callback");
        InvokeSafely(DefenseWindowClosed, nameof(DefenseWindowClosed));
    }

    public void StartSequenceQTE(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete)
    {
        StartSequenceQTEWithResult(nodes, timeLimit, onComplete);
    }

    public QteExecution StartSequenceQTEWithResult(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete)
    {
        CancelActiveQTE();

        var execution = new QteExecution();
        if (nodes == null || nodes.Count == 0)
        {
            execution.Complete(QteTermination.Failed);
            return execution;
        }

        _activeExecution = execution;
        _activeIsSequence = true;
        Coroutine coroutine = StartCoroutine(SequenceQTERoutine(
            nodes,
            Mathf.Max(0.01f, timeLimit),
            onComplete,
            execution));
        if (ReferenceEquals(execution, _activeExecution) && !execution.IsDone)
            _activeCoroutine = coroutine;

        return execution;
    }

    private IEnumerator SequenceQTERoutine(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete,
        QteExecution execution)
    {
        IsActive = true;
        int successCount = 0;
        Canvas.ForceUpdateCanvases();
        BattleUIController.Instance?.ShowSkillQTE(Vector2.zero, "", 0f);

        yield return null;
        yield return null;

        for (int i = 0; i < nodes.Count && !execution.IsDone; i++)
        {
            SkillQTENode node = nodes[i];

            Vector2 relativePos = new Vector2(node.PosX, node.PosY);
            BattleUIController.Instance?.ShowSkillQTE(relativePos, node.TargetKey, timeLimit);

            float elapsed = 0f;
            bool answered = false;
            bool hit = false;
            yield return null;

            while (elapsed < timeLimit && !answered && !execution.IsDone)
            {
                elapsed += Time.unscaledDeltaTime;
                if (GameInput.TryReadDefenseInputThisFrame(out DefenseInput sequenceInput))
                {
                    answered = true;
                    string key = (node.TargetKey ?? string.Empty).ToLowerInvariant();
                    hit = (key == "z" && sequenceInput == DefenseInput.Parry)
                        || (key == "x" && sequenceInput == DefenseInput.Dodge)
                        || (key == "c" && sequenceInput == DefenseInput.Jump);
                    if (hit) successCount++;
                }

                yield return null;
            }

            if (execution.IsDone)
                yield break;

            BattleUIController.Instance?.ShowSkillQTEResult(hit);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        if (execution.IsDone)
            yield break;

        BattleUIController.Instance?.HideSkillQTE();
        CompleteExecution(execution, QteTermination.Completed);
        onComplete?.Invoke(successCount, nodes.Count);
    }

    public void ForceStop()
    {
        CancelActiveQTE();
    }

    public bool Cancel(QteExecution execution)
    {
        if (execution == null || execution.IsDone || !ReferenceEquals(execution, _activeExecution))
            return false;

        CancelActiveQTE();
        return true;
    }

    public void CancelActiveQTE()
    {
        QteExecution execution = _activeExecution;
        if (execution == null || execution.IsDone)
            return;

        bool wasSequence = _activeIsSequence;
        if (_activeCoroutine != null)
            StopCoroutine(_activeCoroutine);

        execution.Complete(QteTermination.Cancelled);
        ClearActive(execution);

        if (wasSequence)
        {
            BattleUIController.Instance?.HideSkillQTE();
        }
        else
        {
            InvokePresentation(
                () => BattleUIController.Instance?.HideDefenseQTE(),
                "hide defense QTE");
            InvokeSafely(DefenseWindowClosed, nameof(DefenseWindowClosed));
        }
    }

    private static IDefenseInputSource ResolveDefenseInputSource()
    {
        BattleManager battleManager = BattleManager.Instance;
        if (battleManager == null || battleManager._playerParty.Count == 0)
            return null;

        return battleManager._playerParty[0]?.GetComponent<PlayerController>();
    }

    private static bool IsInputSourceAvailable(IDefenseInputSource inputSource)
    {
        return inputSource != null
            && (!(inputSource is UnityEngine.Object unityObject) || unityObject != null);
    }

    private void CompleteExecution(QteExecution execution, QteTermination termination)
    {
        execution.Complete(termination);
        ClearActive(execution);
    }

    private void ClearActive(QteExecution execution)
    {
        if (!ReferenceEquals(execution, _activeExecution))
            return;

        _activeExecution = null;
        _activeCoroutine = null;
        _activeIsSequence = false;
        IsActive = false;
    }

    private static void InvokePresentation(Action action, string operation)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"[{nameof(QTEManager)}] Failed to {operation}.",
                exception));
        }
    }

    private static void InvokeSafely<T>(Action<T> action, T value, string source)
    {
        if (action == null)
            return;

        Delegate[] subscribers = action.GetInvocationList();
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                ((Action<T>)subscribers[i]).Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"[{nameof(QTEManager)}] {source} failed.",
                    exception));
            }
        }
    }

    private static void InvokeSafely(Action action, string source)
    {
        if (action == null)
            return;

        Delegate[] subscribers = action.GetInvocationList();
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                ((Action)subscribers[i]).Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"[{nameof(QTEManager)}] {source} failed.",
                    exception));
            }
        }
    }

    private void OnDisable()
    {
        CancelActiveQTE();
    }

    private void OnDestroy()
    {
        CancelActiveQTE();
        if (Instance == this)
            Instance = null;
    }
}
