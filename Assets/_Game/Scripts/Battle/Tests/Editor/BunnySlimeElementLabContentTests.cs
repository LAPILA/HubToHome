using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>검사만 수행합니다. Import/Refresh/자산 복구는 호출하지 않습니다.</summary>
public sealed class BunnySlimeElementLabContentTests
{
    private const string DataRoot = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data";

    [Test]
    public void Frontline_HasThreeStableIdentitiesAndEighteenCataloguedSkills()
    {
        var lab = Load<BunnySlimeBattleLabData>(DataRoot + "/BunnySlimeBattleLab.asset");
        var catalog = Load<GameContentCatalog>("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        string[] ids = { "lab.wizzel.front", "lab.wizzel.support", "lab.wizzel.striker" };
        var skills = new HashSet<SkillData>();
        var kinds = new HashSet<ElementalStatusKind>();
        Assert.That(lab.Party, Has.Length.EqualTo(6));
        Assert.That(lab.GetPartyCount(false), Is.EqualTo(3));
        Assert.That(lab.GetPartyCount(true), Is.EqualTo(6));
        for (int i = 0; i < 3; i++)
        {
            CharacterData member = lab.Party[i];
            Assert.That(member.CharacterID, Is.EqualTo(ids[i]));
            Assert.That(member.DefaultSkills, Has.Count.EqualTo(6));
            Assert.That(member.StatusProfile, Is.Not.Null);
            Assert.That(member.BattlePrefab, Is.Not.Null);
            Assert.That(member.BattlePrefab.GetComponent<PlayerCharacter>(), Is.Not.Null);
            Assert.That(member.BattlePrefab.GetComponent<PlayerController>(), Is.Not.Null);
            foreach (SkillData skill in member.DefaultSkills)
            {
                Assert.That(skill, Is.Not.Null);
                Assert.That(skills.Add(skill), Is.True, "스킬은 각 역할에 하나씩만 편성합니다.");
                Assert.That(catalog.Skills, Does.Contain(skill));
                Assert.That(skill.SkillID, Does.StartWith("lab.elements."));
                Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.PlayerOnly));
                foreach (ElementalStatusApplication application in skill.ElementalApplications)
                    kinds.Add(application.Kind);
                foreach (SkillActionBlock block in skill.ActionTimeline)
                    Assert.That(block, Is.Not.InstanceOf<Action_ApplyStatus>(), "새 실험실은 구형 N턴 상태와 혼합하지 않습니다.");
            }
        }
        Assert.That(skills.Count, Is.EqualTo(18));
        Assert.That(kinds.Count, Is.EqualTo(5));
        Assert.That(lab.Party[0].BattleSpriteTint, Is.EqualTo(Color.white));
        Assert.That(lab.Party[1].BattleSpriteTint, Is.Not.EqualTo(lab.Party[2].BattleSpriteTint));
    }

    [Test]
    public void RapidSample_HasFiftyHitsAndExplicitTenthProcCoefficient()
    {
        SkillData rapid = Load<SkillData>(BunnySlimeElementLabBuilder.AllyRoot + "/striker_rapid50.asset");
        Assert.That(rapid.StatusProcCoefficient, Is.EqualTo(.1f));
        Assert.That(rapid.ElementalApplications, Has.Length.EqualTo(1));
        Assert.That(rapid.ElementalApplications[0].Mode, Is.EqualTo(ElementalApplicationMode.PerHit));
        Assert.That(rapid.ElementalApplications[0].Amount, Is.EqualTo(.2f));
        Assert.That(rapid.ActionTimeline, Has.Count.EqualTo(1));
        var block = rapid.ActionTimeline[0] as Action_RapidStrikes;
        Assert.That(block, Is.Not.Null);
        Assert.That(Action_RapidStrikes.CountDueHits(block.Duration, block.Duration, block.HitInterval), Is.EqualTo(50));
    }

    [Test]
    public void LabEntries_PreserveStoryAndSeparateSixMemberWipeExercise()
    {
        var lab = Load<BunnySlimeBattleLabData>(DataRoot + "/BunnySlimeBattleLab.asset");
        Assert.That(lab.Encounters, Has.Length.EqualTo(3));
        Assert.That(lab.Encounters[0].Enemy.EnemyId, Is.EqualTo("lab.bunny_slime"));
        Assert.That(lab.Encounters[0].Scenario, Is.Not.Null);
        Assert.That(lab.Encounters[0].WeakenFrontLine, Is.False);
        Assert.That(lab.Encounters[1].Id, Is.EqualTo("lab.zev.presentation"));
        Assert.That(lab.Encounters[1].Scenario, Is.Not.Null);
        Assert.That(lab.Encounters[1].WeakenFrontLine, Is.False);
        Assert.That(lab.Encounters[2].Id, Is.EqualTo("lab.bunny.wave"));
        Assert.That(lab.Encounters[2].WeakenFrontLine, Is.True);
        foreach (BattleLabEncounterEntry entry in lab.Encounters)
        {
            Assert.That(Mathf.Min(3, lab.GetPartyCount(entry.WeakenFrontLine)), Is.EqualTo(3), entry.Id);
            Assert.That(entry.Enemy, Is.Not.Null, entry.Id);
            Assert.That(entry.Enemy.Prefab, Is.Not.Null, entry.Id);
            Assert.That(entry.Enemy.Prefab.GetComponentsInChildren<EnemyCharacter>(true), Has.Length.EqualTo(1), entry.Id);
        }
        EnemyData bunny = lab.Encounters[0].Enemy;
        Assert.That(bunny.SkillList, Has.Count.EqualTo(8));
        Assert.That(bunny.UseOrderedSkills, Is.True);
        Assert.That(bunny.StatusProfile, Is.SameAs(lab.Party[0].StatusProfile));
        Assert.That(bunny.StatusProfile.StatusDamageUnit, Is.EqualTo(3f));
        foreach (SkillData skill in bunny.SkillList)
            foreach (SkillActionBlock block in skill.ActionTimeline)
                Assert.That(block, Is.Not.InstanceOf<Action_ApplyStatus>());
    }

    private static T Load<T>(string path) where T : Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(value, Is.Not.Null, path);
        return value;
    }
}
