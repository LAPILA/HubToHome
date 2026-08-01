using System;
using System.Collections.Generic;
using UnityEngine;

public enum GrowthStat
{
    Vitality = 0,
    Attack = 1,
    Defense = 2,
    Speed = 3,
    ActionPoints = 4
}

[Serializable]
public sealed class CharacterStatInvestments
{
    public int Vitality;
    public int Attack;
    public int Defense;
    public int Speed;
    public int ActionPoints;

    public int Total =>
        SaturatingSum(Vitality, Attack, Defense, Speed, ActionPoints);

    public int Get(GrowthStat stat)
    {
        return stat switch
        {
            GrowthStat.Vitality => Vitality,
            GrowthStat.Attack => Attack,
            GrowthStat.Defense => Defense,
            GrowthStat.Speed => Speed,
            GrowthStat.ActionPoints => ActionPoints,
            _ => 0
        };
    }

    public void Set(GrowthStat stat, int value)
    {
        int safeValue = Mathf.Max(0, value);
        switch (stat)
        {
            case GrowthStat.Vitality:
                Vitality = safeValue;
                break;
            case GrowthStat.Attack:
                Attack = safeValue;
                break;
            case GrowthStat.Defense:
                Defense = safeValue;
                break;
            case GrowthStat.Speed:
                Speed = safeValue;
                break;
            case GrowthStat.ActionPoints:
                ActionPoints = safeValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }

    public void Clamp(int maximumRank)
    {
        int maximum = Mathf.Max(0, maximumRank);
        Vitality = Mathf.Clamp(Vitality, 0, maximum);
        Attack = Mathf.Clamp(Attack, 0, maximum);
        Defense = Mathf.Clamp(Defense, 0, maximum);
        Speed = Mathf.Clamp(Speed, 0, maximum);
        ActionPoints = Mathf.Clamp(ActionPoints, 0, maximum);
    }

    public void Reset()
    {
        Vitality = 0;
        Attack = 0;
        Defense = 0;
        Speed = 0;
        ActionPoints = 0;
    }

    public CharacterStatInvestments Clone()
    {
        return new CharacterStatInvestments
        {
            Vitality = Vitality,
            Attack = Attack,
            Defense = Defense,
            Speed = Speed,
            ActionPoints = ActionPoints
        };
    }

    private static int SaturatingSum(params int[] values)
    {
        long total = 0;
        for (int i = 0; i < values.Length; i++)
            total += Mathf.Max(0, values[i]);
        return (int)Math.Min(total, int.MaxValue);
    }
}

[Serializable]
public sealed class CharacterGrowthSaveData
{
    public bool IsInitialized;
    public int AttributePointsEarned;
    public int SkillPointsEarned;
    public int SkillPointsSpent;
    public CharacterStatInvestments Investments = new CharacterStatInvestments();
    public List<SkillTreeUnlockSaveData> SkillTreeUnlocks =
        new List<SkillTreeUnlockSaveData>();

    public int AvailableAttributePoints =>
        Mathf.Max(0, AttributePointsEarned - (Investments?.Total ?? 0));

    public int AvailableSkillPoints =>
        Mathf.Max(0, SkillPointsEarned - Mathf.Max(0, SkillPointsSpent));

    public CharacterGrowthSaveData Clone()
    {
        return new CharacterGrowthSaveData
        {
            IsInitialized = IsInitialized,
            AttributePointsEarned = Mathf.Max(0, AttributePointsEarned),
            SkillPointsEarned = Mathf.Max(0, SkillPointsEarned),
            SkillPointsSpent = Mathf.Max(0, SkillPointsSpent),
            Investments = Investments?.Clone() ?? new CharacterStatInvestments(),
            SkillTreeUnlocks = CloneSkillTreeUnlocks(SkillTreeUnlocks)
        };
    }

