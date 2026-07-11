using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ScenarioSourceSyncTests
{
    [Test]
    public void MissingYamlParserProducesHelpfulError()
    {
        var parser = new MissingYamlScenarioSourceParser();

        ScenarioSourceParseResult result = parser.Parse("id: test", "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.HasErrors, Is.True);
        Assert.That(result.Validation.Messages.Exists(message => message.Message.Contains("YAML parser")), Is.True);
    }

    [Test]
    public void ImporterCreatesBattleScenarioWithSourceMetadata()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml";
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
    public void ImporterPreservesGameModuleOutcomeRule()
    {
        var importer = new ScenarioSourceImporter(new FakeScenarioSourceParser(MakeModuleOutcomeDocument()));

        ScenarioSourceSyncResult result = importer.Import(
            "id: module_outcome\n",
            "Assets/_Game/Content/Scenarios/Source/module_outcome.scenario.yaml");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Scenario.Rules.Count, Is.EqualTo(1));
        Assert.That(result.Scenario.Rules[0].EventType, Is.EqualTo(BattleEventType.GameModuleCompleted));
        Assert.That(result.Scenario.Rules[0].SubjectId, Is.EqualTo("aim_shooter"));
        Assert.That(result.Scenario.Rules[0].OutcomeId, Is.EqualTo("victory"));
        Assert.That(result.Scenario.Rules[0].Timing, Is.EqualTo(BattleRuleTiming.AfterCurrentModule));

        DestroyScenario(result.Scenario);
    }

    [Test]
    public void ImporterCreatesDialogueReferencesThroughResolver()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml";
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
        const string sourcePath = "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml";
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
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");
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
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");
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
    public void ExporterPreservesGameModuleOutcomeRule()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeModuleOutcomeDocument(),
            "id: module_outcome\n",
            "Assets/_Game/Content/Scenarios/Source/module_outcome.scenario.yaml");

        ScenarioSourceExportResult result = new ScenarioSourceExporter().Export(scenario);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Document.Rules.Count, Is.EqualTo(1));
        Assert.That(result.Document.Rules[0].EventType, Is.EqualTo(BattleEventType.GameModuleCompleted));
        Assert.That(result.Document.Rules[0].SubjectId, Is.EqualTo("aim_shooter"));
        Assert.That(result.Document.Rules[0].OutcomeId, Is.EqualTo("victory"));
        Assert.That(result.Document.Rules[0].SequenceId, Is.EqualTo("after_shooter_victory"));

        DestroyScenario(scenario);
    }

    [Test]
    public void ExporterCanRecoverDialogueDataIdFromProvider()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");
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
    public void YamlWriterSerializesGameModuleOutcomeRule()
    {
        ScenarioSourceDocument document = MakeModuleOutcomeDocument();

        ScenarioSourceYamlWriteResult result = new ScenarioSourceYamlWriter().Write(document);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Text, Does.Contain("event: module.completed"));
        Assert.That(result.Text, Does.Contain("module: aim_shooter"));
        Assert.That(result.Text, Does.Contain("outcome: victory"));
        Assert.That(result.Text, Does.Contain("timing: after_current_module"));
        Assert.That(result.Text, Does.Not.Contain("enemy: aim_shooter"));
    }

    [Test]
    public void YamlParserRoundTripsWriterOutputIntoBattleScenario()
    {
        ScenarioSourceDocument document = MakeModuleOutcomeDocument();
        document.Sequences[0].Actions.Insert(0, new ScenarioActionData
        {
            ActionId = ModuleSwitchActionAdapter.Id,
            ParametersJson = "{\"to\":\"dummy_shooter\"}"
        });
        document.Sequences[0].Actions.Insert(1, new ScenarioActionData
        {
            ActionId = ModuleStartActionAdapter.Id,
            ParametersJson = "{\"module\":\"dummy_shooter\"}"
        });
        var parallel = new ScenarioActionData { ActionId = ActionDirector.ParallelActionId };
        parallel.Children.Add(new ScenarioActionData
        {
            ActionId = "battle.flag.set",
            ParametersJson = "{\"flag\":\"phase.two\",\"value\":\"entered\"}"
        });
        parallel.Children.Add(new ScenarioActionData
        {
            ActionId = "flow.wait",
            ParametersJson = "{\"duration\":0.25}",
            Disabled = true
        });
        document.Sequences[0].Actions.Add(parallel);

        ScenarioSourceYamlWriteResult writeResult = new ScenarioSourceYamlWriter().Write(document);
        var importer = new ScenarioSourceImporter(new ScenarioSourceYamlParser());

        ScenarioSourceSyncResult importResult = importer.Import(
            writeResult.Text,
            "Assets/_Game/Content/Scenarios/Source/module_outcome.scenario.yaml",
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(writeResult.Success, Is.True);
        Assert.That(importResult.Success, Is.True);
        Assert.That(importResult.Scenario.ScenarioId, Is.EqualTo("module_outcome"));
        Assert.That(importResult.Scenario.Rules[0].EventType, Is.EqualTo(BattleEventType.GameModuleCompleted));
        Assert.That(importResult.Scenario.Rules[0].SubjectId, Is.EqualTo("aim_shooter"));
        Assert.That(importResult.Scenario.Rules[0].OutcomeId, Is.EqualTo("victory"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[0].ActionId, Is.EqualTo(ModuleSwitchActionAdapter.Id));
        Assert.That(importResult.Scenario.Sequences[0].Actions[0].ParametersJson, Is.EqualTo("{\"to\":\"dummy_shooter\"}"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[1].ActionId, Is.EqualTo(ModuleStartActionAdapter.Id));
        Assert.That(importResult.Scenario.Sequences[0].Actions[1].ParametersJson, Is.EqualTo("{\"module\":\"dummy_shooter\"}"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[3].ActionId, Is.EqualTo(ActionDirector.ParallelActionId));
        Assert.That(importResult.Scenario.Sequences[0].Actions[3].Children.Count, Is.EqualTo(2));
        Assert.That(importResult.Scenario.Sequences[0].Actions[3].Children[1].Disabled, Is.True);

        DestroyScenario(importResult.Scenario);
    }

    [Test]
    public void YamlParserRoundTripsWriterOutputPreservingDesignerLabelNoteDisabledAndChildren()
    {
        ScenarioSourceDocument document = MakeDocument();
        document.Sequences[0].Actions.Clear();
        document.Sequences[0].Actions.Add(new ScenarioActionData
        {
            DesignerLabel = "인트로 대사",
            ActionId = "dialogue.wait",
            ParametersJson = "{\"id\":\"zev.phase2\"}",
            Note = "첫 대사",
            Disabled = true
        });

        var parallel = new ScenarioActionData { ActionId = ActionDirector.ParallelActionId };
        parallel.Children.Add(new ScenarioActionData
        {
            DesignerLabel = "플래그 설정",
            ActionId = "battle.flag.set",
            ParametersJson = "{\"flag\":\"phase.two\",\"value\":\"entered\"}",
            Note = "자식 메모"
        });
        document.Sequences[0].Actions.Add(parallel);

        ScenarioSourceYamlWriteResult writeResult = new ScenarioSourceYamlWriter().Write(document);
        var importer = new ScenarioSourceImporter(new ScenarioSourceYamlParser());

        ScenarioSourceSyncResult importResult = importer.Import(
            writeResult.Text,
            "Assets/_Game/Content/Scenarios/Source/metadata_roundtrip.scenario.yaml",
            new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(writeResult.Success, Is.True);
        Assert.That(importResult.Success, Is.True);
        Assert.That(importResult.Scenario.Sequences[0].Actions[0].DesignerLabel, Is.EqualTo("인트로 대사"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[0].Note, Is.EqualTo("첫 대사"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[0].Disabled, Is.True);
        Assert.That(importResult.Scenario.Sequences[0].Actions[1].Children.Count, Is.EqualTo(1));
        Assert.That(importResult.Scenario.Sequences[0].Actions[1].Children[0].DesignerLabel, Is.EqualTo("플래그 설정"));
        Assert.That(importResult.Scenario.Sequences[0].Actions[1].Children[0].Note, Is.EqualTo("자식 메모"));

        DestroyScenario(importResult.Scenario);
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
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");
        var fileWriter = new FakeScenarioSourceTextFileWriter();
        var command = new ScenarioSourceYamlExportCommand(
            new ScenarioSourceExporter(),
            new ScenarioSourceYamlWriter(),
            fileWriter);

        ScenarioSourceYamlExportResult result = command.ExportToFile(
            scenario,
            "Assets/_Game/Content/Scenarios/Source/exported.scenario.yaml");

        Assert.That(result.Success, Is.True);
        Assert.That(fileWriter.Path, Is.EqualTo("Assets/_Game/Content/Scenarios/Source/exported.scenario.yaml"));
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

    [Test]
    public void ScenarioAuthoringCatalogViewBuildsKoreanPickerLabels()
    {
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = "dialogue.wait",
            DisplayNameKo = "대사 재생",
            Category = "dialogue",
            RuntimeAdapterId = "dialogue.wait",
            ExampleYaml = "- dialogue.wait:\n    id: zev.intro"
        });
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = "disabled.action",
            DisplayNameKo = "비활성 액션",
            Disabled = true
        });

        List<string> labels = ScenarioAuthoringCatalogView.BuildActionPickerLabels(catalog);

        Assert.That(labels, Is.EqualTo(new[] { "대사 재생 (dialogue.wait)" }));
        Assert.That(
            ScenarioAuthoringCatalogView.ResolveActionIdFromPickerLabel(labels[0]),
            Is.EqualTo("dialogue.wait"));

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ScenarioAuthoringCatalogViewReturnsMostSevereMessageForActionRow()
    {
        var validation = new ScenarioValidationResult();
        validation.AddWarning("scenario.warning", "warning", "phase2.actions[0]");
        validation.AddError("scenario.error", "error", "phase2.actions[0]");

        ScenarioValidationMessage message = ScenarioAuthoringCatalogView.FindMessageForObject(
            validation,
            "phase2.actions[0]");

        Assert.That(message.Code, Is.EqualTo("scenario.error"));
        Assert.That(message.Severity, Is.EqualTo(ScenarioValidationSeverity.Error));
    }

    [Test]
    public void ScenarioAuthoringParameterViewCombinesCatalogAndJsonKeys()
    {
        var action = new ScenarioActionData
        {
            ParametersJson = "{\"id\":\"zev.intro\",\"duration\":0.5}"
        };
        var entry = new ActionCatalogEntry();
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "id",
            Type = "String",
            Required = true
        });
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "mode",
            Type = "String"
        });

        List<string> names = ScenarioAuthoringParameterView.GetParameterNames(action, entry);

        Assert.That(names, Is.EqualTo(new[] { "id", "mode", "duration" }));
    }

    [Test]
    public void ScenarioAuthoringParameterViewSetsTypedValues()
    {
        var action = new ScenarioActionData
        {
            ParametersJson = "{\"id\":\"zev.intro\"}"
        };
        string error;

        bool floatResult = ScenarioAuthoringParameterView.SetParameterValue(
            action,
            "duration",
            "0.75",
            new ActionCatalogParameter { Name = "duration", Type = "Float" },
            out error);
        bool boolResult = ScenarioAuthoringParameterView.SetParameterValue(
            action,
            "enabled",
            "true",
            new ActionCatalogParameter { Name = "enabled", Type = "Bool" },
            out error);
        bool intResult = ScenarioAuthoringParameterView.SetParameterValue(
            action,
            "amount",
            "12",
            new ActionCatalogParameter { Name = "amount", Type = "Integer" },
            out error);

        Assert.That(floatResult, Is.True);
        Assert.That(boolResult, Is.True);
        Assert.That(intResult, Is.True);
        Assert.That(action.ParametersJson, Does.Contain("\"duration\":0.75"));
        Assert.That(action.ParametersJson, Does.Contain("\"enabled\":true"));
        Assert.That(action.ParametersJson, Does.Contain("\"amount\":12"));
        Assert.That(ScenarioAuthoringParameterView.GetParameterValue(action, "id"), Is.EqualTo("zev.intro"));
    }

    [Test]
    public void ScenarioAuthoringParameterViewRepairsInvalidJsonWhenSettingParameter()
    {
        var action = new ScenarioActionData
        {
            ParametersJson = "{not json"
        };
        string error;

        bool result = ScenarioAuthoringParameterView.SetParameterValue(
            action,
            "clip",
            "zev_phase2",
            new ActionCatalogParameter { Name = "clip", Type = "String" },
            out error);

        Assert.That(result, Is.True);
        Assert.That(action.ParametersJson, Is.EqualTo("{\"clip\":\"zev_phase2\"}"));
    }

    [Test]
    public void ScenarioAuthoringParameterViewCreatesDefaultJsonFromCatalogParameters()
    {
        var entry = new ActionCatalogEntry();
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "module",
            Type = "String",
            DefaultValue = "aim_shooter"
        });
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "duration",
            Type = "Float",
            DefaultValue = "0.25"
        });

        string json = ScenarioAuthoringParameterView.CreateDefaultParameterJson(entry);

        Assert.That(json, Is.EqualTo("{\"module\":\"aim_shooter\",\"duration\":0.25}"));
    }

    [Test]
    public void ScenarioActionChildrenUseManagedReferencesToAvoidUnitySerializationDepthErrors()
    {
        FieldInfo childrenField = typeof(ScenarioActionData).GetField(nameof(ScenarioActionData.Children));

        Assert.That(childrenField, Is.Not.Null);
        Assert.That(
            childrenField.GetCustomAttribute<SerializeReference>() != null,
            Is.True,
            "ScenarioActionData.Children is recursive and must stay SerializeReference-backed so Unity does not value-serialize the type until its depth limit.");
    }

    [Test]
    public void ScenarioSourceMetadataEditorSyncUpdatesScenarioAndSequencesAfterSourceSave()
    {
        BattleScenarioData scenario = ScenarioSourceImporter.CreateBattleScenario(
            MakeDocument(),
            "id: test_battle\n",
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml");
        var result = new ScenarioSourceYamlExportResult
        {
            Text = "id: test_battle\nsequences:\n",
            TargetPath = "Assets/_Game/Content/Scenarios/Source/test_saved.scenario.yaml"
        };
        DateTime writtenAt = new DateTime(2026, 6, 16, 1, 2, 3, DateTimeKind.Utc);

        bool changed = ScenarioSourceMetadataEditorSync.ApplyExportResult(scenario, result, writtenAt);

        Assert.That(changed, Is.True);
        Assert.That(scenario.Source.SourcePath, Is.EqualTo(result.TargetPath));
        Assert.That(scenario.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(result.Text)));
        Assert.That(scenario.Source.ImportedAtIso8601, Is.EqualTo(writtenAt.ToString("O")));
        Assert.That(scenario.Sequences[0].Source.SourcePath, Is.EqualTo(result.TargetPath));
        Assert.That(scenario.Sequences[0].Source.SourceHash, Is.EqualTo(scenario.Source.SourceHash));

        DestroyScenario(scenario);
    }

    [Test]
    public void RuntimeAssetReimportReusesMatchingSequenceAsset()
    {
        const string sourceText = "id: test_battle\n";
        const string sourcePath = "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml";
        DateTime importedAt = new DateTime(2026, 6, 16, 2, 3, 4, DateTimeKind.Utc);
        BattleScenarioData target = ScriptableObject.CreateInstance<BattleScenarioData>();
        target.ScenarioId = "old_scenario";
        target.TitleKo = "이전 전투";
        target.PartyIds.Add("old_party");
        ActionSequenceAsset existingSequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        existingSequence.SequenceId = "phase2_intro";
        existingSequence.Actions.Add(new ScenarioActionData
        {
            ActionId = "flow.wait",
            ParametersJson = "{\"duration\":99}"
        });
        target.Sequences.Add(existingSequence);

        var command = new ScenarioSourceRuntimeAssetReimportCommand(new FakeScenarioSourceParser(MakeDocument()));

        ScenarioSourceRuntimeAssetReimportResult result = command.ReimportFromText(
            target,
            sourceText,
            sourcePath,
            importedAtUtc: importedAt);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ReusedSequenceCount, Is.EqualTo(1));
        Assert.That(result.CreatedSequenceCount, Is.EqualTo(0));
        Assert.That(result.DetachedSequenceCount, Is.EqualTo(0));
        Assert.That(target.ScenarioId, Is.EqualTo("test_battle"));
        Assert.That(target.TitleKo, Is.EqualTo("테스트 전투"));
        Assert.That(target.PartyIds, Is.EqualTo(new[] { "player" }));
        Assert.That(target.Sequences.Count, Is.EqualTo(1));
        Assert.That(target.Sequences[0], Is.SameAs(existingSequence));
        Assert.That(existingSequence.DisplayNameKo, Is.EqualTo("2페이즈 진입"));
        Assert.That(existingSequence.Actions[0].ActionId, Is.EqualTo("dialogue.wait"));
        Assert.That(target.Source.SourcePath, Is.EqualTo(sourcePath));
        Assert.That(target.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(sourceText)));
        Assert.That(target.Source.ImportedAtIso8601, Is.EqualTo(importedAt.ToString("O")));
        Assert.That(existingSequence.Source.SourceHash, Is.EqualTo(target.Source.SourceHash));

        DestroyScenario(target);
    }

    [Test]
    public void RuntimeAssetReimportDoesNotMutateTargetWhenCatalogValidationFails()
    {
        BattleScenarioData target = ScriptableObject.CreateInstance<BattleScenarioData>();
        target.ScenarioId = "old_scenario";
        target.TitleKo = "이전 전투";
        ActionSequenceAsset existingSequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        existingSequence.SequenceId = "old_sequence";
        existingSequence.Actions.Add(new ScenarioActionData
        {
            ActionId = "flow.wait",
            ParametersJson = "{\"duration\":1}"
        });
        target.Sequences.Add(existingSequence);
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = "flow.wait",
            Category = "flow",
            DisplayNameKo = "기다리기",
            RuntimeAdapterId = "flow.wait",
            ExampleYaml = "- flow.wait:\n    duration: 0.5"
        });

        var command = new ScenarioSourceRuntimeAssetReimportCommand(new FakeScenarioSourceParser(MakeDocument()));

        ScenarioSourceRuntimeAssetReimportResult result = command.ReimportFromText(
            target,
            "id: test_battle\n",
            "Assets/_Game/Content/Scenarios/Source/test.scenario.yaml",
            catalog);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.Messages.Exists(message => message.Code == "sequence.action.unknown"), Is.True);
        Assert.That(target.ScenarioId, Is.EqualTo("old_scenario"));
        Assert.That(target.TitleKo, Is.EqualTo("이전 전투"));
        Assert.That(target.Sequences.Count, Is.EqualTo(1));
        Assert.That(target.Sequences[0], Is.SameAs(existingSequence));
        Assert.That(existingSequence.SequenceId, Is.EqualTo("old_sequence"));
        Assert.That(existingSequence.Actions[0].ActionId, Is.EqualTo("flow.wait"));
        Assert.That(existingSequence.Actions[0].ParametersJson, Is.EqualTo("{\"duration\":1}"));

        DestroyScenario(target);
        UnityEngine.Object.DestroyImmediate(catalog);
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

    private static ScenarioSourceDocument MakeModuleOutcomeDocument()
    {
        var document = new ScenarioSourceDocument
        {
            Id = "module_outcome",
            TitleKo = "모듈 결과 테스트",
            PrimaryMode = "battle",
            OpeningModule = "turn_qte",
            MemoryKey = "module_outcome"
        };

        document.PartyIds.Add("player");
        document.EnemyIds.Add("zev");
        document.Rules.Add(new ScenarioSourceRuleDocument
        {
            RuleId = "after_shooter_victory",
            EventType = BattleEventType.GameModuleCompleted,
            Timing = BattleRuleTiming.AfterCurrentModule,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "aim_shooter",
            OutcomeId = "victory",
            SequenceId = "after_shooter_victory"
        });
        document.Sequences.Add(new ScenarioSourceSequenceDocument
        {
            SequenceId = "after_shooter_victory",
            DisplayNameKo = "슈팅 승리 후 연출",
            Actions =
            {
                new ScenarioActionData { ActionId = "dialogue.wait", ParametersJson = "{\"id\":\"zev.shooter_victory\"}" }
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
