using NUnit.Framework;
using UnityEngine;

public sealed class CharacterGrowthServiceTests
{
    private CharacterData _data;

    [SetUp]
    public void SetUp()
    {
        _data = ScriptableObject.CreateInstance<CharacterData>();
        _data.BaseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 20,
            ATK = 10,
            DEF = 5,
            SPD = 7,
        };
        _data.BaseExperienceToLevel = 10;
        _data.ExperienceGrowth = 1f;
        _data.MaxLevel = 99;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_data);
    }

    [Test]
    public void LevelUpGrantsPointsWithoutChangingBaseStats()
    {
        CharacterSaveData character = CreateCharacter();

        CharacterLevelUpResult result =
            CharacterProgressionService.GrantExperience(character, _data, 20);

        Assert.That(character.Level, Is.EqualTo(3));
        Assert.That(character.MaxHP, Is.EqualTo(100));
        Assert.That(character.MaxAP, Is.EqualTo(20));
        Assert.That(character.ATK, Is.EqualTo(10));
        Assert.That(character.DEF, Is.EqualTo(5));
        Assert.That(character.SPD, Is.EqualTo(7));
        Assert.That(result.AttributePointsGained, Is.EqualTo(6));
        Assert.That(result.SkillPointsGained, Is.EqualTo(2));
    }

    [Test]
    public void InvestmentsUseApprovedConversionsAndExactSpeedValue()
    {
        CharacterSaveData character = CreateCharacter(level: 5);
        CharacterGrowthService.EnsureInitialized(character, _data);

        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Vitality, 2).Succeeded,
            Is.True);
        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Attack, 3).Succeeded,
            Is.True);
        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Defense, 1).Succeeded,
            Is.True);
        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Speed, 4).Succeeded,
            Is.True);
        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.ActionPoints, 2).Succeeded,
            Is.True);

        Assert.That(character.MaxHP, Is.EqualTo(120));
        Assert.That(character.MaxAP, Is.EqualTo(22));
        Assert.That(character.ATK, Is.EqualTo(13));
        Assert.That(character.DEF, Is.EqualTo(6));
        Assert.That(character.SPD, Is.EqualTo(11));
        Assert.That(character.Growth.AvailableAttributePoints, Is.Zero);
    }

    [Test]
    public void InvestmentAndRefundPreserveMissingHpAndAp()
    {
        CharacterSaveData character = CreateCharacter(level: 2);
        character.HP = 80;
        character.AP = 12;

        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Vitality).Succeeded,
            Is.True);
        Assert.That(
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.ActionPoints).Succeeded,
            Is.True);
        Assert.That(character.MaxHP, Is.EqualTo(110));
        Assert.That(character.HP, Is.EqualTo(90));
        Assert.That(character.MaxAP, Is.EqualTo(21));
        Assert.That(character.AP, Is.EqualTo(13));

        Assert.That(
            CharacterGrowthService.TryRefund(character, _data, GrowthStat.Vitality),
            Is.True);
        Assert.That(character.MaxHP, Is.EqualTo(100));
        Assert.That(character.HP, Is.EqualTo(80));
    }

    [Test]
    public void SkillPointWalletSupportsFreeRefundAndReset()
    {
        CharacterSaveData character = CreateCharacter(level: 5);
        CharacterGrowthService.EnsureInitialized(character, _data);

        Assert.That(
            CharacterGrowthService.TrySpendSkillPoints(character, _data, 3),
            Is.True);
        Assert.That(character.Growth.AvailableSkillPoints, Is.EqualTo(1));
        Assert.That(
            CharacterGrowthService.TryRefundSkillPoints(character, _data, 1),
            Is.True);
        Assert.That(character.Growth.AvailableSkillPoints, Is.EqualTo(2));
        Assert.That(
            CharacterGrowthService.ResetSkillPointSpending(character, _data),
            Is.EqualTo(2));
        Assert.That(character.Growth.AvailableSkillPoints, Is.EqualTo(4));
    }

    [Test]
    public void InvestmentRankCannotExceedNinetyNine()
    {
        CharacterSaveData character = CreateCharacter(level: 99);
        CharacterGrowthService.EnsureInitialized(character, _data);
        character.Growth.Investments.Attack = 99;
        CharacterGrowthService.RecalculateBaseStats(character, _data, false);

        GrowthInvestmentResult result =
            CharacterGrowthService.TryInvest(character, _data, GrowthStat.Attack);

        Assert.That(result.Status, Is.EqualTo(GrowthInvestmentStatus.RankCapReached));
        Assert.That(character.Growth.Investments.Attack, Is.EqualTo(99));
    }

    private CharacterSaveData CreateCharacter(int level = 1)
    {
        return new CharacterSaveData
        {
            CharacterDataID = "hero",
            CharacterID = "hero",
            Level = level,
            HP = _data.BaseStats.MaxHP,
            MaxHP = _data.BaseStats.MaxHP,
            AP = _data.BaseStats.MaxAP,
            MaxAP = _data.BaseStats.MaxAP,
            ATK = _data.BaseStats.ATK,
            DEF = _data.BaseStats.DEF,
            SPD = _data.BaseStats.SPD
        };
    }
}
