using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public static class TriggerLibraryContractValidator
{
    private static readonly Regex StableIdPattern = new Regex(
        "^[a-z][a-z0-9_]*(\\.[a-z0-9_]+)+$",
        RegexOptions.CultureInvariant);

    public static ScenarioValidationResult Validate(TriggerLibraryAsset library)
    {
        var result = new ScenarioValidationResult();
        if (library == null)
        {
            result.AddError("trigger_library.asset.missing", "Trigger Library asset is missing.");
            return result;
        }

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        var conditionIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateEvents(library.Events, eventIds, result);
        ValidateConditions(library.Conditions, conditionIds, result);
        if (eventIds.Count == 0 && conditionIds.Count == 0)
        {
            result.AddError(
                "trigger_library.entries.required",
                "Trigger Library requires at least one Event or Condition.");
        }

        ValidateReplacements(library.Events, eventIds, library.Conditions, conditionIds, result);
        return result;
    }

    private static void ValidateEvents(
        IList<ScenarioEventDefinition> events,
        HashSet<string> ids,
        ScenarioValidationResult result)
    {
        if (events == null)
        {
            return;
        }

        for (int i = 0; i < events.Count; i++)
        {
            ScenarioEventDefinition definition = events[i];
            string location = "event:" + Normalize(definition?.EventId);
            if (definition == null)
            {
                result.AddError("trigger_library.event.null", "Event definition is null.", "events[" + i + "]");
                continue;
            }

            ValidateEntry(
                definition.EventId,
                definition.Category,
                definition.DisplayNameKo,
                definition.DescriptionKo,
                definition.UsageKo,
                definition.SentenceTemplateKo,
                definition.Tags,
                "event",
                location,
                ids,
                result);
            ValidateAccent(definition.AccentHex, location, result);
            ValidateFields(definition.Payload, location + ".payload", result);
        }
    }

    private static void ValidateConditions(
        IList<TriggerConditionDefinition> conditions,
        HashSet<string> ids,
        ScenarioValidationResult result)
    {
        if (conditions == null)
        {
            return;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            TriggerConditionDefinition definition = conditions[i];
            string location = "condition:" + Normalize(definition?.ConditionId);
            if (definition == null)
            {
                result.AddError("trigger_library.condition.null", "Condition definition is null.", "conditions[" + i + "]");
                continue;
            }

            ValidateEntry(
                definition.ConditionId,
                definition.Category,
                definition.DisplayNameKo,
                definition.DescriptionKo,
                definition.UsageKo,
                definition.SentenceTemplateKo,
                definition.Tags,
                "condition",
                location,
                ids,
                result);
            ValidateAccent(definition.AccentHex, location, result);
            ValidateFields(definition.Parameters, location + ".parameters", result);
        }
    }

    private static void ValidateEntry(
        string id,
        string category,
        string displayName,
        string description,
        string usage,
        string sentence,
        IList<string> tags,
        string kind,
        string location,
        HashSet<string> ids,
        ScenarioValidationResult result)
    {
        string normalized = Normalize(id);
        if (string.IsNullOrEmpty(normalized))
        {
            result.AddError("trigger_library." + kind + ".id.required", kind + " ID is required.", location);
        }
        else
        {
            if (!StableIdPattern.IsMatch(normalized))
            {
                result.AddError(
                    "trigger_library." + kind + ".id.invalid",
                    "ID must use dotted lower-case segments: " + normalized,
                    location);
            }

            if (!ids.Add(normalized))
            {
                result.AddError(
                    "trigger_library." + kind + ".duplicate",
                    "Duplicate " + kind + " ID: " + normalized,
                    location);
            }
        }

        Required(category, kind + ".category", location, result);
        Required(displayName, kind + ".name", location, result);
        Required(description, kind + ".description", location, result);
        Required(usage, kind + ".usage", location, result);
        Required(sentence, kind + ".sentence", location, result);
        if (tags == null || tags.Count == 0)
        {
            result.AddError("trigger_library." + kind + ".tags.required", kind + " requires search tags.", location);
        }
    }

    private static void ValidateFields(
        IList<TriggerFieldDefinition> fields,
        string location,
        ScenarioValidationResult result)
    {
        if (fields == null)
        {
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < fields.Count; i++)
        {
            TriggerFieldDefinition field = fields[i];
            string fieldLocation = location + "[" + i + "]";
            if (field == null)
            {
                result.AddError("trigger_library.field.null", "Field definition is null.", fieldLocation);
                continue;
            }

            string fieldId = Normalize(field.FieldId);
            Required(fieldId, "field.id", fieldLocation, result);
            Required(field.DisplayNameKo, "field.name", fieldLocation, result);
            Required(field.DescriptionKo, "field.description", fieldLocation, result);
            Required(field.TypeId, "field.type", fieldLocation, result);
            Required(field.EditorControlId, "field.control", fieldLocation, result);
            if (!string.IsNullOrEmpty(fieldId) && !ids.Add(fieldId))
            {
                result.AddError("trigger_library.field.duplicate", "Duplicate field ID: " + fieldId, location);
            }

            if (!string.IsNullOrWhiteSpace(field.DefaultValueJson))
            {
                try
                {
                    JToken.Parse(field.DefaultValueJson);
                }
                catch (Exception exception)
                {
                    result.AddError(
                        "trigger_library.field.default.invalid",
                        "Default value is not valid JSON: " + exception.Message,
                        fieldLocation);
                }
            }

            if (field.HasMinimum && field.HasMaximum && field.Minimum > field.Maximum)
            {
                result.AddError(
                    "trigger_library.field.range.invalid",
                    "Field minimum cannot be greater than maximum.",
                    fieldLocation);
            }
        }
    }

    private static void ValidateReplacements(
        IList<ScenarioEventDefinition> events,
        HashSet<string> eventIds,
        IList<TriggerConditionDefinition> conditions,
        HashSet<string> conditionIds,
        ScenarioValidationResult result)
    {
        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                ScenarioEventDefinition item = events[i];
                if (item != null && item.Deprecated)
                {
                    ValidateReplacement(item.EventId, item.ReplacementEventId, eventIds, "event", result);
                }
            }
        }

        if (conditions != null)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                TriggerConditionDefinition item = conditions[i];
                if (item != null && item.Deprecated)
                {
                    ValidateReplacement(item.ConditionId, item.ReplacementConditionId, conditionIds, "condition", result);
                }
            }
        }
    }

    private static void ValidateReplacement(
        string id,
        string replacement,
        HashSet<string> knownIds,
        string kind,
        ScenarioValidationResult result)
    {
        string normalized = Normalize(replacement);
        string location = kind + ":" + Normalize(id);
        if (string.IsNullOrEmpty(normalized))
        {
            result.AddError("trigger_library." + kind + ".replacement.required", "Deprecated entry requires a replacement ID.", location);
        }
        else if (normalized == Normalize(id))
        {
            result.AddError("trigger_library." + kind + ".replacement.self", "Entry cannot replace itself.", location);
        }
        else if (!knownIds.Contains(normalized))
        {
            result.AddError("trigger_library." + kind + ".replacement.missing", "Replacement ID is not defined: " + normalized, location);
        }
    }

    private static void ValidateAccent(string value, string location, ScenarioValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value.Trim(), "^#[0-9A-Fa-f]{6}$"))
        {
            result.AddError("trigger_library.accent.invalid", "Accent must use #RRGGBB.", location);
        }
    }

    private static void Required(
        string value,
        string field,
        string location,
        ScenarioValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError("trigger_library." + field + ".required", field + " is required.", location);
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
