using System.Collections.Generic;

public sealed class BattleScenarioEventRouter
{
    private readonly BattleScenarioRuleRunner _ruleRunner;
    private readonly List<BattleEventData> _deferredEvents = new List<BattleEventData>();

    public BattleScenarioEventRouter(BattleScenarioRuleRunner ruleRunner)
    {
        _ruleRunner = ruleRunner;
    }

    public List<BattleScenarioTrigger> Publish(BattleEventData battleEvent)
    {
        if (battleEvent == null || _ruleRunner == null)
        {
            return new List<BattleScenarioTrigger>();
        }

        if (battleEvent.Timing == BattleRuleTiming.Immediate)
        {
            return _ruleRunner.Evaluate(battleEvent);
        }

        _deferredEvents.Add(battleEvent);
        return new List<BattleScenarioTrigger>();
    }

    public List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)
    {
        var triggers = new List<BattleScenarioTrigger>();
        if (_ruleRunner == null || _deferredEvents.Count == 0)
        {
            return triggers;
        }

        var remainingEvents = new List<BattleEventData>();
        for (int i = 0; i < _deferredEvents.Count; i++)
        {
            BattleEventData battleEvent = _deferredEvents[i];
            if (battleEvent == null || battleEvent.Timing != timing)
            {
                remainingEvents.Add(battleEvent);
                continue;
            }

            List<BattleScenarioTrigger> eventTriggers = _ruleRunner.Evaluate(battleEvent);
            for (int triggerIndex = 0; triggerIndex < eventTriggers.Count; triggerIndex++)
            {
                triggers.Add(eventTriggers[triggerIndex]);
            }
        }

        _deferredEvents.Clear();
        _deferredEvents.AddRange(remainingEvents);
        return triggers;
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        sequence = null;
        return _ruleRunner != null && _ruleRunner.TryResolveSequence(sequenceId, out sequence);
    }
}
