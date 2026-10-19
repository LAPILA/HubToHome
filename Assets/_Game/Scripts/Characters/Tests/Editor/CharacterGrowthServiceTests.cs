using System;
using System.Collections.Generic;
using System.Reflection;
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
        UnityEngine.Object.DestroyImmediate(_data);
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

    [Test]
    public void BatchInvestmentAppliesAllRequestedStatsAndPreservesResourceDeficits()
    {
        CharacterSaveData character = CreateCharacter(level: 5);
        character.HP = 80;
        character.AP = 12;
        var additions = new CharacterStatInvestments
        {
            Vitality = 2, Attack = 3, Defense = 1, Speed = 4, ActionPoints = 2
        };

        GrowthInvestmentResult result = CharacterGrowthService.TryInvestBatch(character, _data, additions);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Stat, Is.EqualTo(GrowthStat.Vitality));
        Assert.That(character.MaxHP, Is.EqualTo(120));
        Assert.That(character.HP, Is.EqualTo(100));
        Assert.That(character.MaxAP, Is.EqualTo(22));
        Assert.That(character.AP, Is.EqualTo(14));
        Assert.That(character.ATK, Is.EqualTo(13));
        Assert.That(character.DEF, Is.EqualTo(6));
        Assert.That(character.SPD, Is.EqualTo(11));
        Assert.That(character.Growth.AvailableAttributePoints, Is.Zero);
        Assert.That(character.Growth.Investments, Is.Not.SameAs(additions));
        additions.Attack = 99;
        Assert.That(character.Growth.Investments.Attack, Is.EqualTo(3));
    }

    [Test]
    public void BatchInvestmentFailureDoesNotInitializeOrPartiallyChangeCharacter()
    {
        CharacterSaveData character = CreateCharacter(level: 2);
        CharacterGrowthSaveData originalGrowth = character.Growth;

        GrowthInvestmentResult result = CharacterGrowthService.TryInvestBatch(character, _data,
            new CharacterStatInvestments { Attack = 2, Speed = 2 });

        Assert.That(result.Status, Is.EqualTo(GrowthInvestmentStatus.InsufficientPoints));
        Assert.That(character.Growth, Is.SameAs(originalGrowth));
        Assert.That(character.Growth.IsInitialized, Is.False);
        Assert.That(character.Growth.AttributePointsEarned, Is.Zero);
        Assert.That(character.Growth.Investments.Total, Is.Zero);
        Assert.That(character.ATK, Is.EqualTo(10));
        Assert.That(character.SPD, Is.EqualTo(7));
    }

    [Test]
    public void BatchInvestmentCapFailureRejectsOtherValidStats()
    {
        CharacterSaveData character = CreateCharacter(level: 99);
        CharacterGrowthService.EnsureInitialized(character, _data);
        character.Growth.Investments.Attack = 99;
        CharacterGrowthService.RecalculateBaseStats(character, _data, false);

        GrowthInvestmentResult result = CharacterGrowthService.TryInvestBatch(character, _data,
            new CharacterStatInvestments { Vitality = 1, Attack = 1 });

        Assert.That(result.Status, Is.EqualTo(GrowthInvestmentStatus.RankCapReached));
        Assert.That(character.Growth.Investments.Vitality, Is.Zero);
        Assert.That(character.Growth.Investments.Attack, Is.EqualTo(99));
        Assert.That(character.MaxHP, Is.EqualTo(100));
    }

    [Test]
    public void BatchInvestmentRejectsEmptyNullAndNegativeRequestsWithoutChanges()
    {
        CharacterSaveData character = CreateCharacter(level: 5);
        Assert.That(CharacterGrowthService.TryInvestBatch(character, _data, null).Status,
            Is.EqualTo(GrowthInvestmentStatus.InvalidAmount));
        Assert.That(CharacterGrowthService.TryInvestBatch(character, _data, new CharacterStatInvestments()).Status,
            Is.EqualTo(GrowthInvestmentStatus.InvalidAmount));
        Assert.That(CharacterGrowthService.TryInvestBatch(character, _data,
            new CharacterStatInvestments { Attack = 2, Speed = -1 }).Status,
            Is.EqualTo(GrowthInvestmentStatus.InvalidAmount));
        Assert.That(character.Growth.IsInitialized, Is.False);
        Assert.That(character.Growth.Investments.Total, Is.Zero);
    }

    [Test]
    public void BatchInvestmentRejectsOverflowSizedTotalWithoutChanges()
    {
        CharacterSaveData character = CreateCharacter(level: 99);
        GrowthInvestmentResult result = CharacterGrowthService.TryInvestBatch(character, _data,
            new CharacterStatInvestments { Attack = int.MaxValue, Speed = int.MaxValue });
        Assert.That(result.Status, Is.EqualTo(GrowthInvestmentStatus.InsufficientPoints));
        Assert.That(character.Growth.IsInitialized, Is.False);
        Assert.That(character.Growth.Investments.Total, Is.Zero);
    }

    [Test]
    public void BatchInvestmentDoesNotReviveDefeatedCharacterAndReportsFirstRequestedStat()
    {
        CharacterSaveData character = CreateCharacter(level: 2);
        character.HP = 0;
        GrowthInvestmentResult result = CharacterGrowthService.TryInvestBatch(character, _data,
            new CharacterStatInvestments { Attack = 1, ActionPoints = 1 });
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Stat, Is.EqualTo(GrowthStat.Attack));
        Assert.That(result.PreviousRank, Is.Zero);
        Assert.That(result.CurrentRank, Is.EqualTo(1));
        Assert.That(character.HP, Is.Zero);
    }

    [Test]
    public void BatchInvestmentRejectsMissingCharacterDataWithoutInitializingGrowth()
    {
        CharacterSaveData character = CreateCharacter(level: 5);
        var additions = new CharacterStatInvestments { Attack = 1 };
        Assert.That(CharacterGrowthService.TryInvestBatch(null, _data, additions).Status,
            Is.EqualTo(GrowthInvestmentStatus.InvalidCharacter));
        Assert.That(CharacterGrowthService.TryInvestBatch(character, null, additions).Status,
            Is.EqualTo(GrowthInvestmentStatus.InvalidCharacter));
        Assert.That(character.Growth.IsInitialized, Is.False);
    }

    [Test]
    public void GlobalBatchInvestmentPublishesOneCompleteChangeAndNoFailureEvent()
    {
        WithGlobal((global, state) =>
        {
            CharacterSaveData character = CreateCharacter(level: 5);
            global.Party.Add(character);
            int events = 0;
            global.CharacterGrowthChanged += changed =>
            {
                events++;
                Assert.That(changed, Is.SameAs(character));
                Assert.That(changed.Growth.Investments.Attack, Is.EqualTo(2));
                Assert.That(changed.Growth.Investments.Speed, Is.EqualTo(3));
            };

            Assert.That(global.TryInvestGrowthStats(character,
                new CharacterStatInvestments { Attack = 2, Speed = 3 }).Succeeded, Is.True);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(global.TryInvestGrowthStats(character,
                new CharacterStatInvestments { Vitality = 99 }).Succeeded, Is.False);
            Assert.That(events, Is.EqualTo(1));
        });
    }

    [Test]
    public void GlobalBatchInvestmentRejectsUnownedAndBattleLockedWithoutInitialization()
    {
        WithGlobal((global, state) =>
        {
            CharacterSaveData character = CreateCharacter(level: 5);
            var additions = new CharacterStatInvestments { Attack = 1 };
            int events = 0;
            global.CharacterGrowthChanged += changed => events++;
            Assert.That(global.TryInvestGrowthStats(character, additions).Status,
                Is.EqualTo(GrowthInvestmentStatus.InvalidCharacter));
            global.Party.Add(character);
            state.ChangeState(GameState.Battle);
            Assert.That(global.TryInvestGrowthStats(character, additions).Status,
                Is.EqualTo(GrowthInvestmentStatus.MutationLocked));
            Assert.That(character.Growth.IsInitialized, Is.False);
            Assert.That(character.Growth.Investments.Total, Is.Zero);
            Assert.That(events, Is.Zero);
        });
    }

    private void WithGlobal(Action<GlobalDataManager, GameStateManager> check)
    {
        GlobalDataManager previousGlobal = GlobalDataManager.Instance;
        GameStateManager previousState = GameStateManager.Instance;
        FieldInfo cache = typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        object previousCache = cache.GetValue(null);
        GameObject globalObject = null;
        GameObject stateObject = null;
        try
        {
            SetSingleton<GlobalDataManager>(null);
            SetSingleton<GameStateManager>(null);
            cache.SetValue(null, new Dictionary<string, CharacterData>(StringComparer.Ordinal) { { "hero", _data } });
            globalObject = new GameObject("BatchGrowthTests_Global");
            GlobalDataManager global = globalObject.AddComponent<GlobalDataManager>();
            stateObject = new GameObject("BatchGrowthTests_State");
            GameStateManager state = stateObject.AddComponent<GameStateManager>();
            global.Party.Clear();
            check(global, state);
        }
        finally
        {
            if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
            if (globalObject != null) UnityEngine.Object.DestroyImmediate(globalObject);
            SetSingleton(previousGlobal);
            SetSingleton(previousState);
            cache.SetValue(null, previousCache);
        }
    }

    private static void SetSingleton<T>(T value)
    {
        typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetSetMethod(true).Invoke(null, new object[] { value });
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
