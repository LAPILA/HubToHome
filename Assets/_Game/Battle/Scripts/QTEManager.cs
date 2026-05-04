using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }
    public event Action<int, int> OnSequenceQTECompleted;

    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;
    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.4f)] private float _greatWindow   = 0.22f;
    [BoxGroup("Defense QTE"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.6f)] private float _goodWindow    = 0.40f;

    public bool IsActive { get; private set; } = false;
    private readonly WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ═══════════════════════════════════════════════════════════
    // ── 방어 QTE ──────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════
    public void StartDefenseQTE(float attackDelay, float difficultyMult, Action<DefenseInput, QTEGrade> onResult)
    {
        if (IsActive) return;
        StartCoroutine(DefenseQTERoutine(attackDelay, difficultyMult, onResult));
    }

    private IEnumerator DefenseQTERoutine(float attackDelay, float difficultyMult, Action<DefenseInput, QTEGrade> onResult)
    {
        IsActive = true;
        float elapsed = 0f;
        bool inputReceived = false;
        var input = DefenseInput.None;

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
        QTEGrade grade = QTEGrade.Miss;
        if (inputReceived)
        {
            float timeLeft = (attackDelay - elapsed) / attackDelay; 
            float pm = _perfectWindow / difficultyMult;
            float gm = _greatWindow   / difficultyMult;
            float gd = _goodWindow    / difficultyMult;

            if      (timeLeft <= pm) grade = QTEGrade.Perfect;
            else if (timeLeft <= gm) grade = QTEGrade.Great;
            else if (timeLeft <= gd) grade = QTEGrade.Good;
            else                     grade = QTEGrade.Bad;
        }
        onResult?.Invoke(input, grade);
    }

    // ═══════════════════════════════════════════════════════════
    // ── 스킬 QTE (시퀀스 데이터 기반) ──────────────────────────
    // ═══════════════════════════════════════════════════════════
    public void StartSequenceQTE(List<SkillQTENode> nodes, float timeLimit)
    {
        if (IsActive || nodes == null || nodes.Count == 0) return;
        StartCoroutine(SequenceQTERoutine(nodes, timeLimit));
    }

    private IEnumerator SequenceQTERoutine(List<SkillQTENode> nodes, float timeLimit)
    {
        IsActive = true;
        int successCount = 0;
        float gracePeriod = 0.08f; 
        float totalInputAllowedTime = timeLimit + gracePeriod;

        // 🚨 [핵심 방어 1] UI 캔버스 콜드 스타트 렉 흡수 (Pre-warm)
        // 화면 밖(-9999)에 안 보이게 UI를 0.1초 켰다가 꺼서 초기 로딩 렉을 미리 빼버립니다.
        BattleUIController.Instance.ShowSkillQTE(new Vector2(-9999, -9999), "", 0f);
        yield return new WaitForSeconds(0.15f); // 렉이 발생하고 진정될 때까지 대기

        foreach (var node in nodes)
        {
            Vector2 screenPos = new Vector2(Screen.width * node.PosX, Screen.height * node.PosY);
            BattleUIController.Instance.ShowSkillQTE(screenPos, node.TargetKey, timeLimit);

            float elapsed = 0f;
            bool isAnswered = false;
            bool isHit = false;

            yield return null; 

            while (elapsed < totalInputAllowedTime && !isAnswered)
            {
                float dt = Time.deltaTime;
                // 🚨 [핵심 방어 2] 렉 스파이크 차단
                // 프레임이 심하게 떨어져도 타이머가 폭주해서 훅 지나가는 것을 막아줍니다.
                if (dt > 0.1f) dt = 0.016f; 
                
                elapsed += dt;

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.zKey.wasPressedThisFrame || 
                        Keyboard.current.xKey.wasPressedThisFrame || 
                        Keyboard.current.cKey.wasPressedThisFrame)
                    {
                        isAnswered = true;
                        if (IsCorrectKeyPressed(node.TargetKey)) { isHit = true; successCount++; }
                        else { isHit = false; }
                    }
                }
                yield return _waitEOF;
            }

            BattleUIController.Instance.ShowSkillQTEResult(isHit);
            yield return new WaitForSeconds(0.4f); 
        }

        BattleUIController.Instance.HideSkillQTE();
        IsActive = false;
        OnSequenceQTECompleted?.Invoke(successCount, nodes.Count);
    }

    private bool IsCorrectKeyPressed(string targetKeyStr)
    {
        if (targetKeyStr.Equals("Z", StringComparison.OrdinalIgnoreCase)) return Keyboard.current.zKey.wasPressedThisFrame;
        if (targetKeyStr.Equals("X", StringComparison.OrdinalIgnoreCase)) return Keyboard.current.xKey.wasPressedThisFrame;
        if (targetKeyStr.Equals("C", StringComparison.OrdinalIgnoreCase)) return Keyboard.current.cKey.wasPressedThisFrame;
        return false;
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        IsActive = false;
    }
}