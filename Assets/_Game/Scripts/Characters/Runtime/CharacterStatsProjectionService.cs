using System;

/// <summary>
/// 저장 데이터 기반 화면에서도 런타임과 같은 CharacterStats 계산 경로를 사용한다.
/// </summary>
public static class CharacterStatsProjectionService
{
    public static StatBlock ResolveFromSave(
        CharacterSaveData saveData,
        CharacterData characterData)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (characterData == null)
            throw new ArgumentNullException(nameof(characterData));

        var stats = new CharacterStats();
        stats.SetBaseStats(characterData.BaseStats);

        CharacterBaseStatSnapshot calculated =
            CharacterGrowthService.CalculateBaseStats(saveData, characterData);
        StatBlock progressed = stats.BaseStats.Clone();
        progressed.MaxHP = calculated.MaxHP;
        progressed.MaxAP = calculated.MaxAP;
        progressed.ATK = calculated.Attack;
        progressed.DEF = calculated.Defense;
        progressed.SPD = calculated.Speed;

        stats.SetProgressedBaseStats(progressed);
        stats.SetEquipmentModifiers(
            EquipmentLoadoutService.BuildStatModifiers(saveData));
        return stats.ResolvedStats.Clone();
    }
}
