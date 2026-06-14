using System.Collections.Generic;
using UnityEngine;

public sealed class BattleScenarioRuntime
{
    private readonly BattleScenarioEventRouter _eventRouter;

    public BattleScenarioRuntime(BattleScenarioData scenarioData)
    {
        ScenarioData = scenarioData;
        if (scenarioData == null)
        {
            return;
        }

        var session = new BattleScenarioSession(
            scenarioData.ScenarioId,
            scenarioData.MemoryKey);
        var ruleRunner = new BattleScenarioRuleRunner(scenarioData, session);
        _eventRouter = new BattleScenarioEventRouter(ruleRunner);
    }

    public BattleScenarioData ScenarioData { get; }

    public bool HasScenario
    {
        get { return ScenarioData != null && _eventRouter != null; }
    }

    public List<BattleScenarioTrigger> PublishEnemyHpCrossedBelow(
        string subjectId,
        int previousHp,
        int currentHp,
        int maxHp,
        BattleRuleTiming timing)
    {
        if (!HasScenario || string.IsNullOrWhiteSpace(subjectId) || maxHp <= 0)
        {
            return new List<BattleScenarioTrigger>();
        }

        BattleEventData battleEvent = BattleEventData.EnemyHpCrossedBelow(
            subjectId,
            Mathf.Clamp01((float)previousHp / maxHp),
            Mathf.Clamp01((float)currentHp / maxHp),
            timing);

        return _eventRouter.Publish(battleEvent);
    }

    public List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)
    {
        if (!HasScenario)
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Flush(timing);
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        sequence = null;
        return HasScenario && _eventRouter.TryResolveSequence(sequenceId, out sequence);
    }
}