    private static List<SkillTreeUnlockSaveData> CloneSkillTreeUnlocks(
        IReadOnlyList<SkillTreeUnlockSaveData> source)
    {
        var result = new List<SkillTreeUnlockSaveData>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i].Clone());
        }

        return result;
    }
}

public readonly struct CharacterBaseStatSnapshot
{
    public CharacterBaseStatSnapshot(
        int maxHp,
        int maxAp,
        int attack,
        int defense,
        int speed)
    {
        MaxHP = Mathf.Max(1, maxHp);
        MaxAP = Mathf.Max(0, maxAp);
        Attack = Mathf.Max(1, attack);
        Defense = Mathf.Max(0, defense);
        Speed = Mathf.Max(1, speed);
    }

    public int MaxHP { get; }
    public int MaxAP { get; }
    public int Attack { get; }
    public int Defense { get; }
    public int Speed { get; }
}

public enum GrowthInvestmentStatus
{
    Success,
    InvalidCharacter,
    InvalidAmount,
    InsufficientPoints,
    RankCapReached,
    MutationLocked
}

public readonly struct GrowthInvestmentResult
{
    public GrowthInvestmentResult(
        GrowthInvestmentStatus status,
        GrowthStat stat,
        int previousRank,
        int currentRank,
        int availablePoints)
    {
        Status = status;
        Stat = stat;
        PreviousRank = previousRank;
        CurrentRank = currentRank;
        AvailablePoints = Mathf.Max(0, availablePoints);
    }

    public GrowthInvestmentStatus Status { get; }
    public GrowthStat Stat { get; }
    public int PreviousRank { get; }
    public int CurrentRank { get; }
    public int AvailablePoints { get; }
    public bool Succeeded => Status == GrowthInvestmentStatus.Success;
}

public static class CharacterGrowthService
{
    public static void EnsureInitialized(
        CharacterSaveData character,
        CharacterData data)
    {
        if (character == null)
            return;

        character.Growth ??= new CharacterGrowthSaveData();
        character.Growth.Investments ??= new CharacterStatInvestments();
        character.Growth.SkillTreeUnlocks ??=
            new List<SkillTreeUnlockSaveData>();

        GrowthBalanceProfile profile = data != null ? data.GrowthProfile : null;
        int maximumRank = ResolveMaxInvestmentRank(profile);
        character.Growth.Investments.Clamp(maximumRank);
        character.Growth.AttributePointsEarned =
            Mathf.Max(0, character.Growth.AttributePointsEarned);
        character.Growth.SkillPointsEarned =
            Mathf.Max(0, character.Growth.SkillPointsEarned);
        character.Growth.SkillPointsSpent =
            Mathf.Clamp(
                character.Growth.SkillPointsSpent,
                0,
                character.Growth.SkillPointsEarned);

        if (!character.Growth.IsInitialized)
        {
            int completedLevels = Mathf.Max(0, character.Level - 1);
            character.Growth.AttributePointsEarned = SaturatingMultiply(
                completedLevels,
                ResolveAttributePointsPerLevel(profile));
            character.Growth.SkillPointsEarned = SaturatingMultiply(
                completedLevels,
                ResolveSkillPointsPerLevel(profile));
            character.Growth.IsInitialized = true;
        }

        character.Growth.AttributePointsEarned = Mathf.Max(
            character.Growth.AttributePointsEarned,
            character.Growth.Investments.Total);

        RecalculateBaseStats(character, data, false);
    }

