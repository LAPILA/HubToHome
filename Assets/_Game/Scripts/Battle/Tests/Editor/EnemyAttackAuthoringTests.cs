#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAttackAuthoringTests
{
    [Test]
    public void AnalyzerBuildsCumulativeTimelineFromRuntimeTimingContract()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_Move
            {
                Destination = Action_Move.MoveDest.AttackStaging,
                Duration = 0.2f
            });
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                PatternMode = EnemyDefensePatternMode.TelegraphThenWindow,
                UseTelegraph = true,
                TelegraphVisualMode = TelegraphVisualMode.AnimatorTrigger,
                TelegraphAnimatorTriggerName = "Warn",
                TelegraphDuration = 0.5f,
                DefenseOpenDelay = 0.1f,
                TimeWindow = 0.8f,
                AttackAnimTriggerName = "Attack",
                AttackAnimDelay = 0.2f,
                DelayAfter = 0.1f
            });
            skill.ActionTimeline.Add(new Action_Damage { SkillMultiplier = 1.5f });
            skill.ActionTimeline.Add(new Action_Wait { WaitTime = 0.25f });

            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);

            Assert.That(report.EstimatedDuration, Is.EqualTo(1.95f).Within(0.0001f));
            Assert.That(report.Entries.Count, Is.EqualTo(4));
            Assert.That(report.Entries[1].StartTime, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(report.Entries[1].Duration, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(report.DefenseWindowCount, Is.EqualTo(1));
            Assert.That(report.DamageBlockCount, Is.EqualTo(1));
            Assert.That(report.ErrorCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void AnalyzerReportsInvalidDefenseTimingReferencesAndCameraSafety()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                PatternMode = EnemyDefensePatternMode.TelegraphThenWindow,
                UseTelegraph = true,
                TelegraphVisualMode = TelegraphVisualMode.PrefabVFX,
                WarningVfxPrefab = null,
                TelegraphDuration = 0f,
                TimeWindow = 0f,
                OverrideTimingProfile = true,
                TimingProfile = new DefenseTimingProfile(0.3f, 0.1f, 0.5f),
                ShakeOnFail = true,
                FailShakeIntensity = 2f,
                FailShakeDuration = 0.8f,
                FailShakeSafety = CameraShakeSafety.Cinematic
            });

            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.telegraph.prefab.missing"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.telegraph.duration.invalid"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.window.invalid"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.timing.order.invalid"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.timing.exceeds_window"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.camera.safety.cinematic"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.camera.intensity.excessive"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.camera.duration.excessive"));
            Assert.That(report.ErrorCount, Is.GreaterThan(0));
            Assert.That(report.WarningCount, Is.GreaterThan(0));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void DisabledInvalidBlockStaysVisibleButDoesNotFailValidation()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                Disabled = true,
                TimeWindow = -1f,
                UseTelegraph = true,
                WarningVfxPrefab = null
            });
            skill.ActionTimeline.Add(new Action_Damage());

            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);

            Assert.That(report.Entries.Count, Is.EqualTo(2));
            Assert.That(report.Entries[0].Enabled, Is.False);
            Assert.That(report.ErrorCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void UnsupportedCustomBlockUsesExplicitPreviewExtensionPoint()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new UnsupportedCustomBlock());

            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);

            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Does.Contain("skill.enemy_attack.timeline.preview.unsupported"));
            Assert.That(report.Entries[0].TimingSupported, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void TemplateUsesRelativeAttackStagingAndReturnsToBattleSlot()
    {
        var blocks = EnemyAttackTemplateFactory.CreateTelegraphedStrike();

        Assert.That(blocks.Count, Is.EqualTo(4));
        Assert.That(blocks[0], Is.TypeOf<Action_Move>());
        Assert.That(((Action_Move)blocks[0]).Destination, Is.EqualTo(Action_Move.MoveDest.AttackStaging));
        Assert.That(blocks[1], Is.TypeOf<Action_DefenseWindow>());
        Assert.That(
            ((Action_DefenseWindow)blocks[1]).PatternMode,
            Is.EqualTo(EnemyDefensePatternMode.TelegraphThenWindow));
        Assert.That(blocks[2], Is.TypeOf<Action_Damage>());
        Assert.That(((Action_Move)blocks[3]).Destination, Is.EqualTo(Action_Move.MoveDest.OriginalPos));
    }

    private static SkillData CreateEnemySkill()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        skill.SkillID = "enemy.authoring_test";
        skill.UsageProfile = SkillUsageProfile.EnemyOnly;
        skill.ActionTimeline.Clear();
        return skill;
    }

    private sealed class UnsupportedCustomBlock : SkillActionBlock
    {
        public override IEnumerator Execute(SkillContext context)
        {
            yield break;
        }
    }
}
#endif
