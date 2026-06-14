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
    public void ImporterCreatesAudioReferencesThroughResolver()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Features/Scenario/Source/test.scenario.yaml";
        AudioClip clip = AudioClip.Create("zev_phase2_clip", 1, 1, 44100, false);
        ScenarioSourceDocument document = MakeDocument();
        document.AudioClips.Add(new ScenarioSourceAudioDocument
        {
            AudioId = "zev_phase2",
            AudioClipId = "audio/zev_phase2"
        });

        var importer = new ScenarioSourceImporter(
            new FakeScenarioSourceParser(document),
            audioResolver: new FakeAudioReferenceResolver("audio/zev_phase2", clip));

        ScenarioSourceSyncResult result = importer.Import(sourceText, sourcePath);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Scenario.AudioClips.Count, Is.EqualTo(1));
        Assert.That(result.Scenario.AudioClips[0].AudioId, Is.EqualTo("zev_phase2"));
        Assert.That(result.Scenario.AudioClips[0].AudioClipId, Is.EqualTo("audio/zev_phase2"));
        Assert.That(result.Scenario.AudioClips[0].Clip, Is.SameAs(clip));

        DestroyScenario(result.Scenario);
        UnityEngine.Object.DestroyImmediate(clip);
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
    public void ExporterPreservesAudioClipIdsForScenarioSource()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Features/Scenario/Source/test.scenario.yaml");
        scenario.AudioClips.Add(new ScenarioAudioReferenceData
        {
            AudioId = "zev_phase2",
            AudioClipId = "audio/zev_phase2"
        });

        ScenarioSourceExportResult result = new ScenarioSourceExporter().Export(scenario);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Document.AudioClips.Count, Is.EqualTo(1));
        Assert.That(result.Document.AudioClips[0].AudioId, Is.EqualTo("zev_phase2"));
        Assert.That(result.Document.AudioClips[0].AudioClipId, Is.EqualTo("audio/zev_phase2"));

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
    public void ImporterReportsUnresolvedAudioReferences()
    {
        ScenarioSourceDocument document = MakeDocument();
        document.AudioClips.Add(new ScenarioSourceAudioDocument
        {
            AudioId = "zev_phase2",
            AudioClipId = "missing_audio"
        });
        var importer = new ScenarioSourceImporter(
            new FakeScenarioSourceParser(document),
            audioResolver: new FakeAudioReferenceResolver("other_audio", null));

        ScenarioSourceSyncResult result = importer.Import("id: test_battle\n", "Assets/test.scenario.yaml");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.Messages.Exists(message => message.Code == "scenario.audio.unresolved"), Is.True);
        DestroyScenario(result.Scenario);
    }

    [Test]
    public void YamlWriterSerializesReadableScenarioSource()
    {
        ScenarioSourceDocument document = MakeDocument();
        document.Dialogues.Add(new ScenarioSourceDialogueDocument
        {
            DialogueId = "zev.phase2",
            DialogueDataId = "dlg_zev_phase2"
        });
        document.AudioClips.Add(new ScenarioSourceAudioDocument
        {
            AudioId = "zev_phase2",
            AudioClipId = "bgm_zev_phase2"
        });
        document.Sequences[0].Actions.Add(new ScenarioActionData
        {
            ActionId = "bgm.crossfade",
            ParametersJson = "{\"clip\":\"zev_phase2\",\"duration\":0.8,\"label\":\"phase two\"}"
        });
        var parallel = new ScenarioActionData { ActionId = ActionDirector.ParallelActionId };
        parallel.Children.Add(new ScenarioActionData
        {
            ActionId = "battle.skill.timeline",
            ParametersJson = "{\"skill\":\"zev_crosscut\",\"actor\":\"zev\",\"targets\":[\"player\",\"ally\"]}"
        });
        document.Sequences[0].Actions.Add(parallel);

        ScenarioSourceYamlWriteResult result = new ScenarioSourceYamlWriter().Write(document);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Text, Does.Contain("title: \"테스트 전투\""));
        Assert.That(result.Text, Does.Contain("party: [player]"));
        Assert.That(result.Text, Does.Contain("dialogueData: dlg_zev_phase2"));
        Assert.That(result.Text, Does.Contain("audioClip: bgm_zev_phase2"));
        Assert.That(result.Text, Does.Contain("event: enemy.hp_crossed_below"));
        Assert.That(result.Text, Does.Contain("timing: after_current_skill"));
        Assert.That(result.Text, Does.Contain("- bgm.crossfade:"));
        Assert.That(result.Text, Does.Contain("duration: 0.8"));
        Assert.That(result.Text, Does.Contain("label: \"phase two\""));
        Assert.That(result.Text, Does.Not.Contain("label: \"\\\"phase two\\\"\""));
        Assert.That(result.Text, Does.Contain("- parallel:"));
        Assert.That(result.Text, Does.Contain("targets: [player, ally]"));
    }

    [Test]
    public void YamlWriterReportsInvalidActionParameterJson()
    {
        ScenarioSourceDocument document = MakeDocument();
        document.Sequences[0].Actions.Add(new ScenarioActionData
        {
            ActionId = "flow.wait",
            ParametersJson = "{not json"
        });

        ScenarioSourceYamlWriteResult result = new ScenarioSourceYamlWriter().Write(document);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.Messages.Exists(
            message => message.Code == "scenario.yaml.action.parameters.invalid"), Is.True);
    }

    [Test]
    public void YamlExportCommandWritesScenarioToTargetPath()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Features/Scenario/Source/test.scenario.yaml");
        var fileWriter = new FakeScenarioSourceTextFileWriter();
        var command = new ScenarioSourceYamlExportCommand(
            new ScenarioSourceExporter(),
            new ScenarioSourceYamlWriter(),
            fileWriter);

        ScenarioSourceYamlExportResult result = command.ExportToFile(
            scenario,
            "Assets/_Game/Features/Scenario/Source/exported.scenario.yaml");

        Assert.That(result.Success, Is.True);
        Assert.That(fileWriter.Path, Is.EqualTo("Assets/_Game/Features/Scenario/Source/exported.scenario.yaml"));
        Assert.That(fileWriter.Text, Does.Contain("id: test_battle"));
        Assert.That(fileWriter.Text, Does.Contain("sequences:"));
        DestroyScenario(scenario);
    }

    [Test]
    public void YamlExportCommandRequiresTargetPathBeforeWriting()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            string.Empty);
        var fileWriter = new FakeScenarioSourceTextFileWriter();
        var command = new ScenarioSourceYamlExportCommand(
            new ScenarioSourceExporter(),
            new ScenarioSourceYamlWriter(),
            fileWriter);

        ScenarioSourceYamlExportResult result = command.ExportToFile(scenario, " ");

        Assert.That(result.Success, Is.False);
        Assert.That(fileWriter.WriteCount, Is.EqualTo(0));
        Assert.That(result.Validation.Messages.Exists(
            message => message.Code == "scenario.yaml.export.path.required"), Is.True);
        DestroyScenario(scenario);
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

    private sealed class FakeAudioReferenceResolver : IScenarioAudioReferenceResolver
    {
        private readonly string _expectedAudioClipId;
        private readonly AudioClip _clip;

        public FakeAudioReferenceResolver(string expectedAudioClipId, AudioClip clip)
        {
            _expectedAudioClipId = expectedAudioClipId;
            _clip = clip;
        }

        public bool TryResolveAudioClip(string audioClipId, out AudioClip clip)
        {
            if (audioClipId == _expectedAudioClipId && _clip != null)
            {
                clip = _clip;
                return true;
            }

            clip = null;
            return false;
        }
    }

    private sealed class FakeScenarioSourceTextFileWriter : IScenarioSourceTextFileWriter
    {
        public string Path = string.Empty;
        public string Text = string.Empty;
        public int WriteCount;

        public void WriteAllText(string path, string text)
        {
            WriteCount++;
            Path = path;
            Text = text;
        }
    }
}
