using System.Collections.Generic;

public static class BattleEncounterMemoryRecorder
{
    public static BattleScenarioRuntime CreateRuntime(
        BattleScenarioData scenarioData,
        GlobalDataManager globalData,
        string fallbackEncounterId)
    {
        if (scenarioData == null)
        {
            return null;
        }

        string memoryKey = ResolveMemoryKey(scenarioData, fallbackEncounterId);
        IEnumerable<string> rememberedBeatIds = null;
        if (globalData != null && !string.IsNullOrEmpty(memoryKey))
        {
            rememberedBeatIds = globalData.GetEncounterSeenBeatIds(memoryKey);
        }

        return new BattleScenarioRuntime(scenarioData, rememberedBeatIds);
    }

    public static void RecordBattleStarted(
        BattleScenarioData scenarioData,
        GlobalDataManager globalData,
        string fallbackEncounterId)
    {
        if (globalData == null)
        {
            return;
        }

        string memoryKey = ResolveMemoryKey(scenarioData, fallbackEncounterId);
        if (!string.IsNullOrEmpty(memoryKey))
        {
            globalData.IncrementEncounterMeetCount(memoryKey);
        }
    }

    public static void RecordBattleResult(
        BattleScenarioData scenarioData,
        BattleScenarioRuntime runtime,
        GlobalDataManager globalData,
        string fallbackEncounterId,
        bool isVictory)
    {
        if (globalData == null)
        {
            return;
        }

        string memoryKey = ResolveMemoryKey(scenarioData, fallbackEncounterId);
        if (string.IsNullOrEmpty(memoryKey))
        {
            return;
        }

        if (runtime != null)
        {
            globalData.RememberEncounterBeatIds(memoryKey, runtime.ExportEncounterFiredRuleIds());
        }

        if (isVictory)
        {
            globalData.MarkEncounterDefeated(memoryKey);
        }
    }

    public static string ResolveMemoryKey(BattleScenarioData scenarioData, string fallbackEncounterId)
    {
        if (scenarioData != null && !string.IsNullOrWhiteSpace(scenarioData.MemoryKey))
        {
            return scenarioData.MemoryKey.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackEncounterId) ? string.Empty : fallbackEncounterId.Trim();
    }
}
