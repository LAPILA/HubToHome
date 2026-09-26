#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillContentValidationTests
{
    [Test]
    public void RapidSample_IsConnectedToLabPartyAndCatalog_AndExposesFiveSecondTiming()
    {
        const string root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data/";
        SkillData skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(root + "Skills/rapid_slash.asset");
        CharacterData actor = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(root + "Party/front.asset");
        GameContentCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        Assert.That(skill, Is.Not.Null);
        Assert.That(actor.DefaultSkills, Does.Contain(skill));
        Assert.That(catalog.Skills, Does.Contain(skill));
        Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.PlayerOnly));
        Assert.That(skill.ActionTimeline[0], Is.TypeOf<Action_RapidStrikes>());
        Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).HasErrors, Is.False);
        Assert.That(skill.ActionTimeline[0].GetAuthoringTiming().Duration, Is.EqualTo(5f));
        var rapid = (Action_RapidStrikes)skill.ActionTimeline[0];
        Assert.That(rapid.HasValidSettings, Is.True);
        Assert.That(rapid.HitInterval, Is.EqualTo(.1f));
        Assert.That(rapid.PromptInterval, Is.EqualTo(.65f));
        Assert.That(rapid.CameraRoll, Is.False);
        Assert.That(Action_RapidStrikes.CountDueHits(rapid.Duration, rapid.Duration, rapid.HitInterval), Is.EqualTo(50));
    }

    [Test]
    public void RapidBlock_RejectsEnemyUseAndInvalidInputsInAuthoring()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            skill.UsageProfile = SkillUsageProfile.EnemyOnly;
            skill.ActionTimeline.Add(new Action_RapidStrikes { Duration = 0f, PromptInterval = 0f });
            Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).HasErrors, Is.True);
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [Test]
    public void AerialSample_IsSeparateAndConnectedToLabPartyAndCatalog()
    {
        const string root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data/";
        SkillData skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(root + "Skills/aerial_cross_slash.asset");
        CharacterData actor = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(root + "Party/front.asset");
        GameContentCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        Assert.That(skill, Is.Not.Null);
        Assert.That(actor.DefaultSkills, Does.Contain(skill));
        Assert.That(catalog.Skills, Does.Contain(skill));
        Assert.That(skill.ActionTimeline[0], Is.TypeOf<Action_AerialCrossSlash>());
        Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).HasErrors, Is.False);
        Assert.That(((Action_AerialCrossSlash)skill.ActionTimeline[0]).HasValidSettings, Is.True);
    }

    [Test]
    public void AerialBlock_RejectsInvalidRotationTiming()
    {
        Assert.That(new Action_AerialCrossSlash { SpinDuration = float.NaN }.HasValidSettings, Is.False);
        Assert.That(new Action_AerialCrossSlash { InputWindow = 10f }.HasValidSettings, Is.False);
        Assert.That(new Action_AerialCrossSlash { Height = 0f }.HasValidSettings, Is.False);
    }

    [Test]
    public void ValidatorReportsMissingSkillBlockReferences()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            skill.SkillID = "skill.reference_test";
            skill.ActionTimeline.Add(null);
            skill.ActionTimeline.Add(new Action_VFX());
            skill.ActionTimeline.Add(new Action_Projectile());

            var snapshot = new ProjectContentSnapshot();
            snapshot.Skills.Add(skill);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("skill.timeline.block.missing"));
            Assert.That(codes, Does.Contain("skill.timeline.vfx_prefab.missing"));
            Assert.That(codes, Does.Contain("skill.timeline.projectile_prefab.missing"));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }
}
#endif
