using System.Collections.Generic;

public sealed class BattleScenarioSession : IScenarioTriggerHistory
{
    private readonly HashSet<string> _battleFiredRuleIds = new HashSet<string>();
    private readonly HashSet<string> _encounterFiredRuleIds = new HashSet<string>();
    private readonly HashSet<string> _saveFiredRuleIds = new HashSet<string>();

    public BattleScenarioSession(string scenarioId = "", string encounterMemoryKey = "")
    {
        ScenarioId = scenarioId ?? string.Empty;
        EncounterMemoryKey = encounterMemoryKey ?? string.Empty;
    }

    public string ScenarioId { get; }
    public string EncounterMemoryKey { get; }

    public bool HasRuleFired(BattleEventRuleData rule)
    {
        if (rule == null)
        {
            return false;
        }

        return HasRuleFired(GetRuleKey(rule), ToScenarioScope(rule.Once));
    }

    public void MarkRuleFired(BattleEventRuleData rule)
    {
        if (rule == null)
        {
            return;
        }

        MarkRuleFired(GetRuleKey(rule), ToScenarioScope(rule.Once));
    }

    public bool HasRuleFired(string ruleKey, ScenarioTriggerOnceScope scope)
    {
        if (scope == ScenarioTriggerOnceScope.Always)
        {
            return false;
        }

        string key = Normalize(ruleKey);
        switch (scope)
        {
            case ScenarioTriggerOnceScope.EncounterMemory:
                return _encounterFiredRuleIds.Contains(key);
            case ScenarioTriggerOnceScope.Save:
                return _saveFiredRuleIds.Contains(key);
            default:
                return _battleFiredRuleIds.Contains(key);
        }
    }

    public void MarkRuleFired(string ruleKey, ScenarioTriggerOnceScope scope)
    {
        if (scope == ScenarioTriggerOnceScope.Always)
        {
            return;
        }

        string key = Normalize(ruleKey);
        switch (scope)
        {
            case ScenarioTriggerOnceScope.EncounterMemory:
                _encounterFiredRuleIds.Add(key);
                break;
            case ScenarioTriggerOnceScope.Save:
                _saveFiredRuleIds.Add(key);
                break;
            default:
                _battleFiredRuleIds.Add(key);
                break;
        }
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

    public void ImportSaveFiredRuleIds(IEnumerable<string> ruleIds)
    {
        Import(ruleIds, _saveFiredRuleIds);
    }

    public string[] ExportSaveFiredRuleIds()
    {
        string[] result = new string[_saveFiredRuleIds.Count];
        _saveFiredRuleIds.CopyTo(result);
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

    private static ScenarioTriggerOnceScope ToScenarioScope(BattleRuleOnceMode once)
    {
        switch (once)
        {
            case BattleRuleOnceMode.Always: return ScenarioTriggerOnceScope.Always;
            case BattleRuleOnceMode.PerEncounterMemory: return ScenarioTriggerOnceScope.EncounterMemory;
            default: return ScenarioTriggerOnceScope.Session;
        }
    }

    private static void Import(IEnumerable<string> ruleIds, HashSet<string> target)
    {
        if (ruleIds == null)
        {
            return;
        }

        foreach (string ruleId in ruleIds)
        {
            if (!string.IsNullOrWhiteSpace(ruleId))
            {
                target.Add(ruleId.Trim());
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
