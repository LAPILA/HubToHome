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
    public void ImporterCreatesDialogueReferencesThroughResolver()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Features/Scenario/Source/test.scenario.yaml";
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        ScenarioSourceDocument document = MakeDocument();
        document.Dialogues.Add(new ScenarioSourceDialogueDocument
        {
            DialogueId = "zev.phase2",
            DialogueDataId = "dlg_zev_phase2"
        });

        var importer = new ScenarioSourceImporter(
            new FakeScenarioSourceParser(document),
            new FakeDialogueReferenceResolver("dlg_zev_phase2", dialogue));

        ScenarioSourceSyncResult result = importer.Import(sourceText, sourcePath);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Scenario.Dialogues.Count, Is.EqualTo(1));
        Assert.That(result.Scenario.Dialogues[0].DialogueId, Is.EqualTo("zev.phase2"));
        Assert.That(result.Scenario.Dialogues[0].DialogueDataId, Is.EqualTo("dlg_zev_phase2"));
        Assert.That(result.Scenario.Dialogues[0].Dialogue, Is.SameAs(dialogue));

        DestroyScenario(result.Scenario);
        UnityEngine.Object.DestroyImmediate(dialogue);
    }

    [Test]
    public void ExporterPreservesDialogueDataIdsForScenarioSource()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Features/Scenario/Source/test.scenario.yaml");
        scenario.Dialogues.Add(new ScenarioDialogueReferenceData
        {
            DialogueId = "zev.phase2",
            DialogueDataId = "dlg_zev_phase2"
        });

        ScenarioSourceExportResult result = new ScenarioSourceExporter().Export(scenario);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Document.Dialogues.Count, Is.EqualTo(1));
        Assert.That(result.Document.Dialogues[0].DialogueId, Is.EqualTo("zev.phase2"));
        Assert.That(result.Document.Dialogues[0].DialogueDataId, Is.EqualTo("dlg_zev_phase2"));

        DestroyScenario(scenario);
    }

    [Test]
    public void ExporterCanRecoverDialogueDataIdFromProvider()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Features/Scenario/Source/test.scenario.yaml");
        scenario.Dialogues.Add(new ScenarioDialogueReferenceData
        {
            DialogueId = "zev.phase2",
            Dialogue = dialogue
        });

        var exporter = new ScenarioSourceExporter(new FakeDialogueReferenceIdProvider(dialogue, "Assets/Dialogues/dlg_zev_phase2.asset"));

        ScenarioSourceExportResult result = exporter.Export(scenario);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Document.Dialogues[0].DialogueDataId, Is.EqualTo("Assets/Dialogues/dlg_zev_phase2.asset"));

        DestroyScenario(scenario);
        UnityEngine.Object.DestroyImmediate(dialogue);
    }

    [Test]
    public void ImporterReportsUnresolvedDialogueReferences()
    {
        ScenarioSourceDocument document = MakeDocument();
        document.Dialogues.Add(new ScenarioSourceDialogueDocument
        {
            DialogueId = "zev.phase2",
            DialogueDataId = "missing_dialogue"
        });
        var importer = new ScenarioSourceImporter(
            new FakeScenarioSourceParser(document),
            new FakeDialogueReferenceResolver("other_dialogue", null));

        ScenarioSourceSyncResult result = importer.Import("id: test_battle\n", "Assets/test.scenario.yaml");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.Messages.Exists(message => message.Code == "scenario.dialogue.unresolved"), Is.True);
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

    private sealed class FakeDialogueReferenceResolver : IScenarioDialogueReferenceResolver
    {
        private readonly string _expectedDialogueDataId;
        private readonly DialogueData _dialogue;

        public FakeDialogueReferenceResolver(string expectedDialogueDataId, DialogueData dialogue)
        {
            _expectedDialogueDataId = expectedDialogueDataId;
            _dialogue = dialogue;
        }

        public bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue)
        {
            if (dialogueDataId == _expectedDialogueDataId && _dialogue != null)
            {
                dialogue = _dialogue;
                return true;
            }

            dialogue = null;
            return false;
        }
    }

    private sealed class FakeDialogueReferenceIdProvider : IScenarioDialogueReferenceIdProvider
    {
        private readonly DialogueData _expectedDialogue;
        private readonly string _dialogueDataId;

        public FakeDialogueReferenceIdProvider(DialogueData expectedDialogue, string dialogueDataId)
        {
            _expectedDialogue = expectedDialogue;
            _dialogueDataId = dialogueDataId;
        }

        public bool TryGetDialogueDataId(DialogueData dialogue, out string dialogueDataId)
        {
            if (dialogue == _expectedDialogue)
            {
                dialogueDataId = _dialogueDataId;
                return true;
            }

            dialogueDataId = string.Empty;
            return false;
        }
    }
}
