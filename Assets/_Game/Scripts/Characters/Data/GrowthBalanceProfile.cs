using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GrowthBalanceProfile",
    menuName = "HubToHome/Growth/Balance Profile")]
public sealed class GrowthBalanceProfile : SerializedScriptableObject
{
    public const int DefaultMaxLevel = 99;
    public const int DefaultMaxInvestmentRank = 99;
    public const int DefaultAttributePointsPerLevel = 3;
    public const int DefaultSkillPointsPerLevel = 1;
    public const int DefaultHealthPerVitalityRank = 10;
    public const int DefaultStatValuePerRank = 1;
    public const int DefaultBaseExperienceToLevel = 100;
    public const float DefaultExperienceGrowth = 1.18f;

    [BoxGroup("Limits"), MinValue(1), MaxValue(DefaultMaxLevel)]
    public int MaxLevel = DefaultMaxLevel;

    [BoxGroup("Limits"), MinValue(1), MaxValue(DefaultMaxInvestmentRank)]
    public int MaxInvestmentRank = DefaultMaxInvestmentRank;

    [BoxGroup("Level Rewards"), MinValue(0)]
    public int AttributePointsPerLevel = DefaultAttributePointsPerLevel;

    [BoxGroup("Level Rewards"), MinValue(0)]
    public int SkillPointsPerLevel = DefaultSkillPointsPerLevel;

    [BoxGroup("Experience"), MinValue(1)]
    public int BaseExperienceToLevel = DefaultBaseExperienceToLevel;

    [BoxGroup("Experience"), MinValue(1f)]
    public float ExperienceGrowth = DefaultExperienceGrowth;

    [BoxGroup("Investment Conversion"), MinValue(1)]
    [LabelText("생명력 1단계당 최대 HP")]
    public int HealthPerVitalityRank = DefaultHealthPerVitalityRank;

    [BoxGroup("Investment Conversion"), MinValue(1)]
    [LabelText("공격력 1단계당 ATK")]
    public int AttackPerRank = DefaultStatValuePerRank;

    [BoxGroup("Investment Conversion"), MinValue(1)]
    [LabelText("방어력 1단계당 DEF")]
    public int DefensePerRank = DefaultStatValuePerRank;

    [BoxGroup("Investment Conversion"), MinValue(1)]
    [LabelText("속도 1단계당 SPD")]
    public int SpeedPerRank = DefaultStatValuePerRank;

    [BoxGroup("Investment Conversion"), MinValue(1)]
    [LabelText("행동력 1단계당 최대 AP")]
    public int ActionPointsPerRank = DefaultStatValuePerRank;

    public int ResolveMaxLevel()
    {
        return Mathf.Clamp(MaxLevel, 1, DefaultMaxLevel);
    }

    public int ResolveMaxInvestmentRank()
    {
        return Mathf.Clamp(MaxInvestmentRank, 1, DefaultMaxInvestmentRank);
    }
}