    public static CharacterBaseStatSnapshot CalculateBaseStats(
        CharacterSaveData character,
        CharacterData data)
    {
        CharacterStatInvestments investments =
            character?.Growth?.Investments ?? new CharacterStatInvestments();
        GrowthBalanceProfile profile = data != null ? data.GrowthProfile : null;
        int maximumRank = ResolveMaxInvestmentRank(profile);

        int vitality = Mathf.Clamp(investments.Vitality, 0, maximumRank);
        int attack = Mathf.Clamp(investments.Attack, 0, maximumRank);
        int defense = Mathf.Clamp(investments.Defense, 0, maximumRank);
        int speed = Mathf.Clamp(investments.Speed, 0, maximumRank);
        int actionPoints = Mathf.Clamp(
            investments.ActionPoints,
            0,
            maximumRank);

        int baseMaxHp = data != null
            ? data.BaseMaxHP
            : Mathf.Max(1, character?.MaxHP ?? 100);
        int baseMaxAp = data != null
            ? data.BaseMaxAP
            : Mathf.Max(0, character?.MaxAP ?? 50);
        int baseAttack = data != null
            ? data.BaseATK
            : Mathf.Max(1, character?.ATK ?? 10);
        int baseDefense = data != null
            ? data.BaseDEF
            : Mathf.Max(0, character?.DEF ?? 5);
        int baseSpeed = data != null
            ? data.BaseSPD
            : Mathf.Max(1, character?.SPD ?? 10);

        return new CharacterBaseStatSnapshot(
            SaturatingAdd(
                baseMaxHp,
                SaturatingMultiply(
                    vitality,
                    ResolveHealthPerVitalityRank(profile))),
            SaturatingAdd(
                baseMaxAp,
                SaturatingMultiply(
                    actionPoints,
                    ResolveActionPointsPerRank(profile))),
            SaturatingAdd(
                baseAttack,
                SaturatingMultiply(attack, ResolveAttackPerRank(profile))),
            SaturatingAdd(
                baseDefense,
                SaturatingMultiply(
                    defense,
                    ResolveDefensePerRank(profile))),
            SaturatingAdd(
                baseSpeed,
                SaturatingMultiply(speed, ResolveSpeedPerRank(profile))));
    }

    public static void RecalculateBaseStats(
        CharacterSaveData character,
        CharacterData data,
        bool preserveResourceDeficit)
    {
        if (character == null)
            return;

        int previousMaxHp = Mathf.Max(1, character.MaxHP);
        int previousMaxAp = Mathf.Max(0, character.MaxAP);
        int equipmentMaxHp = EquipmentLoadoutService.GetFlatBonus(
            character,
            equipment => equipment.BonusMaxHP);
        int equipmentMaxAp = EquipmentLoadoutService.GetFlatBonus(
            character,
            equipment => equipment.BonusMaxAP);
        int previousFinalMaxHp = AddSignedAndClamp(previousMaxHp, equipmentMaxHp, 1);
        int previousFinalMaxAp = AddSignedAndClamp(previousMaxAp, equipmentMaxAp, 0);
        int missingHp = Mathf.Max(0, previousFinalMaxHp - Mathf.Max(0, character.HP));
        int missingAp = Mathf.Max(0, previousFinalMaxAp - Mathf.Max(0, character.AP));

        CharacterBaseStatSnapshot calculated = CalculateBaseStats(character, data);
        character.MaxHP = calculated.MaxHP;
        character.MaxAP = calculated.MaxAP;
        character.ATK = calculated.Attack;
        character.DEF = calculated.Defense;
        character.SPD = calculated.Speed;

        int finalMaxHp = AddSignedAndClamp(calculated.MaxHP, equipmentMaxHp, 1);
        int finalMaxAp = AddSignedAndClamp(calculated.MaxAP, equipmentMaxAp, 0);
        if (preserveResourceDeficit)
        {
            character.HP = Mathf.Clamp(
                finalMaxHp - missingHp,
                0,
                finalMaxHp);
            character.AP = Mathf.Clamp(
                finalMaxAp - missingAp,
                0,
                finalMaxAp);
        }
        else
        {
            character.HP = Mathf.Clamp(character.HP, 0, finalMaxHp);
            character.AP = Mathf.Clamp(character.AP, 0, finalMaxAp);
        }
    }

