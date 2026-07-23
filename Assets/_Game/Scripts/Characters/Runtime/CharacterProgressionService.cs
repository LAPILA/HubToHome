using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterLevelUpResult
{
    public string CharacterDataId;
    public int PreviousLevel;
    public int NewLevel;
    public int ExperienceGained;
    public int RemainingExperience;
    public int MaxHpGained;
    public int MaxMpGained;
    public int AttackGained;
    public int DefenseGained;
    public int SpeedGained;

    public bool DidLevelUp => NewLevel > PreviousLevel;
}

public static class CharacterProgressionService
{
    public const int DefaultMaxLevel = 99;

    public static int ExperienceRequiredForNextLevel(CharacterData data, int currentLevel)
    {
        int level = Mathf.Max(1, currentLevel);
        int baseExperience = data != null ? Mathf.Max(1, data.BaseExperienceToLevel) : 100;
        float growth = data != null ? Mathf.Max(1f, data.ExperienceGrowth) : 1.18f;
        double required = baseExperience * Math.Pow(growth, level - 1);
        if (double.IsNaN(required) || required <= 1d) return 1;
        if (double.IsInfinity(required) || required >= int.MaxValue) return int.MaxValue;
        return Math.Max(1, (int)Math.Round(required, MidpointRounding.ToEven));
    }

    public static CharacterLevelUpResult GrantExperience(CharacterSaveData saveData, CharacterData data, int amount)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));

        int gained = Mathf.Max(0, amount);
        int previousLevel = Mathf.Max(1, saveData.Level);
        int maxLevel = data != null ? Mathf.Clamp(data.MaxLevel, 1, DefaultMaxLevel) : DefaultMaxLevel;

        saveData.Level = previousLevel;
        saveData.EXP = SaturatingAdd(saveData.EXP, gained);

        int hpGain = 0;
        int mpGain = 0;
        int attackGain = 0;
        int defenseGain = 0;
        int speedGain = 0;

        while (saveData.Level < maxLevel)
        {
            int required = ExperienceRequiredForNextLevel(data, saveData.Level);
            if (saveData.EXP < required) break;

            saveData.EXP -= required;
            saveData.Level++;

            hpGain = SaturatingAdd(hpGain, data != null ? data.MaxHpPerLevel : 5);
            mpGain = SaturatingAdd(mpGain, data != null ? data.MaxMpPerLevel : 2);
            attackGain = SaturatingAdd(attackGain, data != null ? data.AttackPerLevel : 1);
            defenseGain = SaturatingAdd(defenseGain, data != null ? data.DefensePerLevel : 1);
            speedGain = SaturatingAdd(speedGain, data != null ? data.SpeedPerLevel : 0);
        }

        if (saveData.Level >= maxLevel)
            saveData.EXP = 0;

        saveData.MaxHP = Mathf.Max(1, SaturatingAdd(saveData.MaxHP, hpGain));
        saveData.MaxMP = SaturatingAdd(saveData.MaxMP, mpGain);
        saveData.ATK = Mathf.Max(1, SaturatingAdd(saveData.ATK, attackGain));
        saveData.DEF = SaturatingAdd(saveData.DEF, defenseGain);
        saveData.SPD = Mathf.Max(1, SaturatingAdd(saveData.SPD, speedGain));
        saveData.HP = Math.Min(SaturatingAdd(saveData.HP, hpGain), saveData.MaxHP);
        saveData.MP = Math.Min(SaturatingAdd(saveData.MP, mpGain), saveData.MaxMP);

        return new CharacterLevelUpResult
        {
            CharacterDataId = saveData.CharacterDataID,
            PreviousLevel = previousLevel,
            NewLevel = saveData.Level,
            ExperienceGained = gained,
            RemainingExperience = saveData.EXP,
            MaxHpGained = hpGain,
            MaxMpGained = mpGain,
            AttackGained = attackGain,
            DefenseGained = defenseGain,
            SpeedGained = speedGain
        };
    }

    private static int SaturatingAdd(int left, int right)
    {
        long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return (int)Math.Min(sum, int.MaxValue);
    }
}
