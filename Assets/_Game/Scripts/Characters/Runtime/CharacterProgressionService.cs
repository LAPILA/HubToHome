using System;
using UnityEngine;

[Serializable]
public sealed class CharacterLevelUpResult
{
    public string CharacterDataId;
    public int PreviousLevel;
    public int NewLevel;
    public int ExperienceGained;
    public int RemainingExperience;
    public int AttributePointsGained;
    public int SkillPointsGained;
    public int AvailableAttributePoints;
    public int AvailableSkillPoints;

    public bool DidLevelUp => NewLevel > PreviousLevel;
}

public static class CharacterProgressionService
{
    public const int DefaultMaxLevel = GrowthBalanceProfile.DefaultMaxLevel;

    public static int ExperienceRequiredForNextLevel(
        CharacterData data,
        int currentLevel)
    {
        return CalculateExperienceRequirement(
            CharacterGrowthService.ResolveBaseExperience(data),
            CharacterGrowthService.ResolveExperienceGrowth(data),
            currentLevel);
    }

    public static int ExperienceRequiredForNextLevel(
        GrowthBalanceProfile profile,
        int currentLevel)
    {
        int baseExperience = profile != null
            ? Mathf.Max(1, profile.BaseExperienceToLevel)
            : GrowthBalanceProfile.DefaultBaseExperienceToLevel;
        float growth = profile != null
            ? Mathf.Max(1f, profile.ExperienceGrowth)
            : GrowthBalanceProfile.DefaultExperienceGrowth;
        return CalculateExperienceRequirement(
            baseExperience,
            growth,
            currentLevel);
    }

    public static long CumulativeExperienceRequiredForLevel(
        CharacterData data,
        int targetLevel)
    {
        return CalculateCumulativeExperience(
            targetLevel,
            CharacterGrowthService.ResolveMaxLevel(data),
            level => ExperienceRequiredForNextLevel(data, level));
    }

    public static long CumulativeExperienceRequiredForLevel(
        GrowthBalanceProfile profile,
        int targetLevel)
    {
        int maximumLevel = profile != null
            ? profile.ResolveMaxLevel()
            : GrowthBalanceProfile.DefaultMaxLevel;
        return CalculateCumulativeExperience(
            targetLevel,
            maximumLevel,
            level => ExperienceRequiredForNextLevel(profile, level));
    }

    public static CharacterLevelUpResult GrantExperience(
        CharacterSaveData saveData,
        CharacterData data,
        int amount)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));

        CharacterGrowthService.EnsureInitialized(saveData, data);
        int gained = Mathf.Max(0, amount);
        int previousLevel = Mathf.Max(1, saveData.Level);
        int maximumLevel = CharacterGrowthService.ResolveMaxLevel(data);

        saveData.Level = previousLevel;
        saveData.EXP = SaturatingAdd(saveData.EXP, gained);

        while (saveData.Level < maximumLevel)
        {
            int required = ExperienceRequiredForNextLevel(data, saveData.Level);
            if (saveData.EXP < required)
                break;

            saveData.EXP -= required;
            saveData.Level++;
        }

        if (saveData.Level >= maximumLevel)
            saveData.EXP = 0;

        int levelsGained = saveData.Level - previousLevel;
        CharacterGrowthService.GrantLevelRewards(
            saveData,
            data,
            levelsGained,
            out int attributePointsGained,
            out int skillPointsGained);

        return new CharacterLevelUpResult
        {
            CharacterDataId = saveData.CharacterDataID,
            PreviousLevel = previousLevel,
            NewLevel = saveData.Level,
            ExperienceGained = gained,
            RemainingExperience = saveData.EXP,
            AttributePointsGained = attributePointsGained,
            SkillPointsGained = skillPointsGained,
            AvailableAttributePoints = saveData.Growth.AvailableAttributePoints,
            AvailableSkillPoints = saveData.Growth.AvailableSkillPoints
        };
    }

    private static int CalculateExperienceRequirement(
        int baseExperience,
        float growth,
        int currentLevel)
    {
        int level = Mathf.Max(1, currentLevel);
        double required = Mathf.Max(1, baseExperience)
            * Math.Pow(Mathf.Max(1f, growth), level - 1);
        if (double.IsNaN(required) || required <= 1d)
            return 1;
        if (double.IsInfinity(required) || required >= int.MaxValue)
            return int.MaxValue;
        return Math.Max(
            1,
            (int)Math.Round(required, MidpointRounding.ToEven));
    }

    private static long CalculateCumulativeExperience(
        int targetLevel,
        int maximumLevel,
        Func<int, int> requirementAtLevel)
    {
        int clampedTarget = Mathf.Clamp(targetLevel, 1, Mathf.Max(1, maximumLevel));
        long total = 0L;
        for (int level = 1; level < clampedTarget; level++)
        {
            int required = requirementAtLevel(level);
            if (total > long.MaxValue - required)
                return long.MaxValue;
            total += required;
        }

        return total;
    }

    private static int SaturatingAdd(int left, int right)
    {
        long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return (int)Math.Min(sum, int.MaxValue);
    }
}