    public static GrowthInvestmentResult TryInvest(
        CharacterSaveData character,
        CharacterData data,
        GrowthStat stat,
        int amount = 1)
    {
        if (character == null)
        {
            return new GrowthInvestmentResult(
                GrowthInvestmentStatus.InvalidCharacter,
                stat,
                0,
                0,
                0);
        }

        if (!Enum.IsDefined(typeof(GrowthStat), stat))
        {
            return new GrowthInvestmentResult(
                GrowthInvestmentStatus.InvalidAmount,
                stat,
                0,
                0,
                0);
        }

        EnsureInitialized(character, data);
        CharacterGrowthSaveData growth = character.Growth;
        int previousRank = growth.Investments.Get(stat);
        if (amount <= 0)
        {
            return Result(
                GrowthInvestmentStatus.InvalidAmount,
                stat,
                previousRank,
                growth);
        }

        if (growth.AvailableAttributePoints < amount)
        {
            return Result(
                GrowthInvestmentStatus.InsufficientPoints,
                stat,
                previousRank,
                growth);
        }

        int maximumRank = ResolveMaxInvestmentRank(data?.GrowthProfile);
        if (previousRank > maximumRank - amount)
        {
            return Result(
                GrowthInvestmentStatus.RankCapReached,
                stat,
                previousRank,
                growth);
        }

        growth.Investments.Set(stat, previousRank + amount);
        RecalculateBaseStats(character, data, true);
        return Result(
            GrowthInvestmentStatus.Success,
            stat,
            previousRank,
            growth);
    }

    public static bool TryRefund(
        CharacterSaveData character,
        CharacterData data,
        GrowthStat stat,
        int amount = 1)
    {
        if (character == null
            || amount <= 0
            || !Enum.IsDefined(typeof(GrowthStat), stat))
            return false;

        EnsureInitialized(character, data);
        int currentRank = character.Growth.Investments.Get(stat);
        if (currentRank < amount)
            return false;

        character.Growth.Investments.Set(stat, currentRank - amount);
        RecalculateBaseStats(character, data, true);
        return true;
    }

    public static int ResetInvestments(
        CharacterSaveData character,
        CharacterData data)
    {
        if (character == null)
            return 0;

        EnsureInitialized(character, data);
        int refunded = character.Growth.Investments.Total;
        if (refunded <= 0)
            return 0;

        character.Growth.Investments.Reset();
        RecalculateBaseStats(character, data, true);
        return refunded;
    }

    public static bool TrySpendSkillPoints(
        CharacterSaveData character,
        CharacterData data,
        int amount)
    {
        if (character == null || amount <= 0)
            return false;

        EnsureInitialized(character, data);
        if (character.Growth.AvailableSkillPoints < amount)
            return false;

        character.Growth.SkillPointsSpent = SaturatingAdd(
            character.Growth.SkillPointsSpent,
            amount);
        return true;
    }

    public static bool TryRefundSkillPoints(
        CharacterSaveData character,
        CharacterData data,
        int amount)
    {
        if (character == null || amount <= 0)
            return false;

        EnsureInitialized(character, data);
        if (character.Growth.SkillPointsSpent < amount)
            return false;

        character.Growth.SkillPointsSpent -= amount;
        return true;
    }

    public static int ResetSkillPointSpending(
        CharacterSaveData character,
        CharacterData data)
    {
        if (character == null)
            return 0;

        EnsureInitialized(character, data);
        int refunded = Mathf.Max(0, character.Growth.SkillPointsSpent);
        character.Growth.SkillPointsSpent = 0;
        return refunded;
    }

    public static void GrantLevelRewards(
        CharacterSaveData character,
        CharacterData data,
        int levelsGained,
        out int attributePointsGained,
        out int skillPointsGained)
    {
        attributePointsGained = 0;
        skillPointsGained = 0;
        if (character == null || levelsGained <= 0)
            return;

        EnsureInitialized(character, data);
        GrowthBalanceProfile profile = data != null ? data.GrowthProfile : null;
        attributePointsGained = SaturatingMultiply(
            levelsGained,
            ResolveAttributePointsPerLevel(profile));
        skillPointsGained = SaturatingMultiply(
            levelsGained,
            ResolveSkillPointsPerLevel(profile));
        character.Growth.AttributePointsEarned = SaturatingAdd(
            character.Growth.AttributePointsEarned,
            attributePointsGained);
        character.Growth.SkillPointsEarned = SaturatingAdd(
            character.Growth.SkillPointsEarned,
            skillPointsGained);
    }

