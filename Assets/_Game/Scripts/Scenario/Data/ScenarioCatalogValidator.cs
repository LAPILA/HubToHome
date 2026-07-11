using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class ScenarioCatalogValidator
{
    private const string DialogueWaitActionId = "dialogue.wait";
    private const string TimelinePlayActionId = "timeline.play";

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

        Dictionary<string, ActionCatalogEntry> entryMap = BuildActionEntryMap(catalog);
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

            ValidateSequenceActions(sequence, entryMap, dialogueRegistry, scenario, scenario.TimelineCutsceneCatalog, result);
        }

        result.Merge(SequenceCallGraphValidator.Validate(scenario.Sequences));

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

        Dictionary<string, ActionCatalogEntry> entryMap = BuildActionEntryMap(catalog);
        if (sequence.Actions == null)
        {
            result.AddError("sequence.actions.missing", "Action Sequence actions list is missing.", sequence.SequenceId);
            return result;
        }

        for (int i = 0; i < sequence.Actions.Count; i++)
        {
            ValidateAction(sequence.Actions[i], entryMap, null, null, null, result, sequence.SequenceId, i);
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
        Dictionary<string, ActionCatalogEntry> entryMap,
        ScenarioDialogueRegistry dialogueRegistry,
        BattleScenarioData scenario,
        TimelineCutsceneCatalog timelineCatalog,
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
        ActionCatalogEntry entry = FindEntry(entryMap, actionId);
        if (string.IsNullOrEmpty(actionId))
        {
            result.AddError("sequence.action.action_id.required", "ActionId is required.", objectId);
        }
        else if (entry == null)
        {
            result.AddError("sequence.action.unknown", "Unknown action id: " + actionId, objectId);
        }
        else
        {
            ValidateCatalogParameters(action, entry, result, objectId);

            if (actionId == DialogueWaitActionId)
            {
                ValidateDialogueWaitAction(action, dialogueRegistry, result, objectId);
            }
            else if (actionId == TimelinePlayActionId)
            {
                ValidateTimelinePlayAction(action, timelineCatalog, result, objectId);
            }

            ValidateCanonicalSubjectIds(action, scenario, result, objectId);
        }

        if (action.Children == null)
        {
            return;
        }

        for (int i = 0; i < action.Children.Count; i++)
        {
            ValidateAction(action.Children[i], entryMap, dialogueRegistry, scenario, timelineCatalog, result, objectId, i);
        }
    }

    private static void ValidateSequenceActions(
        ActionSequenceAsset sequence,
        Dictionary<string, ActionCatalogEntry> entryMap,
        ScenarioDialogueRegistry dialogueRegistry,
        BattleScenarioData scenario,
        TimelineCutsceneCatalog timelineCatalog,
        ScenarioValidationResult result)
    {
        if (sequence.Actions == null)
        {
            result.AddError("sequence.actions.missing", "Action Sequence actions list is missing.", sequence.SequenceId);
            return;
        }

        for (int i = 0; i < sequence.Actions.Count; i++)
        {
            ValidateAction(sequence.Actions[i], entryMap, dialogueRegistry, scenario, timelineCatalog, result, sequence.SequenceId, i);
        }
    }

    private static void ValidateCatalogParameters(
        ScenarioActionData action,
        ActionCatalogEntry entry,
        ScenarioValidationResult result,
        string objectId)
    {
        if (entry == null || entry.Parameters == null || entry.Parameters.Count == 0)
        {
            return;
        }

        JObject root;
        string error;
        if (!TryParseParameters(action, out root, out error))
        {
            result.AddError("scenario.action.parameters.invalid", error, objectId);
            return;
        }

        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            string parameterName = parameter.Name.Trim();
            JToken token = null;
            bool hasToken = root != null && root.TryGetValue(parameterName, out token) && token != null && token.Type != JTokenType.Null;
            if (!hasToken)
            {
                if (parameter.Required)
                {
                    result.AddError(
                        "scenario.action.parameter.required",
                        "Required parameter is missing: " + parameterName,
                        objectId);
                }

                continue;
            }

            if (token.Type == JTokenType.String && string.IsNullOrWhiteSpace(token.Value<string>()) && parameter.Required)
            {
                result.AddError(
                    "scenario.action.parameter.required",
                    "Required parameter is blank: " + parameterName,
                    objectId);
                continue;
            }

            if (ScenarioValueBinding.HasMarker(token))
            {
                if (!ScenarioValueBinding.TryRead(token, out _, out string bindingError))
                {
                    result.AddError("scenario.action.parameter.binding", bindingError, objectId);
                }

                continue;
            }

            string typeHint = Trim(parameter.Type).ToLowerInvariant();
            if (string.IsNullOrEmpty(typeHint))
            {
                continue;
            }

            if (typeHint.Contains("bool") && token.Type != JTokenType.Boolean)
            {
                result.AddError("scenario.action.parameter.type", "Parameter must be a boolean: " + parameterName, objectId);
            }
            else if (typeHint.Contains("int") && token.Type != JTokenType.Integer)
            {
                result.AddError("scenario.action.parameter.type", "Parameter must be an integer: " + parameterName, objectId);
            }
            else if ((typeHint.Contains("float") || typeHint.Contains("number"))
                && token.Type != JTokenType.Integer
                && token.Type != JTokenType.Float)
            {
                result.AddError("scenario.action.parameter.type", "Parameter must be a number: " + parameterName, objectId);
            }
            else if (typeHint.Contains("[]"))
            {
                if (token.Type != JTokenType.Array && token.Type != JTokenType.String)
                {
                    result.AddError("scenario.action.parameter.type", "Parameter must be a string or string array: " + parameterName, objectId);
                }
            }
            else if ((typeHint.Contains("string") || typeHint.Contains("id")) && token.Type != JTokenType.String)
            {
                result.AddError("scenario.action.parameter.type", "Parameter must be a string: " + parameterName, objectId);
            }
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

    private static void ValidateTimelinePlayAction(
        ScenarioActionData action,
        TimelineCutsceneCatalog timelineCatalog,
        ScenarioValidationResult result,
        string objectId)
    {
        string cutsceneId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "cutsceneId", out cutsceneId, out error))
        {
            result.AddError("scenario.action.parameters.invalid", error, objectId);
            return;
        }

        if (string.IsNullOrWhiteSpace(cutsceneId))
        {
            result.AddError("scenario.timeline.cutscene.required", "timeline.play requires parameter 'cutsceneId'.", objectId);
            return;
        }

        bool skipIfMissing;
        if (!ScenarioActionParameterReader.TryGetBool(action, "skipIfMissing", false, out skipIfMissing, out error))
        {
            result.AddError("scenario.action.parameters.invalid", error, objectId);
            return;
        }

        if (timelineCatalog == null)
        {
            AddTimelineValidation(
                result,
                skipIfMissing,
                "scenario.timeline.catalog.missing",
                "timeline.play requires a TimelineCutsceneCatalog on BattleScenarioData.",
                objectId);
            return;
        }

        TimelineCutsceneData cutscene = timelineCatalog.FindById(cutsceneId.Trim());
        if (cutscene == null)
        {
            AddTimelineValidation(
                result,
                skipIfMissing,
                "scenario.timeline.cutscene.unknown",
                "Unknown timeline cutscene id: " + cutsceneId.Trim(),
                objectId);
            return;
        }

        if (cutscene.TimelineAsset == null)
        {
            AddTimelineValidation(
                result,
                skipIfMissing,
                "scenario.timeline.asset.missing",
                "Timeline cutscene is missing TimelineAsset: " + cutsceneId.Trim(),
                objectId);
        }

        ValidateTimelineBindings(cutscene, result, objectId, skipIfMissing);
    }

    private static void ValidateTimelineBindings(
        TimelineCutsceneData cutscene,
        ScenarioValidationResult result,
        string objectId,
        bool skipIfMissing)
    {
        ValidateTimelineBindingList(cutscene != null ? cutscene.OutputBindings : null, "outputBindings", result, objectId, skipIfMissing);
        ValidateTimelineBindingList(cutscene != null ? cutscene.ReferenceBindings : null, "referenceBindings", result, objectId, skipIfMissing);
    }

    private static void ValidateTimelineBindingList(
        List<TimelineCutsceneBindingEntry> bindings,
        string listName,
        ScenarioValidationResult result,
        string objectId,
        bool skipIfMissing)
    {
        if (bindings == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            TimelineCutsceneBindingEntry binding = bindings[i];
            string bindingObjectId = objectId + "." + listName + "[" + i + "]";
            if (binding == null)
            {
                AddTimelineValidation(result, skipIfMissing, "scenario.timeline.binding.null", "Timeline binding entry is null.", bindingObjectId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.BindingName))
            {
                AddTimelineValidation(result, skipIfMissing, "scenario.timeline.binding_name.required", "Timeline binding entry requires BindingName.", bindingObjectId);
            }

            if (string.IsNullOrWhiteSpace(binding.Key))
            {
                AddTimelineValidation(result, skipIfMissing, "scenario.timeline.binding_key.required", "Timeline binding entry requires Key.", bindingObjectId);
            }
        }
    }

    private static void AddTimelineValidation(
        ScenarioValidationResult result,
        bool skipIfMissing,
        string code,
        string message,
        string objectId)
    {
        if (skipIfMissing)
        {
            result.AddWarning(code, message, objectId);
            return;
        }

        result.AddError(code, message, objectId);
    }

    private static void ValidateCanonicalSubjectIds(
        ScenarioActionData action,
        BattleScenarioData scenario,
        ScenarioValidationResult result,
        string objectId)
    {
        if (scenario == null || action == null)
        {
            return;
        }

        if (!ContainsNormalized(scenario.PartyIds, "player"))
        {
            return;
        }

        JObject root;
        string error;
        if (!TryParseParameters(action, out root, out error) || root == null)
        {
            return;
        }

        WarnIfLegacyPlayerAlias(root, "actor", result, objectId);
        WarnIfLegacyPlayerAlias(root, "target", result, objectId);
        WarnIfLegacyPlayerAlias(root, "subject", result, objectId);
        WarnIfLegacyPlayerAlias(root, "targets", result, objectId);
    }

    private static void WarnIfLegacyPlayerAlias(
        JObject root,
        string parameterName,
        ScenarioValidationResult result,
        string objectId)
    {
        if (root == null || !root.TryGetValue(parameterName, out JToken token) || token == null)
        {
            return;
        }

        if (token.Type == JTokenType.String)
        {
            if (Trim(token.Value<string>()) == "player_001")
            {
                result.AddWarning(
                    "scenario.subject.player.alias.prefer_player",
                    "Scenario source canonical player subject ID는 'player'를 권장합니다. parameter '" + parameterName + "'에서 legacy alias 'player_001'을 사용 중입니다.",
                    objectId);
            }

            return;
        }

        if (token.Type != JTokenType.Array)
        {
            return;
        }

        foreach (JToken child in token.Children())
        {
            if (child != null && child.Type == JTokenType.String && Trim(child.Value<string>()) == "player_001")
            {
                result.AddWarning(
                    "scenario.subject.player.alias.prefer_player",
                    "Scenario source canonical player subject ID는 'player'를 권장합니다. parameter '" + parameterName + "' 목록에서 legacy alias 'player_001'을 사용 중입니다.",
                    objectId);
                return;
            }
        }
    }

    private static bool ContainsNormalized(List<string> values, string target)
    {
        if (values == null)
        {
            return false;
        }

        string normalizedTarget = Trim(target);
        for (int i = 0; i < values.Count; i++)
        {
            if (Trim(values[i]) == normalizedTarget)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, ActionCatalogEntry> BuildActionEntryMap(ActionCatalogAsset catalog)
    {
        var entries = new Dictionary<string, ActionCatalogEntry>();
        if (catalog == null || catalog.Entries == null)
        {
            return entries;
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
                entries[actionId] = entry;
            }
        }

        return entries;
    }

    private static ActionCatalogEntry FindEntry(Dictionary<string, ActionCatalogEntry> entryMap, string actionId)
    {
        if (entryMap == null || string.IsNullOrWhiteSpace(actionId))
        {
            return null;
        }

        ActionCatalogEntry entry;
        return entryMap.TryGetValue(actionId.Trim(), out entry) ? entry : null;
    }

    private static bool TryParseParameters(
        ScenarioActionData action,
        out JObject root,
        out string error)
    {
        root = null;
        error = string.Empty;

        string json = action != null ? action.ParametersJson : null;
        if (string.IsNullOrWhiteSpace(json))
        {
            root = new JObject();
            return true;
        }

        try
        {
            root = JObject.Parse(json);
            return true;
        }
        catch (System.Exception exception)
        {
            error = "Action parameters must be a JSON object: " + exception.Message;
            return false;
        }
    }

    private static string Trim(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
