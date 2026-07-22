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

    public bool IsActive { get; private set; }
    public DefenseTimingProfile DefaultDefenseTimingProfile =>
        new DefenseTimingProfile(_perfectWindow, _greatWindow, _goodWindow);

    public event Action<DefenseQteRequest> DefenseWindowOpened;
    public event Action<DefenseQteResult> DefenseResolved;
    public event Action DefenseWindowClosed;

    private Coroutine _activeCoroutine;
    private QteExecution _activeExecution;
    private bool _activeIsSequence;

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
            allowNearSuccess);
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

        InvokePresentation(
            () => BattleUIController.Instance?.ShowDefenseQTE(request),
            "show defense QTE");
        InvokeSafely(DefenseWindowOpened, request, nameof(DefenseWindowOpened));

        while (!execution.IsDone)
        {
            float now = Time.realtimeSinceStartup;
            if (now >= impactAt)
                break;

            IDefenseInputSource controller = IsInputSourceAvailable(inputSource)
                ? inputSource : ResolveDefenseInputSource();
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
        DefenseQteResult result = DefenseJudgementPolicy.Evaluate(
            request,
            inputStatus,
            input,
            secondsBeforeImpact);
        QteTermination termination = inputStatus == DefenseInputReadStatus.None
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