    public static int ResolveMaxLevel(CharacterData data)
    {
        if (data?.GrowthProfile != null)
            return data.GrowthProfile.ResolveMaxLevel();
        return Mathf.Clamp(
            data != null ? data.MaxLevel : GrowthBalanceProfile.DefaultMaxLevel,
            1,
            GrowthBalanceProfile.DefaultMaxLevel);
    }

    public static int ResolveMaxInvestmentRank(GrowthBalanceProfile profile)
    {
        return profile != null
            ? profile.ResolveMaxInvestmentRank()
            : GrowthBalanceProfile.DefaultMaxInvestmentRank;
    }

    public static int ResolveAttributePointsPerLevel(
        GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(0, profile.AttributePointsPerLevel)
            : GrowthBalanceProfile.DefaultAttributePointsPerLevel;
    }

    public static int ResolveSkillPointsPerLevel(GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(0, profile.SkillPointsPerLevel)
            : GrowthBalanceProfile.DefaultSkillPointsPerLevel;
    }

    public static int ResolveBaseExperience(CharacterData data)
    {
        if (data?.GrowthProfile != null)
            return Mathf.Max(1, data.GrowthProfile.BaseExperienceToLevel);
        return data != null
            ? Mathf.Max(1, data.BaseExperienceToLevel)
            : GrowthBalanceProfile.DefaultBaseExperienceToLevel;
    }

    public static float ResolveExperienceGrowth(CharacterData data)
    {
        if (data?.GrowthProfile != null)
            return Mathf.Max(1f, data.GrowthProfile.ExperienceGrowth);
        return data != null
            ? Mathf.Max(1f, data.ExperienceGrowth)
            : GrowthBalanceProfile.DefaultExperienceGrowth;
    }

    private static GrowthInvestmentResult Result(
        GrowthInvestmentStatus status,
        GrowthStat stat,
        int previousRank,
        CharacterGrowthSaveData growth)
    {
        return new GrowthInvestmentResult(
            status,
            stat,
            previousRank,
            growth.Investments.Get(stat),
            growth.AvailableAttributePoints);
    }

    private static int ResolveHealthPerVitalityRank(
        GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(1, profile.HealthPerVitalityRank)
            : GrowthBalanceProfile.DefaultHealthPerVitalityRank;
    }

    private static int ResolveAttackPerRank(GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(1, profile.AttackPerRank)
            : GrowthBalanceProfile.DefaultStatValuePerRank;
    }

    private static int ResolveDefensePerRank(GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(1, profile.DefensePerRank)
            : GrowthBalanceProfile.DefaultStatValuePerRank;
    }

    private static int ResolveSpeedPerRank(GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(1, profile.SpeedPerRank)
            : GrowthBalanceProfile.DefaultStatValuePerRank;
    }

    private static int ResolveActionPointsPerRank(
        GrowthBalanceProfile profile)
    {
        return profile != null
            ? Mathf.Max(1, profile.ActionPointsPerRank)
            : GrowthBalanceProfile.DefaultStatValuePerRank;
    }

    private static int AddSignedAndClamp(int value, int delta, int minimum)
    {
        long sum = (long)value + delta;
        return (int)Math.Max(minimum, Math.Min(sum, int.MaxValue));
    }

    private static int SaturatingMultiply(int left, int right)
    {
        long product = (long)Mathf.Max(0, left) * Mathf.Max(0, right);
        return (int)Math.Min(product, int.MaxValue);
    }

    private static int SaturatingAdd(int left, int right)
    {
        long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return (int)Math.Min(sum, int.MaxValue);
    }
}
