using System.Collections.Generic;
using UnityEngine;

public sealed class BattleScenarioRuntime
{
    private readonly BattleScenarioEventRouter _eventRouter;
    private readonly BattleScenarioSession _session;

    public BattleScenarioRuntime(
        BattleScenarioData scenarioData,
        IEnumerable<string> encounterFiredRuleIds = null)
    {
        ScenarioData = scenarioData;
        SessionState = BattleSessionState.Create(scenarioData);
        if (scenarioData == null)
        {
            return;
        }

        _session = new BattleScenarioSession(
            scenarioData.ScenarioId,
            scenarioData.MemoryKey);
        _session.ImportEncounterFiredRuleIds(encounterFiredRuleIds);

        var ruleRunner = new BattleScenarioRuleRunner(scenarioData, _session);
        _eventRouter = new BattleScenarioEventRouter(ruleRunner);
    }

    public BattleScenarioData ScenarioData { get; }

    public BattleSessionState SessionState { get; }

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

    public string[] ExportEncounterFiredRuleIds()
    {
        return _session != null ? _session.ExportEncounterFiredRuleIds() : new string[0];
    }
}

public sealed class BattleSessionState : IGameModuleStateStore
{
    private BattleSessionState(
        string scenarioId,
        string primaryMode,
        string openingModule)
    {
        ScenarioId = Normalize(scenarioId);
        PrimaryMode = string.IsNullOrWhiteSpace(primaryMode) ? "battle" : primaryMode.Trim();
        OpeningModule = Normalize(openingModule);
        CurrentModuleId = OpeningModule;
    }

    public string ScenarioId { get; }
    public string PrimaryMode { get; }
    public string OpeningModule { get; }
    public string CurrentModuleId { get; private set; }

    public static BattleSessionState Create(BattleScenarioData scenarioData)
    {
        if (scenarioData == null)
        {
            return new BattleSessionState(string.Empty, "battle", BattleTurnQteGameModuleRuntime.Id);
        }

        string openingModule = string.IsNullOrWhiteSpace(scenarioData.OpeningModule)
            ? BattleTurnQteGameModuleRuntime.Id
            : scenarioData.OpeningModule;

        return new BattleSessionState(
            scenarioData.ScenarioId,
            scenarioData.PrimaryMode,
            openingModule);
    }

    public void SetCurrentModuleId(string moduleId)
    {
        CurrentModuleId = Normalize(moduleId);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
