using System;
using UnityEngine;

public enum BattleEventType
{
    None,
    EnemyHpCrossedBelow,
    EnemyDefeated,
    SkillCompleted,
    GameModuleCompleted,
    BattleStarted
}

public enum BattleRuleTiming
{
    Immediate,
    AfterCurrentAction,
    AfterCurrentSkill,
    AfterCurrentModule
}

public enum BattleRuleOnceMode
{
    Always,
    PerBattle,
    PerEncounterMemory
}

[Serializable]
public sealed class BattleEventRuleData
{
    public string RuleId = string.Empty;
    public BattleEventType EventType = BattleEventType.None;
    public BattleRuleTiming Timing = BattleRuleTiming.Immediate;
    public BattleRuleOnceMode Once = BattleRuleOnceMode.PerBattle;

    [Tooltip("적, 아군, 모듈 등 이벤트 대상의 안정적인 ID입니다.")]
    public string SubjectId = string.Empty;

    [Tooltip("모듈 완료/결과 규칙에서 선택적으로 비교할 outcome ID입니다. 비워두면 어떤 outcome이든 허용합니다.")]
    public string OutcomeId = string.Empty;

    [Range(0f, 1f)]
    [Tooltip("HP 임계치 규칙에서 사용하는 0~1 비율입니다.")]
    public float ThresholdRatio = 0.5f;

    [Tooltip("조건이 성립하면 실행할 Action Sequence ID입니다.")]
    public string SequenceId = string.Empty;

    [Tooltip("임시로 규칙을 비활성화합니다.")]
    public bool Disabled;
}
