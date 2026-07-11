using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class BattleTriggerRuleCompatibilityMapper
{
    public static bool TryMap(
        BattleEventRuleData source,
        out ScenarioTriggerRuleData mapped,
        out string error)
    {
        mapped = null;
        error = string.Empty;
        if (source == null)
        {
            error = "Legacy Battle Event Rule is missing.";
            return false;
        }

        string eventId = EventId(source.EventType);
        if (string.IsNullOrEmpty(eventId))
        {
            error = "Unsupported legacy Battle Event Type: " + source.EventType;
            return false;
        }

        mapped = new ScenarioTriggerRuleData
        {
            RuleId = source.RuleId,
            DisplayNameKo = source.RuleId,
            EventId = eventId,
            Timing = ToScenarioTiming(source.Timing),
            Once = ToScenarioOnce(source.Once),
            Disabled = source.Disabled,
            SequenceId = source.SequenceId,
            TargetInputsJson = "{}",
            Conditions = Group()
        };

        AddLegacyConditions(source, mapped.Conditions);
        return true;
    }

    public static ScenarioTriggerTiming ToScenarioTiming(BattleRuleTiming timing)
    {
        switch (timing)
        {
            case BattleRuleTiming.AfterCurrentAction: return ScenarioTriggerTiming.AfterCurrentAction;
            case BattleRuleTiming.AfterCurrentSkill: return ScenarioTriggerTiming.AfterCurrentSkill;
            case BattleRuleTiming.AfterCurrentModule: return ScenarioTriggerTiming.AfterCurrentModule;
            default: return ScenarioTriggerTiming.Immediate;
        }
    }

    public static BattleRuleTiming ToBattleTiming(ScenarioTriggerTiming timing)
    {
        switch (timing)
        {
            case ScenarioTriggerTiming.AfterCurrentAction: return BattleRuleTiming.AfterCurrentAction;
            case ScenarioTriggerTiming.AfterCurrentSkill: return BattleRuleTiming.AfterCurrentSkill;
            case ScenarioTriggerTiming.AfterCurrentModule: return BattleRuleTiming.AfterCurrentModule;
            default: return BattleRuleTiming.Immediate;
        }
    }

    private static string EventId(BattleEventType eventType)
    {
        switch (eventType)
        {
            case BattleEventType.BattleStarted: return BuiltInScenarioEventIds.BattleStarted;
            case BattleEventType.EnemyHpCrossedBelow: return BuiltInScenarioEventIds.ParticipantHpChanged;
            case BattleEventType.EnemyDefeated: return BuiltInScenarioEventIds.ParticipantDefeated;
            case BattleEventType.SkillCompleted: return BuiltInScenarioEventIds.SkillCompleted;
            case BattleEventType.GameModuleCompleted: return BuiltInScenarioEventIds.ModuleCompleted;
            default: return string.Empty;
        }
    }

    private static ScenarioTriggerOnceScope ToScenarioOnce(BattleRuleOnceMode once)
    {
        switch (once)
        {
            case BattleRuleOnceMode.Always: return ScenarioTriggerOnceScope.Always;
            case BattleRuleOnceMode.PerEncounterMemory: return ScenarioTriggerOnceScope.EncounterMemory;
            default: return ScenarioTriggerOnceScope.Session;
        }
    }

    private static void AddLegacyConditions(
        BattleEventRuleData source,
        ScenarioTriggerConditionNodeData root)
    {
        switch (source.EventType)
        {
            case BattleEventType.BattleStarted:
            case BattleEventType.EnemyDefeated:
                AddParticipant(root, source.SubjectId);
                break;
            case BattleEventType.EnemyHpCrossedBelow:
                AddParticipant(root, source.SubjectId);
                root.Children.Add(Condition(
                    BuiltInTriggerConditionIds.NumberCrossedBelow,
                    new JObject
                    {
                        ["previousPath"] = "event.previousRatio",
                        ["currentPath"] = "event.currentRatio",
                        ["threshold"] = Math.Max(0d, Math.Min(1d, source.ThresholdRatio))
                    }));
                break;
            case BattleEventType.SkillCompleted:
                if (!string.IsNullOrWhiteSpace(source.SubjectId))
                {
                    root.Children.Add(Condition(
                        BuiltInTriggerConditionIds.ValueEquals,
                        new JObject
                        {
                            ["path"] = "event.skill",
                            ["value"] = source.SubjectId.Trim()
                        }));
                }
                break;
            case BattleEventType.GameModuleCompleted:
                AddModuleOutcome(root, source.SubjectId, source.OutcomeId);
                break;
        }
    }

    private static void AddParticipant(ScenarioTriggerConditionNodeData root, string subjectId)
    {
        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            root.Children.Add(Condition(
                BuiltInTriggerConditionIds.EventParticipant,
                new JObject { ["participant"] = subjectId.Trim() }));
        }
    }

    private static void AddModuleOutcome(
        ScenarioTriggerConditionNodeData root,
        string moduleId,
        string outcomeId)
    {
        string module = Normalize(moduleId);
        string outcome = Normalize(outcomeId);
        if (!string.IsNullOrEmpty(module))
        {
            var parameters = new JObject { ["module"] = module };
            if (!string.IsNullOrEmpty(outcome))
            {
                parameters["outcome"] = outcome;
            }

            root.Children.Add(Condition(BuiltInTriggerConditionIds.ModuleOutcome, parameters));
        }
        else if (!string.IsNullOrEmpty(outcome))
        {
            root.Children.Add(Condition(
                BuiltInTriggerConditionIds.ValueEquals,
                new JObject { ["path"] = "event.outcome", ["value"] = outcome }));
        }
    }

    private static ScenarioTriggerConditionNodeData Group()
    {
        return new ScenarioTriggerConditionNodeData
        {
            Kind = ScenarioConditionNodeKind.Group,
            GroupMode = ScenarioConditionGroupMode.All
        };
    }

    private static ScenarioTriggerConditionNodeData Condition(string id, JObject parameters)
    {
        return new ScenarioTriggerConditionNodeData
        {
            Kind = ScenarioConditionNodeKind.Condition,
            ConditionId = id,
            ParametersJson = parameters.ToString(Formatting.None)
        };
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
