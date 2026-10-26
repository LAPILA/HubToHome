using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>검사만 수행합니다. Import/Refresh/자산 복구는 호출하지 않습니다.</summary>
public sealed class BunnySlimeElementLabContentTests
{
    private const string DataRoot = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data";

    [Test]
    public void SoloFrontline_HasOneStableIdentityAndTenCataloguedShowcaseSkills()
    {
        var lab = Load<BunnySlimeBattleLabData>(DataRoot + "/BunnySlimeBattleLab.asset");
        var catalog = Load<GameContentCatalog>("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        var skills = new HashSet<SkillData>();
        Assert.That(lab.Party, Has.Length.EqualTo(1));
        Assert.That(lab.GetPartyCount(false), Is.EqualTo(1));
        Assert.That(lab.GetPartyCount(true), Is.EqualTo(1));
        for (int i = 0; i < lab.Party.Length; i++)
        {
            CharacterData member = lab.Party[i];
            Assert.That(member.CharacterID, Is.EqualTo("lab.wizzel.front"));
            Assert.That(member.DefaultSkills, Has.Count.EqualTo(10));
            Assert.That(member.StatusProfile, Is.Not.Null);
            Assert.That(member.BattlePrefab, Is.Not.Null);
            Assert.That(member.BattlePrefab.GetComponent<PlayerCharacter>(), Is.Not.Null);
            Assert.That(member.BattlePrefab.GetComponent<PlayerController>(), Is.Not.Null);
            for (int j = 0; j < member.DefaultSkills.Count; j++)
            {
                SkillData skill = member.DefaultSkills[j];
                Assert.That(skill, Is.Not.Null);
                Assert.That(skills.Add(skill), Is.True, "실험실 스킬은 중복 편성하지 않습니다.");
                Assert.That(catalog.Skills, Does.Contain(skill));
                Assert.That(skill.SkillID, Does.StartWith("lab.showcase.wizzel."));
                Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.PlayerOnly));
                Assert.That(skill.ActionTimeline, Is.Not.Empty);
            }
        }
        Assert.That(skills.Count, Is.EqualTo(10));
        Assert.That(lab.Party[0].BattleSpriteTint, Is.EqualTo(Color.white));
    }

    [Test]
    public void ShowcaseAccents_UseActualSizeSilhouettesAndOneShotPreImpactReservations()
    {
        var front = Load<CharacterData>(DataRoot + "/Party/front.asset");
        var zev = Load<EnemyData>(DataRoot + "/Enemy/ZEV_Lab.asset");
        var showcase = new List<SkillData>(front.DefaultSkills);
        showcase.AddRange(zev.SkillList);
        Assert.That(showcase, Has.Count.EqualTo(15));
        int silhouettes = 0;
        int reservations = 0;
        foreach (SkillData skill in showcase)
        {
            bool sawImpact = false;
            int skillReservations = 0;
            for (int i = 0; i < skill.ActionTimeline.Count; i++)
            {
                SkillActionBlock block = skill.ActionTimeline[i];
                Assert.That(block, Is.Not.Null, skill.SkillID);
                if (block is Action_PresentationAccent accent)
                {
                    int mode = (int)accent.Mode;
                    Assert.That(mode, Is.EqualTo(1).Or.EqualTo(2), "폐기된 확대 컷인은 실험실에 남기지 않습니다: " + skill.SkillID);
                    Assert.That(accent.PortraitSprite, Is.Null, "실루엣은 배우의 현재 그림만 사용합니다: " + skill.SkillID);
                    if (mode == 1) { silhouettes++; continue; }
                    reservations++;
                    skillReservations++;
                    Assert.That(sawImpact, Is.False, "첫 명중 전에 단발 점멸을 예약합니다: " + skill.SkillID);
                    Assert.That(accent.Strength, Is.LessThanOrEqualTo(0.25f));
                    Assert.That(i + 1, Is.LessThan(skill.ActionTimeline.Count));
                    SkillActionBlock next = skill.ActionTimeline[i + 1];
                    Assert.That(skill.UsageProfile == SkillUsageProfile.EnemyOnly
                        ? next is Action_DefenseWindow
                        : IsShowcaseImpact(next), Is.True, "예약 직후 기존 대응/명중 흐름을 이어갑니다: " + skill.SkillID);
                }
                sawImpact |= IsShowcaseImpact(block);
            }
            Assert.That(skillReservations, Is.LessThanOrEqualTo(1), "난무의 매 타격마다 화면 점멸을 예약하지 않습니다.");
        }
        Assert.That(silhouettes, Is.EqualTo(9));
        Assert.That(reservations, Is.EqualTo(11));
    }

    [TestCase("SteamThrust", 1, 1.7f)]
    [TestCase("ReverseFlow", 2, 1.1f)]
    [TestCase("CriticalBreakthrough", 1, 2.7f)]
    public void WidePassShowcase_HasSingleOwnerOfCrossingDamage(string name, int passCount, float damagePerPass)
    {
        SkillData skill = Load<SkillData>(BunnySlimeElementLabBuilder.ShowcaseAllyRoot + "/Wizzel_" + name + ".asset");
        Action_ScreenPassSlash pass = null;
        foreach (SkillActionBlock block in skill.ActionTimeline)
        {
            if (block is Action_ScreenPassSlash candidate)
            {
                Assert.That(pass, Is.Null, "관통 구간의 피해 소유자는 하나입니다.");
                pass = candidate;
            }
            else Assert.That(IsShowcaseImpact(block), Is.False, "별도 피해 블록으로 중복 타격하지 않습니다.");
            if (block is Action_PlayAnim animation)
                Assert.That(animation.AnimTriggerName, Is.EqualTo("BattleReady"), "관통 전 별도 공격 모션을 먼저 재생하지 않습니다.");
        }
        Assert.That(pass, Is.Not.Null);
        Assert.That(pass.PassCount, Is.EqualTo(passCount));
        Assert.That(pass.DamagePerPass, Is.EqualTo(damagePerPass).Within(0.00001f));
        Assert.That(pass.Element, Is.EqualTo(DamageElement.Physical));
        Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.PlayerOnly));
    }

    private static bool IsShowcaseImpact(SkillActionBlock block)
        => block is Action_Damage || block is Action_Projectile || block is Action_SequentialMelee
        || block is Action_RapidStrikes || block is Action_AerialCrossSlash || block is Action_ScreenPassSlash;

    [Test]
    public void ScreenSplitSample_RemainsReusableWithoutOccupyingTheSoloLoadout()
    {
        SkillData skill = Load<SkillData>(BunnySlimeElementLabBuilder.ScreenSplitSkillPath);
        var front = Load<CharacterData>(DataRoot + "/Party/front.asset");
        var catalog = Load<GameContentCatalog>("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        Assert.That(front.SkillTree, Is.Null);
        CollectionAssert.DoesNotContain(front.DefaultSkills, skill);
        Assert.That(catalog.Skills, Does.Contain(skill));
        Assert.That(skill.SkillID, Is.EqualTo("lab.presentation.diagonal_screen_slash"));
        Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.PlayerOnly));
        Assert.That(skill.APCost, Is.EqualTo(12));
        Assert.That(skill.ElementalApplications, Is.Empty);
        Assert.That(skill.ActionTimeline, Has.Count.EqualTo(8));
        Assert.That(((Action_Move)skill.ActionTimeline[0]).Destination, Is.EqualTo(Action_Move.MoveDest.AttackStaging));
        var ready = skill.ActionTimeline[1] as Action_PlayAnim;
        Assert.That(ready, Is.Not.Null);
        Assert.That(ready.AnimTriggerName, Is.EqualTo("BattleReady"));
        Assert.That(ready.DelayAfter, Is.GreaterThanOrEqualTo(0.08f));
        Assert.That(((Action_PlayAnim)skill.ActionTimeline[2]).AnimTriggerName, Is.EqualTo("Attack"));
        Assert.That(((Action_VFX)skill.ActionTimeline[3]).VfxPrefab, Is.Not.Null);
        var damage = skill.ActionTimeline[4] as Action_Damage;
        Assert.That(damage, Is.Not.Null);
        Assert.That(damage.SkillMultiplier, Is.EqualTo(1.6f));
        Assert.That(damage.Element, Is.EqualTo(DamageElement.Physical));
        Assert.That(damage.ShakeCamera, Is.False);
        var split = skill.ActionTimeline[5] as Action_ScreenSplit;
        Assert.That(split, Is.Not.Null);
        Assert.That(split.HasValidSettings, Is.True);
        Assert.That(split.CanRunWithoutLivingTargets, Is.True);
        Assert.That(damage.CanRunWithoutLivingTargets, Is.False);
        Assert.That(split.ExpandDuration, Is.EqualTo(0.18f));
        Assert.That(split.HoldDuration, Is.EqualTo(1f));
        Assert.That(split.Duration, Is.EqualTo(0.3f));
        Assert.That(split.TotalDuration, Is.EqualTo(1.48f).Within(0.00001f));
        Assert.That(split.AngleDegrees, Is.EqualTo(-35f));
        Assert.That(split.SeparationPixels, Is.EqualTo(22f));
        Assert.That(split.TwistDegrees, Is.EqualTo(1.5f));
        Assert.That(split.LineWidthPixels, Is.EqualTo(1.25f));
        Assert.That(split.LineColor, Is.EqualTo(Color.white));
        Assert.That(split.OutlinePulseStrength, Is.EqualTo(0.65f));
        Assert.That(split.BlockHeader, Does.Contain("[화면 연출]"));
        Assert.That(((Action_Move)skill.ActionTimeline[6]).Destination, Is.EqualTo(Action_Move.MoveDest.OriginalPos));
        Assert.That(((Action_PlayAnim)skill.ActionTimeline[7]).AnimTriggerName, Is.EqualTo("BattleIdle"));
        foreach (SkillActionBlock block in skill.ActionTimeline)
            Assert.That(block, Is.Not.InstanceOf<Action_QTE>(), "화면 참격은 새로운 입력 단계를 추가하지 않습니다.");
    }

    [Test]
    public void ScreenSplitBlock_RejectsInvalidSettingsWithoutAffectingDamage()
    {
        var block = new Action_ScreenSplit();
        Assert.That(block.HasValidSettings, Is.True);
        block.Duration = float.NaN;
        Assert.That(block.HasValidSettings, Is.False);
        block.Duration = 0.3f;
        block.SeparationPixels = -1f;
        Assert.That(block.HasValidSettings, Is.False);
        block.SeparationPixels = 22f;
        block.TwistDegrees = float.PositiveInfinity;
        Assert.That(block.HasValidSettings, Is.False);
        block.TwistDegrees = 1.5f;
        block.LineColor = new Color(1f, float.NaN, 0f, 1f);
        Assert.That(block.HasValidSettings, Is.False);
        Assert.That(block.Execute(null).MoveNext(), Is.False);
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void ScreenSplitBlock_RejectsInvalidHoldDuration(float holdDuration)
    {
        var block = new Action_ScreenSplit { HoldDuration = holdDuration };
        Assert.That(block.HasValidSettings, Is.False);
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void ScreenSplitBlock_RejectsInvalidExpandDuration(float expandDuration)
    {
        var block = new Action_ScreenSplit { ExpandDuration = expandDuration };
        Assert.That(block.HasValidSettings, Is.False);
    }

    [TestCase(-0.01f)]
    [TestCase(1.01f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void ScreenSplitBlock_RejectsInvalidOutlinePulseStrength(float outlinePulseStrength)
    {
        var block = new Action_ScreenSplit { OutlinePulseStrength = outlinePulseStrength };
        Assert.That(block.HasValidSettings, Is.False);
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
    public void LabEntries_PreserveBothStoriesWithoutTheReserveExercise()
    {
        var lab = Load<BunnySlimeBattleLabData>(DataRoot + "/BunnySlimeBattleLab.asset");
        Assert.That(lab.Encounters, Has.Length.EqualTo(2));
        Assert.That(lab.Encounters[0].Enemy.EnemyId, Is.EqualTo("lab.bunny_slime"));
        Assert.That(lab.Encounters[0].Scenario, Is.Not.Null);
        Assert.That(lab.Encounters[0].WeakenFrontLine, Is.False);
        Assert.That(lab.Encounters[1].Id, Is.EqualTo("lab.zev.presentation"));
        Assert.That(lab.Encounters[1].Scenario, Is.Not.Null);
        Assert.That(lab.Encounters[1].WeakenFrontLine, Is.False);
        foreach (BattleLabEncounterEntry entry in lab.Encounters)
        {
            Assert.That(lab.GetPartyCount(entry.WeakenFrontLine), Is.EqualTo(1), entry.Id);
            Assert.That(entry.WeakenFrontLine, Is.False, entry.Id);
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
