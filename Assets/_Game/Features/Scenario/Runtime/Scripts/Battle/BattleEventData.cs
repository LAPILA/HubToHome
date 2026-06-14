using System;
using UnityEngine;

[Serializable]
public sealed class BattleEventData
{
    public BattleEventType EventType = BattleEventType.None;
    public BattleRuleTiming Timing = BattleRuleTiming.Immediate;

    [Tooltip("이벤트가 발생한 적, 아군, 모듈 등의 안정적인 ID입니다.")]
    public string SubjectId = string.Empty;

    [Range(0f, 1f)]
    public float PreviousHpRatio = 1f;

    [Range(0f, 1f)]
    public float CurrentHpRatio = 1f;

    [Tooltip("이 이벤트를 유발한 actor ID입니다. 아직 선택적으로만 사용합니다.")]
    public string SourceActorId = string.Empty;

    [Tooltip("이 이벤트가 발생한 Game Module ID입니다. 아직 선택적으로만 사용합니다.")]
    public string ModuleId = string.Empty;

    public static BattleEventData EnemyHpCrossedBelow(
        string enemyId,
        float previousHpRatio,
        float currentHpRatio,
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentSkill)
    {
        return new BattleEventData
        {
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = timing,
            SubjectId = enemyId ?? string.Empty,
            PreviousHpRatio = Mathf.Clamp01(previousHpRatio),
            CurrentHpRatio = Mathf.Clamp01(currentHpRatio)
        };
    }
}
