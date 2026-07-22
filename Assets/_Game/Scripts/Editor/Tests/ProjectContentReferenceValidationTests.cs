#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ProjectContentReferenceValidationTests
{
    [Test]
    public void ValidatorReportsInvalidBattlePrefabsAndSkillsOutsideProjectContent()
    {
        CharacterData character = ScriptableObject.CreateInstance<CharacterData>();
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        SkillData externalSkill = ScriptableObject.CreateInstance<SkillData>();
        var wrongCharacterPrefab = new GameObject("WrongCharacterPrefab");
        try
        {
            character.CharacterID = "player.test";
            character.BattlePrefab = wrongCharacterPrefab;
            character.DefaultSkills.Add(externalSkill);
            enemy.EnemyId = "enemy.test";
            enemy.BattlePrefab = null;
            enemy.StrongSkillList.Add(externalSkill);

            var snapshot = new ProjectContentSnapshot();
            snapshot.Characters.Add(character);
            snapshot.Enemies.Add(enemy);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("character.battle_prefab.component_missing"));
            Assert.That(codes, Does.Contain("character.default_skill.unknown"));
            Assert.That(codes, Does.Contain("enemy.battle_prefab.missing"));
            Assert.That(codes, Does.Contain("enemy.strong_skill.unknown"));
        }
        finally
        {
            Object.DestroyImmediate(wrongCharacterPrefab);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(externalSkill);
        }
    }
}
#endif
