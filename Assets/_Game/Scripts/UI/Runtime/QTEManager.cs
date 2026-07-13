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
        CancelActiveQTE();

        var execution = new QteExecution();
        _activeExecution = execution;
        _activeIsSequence = false;
        _activeCoroutine = StartCoroutine(DefenseQTERoutine(
            Mathf.Max(0.01f, attackDelay),
            Mathf.Max(0.01f, difficultyMult),
            onResult,
            execution));
        return execution;
    }

    private IEnumerator DefenseQTERoutine(
        float attackDelay,
        float difficultyMult,
        Action<DefenseInput, QTEGrade> onResult,
        QteExecution execution)
    {
        IsActive = true;
        float elapsed = 0f;
        bool inputReceived = false;
        DefenseInput input = DefenseInput.None;

        while (elapsed < attackDelay && !inputReceived && !execution.IsDone)
        {
            elapsed += Time.deltaTime;

            PlayerController controller = null;
            if (BattleManager.Instance != null && BattleManager.Instance._playerParty.Count > 0)
            {
                controller = BattleManager.Instance._playerParty[0]?.GetComponent<PlayerController>();
                if (controller != null && controller.TryConsumeBufferedDefenseInput(out input))
                {
                    inputReceived = true;
                    yield return null;
                    continue;
                }
            }

            if (GameInput.TryReadDefenseInputThisFrame(out input))
            {
                inputReceived = true;
                controller?.PreviewDefenseInput(input);
            }

            yield return null;
        }

        if (execution.IsDone)
            yield break;

        QTEGrade grade = QTEGrade.Miss;
        QteTermination termination = QteTermination.TimedOut;
        if (inputReceived)
        {
            float timeLeft = (attackDelay - elapsed) / attackDelay;
            float perfect = _perfectWindow / difficultyMult;
            float great = _greatWindow / difficultyMult;
            float good = _goodWindow / difficultyMult;

            if (timeLeft <= perfect) grade = QTEGrade.Perfect;
            else if (timeLeft <= great) grade = QTEGrade.Great;
            else if (timeLeft <= good) grade = QTEGrade.Good;
            else grade = QTEGrade.Bad;
            termination = QteTermination.Completed;
        }

        CompleteExecution(execution, termination);
        onResult?.Invoke(input, grade);
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
        _activeCoroutine = StartCoroutine(SequenceQTERoutine(
            nodes,
            Mathf.Max(0.01f, timeLimit),
            onComplete,
            execution));
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
                elapsed += Time.deltaTime;
                if (GameInput.TryReadDefenseInputThisFrame(out DefenseInput input))
                {
                    answered = true;
                    string key = (node.TargetKey ?? string.Empty).ToLowerInvariant();
                    hit = (key == "z" && input == DefenseInput.Parry)
                        || (key == "x" && input == DefenseInput.Dodge)
                        || (key == "c" && input == DefenseInput.Jump);
                    if (hit) successCount++;
                }

                yield return null;
            }

            if (execution.IsDone)
                yield break;

            BattleUIController.Instance?.ShowSkillQTEResult(hit);
            yield return new WaitForSeconds(0.35f);
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

        if (_activeCoroutine != null)
            StopCoroutine(_activeCoroutine);

        if (_activeIsSequence)
            BattleUIController.Instance?.HideSkillQTE();

        execution.Complete(QteTermination.Cancelled);
        ClearActive(execution);
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
