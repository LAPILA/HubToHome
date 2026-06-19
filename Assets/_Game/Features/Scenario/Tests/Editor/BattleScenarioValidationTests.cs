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
