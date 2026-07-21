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
        return Mathf.Max(1, Mathf.RoundToInt(baseExperience * Mathf.Pow(growth, level - 1)));
    }

    public static CharacterLevelUpResult GrantExperience(CharacterSaveData saveData, CharacterData data, int amount)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));

        int gained = Mathf.Max(0, amount);
        int previousLevel = Mathf.Max(1, saveData.Level);
        int maxLevel = data != null ? Mathf.Clamp(data.MaxLevel, 1, DefaultMaxLevel) : DefaultMaxLevel;

        saveData.Level = previousLevel;
        saveData.EXP = Mathf.Max(0, saveData.EXP) + gained;

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

            hpGain += data != null ? Mathf.Max(0, data.MaxHpPerLevel) : 5;
            mpGain += data != null ? Mathf.Max(0, data.MaxMpPerLevel) : 2;
            attackGain += data != null ? Mathf.Max(0, data.AttackPerLevel) : 1;
            defenseGain += data != null ? Mathf.Max(0, data.DefensePerLevel) : 1;
            speedGain += data != null ? Mathf.Max(0, data.SpeedPerLevel) : 0;
        }

        if (saveData.Level >= maxLevel)
            saveData.EXP = 0;

        saveData.MaxHP = Mathf.Max(1, saveData.MaxHP + hpGain);
        saveData.MaxMP = Mathf.Max(0, saveData.MaxMP + mpGain);
        saveData.ATK = Mathf.Max(1, saveData.ATK + attackGain);
        saveData.DEF = Mathf.Max(0, saveData.DEF + defenseGain);
        saveData.SPD = Mathf.Max(1, saveData.SPD + speedGain);
        saveData.HP = Mathf.Clamp(saveData.HP + hpGain, 0, saveData.MaxHP);
        saveData.MP = Mathf.Clamp(saveData.MP + mpGain, 0, saveData.MaxMP);

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
}
