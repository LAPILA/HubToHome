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
        Assert.That(data.Enemy.UseOrderedSkills, Is.True);
        Assert.That(data.Enemy.SkillList[2], Is.SameAs(data.CounterSkill));
    }

    [Test]
    public void LabEncounters_UseTwoBattlesAndPreservedReserveExercise()
    {
        const string root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/";
        var data = AssetDatabase.LoadAssetAtPath<BunnySlimeBattleLabData>(root + "Data/BunnySlimeBattleLab.asset");
        Assert.That(data.Encounters, Has.Length.EqualTo(3));
        Assert.That(data.Encounters[0].Enemy, Is.SameAs(data.Enemy));
        Assert.That(data.Encounters[1].Enemy.EnemyId, Is.EqualTo("lab.zev"));
        Assert.That(data.Encounters[1].Scenario, Is.Not.Null);
        Assert.That(data.Encounters[0].WeakenFrontLine, Is.False);
        Assert.That(data.Encounters[1].WeakenFrontLine, Is.False);
        Assert.That(data.Encounters[2].WeakenFrontLine, Is.True);
        Assert.That(data.Encounters[2].Scenario, Is.Null);
        Assert.That(data.Encounters[2].Enemy.SkillList, Is.EqualTo(new[] { data.WaveSkill }));
        Assert.That(data.Encounters[1].Enemy.SkillList, Has.Count.EqualTo(5));
        foreach (SkillData skill in data.Encounters[1].Enemy.SkillList)
        {
            Assert.That(AssetDatabase.GetAssetPath(skill), Does.StartWith("Assets/_Game/Content/Skills/Enemy/ZEV/"));
            Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.EnemyOnly));
            Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).HasErrors, Is.False, skill.name);
            Assert.That(skill.ActionTimeline.Exists(block => block is Action_EnemyWindup), Is.True);
            Assert.That(skill.ActionTimeline.Exists(block => block is Action_QTE), Is.False);
            Assert.That(skill.ActionTimeline.Exists(block => block is Action_Projectile), Is.False,
                "ZEV is melee-only; ranged patterns belong to the bunny lab.");
        }
    }

    [Test]
    public void ZevOriginalSkills_EachHitHasOneCenterCueBeforeItsEffects()
    {
        const string folder = "Assets/_Game/Content/Skills/Enemy/ZEV/";
        string[] names = { "Skill_ComboSlash", "Skil_Zev", "Skill_Crash", "Skill_BlinkSlash", "Skill_PhantomArc" };
        int[] hits = { 3, 1, 1, 1, 2 };
        for (int i = 0; i < names.Length; i++)
        {
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(folder + names[i] + ".asset");
            Assert.That(skill, Is.Not.Null, names[i]);
            int damageCount = 0, windowCount = 0;
            bool windowOpen = false;
            foreach (SkillActionBlock block in skill.ActionTimeline)
            {
                Assert.That(block, Is.Not.Null, skill.name);
                if (block.Disabled) continue;
                if (block is Action_DefenseWindow defense)
                {
                    Assert.That(windowOpen, Is.False, "Do not stack windows before a single hit.");
                    windowOpen = true;
                    windowCount++;
                    Assert.That(defense.ImpactCuePrefab, Is.Not.Null, skill.name);
                    Assert.That(defense.ImpactCuePivotName, Is.EqualTo(CharacterPivotId.Center));
                    Assert.That(defense.UseTelegraph, Is.False, "Only the impact clock owns the visible cue.");
                    Assert.That(defense.TimeWindow, Is.GreaterThanOrEqualTo(.4f));
                    Assert.That(defense.Requirement, Is.EqualTo(i == 1 ? DefenseRequirement.Counterable : DefenseRequirement.Any));
                }
                if (block is Action_VFX)
                    Assert.That(windowOpen, Is.True, "Slash effects must follow the defense window.");
                if (block is Action_Damage)
                {
                    Assert.That(windowOpen, Is.True, "Every melee impact needs its own defense window.");
                    windowOpen = false;
                    damageCount++;
                }
            }
            Assert.That(windowOpen, Is.False);
            Assert.That(windowCount, Is.EqualTo(hits[i]), skill.name);
            Assert.That(damageCount, Is.EqualTo(hits[i]), skill.name);
        }
    }

    [Test]
    public void OrderedStandardEnemy_SkipsNullWrapsAndResetsOnSetup()
    {
        EnemyCharacter enemy = CreateEnemy<EnemyCharacter>();
        SkillData first = CreateSkill(), second = CreateSkill();
        enemy.Data.UseOrderedSkills = true;
        enemy.Data.SkillUseChance = 0f;
        enemy.Data.SkillList.AddRange(new[] { first, null, second });
        Assert.That(enemy.DecideAction(), Is.EqualTo(EnemyAction.UseSkill));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(first));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(second));
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(first));
        enemy.Setup(enemy.Data);
        Assert.That(enemy.SelectSkill(EnemyAction.UseSkill), Is.SameAs(first));
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
