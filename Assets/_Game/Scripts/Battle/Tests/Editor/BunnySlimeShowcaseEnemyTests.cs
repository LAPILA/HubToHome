using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BunnySlimeShowcaseEnemyTests
{
    [Test]
    public void LabAssets_ProjectilePatternsAreConnectedAndUseDefenseNotPlayerQte()
    {
        const string root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/";
        var data = AssetDatabase.LoadAssetAtPath<BunnySlimeBattleLabData>(root + "Data/BunnySlimeBattleLab.asset");
        Assert.That(data, Is.Not.Null);
        Assert.That(data.ProjectileSkills, Has.Length.EqualTo(3));
        int[] expectedProjectiles = { 1, 2, 1 };
        for (int i = 0; i < data.ProjectileSkills.Length; i++)
        {
            SkillData skill = data.ProjectileSkills[i];
            Assert.That(skill, Is.Not.Null);
            Assert.That(data.Enemy.SkillList, Does.Contain(skill));
            Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.EnemyOnly));
            Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).HasErrors, Is.False);
            int projectileCount = 0;
            for (int block = 0; block < skill.ActionTimeline.Count; block++)
            {
                Assert.That(skill.ActionTimeline[block], Is.Not.TypeOf<Action_QTE>());
                if (!(skill.ActionTimeline[block] is Action_Projectile projectile)) continue;
                projectileCount++;
                Assert.That(projectile.ProjectilePrefab, Is.Not.Null);
                Assert.That(skill.ActionTimeline[block - 1], Is.TypeOf<Action_DefenseWindow>());
                var defense = (Action_DefenseWindow)skill.ActionTimeline[block - 1];
                Assert.That(defense.Requirement, Is.EqualTo(i == 2 ? DefenseRequirement.DodgeOnly : DefenseRequirement.Any));
            }
            Assert.That(projectileCount, Is.EqualTo(expectedProjectiles[i]));
        }
        Assert.That(data.Enemy.SkillList, Has.Count.EqualTo(10));
        var ai = data.Enemy.Prefab.GetComponent<BunnySlimeShowcaseEnemy>();
        Assert.That(ai.Phase2StartIndex, Is.EqualTo(5));
        Assert.That(ai.Phase3StartIndex, Is.EqualTo(9));
        Assert.That(data.CounterSkill.ActionTimeline[0], Is.TypeOf<Action_Move>());
    }

    private readonly List<Object> _objects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void Showcase_UsesEveryAuthoredSkillInOrderAndWraps()
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        SkillData first = CreateSkill();
        SkillData second = CreateSkill();
        SkillData third = CreateSkill();
        enemy.Data.SkillList.AddRange(new[] { first, second, third });

        Assert.That(enemy.DecideAction(), Is.EqualTo(EnemyAction.UseSkill));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(first));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(second));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(third));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(first));
    }

    [Test]
    public void Showcase_SkipsMissingEntriesAndDoesNotAdvanceForOtherActions()
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        SkillData skill = CreateSkill();
        enemy.Data.SkillList.AddRange(new[] { null, skill, null });

        Assert.That(enemy.SelectSkill(EnemyAction.UseStrongSkill), Is.Null);
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(skill));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(skill));
        Assert.That(enemy.Data.SkillList.Count, Is.EqualTo(3));
        Assert.That(enemy.Data.SkillList[0], Is.Null);
    }

    [Test]
    public void Showcase_EmptyListWaitsWithoutFallbackAttack()
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        enemy.Data.SkillList.Add(null);

        Assert.That(enemy.DecideAction(), Is.EqualTo(EnemyAction.Wait));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.Null);
    }

    [Test]
    public void StandardEnemy_PreservesNormalAndStrongSkillSelection()
    {
        EnemyCharacter enemy = CreateEnemy<EnemyCharacter>();
        SkillData normal = CreateSkill();
        SkillData strong = CreateSkill();
        enemy.Data.SkillList.Add(normal);
        enemy.Data.StrongSkillList.Add(strong);

        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(normal));
        Assert.That(enemy.SelectSkill(EnemyAction.UseStrongSkill), Is.SameAs(strong));
        Assert.That(enemy.SelectSkill(EnemyAction.BasicAttack), Is.Null);
        Assert.That(enemy.SelectSkill(EnemyAction.Wait), Is.Null);
    }

    [TestCase(67, 0)]
    [TestCase(66, 3)]
    [TestCase(34, 3)]
    [TestCase(33, 6)]
    [TestCase(1, 6)]
    public void HealthPhase_OpensNewPatternAtExactPercentageBoundary(int hp, int expectedIndex)
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        for (int i = 0; i < 7; i++) enemy.Data.SkillList.Add(CreateSkill());
        enemy.TakePureDamage(enemy.MaxHP - hp);

        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[expectedIndex]));
    }

    [Test]
    public void HealthPhase_NewPhaseStartsWithItsNewPatternAndKeepsEarlierPatterns()
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        for (int i = 0; i < 7; i++) enemy.Data.SkillList.Add(CreateSkill());
        for (int i = 0; i < 4; i++)
            Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[i % 3]));

        enemy.TakePureDamage(34);
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[3]));
        enemy.TakePureDamage(33);
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[6]));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[0]));
    }

    [Test]
    public void HealthPhase_DisabledOrShortDrillKeepsWholeListAvailable()
    {
        BunnySlimeShowcaseEnemy enemy = CreateEnemy<BunnySlimeShowcaseEnemy>();
        for (int i = 0; i < 6; i++) enemy.Data.SkillList.Add(CreateSkill());
        for (int i = 0; i < 6; i++)
            Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[i]));

        enemy.Data.SkillList.Add(CreateSkill());
        enemy.UseHealthPhases = false;
        for (int i = 0; i < 7; i++)
            Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(enemy.Data.SkillList[i]));
    }

    private T CreateEnemy<T>() where T : EnemyCharacter
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        _objects.Add(data);
        GameObject root = new GameObject("Bunny showcase AI test");
        _objects.Add(root);
        T enemy = root.AddComponent<T>();
        enemy.Setup(data);
        return enemy;
    }

    private SkillData CreateSkill()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        _objects.Add(skill);
        return skill;
    }
}
