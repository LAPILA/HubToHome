using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    private Action<DefenseInput, QTEGrade> _activeDefenseCallback;
    private Action<int, int> _activeSequenceCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #region [ Defense QTE (방어) ]
    public void StartDefenseQTE(float attackDelay, float difficultyMult, Action<DefenseInput, QTEGrade> onResult)
    {
        if (IsActive) ForceStop();
        _activeDefenseCallback = onResult;
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

            if (BattleManager.Instance != null && BattleManager.Instance._playerParty.Count > 0)
            {
                var ctrl = BattleManager.Instance._playerParty[0]?.GetComponent<PlayerController>();
                if (ctrl != null && ctrl.TryConsumeBufferedDefenseInput(out input))
                {
                    inputReceived = true;
                    yield return null;
                    continue;
                }
            }
            
            if (GameInput.TryReadDefenseInputThisFrame(out input))
                inputReceived = true;
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

        CompleteDefense(input, grade);
    }
    #endregion

    #region [ Sequence QTE (스킬 공격) ]
    public void StartSequenceQTE(List<SkillQTENode> nodes, float timeLimit, Action<int, int> onComplete)
    {
        if (nodes == null || nodes.Count == 0) return;
        if (IsActive) ForceStop();
        _activeSequenceCallback = onComplete;
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
                
                if (GameInput.TryReadDefenseInputThisFrame(out DefenseInput input))
                {
                    isAnswered = true;
                    string keyLower = node.TargetKey.ToLower();
                    isHit = (keyLower == "z" && input == DefenseInput.Parry)
                        || (keyLower == "x" && input == DefenseInput.Dodge)
                        || (keyLower == "c" && input == DefenseInput.Jump);
                    if (isHit) successCount++;
                }
                yield return null;
            }

            BattleUIController.Instance.ShowSkillQTEResult(isHit);
            yield return new WaitForSeconds(0.35f); 
        }

        BattleUIController.Instance.HideSkillQTE();
        IsActive = false;
        CompleteSequence(successCount, nodes.Count);
    }
    #endregion

    public void ForceStop()
    {
        StopAllCoroutines();
        CompleteDefense(DefenseInput.None, QTEGrade.Miss);
        CompleteSequence(0, 0);
        IsActive = false;
    }

    private void CompleteDefense(DefenseInput input, QTEGrade grade)
    {
        Action<DefenseInput, QTEGrade> callback = _activeDefenseCallback;
        _activeDefenseCallback = null;
        callback?.Invoke(input, grade);
    }

    private void CompleteSequence(int successCount, int totalCount)
    {
        Action<int, int> callback = _activeSequenceCallback;
        _activeSequenceCallback = null;
        callback?.Invoke(successCount, totalCount);
    }
}
