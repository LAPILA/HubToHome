using System;
using NUnit.Framework;
using UnityEngine;

public class ScenarioSourceSyncTests
{
    [Test]
    public void MissingYamlParserProducesHelpfulError()
    {
        var parser = new MissingYamlScenarioSourceParser();

        ScenarioSourceParseResult result = parser.Parse("id: test", "Assets/_Game/Features/Scenario/Source/test.scenario.yaml");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.HasErrors, Is.True);
        Assert.That(result.Validation.Messages.Exists(message => message.Message.Contains("YAML parser")), Is.True);
    }

    [Test]
    public void ImporterCreatesBattleScenarioWithSourceMetadata()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Features/Scenario/Source/test.scenario.yaml";
        DateTime importedAt = new DateTime(2026, 6, 14, 1, 2, 3, DateTimeKind.Utc);

        var importer = new ScenarioSourceImporter(new FakeScenarioSourceParser(MakeDocument()));

        ScenarioSourceSyncResult result = importer.Import(sourceText, sourcePath, importedAt);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Scenario.ScenarioId, Is.EqualTo("test_battle"));
        Assert.That(result.Scenario.TitleKo, Is.EqualTo("테스트 전투"));
        Assert.That(result.Scenario.PartyIds, Is.EqualTo(new[] { "player" }));
        Assert.That(result.Scenario.EnemyIds, Is.EqualTo(new[] { "zev" }));
        Assert.That(result.Scenario.Rules.Count, Is.EqualTo(1));
        Assert.That(result.Scenario.Sequences.Count, Is.EqualTo(1));
        Assert.That(result.Scenario.Source.SourcePath, Is.EqualTo(sourcePath));
        Assert.That(result.Scenario.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(sourceText)));
        Assert.That(result.Scenario.Source.ImportedAtIso8601, Is.EqualTo(importedAt.ToString("O")));

        DestroyScenario(result.Scenario);
    }

    [Test]
    public void SourceHashDetectsStaleRuntimeAsset()
    {
        var metadata = new ScenarioSourceMetadata
        {
            SourceHash = ScenarioSourceHash.Compute("old")
        };

        Assert.That(ScenarioSourceHash.IsStale(metadata, "new"), Is.True);
        Assert.That(ScenarioSourceHash.IsStale(metadata, "old"), Is.False);
    }

    private static ScenarioSourceDocument MakeDocument()
    {
        var document = new ScenarioSourceDocument
        {
            Id = "test_battle",
            TitleKo = "테스트 전투",
            PrimaryMode = "battle",
            OpeningModule = "turn_qte",
            MemoryKey = "test"
        };

        document.PartyIds.Add("player");
        document.EnemyIds.Add("zev");
        document.Rules.Add(new ScenarioSourceRuleDocument
        {
            RuleId = "phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.PerEncounterMemory,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "phase2_intro"
        });
        document.Sequences.Add(new ScenarioSourceSequenceDocument
        {
            SequenceId = "phase2_intro",
            DisplayNameKo = "2페이즈 진입",
            Actions =
            {
                new ScenarioActionData { ActionId = "dialogue.wait", ParametersJson = "{\"id\":\"zev.phase2\"}" }
            }
        });

        return document;
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
        }

        UnityEngine.Object.DestroyImmediate(scenario);
    }

    private sealed class FakeScenarioSourceParser : IScenarioSourceParser
    {
        private readonly ScenarioSourceDocument _document;

        public FakeScenarioSourceParser(ScenarioSourceDocument document)
        {
            _document = document;
        }

        public ScenarioSourceParseResult Parse(string sourceText, string sourcePath)
        {
            return new ScenarioSourceParseResult
            {
                Document = _document
            };
        }
    }
}
