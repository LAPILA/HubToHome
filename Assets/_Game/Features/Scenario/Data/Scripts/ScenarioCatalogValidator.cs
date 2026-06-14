using System.Collections.Generic;

public static class ScenarioCatalogValidator
{
    private const string DialogueWaitActionId = "dialogue.wait";

    public static ScenarioValidationResult Validate(ActionCatalogAsset catalog)
    {
        var result = new ScenarioValidationResult();

        if (catalog == null)
        {
            result.AddError("catalog.missing", "Action Catalog is missing.");
            return result;
        }

        var seenIds = new HashSet<string>();
        if (catalog.Entries == null)
        {
            result.AddError("catalog.entries.missing", "Action Catalog entries list is missing.", catalog.CatalogId);
            return result;
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            ValidateEntry(catalog.Entries[i], i, seenIds, result);
        }

        return result;
    }

    public static ScenarioValidationResult ValidateBattleScenario(
        BattleScenarioData scenario,
        ActionCatalogAsset catalog)
    {
        ScenarioValidationResult result = Validate(catalog);

        if (scenario == null)
        {
            result.AddError("scenario.missing", "Battle Scenario Data is missing.");
            return result;
        }

        HashSet<string> knownIds = BuildKnownActionIds(catalog);
        var dialogueRegistry = new ScenarioDialogueRegistry(scenario.Dialogues);
        if (scenario.Sequences == null)
        {
            result.AddError(
                "scenario.sequences.missing",
                "Battle Scenario sequences list is missing.",
                scenario.ScenarioId);
            return result;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = scenario.Sequences[i];
            if (sequence == null)
            {
                result.AddError(
                    "scenario.sequence.null",
                    "Battle Scenario sequence is null.",
                    scenario.ScenarioId + ".sequences[" + i + "]");
                continue;
            }

            ValidateSequenceActions(sequence, knownIds, dialogueRegistry, result);
        }

        return result;
    }

    public static ScenarioValidationResult ValidateSequence(
        ActionSequenceAsset sequence,
        ActionCatalogAsset catalog)
    {
        ScenarioValidationResult result = Validate(catalog);

        if (sequence == null)
        {
            result.AddError("sequence.missing", "Action Sequence is missing.");
            return result;
        }

        HashSet<string> knownIds = BuildKnownActionIds(catalog);
        if (sequence.Actions == null)
        {
            result.AddError("sequence.actions.missing", "Action Sequence actions list is missing.", sequence.SequenceId);
            return result;
        }

        for (int i = 0; i < sequence.Actions.Count; i++)
        {
            ValidateAction(sequence.Actions[i], knownIds, null, result, sequence.SequenceId, i);
        }

        return result;
    }

    private static void ValidateEntry(
        ActionCatalogEntry entry,
        int index,
        HashSet<string> seenIds,
        ScenarioValidationResult result)
    {
        string objectId = "catalog.entries[" + index + "]";
        if (entry == null)
        {
            result.AddError("catalog.entry.null", "Action Catalog entry is null.", objectId);
            return;
        }

        string actionId = Trim(entry.ActionId);
        if (string.IsNullOrEmpty(actionId))
        {
            result.AddError("catalog.entry.action_id.required", "ActionId is required.", objectId);
        }
        else if (!seenIds.Add(actionId))
        {
            result.AddError("catalog.entry.action_id.duplicate", "Duplicate action id: " + actionId, actionId);
        }

        if (string.IsNullOrEmpty(Trim(entry.Category)))
        {
            result.AddError("catalog.entry.category.required", "Category is required.", objectId);
        }

        if (string.IsNullOrEmpty(Trim(entry.DisplayNameKo)))
        {
            result.AddError("catalog.entry.display_name_ko.required", "DisplayNameKo is required.", objectId);
        }

        if (string.IsNullOrEmpty(Trim(entry.RuntimeAdapterId)))
        {
            result.AddError("catalog.entry.runtime_adapter_id.required", "RuntimeAdapterId is required.", objectId);
        }

        if (string.IsNullOrEmpty(Trim(entry.ExampleYaml)))
        {
            result.AddError("catalog.entry.example_yaml.required", "ExampleYaml is required.", objectId);
        }
    }

    private static void ValidateAction(
        ScenarioActionData action,
        HashSet<string> knownIds,
        ScenarioDialogueRegistry dialogueRegistry,
        ScenarioValidationResult result,
        string sequenceId,
        int index)
    {
        string objectId = sequenceId + ".actions[" + index + "]";
        if (action == null)
        {
            result.AddError("sequence.action.null", "Scenario action is null.", objectId);
            return;
        }

        string actionId = Trim(action.ActionId);
        if (string.IsNullOrEmpty(actionId))
        {
            result.AddError("sequence.action.action_id.required", "ActionId is required.", objectId);
        }
        else if (!knownIds.Contains(actionId))
        {
            result.AddError("sequence.action.unknown", "Unknown action id: " + actionId, objectId);
        }
        else if (actionId == DialogueWaitActionId)
        {
            ValidateDialogueWaitAction(action, dialogueRegistry, result, objectId);
        }

        if (action.Children == null)
        {
            return;
        }

        for (int i = 0; i < action.Children.Count; i++)
        {
            ValidateAction(action.Children[i], knownIds, dialogueRegistry, result, objectId, i);
        }
    }

    private static void ValidateSequenceActions(
        ActionSequenceAsset sequence,
        HashSet<string> knownIds,
        ScenarioDialogueRegistry dialogueRegistry,
        ScenarioValidationResult result)
    {
        if (sequence.Actions == null)
        {
            result.AddError("sequence.actions.missing", "Action Sequence actions list is missing.", sequence.SequenceId);
            return;
        }

        for (int i = 0; i < sequence.Actions.Count; i++)
        {
            ValidateAction(sequence.Actions[i], knownIds, dialogueRegistry, result, sequence.SequenceId, i);
        }
    }

    private static void ValidateDialogueWaitAction(
        ScenarioActionData action,
        ScenarioDialogueRegistry dialogueRegistry,
        ScenarioValidationResult result,
        string objectId)
    {
        string dialogueId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "id", out dialogueId, out error))
        {
            result.AddError("scenario.action.parameters.invalid", error, objectId);
            return;
        }

        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            result.AddError("scenario.dialogue.id.required", "dialogue.wait requires parameter 'id'.", objectId);
            return;
        }

        DialogueData dialogue;
        if (dialogueRegistry != null && !dialogueRegistry.TryResolve(dialogueId, out dialogue))
        {
            result.AddError("scenario.dialogue.unknown", "Unknown dialogue id: " + dialogueId.Trim(), objectId);
        }
    }

    private static HashSet<string> BuildKnownActionIds(ActionCatalogAsset catalog)
    {
        var ids = new HashSet<string>();
        if (catalog == null || catalog.Entries == null)
        {
            return ids;
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            ActionCatalogEntry entry = catalog.Entries[i];
            if (entry == null || entry.Disabled)
            {
                continue;
            }

            string actionId = Trim(entry.ActionId);
            if (!string.IsNullOrEmpty(actionId))
            {
                ids.Add(actionId);
            }
        }

        return ids;
    }

    private static string Trim(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
