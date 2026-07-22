#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ProjectContentIdentityValidationTests
{
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
