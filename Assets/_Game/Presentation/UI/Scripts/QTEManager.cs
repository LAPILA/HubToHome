using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 QTE 매니저 (Singleton & Command Pattern).
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }

    #region [ QTE Settings ]
    [BoxGroup("Defense QTE Windows"), LabelWidth(160)] 
    [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;
    
    [BoxGroup("Defense QTE Windows"), LabelWidth(160)] 
    [SerializeField, Range(0f, 0.4f)] private float _greatWindow   = 0.22f;
    
    [BoxGroup("Defense QTE Windows"), LabelWidth(160)] 
    [SerializeField, Range(0f, 0.6f)] private float _goodWindow    = 0.40f;
    #endregion

    public bool IsActive { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #region [ Defense QTE (방어) ]
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
                
                // 단일 프레임 동시입력 방지를 위해 if-else 구조 명확화
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
    #endregion

    #region [ Sequence QTE (스킬 공격) ]
    public void StartSequenceQTE(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        if (IsActive || nodes == null || nodes.Count == 0) return;
        StartCoroutine(SequenceQTERoutine(nodes, timeLimit, onComplete));
    }

    private IEnumerator SequenceQTERoutine(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        IsActive = true;
        int successCount = 0;

        Canvas.ForceUpdateCanvases();

        // 1. [UI 예열]
        BattleUIController.Instance.ShowSkillQTE(Vector2.zero, "", 0f); 
        
        yield return null; 
        yield return null; 

        foreach (var node in nodes)
        {
            // 2. 실제 노드 좌표 전달
            Vector2 relativePos = new Vector2(node.PosX, node.PosY);
            BattleUIController.Instance.ShowSkillQTE(relativePos, node.TargetKey, timeLimit);

            float elapsed = 0f;
            bool isAnswered = false;
            bool isHit = false;

            yield return null; // UI 렌더링 대기

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
                        
                        // 문자열 할당을 피하기 위해 Equals(OrdinalIgnoreCase) 사용 권장되지만,
                        // 단순 알파벳이므로 ToLower() 유지하되 명확히 처리
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
    #endregion

    public void ForceStop()
    {
        StopAllCoroutines();
        IsActive = false;
    }
}