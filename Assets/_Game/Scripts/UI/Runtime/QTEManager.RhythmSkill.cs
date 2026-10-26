using System;
using System.Collections;
using UnityEngine;

public partial class QTEManager
{
    /// <summary>
    /// 공격 타수와 독립적인 입력 스트림입니다. 이 소유자는 입력/표시/시계만 담당하며
    /// 타격과 이동은 호출한 스킬이 담당합니다. 입력 성공으로 시계가 빨라지거나 멈추지 않습니다.
    /// </summary>
    public QteExecution StartSkillInputStream(float duration, float promptInterval, float inputWindow,
        Action<bool> onResult, Func<bool> canContinue, int firstKey = 0)
    {
        uint version = ++_executionVersion;
        CancelCurrentExecution();
        var execution = new QteExecution();
        if (version != _executionVersion || !isActiveAndEnabled)
        { execution.Complete(QteTermination.Cancelled); return execution; }
        if (!IsFinitePositive(duration) || !IsFinitePositive(promptInterval)
            || !IsFinitePositive(inputWindow))
        { execution.Complete(QteTermination.Failed); return execution; }

        _activeExecution = execution;
        _activeIsSequence = true;
        _activeShowsPresentation = true;
        _activePublishesDefenseEvents = false;
        Coroutine coroutine = StartCoroutine(SkillInputStreamRoutine(duration,
            Mathf.Max(0.2f, promptInterval), Mathf.Min(inputWindow, promptInterval),
            onResult, canContinue, Mathf.Abs(firstKey % 3), execution));
        if (ReferenceEquals(execution, _activeExecution) && !execution.IsDone) _activeCoroutine = coroutine;
        return execution;
    }

    private static bool IsFinitePositive(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    public static DefenseInput SkillPromptInput(int index)
        => index % 3 == 0 ? DefenseInput.Parry : index % 3 == 1 ? DefenseInput.Dodge : DefenseInput.Jump;

    private IEnumerator SkillInputStreamRoutine(float duration, float interval, float window,
        Action<bool> onResult, Func<bool> canContinue, int firstKey, QteExecution execution)
    {
        IsActive = true;
        float elapsed = 0f, nextPrompt = 0f, openedAt = 0f, closesAt = 0f;
        int promptIndex = firstKey, openedFrame = -1;
        bool shown = false, answered = false;
        DefenseInput expected = DefenseInput.None;
        bool automatic = GameConfigManager.Instance != null && GameConfigManager.Instance.AutoSkillQte;
        if (automatic) BattleUIController.Instance?.HideSkillQTE();
        try
        {
            // 메뉴 확정 프레임의 입력은 첫 QTE로 넘기지 않습니다.
            yield return null;
            while (elapsed < duration && !execution.IsDone)
            {
                if (!StreamCanContinue(canContinue, execution)) yield break;
                if (GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f)
                { yield return null; continue; }

                if (shown && elapsed >= closesAt)
                {
                    if (!answered && !ResolveStreamPrompt(automatic, onResult, execution, !automatic)) yield break;
                    shown = false;
                }
                if (!shown && elapsed >= nextPrompt && duration - elapsed >= Mathf.Min(window, 0.2f) - 0.001f)
                {
                    expected = SkillPromptInput(promptIndex++);
                    string key = expected == DefenseInput.Parry ? "Z" : expected == DefenseInput.Dodge ? "X" : "C";
                    openedAt = elapsed;
                    closesAt = Mathf.Min(duration, elapsed + window);
                    openedFrame = Time.frameCount;
                    nextPrompt = elapsed + interval;
                    shown = true; answered = false;
                    if (!automatic) BattleUIController.Instance?.ShowRandomSkillQTE(key, 0f);
                }
                if (shown && !answered && !automatic)
                {
                    BattleUIController.Instance?.SetSkillQTEProgress((closesAt - elapsed) / Mathf.Max(0.01f, closesAt - openedAt));
                    if (Time.frameCount > openedFrame
                        && GameInput.TryReadDefenseInputThisFrame(out DefenseInput input))
                    {
                        answered = true;
                        if (!ResolveStreamPrompt(input == expected, onResult, execution)) yield break;
                    }
                }
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                execution.ElapsedSeconds = elapsed;
                yield return null;
            }
            // 마지막 입력 창의 끝 프레임에도 메뉴/소유자 취소가 먼저입니다.
            // 위 루프의 마지막 yield 직후 시간이 정지될 수 있습니다.
            while (!execution.IsDone && (GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f))
            {
                if (!StreamCanContinue(canContinue, execution)) yield break;
                yield return null;
            }
            if (!StreamCanContinue(canContinue, execution)) yield break;
            if (shown && !answered && !execution.IsDone
                && !ResolveStreamPrompt(automatic, onResult, execution, !automatic)) yield break;
            if (!execution.IsDone) CompleteExecution(execution, QteTermination.Completed);
        }
        finally
        {
            if (ReferenceEquals(execution, _activeExecution) || _activeExecution == null)
            {
                if (execution.Termination == QteTermination.Completed)
                    BattleUIController.Instance?.FinishSkillQTEFeedback();
                else
                    BattleUIController.Instance?.HideSkillQTE();
            }
            if (!execution.IsDone) CompleteExecution(execution, QteTermination.Cancelled);
        }
    }

    private bool StreamCanContinue(Func<bool> predicate, QteExecution execution)
    {
        if (execution.IsDone) return false;
        try { return predicate == null || predicate(); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CompleteExecution(execution, QteTermination.Failed);
            return false;
        }
    }

    private bool ResolveStreamPrompt(bool hit, Action<bool> callback, QteExecution execution, bool showFeedback = true)
    {
        try
        {
            if (showFeedback) BattleUIController.Instance?.ShowSkillQTEResult(hit);
            callback?.Invoke(hit);
            return !execution.IsDone;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CompleteExecution(execution, QteTermination.Failed);
            return false;
        }
    }
}
