#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAttackAuthoringTests
{
    [Test]
    public void ProjectileDefense_IsOneSynchronizedTimelineAndDoesNotModifyAuthoredWindow()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            var defense = new Action_DefenseWindow
            {
                UseTelegraph = false, TimeWindow = 0.8f, AttackAnimationLeadTime = 0.6f,
                DelayAfter = 0.1f, AttackReadyDuration = 0.08f
            };
            skill.ActionTimeline.Add(defense);
            skill.ActionTimeline.Add(new Action_Wait { Disabled = true });
            skill.ActionTimeline.Add(new Action_Projectile { FlightDuration = 0.3f });
            Assert.That(SkillContext.HasSynchronizedImpact(defense, skill.ActionTimeline), Is.True);
            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
            Assert.That(report.Entries[0].Duration, Is.Zero);
            Assert.That(report.EstimatedDuration, Is.EqualTo(0.53f).Within(0.0001f));
            Assert.That(defense.TimeWindow, Is.EqualTo(0.8f));
            Assert.That(defense.AttackAnimationLeadTime, Is.EqualTo(0.6f));
            ((Action_Wait)skill.ActionTimeline[1]).Disabled = false;
            Assert.That(SkillContext.HasSynchronizedImpact(defense, skill.ActionTimeline), Is.False,
                "Authored waits between defense and impact must not be silently reordered.");
        }
        finally { Object.DestroyImmediate(skill); }
    }

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

            // 준비 자세 뒤에 방어창/공격 모션을 함께 시작합니다.
            Assert.That(report.EstimatedDuration, Is.EqualTo(2.08f).Within(0.0001f));
            Assert.That(report.Entries.Count, Is.EqualTo(4));
            Assert.That(report.Entries[1].StartTime, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(report.Entries[1].Duration, Is.EqualTo(1.63f).Within(0.0001f));
            Assert.That(report.DefenseWindowCount, Is.EqualTo(1));
            Assert.That(report.DamageBlockCount, Is.EqualTo(1));
            Assert.That(report.ErrorCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [TestCase(-0.1f, true)]
    [TestCase(0f, false)]
    [TestCase(0.85f, false)]
    [TestCase(0.86f, true)]
    [TestCase(float.NaN, true)]
    [TestCase(float.PositiveInfinity, true)]
    public void AnalyzerChecksAttackAnimationLeadTime(float lead, bool invalid)
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                UseTelegraph = false,
                TimeWindow = 0.85f,
                AttackAnimationLeadTime = lead
            });
            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
            Assert.That(report.Issues.Any(issue => issue.Code == "skill.enemy_attack.animation.lead.invalid"),
                Is.EqualTo(invalid));
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

    [Test]
    public void CounterableTemplate_HasExplicitResponseAndNoExtraFailurePenalty()
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline = EnemyAttackTemplateFactory.CreateCounterableStrike();
            var movement = (Action_Move)skill.ActionTimeline[0];
            var defense = (Action_DefenseWindow)skill.ActionTimeline[1];

            Assert.That(movement.Destination, Is.EqualTo(Action_Move.MoveDest.Center));
            Assert.That(movement.EnemyHopHeight, Is.EqualTo(0.45f));
            Assert.That(defense.Requirement, Is.EqualTo(DefenseRequirement.Counterable));
            Assert.That(defense.CounterDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(defense.UseTelegraph, Is.True);
            Assert.That(defense.TelegraphDuration, Is.EqualTo(0.6f));
            Assert.That(defense.TimeWindow, Is.EqualTo(0.8f));
            Assert.That(defense.FailDamageMultiplier, Is.EqualTo(1f));
            Assert.That(((Action_Damage)skill.ActionTimeline[2]).SkillMultiplier, Is.EqualTo(1.8f));
            Assert.That(EnemyAttackAuthoringAnalyzer.Analyze(skill).ErrorCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefenseWindows_WarnOnlyWhenPreviousResultHasNoDamageConsumer(bool consumeFirstResult)
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_DefenseWindow { UseTelegraph = false });
            skill.ActionTimeline.Add(consumeFirstResult
                ? (SkillActionBlock)new Action_Damage()
                : new Action_Wait { WaitTime = 0.1f });
            skill.ActionTimeline.Add(new Action_DefenseWindow { UseTelegraph = false });
            skill.ActionTimeline.Add(new Action_Damage());

            bool hasWarning = EnemyAttackAuthoringAnalyzer.Analyze(skill).Issues
                .Any(issue => issue.Code == "skill.enemy_attack.defense.unconsumed_result");

            Assert.That(hasWarning, Is.EqualTo(!consumeFirstResult));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void CounterableAttack_RejectsInvalidDamageAndWarnsForUnreadableTelegraph(float multiplier)
    {
        SkillData skill = CreateEnemySkill();
        try
        {
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                Requirement = DefenseRequirement.Counterable,
                PatternMode = EnemyDefensePatternMode.ImmediateReaction,
                UseTelegraph = false,
                TimeWindow = 0.2f,
                CounterDamageMultiplier = multiplier,
                ShakeOnFail = false
            });
            skill.ActionTimeline.Add(new Action_Damage());
            string[] codes = EnemyAttackAuthoringAnalyzer.Analyze(skill).Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("skill.enemy_attack.counter.damage.invalid"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.counter.telegraph.recommended"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.counter.window.short"));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
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
