#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ProjectContentIdentityValidationTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void SelectedSkillReferencesReportDuplicateForEitherOwnerAndMissingCatalogEntry(bool selectFirst)
    {
        SkillData first = ScriptableObject.CreateInstance<SkillData>();
        SkillData second = ScriptableObject.CreateInstance<SkillData>();
        GameContentCatalog catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
        try
        {
            first.SkillID = second.SkillID = "skill.shared";
            SkillData selected = selectFirst ? first : second;
            var snapshot = new ProjectContentSnapshot { Catalog = catalog };
            snapshot.Skills.Add(first);
            snapshot.Skills.Add(second);
            catalog.Skills.Add(selectFirst ? second : first);
            ContentValidationReport report = ProjectContentValidator.ValidateSkillReferences(snapshot, selected);
            Assert.That(report.Issues.Any(issue => issue.Code == "skill.id.duplicate" && issue.Context == selected), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "catalog.skill.missing" && issue.Context == selected), Is.True);
            Assert.That(report.Issues.All(issue => issue.Context == selected || issue.Context == catalog), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "catalog.default_ui_font.missing"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void SelectedSkillReferencesReportMissingCatalogWithoutScanningOtherContentKinds()
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            skill.SkillID = "skill.valid";
            var snapshot = new ProjectContentSnapshot();
            snapshot.Skills.Add(skill);
            ContentValidationReport report = ProjectContentValidator.ValidateSkillReferences(snapshot, skill);
            Assert.That(report.Issues.Select(issue => issue.Code), Is.EqualTo(new[] { "catalog.missing" }));
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ValidatorReportsMissingInvalidAndDuplicateIdsByContentKind()
    {
        CharacterData firstCharacter = ScriptableObject.CreateInstance<CharacterData>();
        CharacterData secondCharacter = ScriptableObject.CreateInstance<CharacterData>();
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            firstCharacter.CharacterID = "player.same";
            secondCharacter.CharacterID = "player.same";
            skill.SkillID = "Skill Invalid";
            item.ItemID = string.Empty;

            var snapshot = new ProjectContentSnapshot();
            snapshot.Characters.Add(firstCharacter);
            snapshot.Characters.Add(secondCharacter);
            snapshot.Skills.Add(skill);
            snapshot.Items.Add(item);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("character.id.duplicate"));
            Assert.That(codes, Does.Contain("skill.id.invalid"));
            Assert.That(codes, Does.Contain("item.id.missing"));
        }
        finally
        {
            Object.DestroyImmediate(firstCharacter);
            Object.DestroyImmediate(secondCharacter);
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(item);
        }
    }
}
#endif
