using System.Collections.Generic;

public enum FieldEncounterResolution
{
    PreemptiveBattle,
    InstantVictory
}

/// <summary>
/// 오버월드 선공이 전투 진입인지 즉시 처치인지 판정하는 순수 정책입니다.
/// 연출과 저장 처리는 호출자가 담당합니다.
/// </summary>
public static class FieldEncounterPolicy
{
    public static FieldEncounterResolution Evaluate(
        int highestPartyLevel,
        IReadOnlyList<EnemyData> enemies,
        bool previouslyDefeated,
        bool encounterAllowsInstantKill)
    {
        if (!previouslyDefeated || !encounterAllowsInstantKill || enemies == null || enemies.Count == 0)
            return FieldEncounterResolution.PreemptiveBattle;

        int partyLevel = highestPartyLevel < 1 ? 1 : highestPartyLevel;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy == null || !enemy.AllowInstantKillAfterDefeat)
                return FieldEncounterResolution.PreemptiveBattle;

            int requiredLevel = System.Math.Max(1, enemy.ThreatLevel)
                + System.Math.Max(0, enemy.InstantKillLevelGap);
            if (partyLevel < requiredLevel)
                return FieldEncounterResolution.PreemptiveBattle;
        }

        return FieldEncounterResolution.InstantVictory;
    }
}
