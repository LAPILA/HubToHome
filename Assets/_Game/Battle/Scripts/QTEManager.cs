using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 QTE 매니저.
/// 글로벌 이벤트 대신 콜백(Command Pattern)을 사용하여 결합도를 낮췄습니다.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }
    
    // 🚨 글로벌 이벤트 제거 완료. (구독/해제 과정에서의 버그 원천 차단)
    // public event Action<int, int> OnSequenceQTECompleted;

    [BoxGroup("Defense QTE"), LabelWidth(160)] [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;
    [BoxGroup("Defense QTE"), LabelWidth(160)] [SerializeField, Range(0f, 0.4f)] private float _greatWindow   = 0.22f;
    [BoxGroup("Defense QTE"), LabelWidth(160)] [SerializeField, Range(0f, 0.6f)] private float _goodWindow    = 0.40f;

    public bool IsActive { get; private set; } = false;

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
                var kb = Keyboard.current;
                if      (kb.zKey.wasPressedThisFrame)     { input = DefenseInput.Parry; inputReceived = true; }
                else if (kb.cKey.wasPressedThisFrame)     { input = DefenseInput.Dodge; inputReceived = true; }
                else if (kb.spaceKey.wasPressedThisFrame) { input = DefenseInput.Jump;  inputReceived = true; }
            }
            
            // 🚨 WaitForEndOfFrame을 yield return null로 변경하여 프레임 간 입력 손실(Drop) 방지
            yield return null; 
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
    // ── 스킬 QTE (콜백 패턴 도입) ─────────────────────────────
    // ═══════════════════════════════════════════════════════════
    
    // 🚨 파라미터로 Action<int, int> onComplete를 직접 받습니다.
    public void StartSequenceQTE(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        if (IsActive || nodes == null || nodes.Count == 0) return;
        StartCoroutine(SequenceQTERoutine(nodes, timeLimit, onComplete));
    }

    private IEnumerator SequenceQTERoutine(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        IsActive = true;
        int successCount = 0;
        float gracePeriod = 0.08f; 
        float totalInputAllowedTime = timeLimit + gracePeriod;

        // UI 캔버스 콜드 스타트 렉 흡수
        BattleUIController.Instance.ShowSkillQTE(new Vector2(-9999, -9999), "", 0f);
        yield return new WaitForSeconds(0.15f); 

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
                if (dt > 0.1f) dt = 0.016f; // 렉 스파이크 차단
                elapsed += dt;

                if (Keyboard.current != null)
                {
                    var kb = Keyboard.current;
                    // 🚨 키 입력을 캐싱하여 한 프레임 내 중복 검사로 인한 평가 오류 해결
                    bool zPressed = kb.zKey.wasPressedThisFrame;
                    bool xPressed = kb.xKey.wasPressedThisFrame;
                    bool cPressed = kb.cKey.wasPressedThisFrame;

                    if (zPressed || xPressed || cPressed)
                    {
                        isAnswered = true;
                        
                        // 타겟 키 매칭 로직 간소화 및 정확도 향상
                        string keyLower = node.TargetKey.ToLower();
                        isHit = (keyLower == "z" && zPressed) || 
                                (keyLower == "x" && xPressed) || 
                                (keyLower == "c" && cPressed);
                                
                        if (isHit) successCount++;
                    }
                }
                yield return null; // 🚨 안전한 프레임 대기
            }

            BattleUIController.Instance.ShowSkillQTEResult(isHit);
            yield return new WaitForSeconds(0.4f); 
        }

        BattleUIController.Instance.HideSkillQTE();
        IsActive = false;
        
        // 🚨 실행이 끝난 후 직접 콜백 호출
        onComplete?.Invoke(successCount, nodes.Count);
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        IsActive = false;
    }
}