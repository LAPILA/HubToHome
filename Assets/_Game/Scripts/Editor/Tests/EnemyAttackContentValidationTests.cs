#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAttackContentValidationTests
{
    [Test]
    public void ProjectValidatorUsesEnemyAttackTimingAndCameraRules()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            skill.SkillID = "enemy.validation_test";
            skill.UsageProfile = SkillUsageProfile.EnemyOnly;
            skill.ActionTimeline.Add(new Action_DefenseWindow
            {
                PatternMode = EnemyDefensePatternMode.TelegraphThenWindow,
                UseTelegraph = true,
                TelegraphVisualMode = TelegraphVisualMode.PrefabVFX,
                TelegraphDuration = 0f,
                TimeWindow = 0f,
                ShakeOnFail = true,
                FailShakeIntensity = 2f,
                FailShakeDuration = 0.8f,
                FailShakeSafety = CameraShakeSafety.Cinematic
            });

            var snapshot = new ProjectContentSnapshot();
            snapshot.Skills.Add(skill);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.telegraph.prefab.missing"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.window.invalid"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.camera.intensity.excessive"));
            Assert.That(codes, Does.Contain("skill.enemy_attack.defense.camera.safety.cinematic"));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }
}
#endif
