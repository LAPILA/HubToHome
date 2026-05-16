using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public enum BattleNarrationEventType
{
    BattleStart,
    PlayerTurnStart,
    PlayerAttack,
    PlayerSkillUse,
    PlayerItemUse,
    EnemyBasicAttack,
    EnemySkillPrepare,
    EnemyStrongAttackPrepare,
    DamageTaken,
    HealReceived,
    Victory,
    Defeat,
    Flavor
}

public enum BattleFlavorTriggerType
{
    TurnCountAtLeast,
    EnemyHpBelowPercent,
    BattleStart
}

[Serializable]
public class BattleNarrationTemplate
{
    [LabelText("이벤트 타입")] public BattleNarrationEventType EventType;
    [TextArea(2, 3)] public string Template = "{actor}가 행동했다!";
    public BattleNarrationStyle Style = BattleNarrationStyle.Normal;
    public BattleNarrationPriority Priority = BattleNarrationPriority.Normal;
    [MinValue(-1f)] public float HoldOverride = -1f;
}

[Serializable]
public class BattleFlavorRule
{
    [LabelText("트리거 타입")] public BattleFlavorTriggerType TriggerType;
    [LabelText("최소 턴 수")] public int MinTurnCount = 1;
    [Range(0f, 1f), LabelText("적 HP 비율 이하")] public float EnemyHpBelowPercent = 0.5f;
    [LabelText("특정 적 ID(선택)")] public string EnemyNameFilter = "";
    [TextArea(2, 3)] public string Template = "{enemy}가 당신을 노려본다...";
    public BattleNarrationStyle Style = BattleNarrationStyle.Warning;
    public BattleNarrationPriority Priority = BattleNarrationPriority.High;
    [MinValue(-1f)] public float HoldOverride = -1f;
    [HideInInspector] public bool TriggeredOnce;
}

[CreateAssetMenu(fileName = "BattleNarrationConfig", menuName = "HubToHome/BattleNarrationConfig")]
public class BattleNarrationConfig : SerializedScriptableObject
{
    [BoxGroup("Templates")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<BattleNarrationTemplate> Templates = new List<BattleNarrationTemplate>();

    [BoxGroup("Flavor Rules")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<BattleFlavorRule> FlavorRules = new List<BattleFlavorRule>();

    public BattleNarrationTemplate GetTemplate(BattleNarrationEventType type)
    {
        return Templates != null ? Templates.Find(t => t != null && t.EventType == type) : null;
    }

    public void ResetRuntimeState()
    {
        if (FlavorRules == null) return;
        foreach (var rule in FlavorRules)
        {
            if (rule != null) rule.TriggeredOnce = false;
        }
    }
}
