using System;
using UnityEngine;

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

        CharacterBaseStatSnapshot calculated =
            CharacterGrowthService.CalculateBaseStats(saveData, characterData);
        return ResolveFromBaseStats(saveData, characterData, calculated);
    }

    /// <summary>저장 HP/AP를 제한하는 실제 상한입니다. 성장 기본치에 장비를 한 번만 적용합니다.</summary>
    public static void ResolveResourceCaps(
        CharacterSaveData saveData,
        CharacterData characterData,
        out int maxHp,
        out int maxAp)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));

        StatBlock resolved = ResolveFromBaseStats(
            saveData,
            characterData,
            characterData != null
                ? CharacterGrowthService.CalculateBaseStats(saveData, characterData)
                : new CharacterBaseStatSnapshot(
                    saveData.MaxHP, saveData.MaxAP, saveData.ATK, saveData.DEF, saveData.SPD));
        maxHp = Mathf.Max(1, resolved.MaxHP);
        maxAp = Mathf.Max(0, resolved.MaxAP);
    }

    // 성장 투자 전후 계산에도 사용합니다. 전달된 기본치를 다시 성장 계산하지 않습니다.
    internal static StatBlock ResolveFromBaseStats(
        CharacterSaveData saveData,
        CharacterData characterData,
        CharacterBaseStatSnapshot calculated)
    {
        var stats = new CharacterStats();
        stats.SetBaseStats(characterData != null ? characterData.BaseStats : new StatBlock());
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
