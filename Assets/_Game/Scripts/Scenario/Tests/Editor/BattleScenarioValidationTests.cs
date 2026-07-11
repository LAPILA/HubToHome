using NUnit.Framework;
using UnityEngine;

public class BattleScenarioValidationTests
{
    [Test]
    public void DialogueWaitWithUnregisteredDialogueIdProducesError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenario("zev.phase2_intro");

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.dialogue.unknown"), Is.True);
            Assert.That(result.Messages.Exists(message => message.Message.Contains("zev.phase2_intro")), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void DialogueWaitWithRegisteredDialogueIdPassesScenarioValidation()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        DialogueData dialogue = MakeDialogue();
        BattleScenarioData scenario = MakeScenario("zev.phase2_intro", dialogue);

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(dialogue);
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void DialogueWaitWithoutIdProducesRequiredIdError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = DialogueWaitActionAdapter.Id,
            ParametersJson = "{}"
        });

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.dialogue.id.required"), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void NestedDialogueWaitWithUnregisteredIdProducesError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = ActionDirector.ParallelActionId,
            Category = "flow",
            DisplayNameKo = "동시 실행",
            RuntimeAdapterId = "ActionDirector",
            ExampleYaml = "parallel:\n  - dialogue.wait:\n      id: zev.phase2_intro"
        });

        var parallel = new ScenarioActionData { ActionId = ActionDirector.ParallelActionId };
        parallel.Children.Add(new ScenarioActionData
        {
            ActionId = DialogueWaitActionAdapter.Id,
            ParametersJson = "{\"id\":\"zev.phase2_intro\"}"
        });

        BattleScenarioData scenario = MakeScenarioWithAction(parallel);

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.dialogue.unknown"), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithSkipIfMissingTrueAndMissingCatalogProducesWarning()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"skipIfMissing\":true}"
        });

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.timeline.catalog.missing" && message.Severity == ScenarioValidationSeverity.Warning), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithSkipIfMissingFalseAndMissingCatalogProducesError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"skipIfMissing\":false}"
        });

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.timeline.catalog.missing" && message.Severity == ScenarioValidationSeverity.Error), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithoutCutsceneIdAlwaysProducesError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"skipIfMissing\":true}"
        });

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.timeline.cutscene.required"), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithMissingTimelineAssetAndSkipIfMissingTrueProducesWarning()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        TimelineCutsceneData cutscene = ScriptableObject.CreateInstance<TimelineCutsceneData>();
        cutscene.CutsceneId = "zev_intro_clash";
        TimelineCutsceneCatalog timelineCatalog = ScriptableObject.CreateInstance<TimelineCutsceneCatalog>();
        timelineCatalog.Cutscenes.Add(cutscene);

        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"skipIfMissing\":true}"
        });
        scenario.TimelineCutsceneCatalog = timelineCatalog;

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.timeline.asset.missing" && message.Severity == ScenarioValidationSeverity.Warning), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(cutscene);
            Object.DestroyImmediate(timelineCatalog);
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithMissingTimelineAssetAndSkipIfMissingFalseProducesError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        TimelineCutsceneData cutscene = ScriptableObject.CreateInstance<TimelineCutsceneData>();
        cutscene.CutsceneId = "zev_intro_clash";
        TimelineCutsceneCatalog timelineCatalog = ScriptableObject.CreateInstance<TimelineCutsceneCatalog>();
        timelineCatalog.Cutscenes.Add(cutscene);

        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"skipIfMissing\":false}"
        });
        scenario.TimelineCutsceneCatalog = timelineCatalog;

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.timeline.asset.missing" && message.Severity == ScenarioValidationSeverity.Error), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(cutscene);
            Object.DestroyImmediate(timelineCatalog);
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TimelinePlayWithInvalidBoolParameterTypeProducesParameterTypeError()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"waitForComplete\":\"bad\"}"
        });

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Exists(message => message.Code == "scenario.action.parameter.type" && message.Message.Contains("waitForComplete")), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void LegacyPlayerAliasInActionProducesCanonicalPlayerWarning()
    {
        ActionCatalogAsset catalog = MakeCatalog();
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = BattleActorPoseActionAdapter.Id,
            ParametersJson = "{\"actor\":\"player_001\",\"pose\":\"parry\"}"
        });
        scenario.PartyIds.Clear();
        scenario.PartyIds.Add("player");

        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

            Assert.That(result.Messages.Exists(message => message.Code == "scenario.subject.player.alias.prefer_player" && message.Severity == ScenarioValidationSeverity.Warning), Is.True);
        }
        finally
        {
            DestroyScenario(scenario);
            Object.DestroyImmediate(catalog);
        }
    }

    private static ActionCatalogAsset MakeCatalog()
    {
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = DialogueWaitActionAdapter.Id,
            Category = "dialogue",
            DisplayNameKo = "대사 표시 후 대기",
            RuntimeAdapterId = "DialogueWaitActionAdapter",
            ExampleYaml = "dialogue.wait:\n  id: zev.phase2_intro"
        });
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = TimelinePlayActionAdapter.Id,
            Category = "timeline",
            DisplayNameKo = "타임라인 컷신 재생",
            RuntimeAdapterId = "TimelinePlayActionAdapter",
            ExampleYaml = "timeline.play:\n  cutsceneId: zev_intro_clash",
            Parameters =
            {
                new ActionCatalogParameter { Name = "cutsceneId", Type = "String", Required = true },
                new ActionCatalogParameter { Name = "waitForComplete", Type = "Bool" },
                new ActionCatalogParameter { Name = "lockInput", Type = "Bool" },
                new ActionCatalogParameter { Name = "restoreCamera", Type = "Bool" },
                new ActionCatalogParameter { Name = "skipIfMissing", Type = "Bool" }
            }
        });
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = BattleActorPoseActionAdapter.Id,
            Category = "battle",
            DisplayNameKo = "전투 액터 포즈",
            RuntimeAdapterId = "BattleActorPoseActionAdapter",
            ExampleYaml = "battle.actor.pose:\n  actor: player\n  pose: parry"
        });
        return catalog;
    }

    private static BattleScenarioData MakeScenario(string dialogueId, DialogueData registeredDialogue = null)
    {
        BattleScenarioData scenario = MakeScenarioWithAction(new ScenarioActionData
        {
            ActionId = DialogueWaitActionAdapter.Id,
            ParametersJson = "{\"id\":\"" + dialogueId + "\"}"
        });

        if (registeredDialogue != null)
        {
            scenario.Dialogues.Add(new ScenarioDialogueReferenceData
            {
                DialogueId = dialogueId,
                Dialogue = registeredDialogue
            });
        }

        return scenario;
    }

    private static BattleScenarioData MakeScenarioWithAction(ScenarioActionData action)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";

        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = "phase2";
        sequence.Actions.Add(action);

        scenario.Sequences.Add(sequence);
        return scenario;
    }

    private static DialogueData MakeDialogue()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "테스트 대사" });
        return dialogue;
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            Object.DestroyImmediate(scenario.Sequences[i]);
        }

        Object.DestroyImmediate(scenario);
    }
}
