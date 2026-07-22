#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillContentValidationTests
{
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
