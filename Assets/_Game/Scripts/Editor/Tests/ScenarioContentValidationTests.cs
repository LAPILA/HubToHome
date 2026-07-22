#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ScenarioContentValidationTests
{
    [Test]
    public void ValidatorAllowsCanonicalPlayerAndReportsInvalidParticipantReferences()
    {
        CharacterData character = ScriptableObject.CreateInstance<CharacterData>();
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        try
        {
            character.CharacterID = "ally.known";
            enemy.EnemyId = "enemy.known";
            scenario.ScenarioId = "scenario.participants";
            scenario.PartyIds.Add("player");
            scenario.PartyIds.Add("ally.known");
            scenario.PartyIds.Add("ally.known");
            scenario.PartyIds.Add("ally.unknown");
            scenario.PartyIds.Add(string.Empty);
            scenario.EnemyIds.Add("enemy.known");
            scenario.EnemyIds.Add("enemy.known");
            scenario.EnemyIds.Add("enemy.unknown");

            var snapshot = new ProjectContentSnapshot();
            snapshot.Characters.Add(character);
            snapshot.Enemies.Add(enemy);
            snapshot.Scenarios.Add(scenario);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("scenario.party_id.missing"));
            Assert.That(codes, Does.Contain("scenario.party_id.duplicate"));
            Assert.That(codes, Does.Contain("scenario.party_id.unknown"));
            Assert.That(codes, Does.Contain("scenario.enemy_id.duplicate"));
            Assert.That(codes, Does.Contain("scenario.enemy_id.unknown"));
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("player") && issue.Code.EndsWith("unknown")), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(scenario);
        }
    }
}
#endif
