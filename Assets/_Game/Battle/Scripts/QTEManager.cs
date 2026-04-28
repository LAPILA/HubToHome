using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// QTE(Quick Time Event) 처리 매니저.
/// 타이밍 바(Timing) / 연타(Mashing) 두 가지 타입을 지원합니다.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    // ── QTE 결과 등급 ─────────────────────────────────────────
    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }

    // ── 이벤트 ────────────────────────────────────────────────
    public event Action<QTEGrade> OnQTECompleted;

    // ── 설정 ──────────────────────────────────────────────────
    [Header("Timing QTE Settings")]
    [SerializeField] private float _timingWindowPerfect = 0.08f;
    [SerializeField] private float _timingWindowGreat   = 0.15f;
    [SerializeField] private float _timingWindowGood    = 0.25f;

    [Header("Mashing QTE Settings")]
    [SerializeField] private int   _mashTargetCount     = 10;
    [SerializeField] private float _mashTimeLimit       = 3f;

    // ── 상태 ──────────────────────────────────────────────────
    public bool IsActive { get; private set; } = false;

    // 캐싱
    private WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 타이밍 QTE 시작 ───────────────────────────────────────
    /// <summary>
    /// 타이밍 바 QTE를 시작합니다.
    /// </summary>
    /// <param name="duration">바가 이동하는 총 시간</param>
    /// <param name="difficultyMultiplier">EnemyData의 QTE 난이도 계수</param>
    public void StartTimingQTE(float duration, float difficultyMultiplier = 1f)
    {
        if (IsActive) return;
        StartCoroutine(TimingQTERoutine(duration, difficultyMultiplier));
    }

    private IEnumerator TimingQTERoutine(float duration, float difficultyMultiplier)
    {
        IsActive = true;
        float elapsed = 0f;
        bool inputReceived = false;
        QTEGrade grade = QTEGrade.Miss;

        // TODO: BattleUI에 타이밍 바 표시 요청

        while (elapsed < duration && !inputReceived)
        {
            elapsed += Time.deltaTime;

            // Z키 입력 감지
            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
            {
                inputReceived = true;
                float normalizedTime = elapsed / duration;
                float distFromCenter = Mathf.Abs(normalizedTime - 0.5f) * 2f; // 0=중앙, 1=끝

                float perfect = _timingWindowPerfect * difficultyMultiplier;
                float great   = _timingWindowGreat   * difficultyMultiplier;
                float good    = _timingWindowGood    * difficultyMultiplier;

                if      (distFromCenter <= perfect) grade = QTEGrade.Perfect;
                else if (distFromCenter <= great)   grade = QTEGrade.Great;
                else if (distFromCenter <= good)    grade = QTEGrade.Good;
                else                                grade = QTEGrade.Bad;
            }

            yield return _waitEOF;
        }

        IsActive = false;
        // TODO: BattleUI에 타이밍 바 숨김 요청
        Debug.Log($"[QTEManager] Timing QTE Result: {grade}");
        OnQTECompleted?.Invoke(grade);
    }

    // ── 연타 QTE 시작 ─────────────────────────────────────────
    public void StartMashingQTE(float difficultyMultiplier = 1f)
    {
        if (IsActive) return;
        StartCoroutine(MashingQTERoutine(difficultyMultiplier));
    }

    private IEnumerator MashingQTERoutine(float difficultyMultiplier)
    {
        IsActive = true;
        int targetCount = Mathf.RoundToInt(_mashTargetCount * difficultyMultiplier);
        int mashCount   = 0;
        float elapsed   = 0f;

        // TODO: BattleUI에 연타 게이지 표시 요청

        while (elapsed < _mashTimeLimit && mashCount < targetCount)
        {
            elapsed += Time.deltaTime;

            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
                mashCount++;

            yield return _waitEOF;
        }

        IsActive = false;
        float ratio = (float)mashCount / targetCount;
        QTEGrade grade;
        if      (ratio >= 1.0f) grade = QTEGrade.Perfect;
        else if (ratio >= 0.8f) grade = QTEGrade.Great;
        else if (ratio >= 0.5f) grade = QTEGrade.Good;
        else if (ratio >= 0.2f) grade = QTEGrade.Bad;
        else                    grade = QTEGrade.Miss;

        // TODO: BattleUI에 연타 게이지 숨김 요청
        Debug.Log($"[QTEManager] Mashing QTE Result: {grade} ({mashCount}/{targetCount})");
        OnQTECompleted?.Invoke(grade);
    }

    // ── 방어 QTE (적 턴) ──────────────────────────────────────
    /// <summary>
    /// 적 공격 타이밍에 방어 입력을 감지합니다.
    /// </summary>
    /// <param name="attackDelay">적 공격까지의 대기 시간</param>
    /// <param name="onResult">결과 콜백: (DefenseInput, QTEGrade)</param>
    public void StartDefenseQTE(float attackDelay, Action<DefenseInput, QTEGrade> onResult)
    {
        StartCoroutine(DefenseQTERoutine(attackDelay, onResult));
    }

    private IEnumerator DefenseQTERoutine(float attackDelay, Action<DefenseInput, QTEGrade> onResult)
    {
        IsActive = true;
        float elapsed = 0f;
        DefenseInput input = DefenseInput.None;
        bool inputReceived = false;

        // TODO: BattleUI에 방어 타이밍 인디케이터 표시

        while (elapsed < attackDelay && !inputReceived)
        {
            elapsed += Time.deltaTime;

            if (Keyboard.current != null)
            {
                if      (Keyboard.current.zKey.wasPressedThisFrame)     { input = DefenseInput.Parry; inputReceived = true; }
                else if (Keyboard.current.cKey.wasPressedThisFrame)     { input = DefenseInput.Dodge; inputReceived = true; }
                else if (Keyboard.current.spaceKey.wasPressedThisFrame) { input = DefenseInput.Jump;  inputReceived = true; }
            }

            yield return _waitEOF;
        }

        IsActive = false;

        // 타이밍 판정
        float timeLeft = attackDelay - elapsed;
        QTEGrade grade;
        if (!inputReceived)
            grade = QTEGrade.Miss;
        else if (timeLeft <= 0.1f)
            grade = QTEGrade.Perfect;
        else if (timeLeft <= 0.2f)
            grade = QTEGrade.Great;
        else
            grade = QTEGrade.Good;

        Debug.Log($"[QTEManager] Defense QTE: {input} / {grade}");
        onResult?.Invoke(input, grade);
    }
}
