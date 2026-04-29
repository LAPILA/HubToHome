using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// QTE(Quick Time Event) 처리 매니저.
/// 
/// 지원 QTE 종류:
/// 1. DefenseQTE  — 적 근거리 공격 시 Z/C/Space 타이밍 입력 (패링/회피/점프)
/// 2. SkillQTE    — 스킬 사용 시 원형 Fill 타이밍 입력 (Perfect/Great/Bad)
/// 
/// 패링(Z) Perfect 성공 시 MP 회복량을 BattleManager에 이벤트로 전달합니다.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    // ── QTE 결과 등급 ─────────────────────────────────────────
    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }

    // ── 이벤트 ────────────────────────────────────────────────
    /// <summary>스킬 QTE 완료 시 발생 (등급 전달)</summary>
    public event Action<QTEGrade> OnSkillQTECompleted;

    // ── 방어 QTE 설정 ─────────────────────────────────────────
    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [Tooltip("Perfect 판정 구간 (0~1, 타이머 끝 기준 남은 비율)")]
    [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;

    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.4f)] private float _greatWindow   = 0.22f;

    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.6f)] private float _goodWindow    = 0.40f;

    // ── 스킬 QTE 설정 ─────────────────────────────────────────
    [BoxGroup("Skill QTE"), LabelWidth(160)]
    [Tooltip("원형 Fill이 이동하는 총 시간")]
    [SerializeField] private float _skillQTEDuration = 2f;

    [BoxGroup("Skill QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.15f)] private float _skillPerfectWindow = 0.08f;

    [BoxGroup("Skill QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.25f)] private float _skillGreatWindow   = 0.15f;

    [BoxGroup("Skill QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.4f)]  private float _skillGoodWindow    = 0.25f;

    // ── 상태 ──────────────────────────────────────────────────
    public bool IsActive { get; private set; } = false;

    private readonly WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ═══════════════════════════════════════════════════════════
    // ── 방어 QTE (적 근거리 단일 공격) ───────────────────────
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 적 공격 타이밍에 Z/C/Space 입력을 감지합니다.
    /// </summary>
    /// <param name="attackDelay">공격까지의 대기 시간 (초)</param>
    /// <param name="difficultyMult">EnemyData.QTEDifficultyMultiplier</param>
    /// <param name="onResult">결과 콜백 (DefenseInput, QTEGrade)</param>
    public void StartDefenseQTE(float attackDelay, float difficultyMult, Action<DefenseInput, QTEGrade> onResult)
    {
        if (IsActive)
        {
            Debug.LogWarning("[QTEManager] QTE already active.");
            return;
        }
        StartCoroutine(DefenseQTERoutine(attackDelay, difficultyMult, onResult));
    }

    private IEnumerator DefenseQTERoutine(float attackDelay, float difficultyMult, Action<DefenseInput, QTEGrade> onResult)
    {
        IsActive = true;
        float elapsed      = 0f;
        bool  inputReceived = false;
        var   input         = DefenseInput.None;

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

        // 타이밍 판정: 남은 시간 비율로 계산 (difficultyMult가 클수록 구간 좁아짐)
        QTEGrade grade;
        if (!inputReceived)
        {
            grade = QTEGrade.Miss;
        }
        else
        {
            float timeLeft = (attackDelay - elapsed) / attackDelay; // 0~1
            float pm = _perfectWindow / difficultyMult;
            float gm = _greatWindow   / difficultyMult;
            float gd = _goodWindow    / difficultyMult;

            if      (timeLeft <= pm) grade = QTEGrade.Perfect;
            else if (timeLeft <= gm) grade = QTEGrade.Great;
            else if (timeLeft <= gd) grade = QTEGrade.Good;
            else                     grade = QTEGrade.Bad;
        }

        Debug.Log($"[QTEManager] DefenseQTE → {input} / {grade}");
        onResult?.Invoke(input, grade);
    }

    // ═══════════════════════════════════════════════════════════
    // ── 스킬 QTE (플레이어 스킬 사용 시) ─────────────────────
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 원형 Fill 타이밍 QTE를 시작합니다.
    /// BattleUIController가 UI를 표시하고, 완료 시 OnSkillQTECompleted 이벤트 발생.
    /// </summary>
    /// <param name="difficultyMult">EnemyData.QTEDifficultyMultiplier</param>
    public void StartSkillQTE(float difficultyMult = 1f)
    {
        if (IsActive) return;
        StartCoroutine(SkillQTERoutine(difficultyMult));
    }

    private IEnumerator SkillQTERoutine(float difficultyMult)
    {
        IsActive = true;
        float elapsed      = 0f;
        bool  inputReceived = false;
        var   grade         = QTEGrade.Miss;

        while (elapsed < _skillQTEDuration && !inputReceived)
        {
            elapsed += Time.deltaTime;

            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
            {
                inputReceived = true;
                float normalizedTime  = elapsed / _skillQTEDuration;
                float distFromCenter  = Mathf.Abs(normalizedTime - 0.5f) * 2f; // 0=중앙, 1=끝

                float pm = _skillPerfectWindow * difficultyMult;
                float gm = _skillGreatWindow   * difficultyMult;
                float gd = _skillGoodWindow    * difficultyMult;

                if      (distFromCenter <= pm) grade = QTEGrade.Perfect;
                else if (distFromCenter <= gm) grade = QTEGrade.Great;
                else if (distFromCenter <= gd) grade = QTEGrade.Good;
                else                           grade = QTEGrade.Bad;
            }

            yield return _waitEOF;
        }

        IsActive = false;
        Debug.Log($"[QTEManager] SkillQTE → {grade}");
        OnSkillQTECompleted?.Invoke(grade);
    }

    // ── 강제 중단 ─────────────────────────────────────────────
    public void ForceStop()
    {
        StopAllCoroutines();
        IsActive = false;
    }
}
