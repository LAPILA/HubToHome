using System;
using UnityEngine;

public static class BattleEventRuleEvaluator
{
    public static bool TryEvaluate(
        BattleEventRuleData rule,
        BattleEventData battleEvent,
        BattleScenarioSession session,
        out BattleScenarioTrigger trigger)
    {
        trigger = null;

        if (!CanEvaluate(rule, battleEvent, session))
        {
            return false;
        }

        bool matched = false;
        switch (rule.EventType)
        {
            case BattleEventType.EnemyHpCrossedBelow:
                matched = IsEnemyHpCrossedBelow(rule, battleEvent);
                break;
            case BattleEventType.GameModuleCompleted:
                matched = IsGameModuleCompleted(rule, battleEvent);
                break;
        }

        if (!matched)
        {
            return false;
        }

        session.MarkRuleFired(rule);
        trigger = new BattleScenarioTrigger(
            rule.RuleId,
            rule.SequenceId,
            rule.Timing,
            battleEvent);
        return true;
    }

    private static bool CanEvaluate(
        BattleEventRuleData rule,
        BattleEventData battleEvent,
        BattleScenarioSession session)
    {
        if (rule == null || battleEvent == null || session == null)
        {
            return false;
        }

        if (rule.Disabled || rule.EventType == BattleEventType.None)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.SequenceId))
        {
            return false;
        }

        if (rule.EventType != battleEvent.EventType || rule.Timing != battleEvent.Timing)
        {
            return false;
        }

        return !session.HasRuleFired(rule);
    }

    private static bool IsEnemyHpCrossedBelow(
        BattleEventRuleData rule,
        BattleEventData battleEvent)
    {
        if (!MatchesSubject(rule.SubjectId, battleEvent.SubjectId))
        {
            return false;
        }

        float threshold = Mathf.Clamp01(rule.ThresholdRatio);
        return battleEvent.PreviousHpRatio > threshold
            && battleEvent.CurrentHpRatio <= threshold;
    }

    private static bool IsGameModuleCompleted(
        BattleEventRuleData rule,
        BattleEventData battleEvent)
    {
        if (!MatchesSubject(rule.SubjectId, battleEvent.ModuleId)
            && !MatchesSubject(rule.SubjectId, battleEvent.SubjectId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.OutcomeId))
        {
            return true;
        }

        return string.Equals(
            rule.OutcomeId.Trim(),
            battleEvent.OutcomeId != null ? battleEvent.OutcomeId.Trim() : string.Empty,
            StringComparison.Ordinal);
    }

    private static bool MatchesSubject(string ruleSubjectId, string eventSubjectId)
    {
        if (string.IsNullOrWhiteSpace(ruleSubjectId))
        {
            return true;
        }

        return string.Equals(
            ruleSubjectId.Trim(),
            eventSubjectId != null ? eventSubjectId.Trim() : string.Empty,
            StringComparison.Ordinal);
    }
}
