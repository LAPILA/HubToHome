using System.Collections.Generic;

public sealed class BattleScenarioEventRouter
{
    private readonly BattleScenarioRuleRunner _ruleRunner;
    private readonly List<BattleScenarioTrigger> _deferredTriggers = new List<BattleScenarioTrigger>();

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

        return Route(_ruleRunner.Evaluate(battleEvent));
    }

    public List<BattleScenarioTrigger> Publish(ScenarioEventData scenarioEvent)
    {
        if (scenarioEvent == null || _ruleRunner == null)
        {
            return new List<BattleScenarioTrigger>();
        }

        return Route(_ruleRunner.Evaluate(scenarioEvent));
    }

    public List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)
    {
        return Flush(BattleTriggerRuleCompatibilityMapper.ToScenarioTiming(timing));
    }

    public List<BattleScenarioTrigger> FlushCheckpoint(string checkpointId)
    {
        return Flush(ScenarioTriggerTiming.Checkpoint, checkpointId);
    }

    public List<BattleScenarioTrigger> Flush(
        ScenarioTriggerTiming timing,
        string checkpointId = "")
    {
        var triggers = new List<BattleScenarioTrigger>();
        if (_ruleRunner == null || _deferredTriggers.Count == 0)
        {
            return triggers;
        }

        string normalizedCheckpoint = Normalize(checkpointId);
        var remaining = new List<BattleScenarioTrigger>();
        for (int i = 0; i < _deferredTriggers.Count; i++)
        {
            BattleScenarioTrigger trigger = _deferredTriggers[i];
            if (trigger == null)
            {
                continue;
            }

            bool checkpointMatches = timing != ScenarioTriggerTiming.Checkpoint
                || Normalize(trigger.CheckpointId) == normalizedCheckpoint;
            if (trigger.ScenarioTiming != timing || !checkpointMatches)
            {
                remaining.Add(trigger);
                continue;
            }

            if (_ruleRunner.TryCommit(trigger))
            {
                triggers.Add(trigger);
            }
        }

        _deferredTriggers.Clear();
        _deferredTriggers.AddRange(remaining);
        return triggers;
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        sequence = null;
        return _ruleRunner != null && _ruleRunner.TryResolveSequence(sequenceId, out sequence);
    }

    private List<BattleScenarioTrigger> Route(IReadOnlyList<BattleScenarioTrigger> evaluated)
    {
        var immediate = new List<BattleScenarioTrigger>();
        if (evaluated == null)
        {
            return immediate;
        }

        for (int i = 0; i < evaluated.Count; i++)
        {
            BattleScenarioTrigger trigger = evaluated[i];
            if (trigger == null)
            {
                continue;
            }

            if (trigger.ScenarioTiming == ScenarioTriggerTiming.Immediate)
            {
                if (_ruleRunner.TryCommit(trigger))
                {
                    immediate.Add(trigger);
                }
            }
            else
            {
                _deferredTriggers.Add(trigger);
            }
        }

        return immediate;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
