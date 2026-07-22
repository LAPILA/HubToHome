#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ContentVisualReferenceValidationTests
{
    [Test]
    public void MissingOptionalPortraitsAndIconsAreWarnings()
    {
        CharacterData character = ScriptableObject.CreateInstance<CharacterData>();
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            character.CharacterID = "player.visual";
            enemy.EnemyId = "enemy.visual";
            skill.SkillID = "skill.visual";
            item.ItemID = "item.visual";

            var snapshot = new ProjectContentSnapshot();
            snapshot.Characters.Add(character);
            snapshot.Enemies.Add(enemy);
            snapshot.Skills.Add(skill);
            snapshot.Items.Add(item);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            ContentValidationIssue[] visualIssues = report.Issues
                .Where(issue => issue.Code.Contains(".visual."))
                .ToArray();

            Assert.That(visualIssues.Select(issue => issue.Code), Does.Contain("character.visual.portrait.missing"));
            Assert.That(visualIssues.Select(issue => issue.Code), Does.Contain("enemy.visual.turn_order_portrait.missing"));
            Assert.That(visualIssues.Select(issue => issue.Code), Does.Contain("skill.visual.icon.missing"));
            Assert.That(visualIssues.Select(issue => issue.Code), Does.Contain("item.visual.icon.missing"));
            Assert.That(visualIssues, Is.Not.Empty);
            Assert.That(visualIssues.All(issue => issue.Severity == ContentValidationSeverity.Warning), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(item);
        }
    }
}
#endif
