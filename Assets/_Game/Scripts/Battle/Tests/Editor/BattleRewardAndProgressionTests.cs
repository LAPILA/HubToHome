using NUnit.Framework;
using UnityEngine;

public class BattleRewardAndProgressionTests
{
    [Test]
    public void RewardCalculationAggregatesEnemiesAndDeterministicDrops()
    {
        EnemyData first = ScriptableObject.CreateInstance<EnemyData>();
        EnemyData second = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            first.EXPReward = 10;
            first.GoldReward = 3;
            first.Drops.Add(new EnemyDropEntry
            {
                ItemId = "potion",
                MinAmount = 2,
                MaxAmount = 2,
                DropChance = 1f
            });
            first.Drops.Add(new EnemyDropEntry
            {
                ItemId = "never",
                MinAmount = 1,
                MaxAmount = 1,
                DropChance = 0f
            });
            second.EXPReward = 20;
            second.GoldReward = 7;

            BattleRewardResult result = BattleRewardService.Calculate(
                new[] { first, second },
                () => 0f);

            Assert.That(result.Experience, Is.EqualTo(30));
            Assert.That(result.Gold, Is.EqualTo(10));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].ItemId, Is.EqualTo("potion"));
            Assert.That(result.Items[0].Amount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void RewardCalculationUsesInjectedRandomForDropAmount()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            enemy.Drops.Add(new EnemyDropEntry
            {
                ItemId = "potion",
                MinAmount = 1,
                MaxAmount = 3,
                DropChance = 1f
            });
            int rollCount = 0;

            BattleRewardResult result = BattleRewardService.Calculate(
                new[] { enemy },
                () =>
                {
                    rollCount++;
                    return 0.999f;
                });

            Assert.That(rollCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Amount, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }

    [Test]
    public void RewardCalculationSaturatesLargeTotals()
    {
        EnemyData first = ScriptableObject.CreateInstance<EnemyData>();
        EnemyData second = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            first.EXPReward = int.MaxValue;
            first.GoldReward = int.MaxValue;
            second.EXPReward = int.MaxValue;
            second.GoldReward = int.MaxValue;

            BattleRewardResult result = BattleRewardService.Calculate(new[] { first, second });

            Assert.That(result.Experience, Is.EqualTo(int.MaxValue));
            Assert.That(result.Gold, Is.EqualTo(int.MaxValue));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void ExperienceCanLevelMultipleTimesAndUpdatesPersistentStats()
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            data.BaseExperienceToLevel = 10;
            data.ExperienceGrowth = 1f;
            data.MaxLevel = 10;
            data.MaxHpPerLevel = 5;
            data.AttackPerLevel = 2;

            var save = new CharacterSaveData
            {
                Level = 1,
                EXP = 0,
                HP = 50,
                MaxHP = 50,
                MP = 10,
                MaxMP = 10,
                ATK = 4,
                DEF = 1,
                SPD = 2
            };

            CharacterLevelUpResult result = CharacterProgressionService.GrantExperience(save, data, 25);

            Assert.That(save.Level, Is.EqualTo(3));
            Assert.That(save.EXP, Is.EqualTo(5));
            Assert.That(save.MaxHP, Is.EqualTo(60));
            Assert.That(save.HP, Is.EqualTo(60));
            Assert.That(save.ATK, Is.EqualTo(8));
            Assert.That(result.DidLevelUp, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ExperienceGrantNeverWrapsToNegative()
    {
        var save = new CharacterSaveData
        {
            Level = 1,
            EXP = int.MaxValue - 10,
            HP = 50,
            MaxHP = 50,
            MP = 10,
            MaxMP = 10,
            ATK = 4,
            DEF = 1,
            SPD = 2
        };

        CharacterProgressionService.GrantExperience(save, null, 100);

        Assert.That(save.EXP, Is.GreaterThanOrEqualTo(0));
        Assert.That(save.Level, Is.GreaterThanOrEqualTo(1));
    }
}
