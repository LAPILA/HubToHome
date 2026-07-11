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

    [Tooltip("Game Module이 보고한 결과 ID입니다. 예: completed, failed, victory, timeout")]
    public string OutcomeId = string.Empty;

    public static BattleEventData BattleStarted(BattleRuleTiming timing = BattleRuleTiming.Immediate)
    {
        return new BattleEventData
        {
            EventType = BattleEventType.BattleStarted,
            Timing = timing,
            SubjectId = "battle"
        };
    }

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

    public static BattleEventData GameModuleCompleted(
        string moduleId,
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
    {
        string normalizedModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
        return new BattleEventData
        {
            EventType = BattleEventType.GameModuleCompleted,
            Timing = timing,
            SubjectId = normalizedModuleId,
            ModuleId = normalizedModuleId,
            OutcomeId = string.IsNullOrWhiteSpace(outcomeId) ? string.Empty : outcomeId.Trim()
        };
    }

    public static BattleEventData EnemyDefeated(
        string enemyId,
        string sourceActorId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentAction)
    {
        return new BattleEventData
        {
            EventType = BattleEventType.EnemyDefeated,
            Timing = timing,
            SubjectId = Normalize(enemyId),
            SourceActorId = Normalize(sourceActorId),
            PreviousHpRatio = 0f,
            CurrentHpRatio = 0f
        };
    }

    public static BattleEventData SkillCompleted(
        string skillId,
        string sourceActorId = "",
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentSkill)
    {
        return new BattleEventData
        {
            EventType = BattleEventType.SkillCompleted,
            Timing = timing,
            SubjectId = Normalize(skillId),
            SourceActorId = Normalize(sourceActorId),
            OutcomeId = Normalize(outcomeId)
        };
    }

    public ScenarioEventData ToScenarioEvent()
    {
        var scenarioEvent = new ScenarioEventData(GetScenarioEventId())
        {
            SourceId = Normalize(SourceActorId)
        };

        switch (EventType)
        {
            case BattleEventType.BattleStarted:
                scenarioEvent.SetPayloadValue("subject", Normalize(SubjectId));
                break;
            case BattleEventType.EnemyHpCrossedBelow:
                scenarioEvent.SetPayloadValue("subject", Normalize(SubjectId));
                scenarioEvent.SetPayloadValue("previousRatio", PreviousHpRatio);
                scenarioEvent.SetPayloadValue("currentRatio", CurrentHpRatio);
                AddOptional(scenarioEvent, "sourceActor", SourceActorId);
                break;
            case BattleEventType.EnemyDefeated:
                scenarioEvent.SetPayloadValue("subject", Normalize(SubjectId));
                AddOptional(scenarioEvent, "sourceActor", SourceActorId);
                break;
            case BattleEventType.SkillCompleted:
                scenarioEvent.SetPayloadValue("skill", Normalize(SubjectId));
                AddOptional(scenarioEvent, "actor", SourceActorId);
                AddOptional(scenarioEvent, "outcome", OutcomeId);
                break;
            case BattleEventType.GameModuleCompleted:
                scenarioEvent.SetPayloadValue("module", Normalize(ModuleId));
                scenarioEvent.SetPayloadValue("subject", Normalize(SubjectId));
                AddOptional(scenarioEvent, "outcome", OutcomeId);
                break;
        }

        return scenarioEvent;
    }

    private string GetScenarioEventId()
    {
        switch (EventType)
        {
            case BattleEventType.BattleStarted: return BuiltInScenarioEventIds.BattleStarted;
            case BattleEventType.EnemyHpCrossedBelow: return BuiltInScenarioEventIds.ParticipantHpChanged;
            case BattleEventType.EnemyDefeated: return BuiltInScenarioEventIds.ParticipantDefeated;
            case BattleEventType.SkillCompleted: return BuiltInScenarioEventIds.SkillCompleted;
            case BattleEventType.GameModuleCompleted: return BuiltInScenarioEventIds.ModuleCompleted;
            default: return string.Empty;
        }
    }

    private static void AddOptional(ScenarioEventData scenarioEvent, string fieldId, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            scenarioEvent.SetPayloadValue(fieldId, value.Trim());
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
