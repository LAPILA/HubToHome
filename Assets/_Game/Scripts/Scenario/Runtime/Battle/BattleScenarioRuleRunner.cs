using System;
using System.Collections.Generic;

public sealed class BattleScenarioRuleRunner
{
    private readonly BattleScenarioData _scenario;
    private readonly BattleScenarioSession _session;

    public BattleScenarioRuleRunner(
        BattleScenarioData scenario,
        BattleScenarioSession session)
    {
        _scenario = scenario;
        _session = session ?? new BattleScenarioSession();
    }

    public BattleScenarioData Scenario
    {
        get { return _scenario; }
    }

    public BattleScenarioSession Session
    {
        get { return _session; }
    }

    public List<BattleScenarioTrigger> Evaluate(BattleEventData battleEvent)
    {
        var triggers = new List<BattleScenarioTrigger>();
        if (_scenario == null || _scenario.Rules == null || battleEvent == null)
        {
            return triggers;
        }

        for (int i = 0; i < _scenario.Rules.Count; i++)
        {
            BattleEventRuleData rule = _scenario.Rules[i];
            BattleScenarioTrigger trigger;
            if (BattleEventRuleEvaluator.TryEvaluate(rule, battleEvent, _session, out trigger))
            {
                triggers.Add(trigger);
            }
        }

        return triggers;
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        sequence = null;
        if (_scenario == null || _scenario.Sequences == null || string.IsNullOrWhiteSpace(sequenceId))
        {
            return false;
        }

        string normalizedId = sequenceId.Trim();
        for (int i = 0; i < _scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset candidate = _scenario.Sequences[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.SequenceId, normalizedId, StringComparison.Ordinal))
            {
                sequence = candidate;
                return true;
            }
        }

        return false;
    }
}
