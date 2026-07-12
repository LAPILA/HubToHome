public static class BattleEventRuleEvaluator
{
    public static bool TryEvaluate(
        BattleEventRuleData rule,
        BattleEventData battleEvent,
        BattleScenarioSession session,
        out BattleScenarioTrigger trigger)
    {
        trigger = null;

        if (rule == null || battleEvent == null || session == null)
        {
            return false;
        }

        if (rule.Timing != battleEvent.Timing)
        {
            return false;
        }

        if (!BattleTriggerRuleCompatibilityMapper.TryMap(rule, out ScenarioTriggerRuleData mapped, out _))
        {
            return false;
        }

        var evaluator = new ScenarioTriggerEvaluator();
        if (!evaluator.TryEvaluate(
                mapped,
                battleEvent.ToScenarioEvent(),
                session,
                null,
                out ScenarioTriggerMatch match,
                out _))
        {
            return false;
        }

        trigger = new BattleScenarioTrigger(match, battleEvent);
        return true;
    }
}
