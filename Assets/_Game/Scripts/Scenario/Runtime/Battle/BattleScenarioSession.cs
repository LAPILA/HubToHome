using System.Collections.Generic;

public sealed class BattleScenarioSession
{
    private readonly HashSet<string> _battleFiredRuleIds = new HashSet<string>();
    private readonly HashSet<string> _encounterFiredRuleIds = new HashSet<string>();

    public BattleScenarioSession(string scenarioId = "", string encounterMemoryKey = "")
    {
        ScenarioId = scenarioId ?? string.Empty;
        EncounterMemoryKey = encounterMemoryKey ?? string.Empty;
    }

    public string ScenarioId { get; }
    public string EncounterMemoryKey { get; }

    public bool HasRuleFired(BattleEventRuleData rule)
    {
        if (rule == null || rule.Once == BattleRuleOnceMode.Always)
        {
            return false;
        }

        string key = GetRuleKey(rule);
        if (rule.Once == BattleRuleOnceMode.PerEncounterMemory)
        {
            return _encounterFiredRuleIds.Contains(key);
        }

        return _battleFiredRuleIds.Contains(key);
    }

    public void MarkRuleFired(BattleEventRuleData rule)
    {
        if (rule == null || rule.Once == BattleRuleOnceMode.Always)
        {
            return;
        }

        string key = GetRuleKey(rule);
        if (rule.Once == BattleRuleOnceMode.PerEncounterMemory)
        {
            _encounterFiredRuleIds.Add(key);
            return;
        }

        _battleFiredRuleIds.Add(key);
    }

    public void ImportEncounterFiredRuleIds(IEnumerable<string> ruleIds)
    {
        if (ruleIds == null)
        {
            return;
        }

        foreach (string ruleId in ruleIds)
        {
            if (!string.IsNullOrWhiteSpace(ruleId))
            {
                _encounterFiredRuleIds.Add(ruleId.Trim());
            }
        }
    }

    public string[] ExportEncounterFiredRuleIds()
    {
        string[] result = new string[_encounterFiredRuleIds.Count];
        _encounterFiredRuleIds.CopyTo(result);
        return result;
    }

    private static string GetRuleKey(BattleEventRuleData rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return rule.RuleId.Trim();
        }

        return rule.SequenceId != null ? rule.SequenceId.Trim() : string.Empty;
    }
}
