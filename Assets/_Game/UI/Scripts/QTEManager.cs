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
        
        if (kb.zKey.wasPressedThisFrame) 
        { 
            input = DefenseInput.Parry; 
            inputReceived = true; 
        }
        else if (kb.xKey.wasPressedThisFrame) 
        { 
            input = DefenseInput.Dodge; 
            inputReceived = true; 
        }
        else if (kb.cKey.wasPressedThisFrame) 
        { 
            input = DefenseInput.Jump;  
            inputReceived = true; 
        }
    }
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
    
    public void StartSequenceQTE(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        if (IsActive || nodes == null || nodes.Count == 0) return;
        StartCoroutine(SequenceQTERoutine(nodes, timeLimit, onComplete));
    }

    private IEnumerator SequenceQTERoutine(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
{
    IsActive = true;
    int successCount = 0;

    // 1. [UI 예열] 투명한 상태로 시스템을 미리 한 번 깨웁니다.
    // 좌표는 zero를 보내도 위 DefenseQTEUI 로직에서 알아서 -9999로 보냅니다.
    BattleUIController.Instance.ShowSkillQTE(Vector2.zero, "", 0f); 
    
    // 레이아웃 엔진이 한 프레임 쉴 시간을 줌
    yield return null; 
    yield return new WaitForEndOfFrame(); 

    foreach (var node in nodes)
    {
        // 2. 실제 노드 좌표 전달
        Vector2 relativePos = new Vector2(node.PosX, node.PosY);
        BattleUIController.Instance.ShowSkillQTE(relativePos, node.TargetKey, timeLimit);

        float elapsed = 0f;
        bool isAnswered = false;
        bool isHit = false;

        // UI가 그려질 시간을 줌
        yield return null; 

        while (elapsed < timeLimit && !isAnswered)
        {
            elapsed += Time.deltaTime;
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                bool z = kb.zKey.wasPressedThisFrame;
                bool x = kb.xKey.wasPressedThisFrame;
                bool c = kb.cKey.wasPressedThisFrame;

                if (z || x || c)
                {
                    isAnswered = true;
                    string keyLower = node.TargetKey.ToLower();
                    isHit = (keyLower == "z" && z) || (keyLower == "x" && x) || (keyLower == "c" && c);
                    if (isHit) successCount++;
                }
            }
            yield return null;
        }

        BattleUIController.Instance.ShowSkillQTEResult(isHit);
        yield return new WaitForSeconds(0.35f); 
    }

    BattleUIController.Instance.HideSkillQTE();
    IsActive = false;
    onComplete?.Invoke(successCount, nodes.Count);
}
    public void ForceStop()
    {
        StopAllCoroutines();
        IsActive = false;
    }
}