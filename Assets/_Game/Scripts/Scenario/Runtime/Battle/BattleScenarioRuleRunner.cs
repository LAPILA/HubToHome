using System;
using System.Collections.Generic;

public sealed class BattleScenarioRuleRunner
{
    private readonly BattleScenarioData _scenario;
    private readonly BattleScenarioSession _session;
    private readonly ScenarioTriggerEvaluator _evaluator;
    private readonly ActionExecutionContext _values;
    private readonly List<ResolvedRule> _rules = new List<ResolvedRule>();
    private readonly List<string> _lastEvaluationErrors = new List<string>();

    public BattleScenarioRuleRunner(
        BattleScenarioData scenario,
        BattleScenarioSession session,
        TriggerConditionRegistry conditions = null,
        ActionExecutionContext values = null)
    {
        _scenario = scenario;
        _session = session ?? new BattleScenarioSession();
        _evaluator = new ScenarioTriggerEvaluator(conditions);
        _values = values;
        BuildResolvedRules();
    }

    public BattleScenarioData Scenario
    {
        get { return _scenario; }
    }

    public BattleScenarioSession Session
    {
        get { return _session; }
    }

    public IReadOnlyList<string> LastEvaluationErrors
    {
        get { return _lastEvaluationErrors; }
    }

    public int ResolvedRuleCount
    {
        get { return _rules.Count; }
    }

    public List<BattleScenarioTrigger> Evaluate(BattleEventData battleEvent)
    {
        if (battleEvent == null)
        {
            return new List<BattleScenarioTrigger>();
        }

        return Evaluate(
            battleEvent.ToScenarioEvent(),
            BattleTriggerRuleCompatibilityMapper.ToScenarioTiming(battleEvent.Timing),
            battleEvent);
    }

    public List<BattleScenarioTrigger> Evaluate(ScenarioEventData scenarioEvent)
    {
        return Evaluate(scenarioEvent, null, null);
    }

    private List<BattleScenarioTrigger> Evaluate(
        ScenarioEventData scenarioEvent,
        ScenarioTriggerTiming? compatibilityTiming,
        BattleEventData sourceEvent)
    {
        var triggers = new List<BattleScenarioTrigger>();
        _lastEvaluationErrors.Clear();
        if (_scenario == null || scenarioEvent == null)
        {
            return triggers;
        }

        for (int i = 0; i < _rules.Count; i++)
        {
            ResolvedRule resolved = _rules[i];
            if (resolved.IsLegacy
                && (!compatibilityTiming.HasValue || compatibilityTiming.Value != resolved.Rule.Timing))
            {
                continue;
            }

            if (_evaluator.TryEvaluate(
                    resolved.Rule,
                    scenarioEvent,
                    _session,
                    _values,
                    out ScenarioTriggerMatch match,
                    out string error,
                    commitHistory: false))
            {
                triggers.Add(new BattleScenarioTrigger(match, sourceEvent));
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                _lastEvaluationErrors.Add(error);
            }
        }

        return triggers;
    }

    public bool TryCommit(BattleScenarioTrigger trigger)
    {
        if (trigger == null)
        {
            return false;
        }

        if (trigger.OnceScope != ScenarioTriggerOnceScope.Always
            && _session.HasRuleFired(trigger.HistoryKey, trigger.OnceScope))
        {
            return false;
        }

        _session.MarkRuleFired(trigger.HistoryKey, trigger.OnceScope);
        return true;
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

    private void BuildResolvedRules()
    {
        if (_scenario == null)
        {
            return;
        }

        if (_scenario.Rules != null)
        {
            for (int i = 0; i < _scenario.Rules.Count; i++)
            {
                BattleEventRuleData legacy = _scenario.Rules[i];
                if (BattleTriggerRuleCompatibilityMapper.TryMap(legacy, out ScenarioTriggerRuleData mapped, out _))
                {
                    _rules.Add(new ResolvedRule(mapped, true));
                }
            }
        }

        if (_scenario.TriggerRules != null)
        {
            for (int i = 0; i < _scenario.TriggerRules.Count; i++)
            {
                ScenarioTriggerRuleData rule = _scenario.TriggerRules[i];
                if (rule != null)
                {
                    _rules.Add(new ResolvedRule(rule, false));
                }
            }
        }
    }

    private readonly struct ResolvedRule
    {
        public ResolvedRule(ScenarioTriggerRuleData rule, bool isLegacy)
        {
            Rule = rule;
            IsLegacy = isLegacy;
        }

        public ScenarioTriggerRuleData Rule { get; }
        public bool IsLegacy { get; }
    }
}
