using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TriggerLibrarySourceParseResult
{
    public TriggerLibrarySourceDocument Document;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => Document != null && !Validation.HasErrors;
}

public static class TriggerLibrarySourceParser
{
    public static TriggerLibrarySourceParseResult Parse(string text, string sourcePath = "")
    {
        var result = new TriggerLibrarySourceParseResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Validation.AddError(
                "trigger_library.source.empty",
                "Trigger Library source text is required.",
                sourcePath);
            return result;
        }

        var document = new TriggerLibrarySourceDocument { SourcePath = sourcePath ?? string.Empty };
        List<ScenarioLibraryYamlLine> lines = ScenarioLibraryYamlUtility.ReadLines(text);
        int index = 0;
        while (index < lines.Count)
        {
            ScenarioLibraryYamlLine line = lines[index];
            if (line.Indent != 0
                || !ScenarioLibraryYamlUtility.TryReadKeyValue(line.Text, out string key, out string value))
            {
                result.Validation.AddError(
                    "trigger_library.source.top_level.invalid",
                    "Trigger Library top-level fields must use 'key: value'.",
                    document.SourcePath);
                index++;
                continue;
            }

            if (key == "events")
            {
                index++;
                ParseEvents(lines, ref index, document, result.Validation);
                continue;
            }

            if (key == "conditions")
            {
                index++;
                ParseConditions(lines, ref index, document, result.Validation);
                continue;
            }

            ApplyHeader(document, key, value);
            index++;
        }

        ApplyDocumentDefaults(document);
        result.Document = document;
        result.Validation.Merge(TriggerLibrarySourceValidation.Validate(document));
        return result;
    }

    private static void ParseEvents(
        List<ScenarioLibraryYamlLine> lines,
        ref int index,
        TriggerLibrarySourceDocument document,
        ScenarioValidationResult validation)
    {
        while (index < lines.Count && lines[index].Indent > 0)
        {
            ScenarioLibraryYamlLine line = lines[index];
            if (line.Indent != 2
                || !ScenarioLibraryYamlUtility.TryReadKeyValue(line.Text, out string eventId, out _))
            {
                validation.AddError(
                    "trigger_library.event.invalid",
                    "Event entries must use two-space indentation and '<event.id>:'.",
                    document.SourcePath);
                index++;
                continue;
            }

            var definition = new ScenarioEventDefinition
            {
                EventId = ScenarioLibraryYamlUtility.Unquote(eventId),
                Category = document.Category,
                AccentHex = document.AccentHex
            };
            index++;
            while (index < lines.Count && lines[index].Indent > 2)
            {
                ScenarioLibraryYamlLine fieldLine = lines[index];
                if (fieldLine.Indent != 4
                    || !ScenarioLibraryYamlUtility.TryReadKeyValue(fieldLine.Text, out string key, out string value))
                {
                    validation.AddError(
                        "trigger_library.event.field.invalid",
                        "Event fields must use four-space indentation.",
                        "event:" + definition.EventId);
                    index++;
                    continue;
                }

                if (key == "payload")
                {
                    index++;
                    ParseFields(lines, ref index, definition.Payload, "event:" + definition.EventId, validation);
                    continue;
                }

                ApplyEvent(definition, key, value);
                index++;
            }

            document.Events.Add(definition);
        }
    }

    private static void ParseConditions(
        List<ScenarioLibraryYamlLine> lines,
        ref int index,
        TriggerLibrarySourceDocument document,
        ScenarioValidationResult validation)
    {
        while (index < lines.Count && lines[index].Indent > 0)
        {
            ScenarioLibraryYamlLine line = lines[index];
            if (line.Indent != 2
                || !ScenarioLibraryYamlUtility.TryReadKeyValue(line.Text, out string conditionId, out _))
            {
                validation.AddError(
                    "trigger_library.condition.invalid",
                    "Condition entries must use two-space indentation and '<condition.id>:'.",
                    document.SourcePath);
                index++;
                continue;
            }

            var definition = new TriggerConditionDefinition
            {
                ConditionId = ScenarioLibraryYamlUtility.Unquote(conditionId),
                Category = document.Category,
                AccentHex = document.AccentHex
            };
            index++;
            while (index < lines.Count && lines[index].Indent > 2)
            {
                ScenarioLibraryYamlLine fieldLine = lines[index];
                if (fieldLine.Indent != 4
                    || !ScenarioLibraryYamlUtility.TryReadKeyValue(fieldLine.Text, out string key, out string value))
                {
                    validation.AddError(
                        "trigger_library.condition.field.invalid",
                        "Condition fields must use four-space indentation.",
                        "condition:" + definition.ConditionId);
                    index++;
                    continue;
                }

                if (key == "parameters")
                {
                    index++;
                    ParseFields(lines, ref index, definition.Parameters, "condition:" + definition.ConditionId, validation);
                    continue;
                }

                ApplyCondition(definition, key, value);
                index++;
            }

            document.Conditions.Add(definition);
        }
    }

    private static void ParseFields(
        List<ScenarioLibraryYamlLine> lines,
        ref int index,
        List<TriggerFieldDefinition> fields,
        string owner,
        ScenarioValidationResult validation)
    {
        while (index < lines.Count && lines[index].Indent > 4)
        {
            ScenarioLibraryYamlLine line = lines[index];
            if (line.Indent != 6
                || !ScenarioLibraryYamlUtility.TryReadKeyValue(line.Text, out string fieldId, out _))
            {
                validation.AddError(
                    "trigger_library.field.invalid",
                    "Field entries must use six-space indentation and '<fieldId>:'.",
                    owner);
                index++;
                continue;
            }

            var field = new TriggerFieldDefinition
            {
                FieldId = ScenarioLibraryYamlUtility.Unquote(fieldId)
            };
            index++;
            while (index < lines.Count && lines[index].Indent > 6)
            {
                ScenarioLibraryYamlLine fieldLine = lines[index];
                if (fieldLine.Indent != 8
                    || !ScenarioLibraryYamlUtility.TryReadKeyValue(fieldLine.Text, out string key, out string value))
                {
                    validation.AddError(
                        "trigger_library.field.property.invalid",
                        "Field properties must use eight-space indentation.",
                        owner + ".field:" + field.FieldId);
                    index++;
                    continue;
                }

                ApplyField(field, key, value);
                index++;
            }

            fields.Add(field);
        }
    }

    private static void ApplyHeader(TriggerLibrarySourceDocument document, string key, string value)
    {
        switch (key)
        {
            case "libraryId": document.LibraryId = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "name": document.DisplayNameKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "description": document.DescriptionKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "category": document.Category = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "order": document.SortOrder = ScenarioLibraryYamlUtility.ParseInt(value); break;
            case "accent": document.AccentHex = ScenarioLibraryYamlUtility.Unquote(value); break;
        }
    }

    private static void ApplyEvent(ScenarioEventDefinition definition, string key, string value)
    {
        switch (key)
        {
            case "name": definition.DisplayNameKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "description": definition.DescriptionKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "usage": definition.UsageKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "sentence": definition.SentenceTemplateKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "category": definition.Category = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "tags": definition.Tags = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "aliases": definition.Aliases = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "modes": definition.AllowedPrimaryModes = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "icon": definition.IconId = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "accent": definition.AccentHex = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "deprecated": definition.Deprecated = ScenarioLibraryYamlUtility.ParseBool(value); break;
            case "replacement": definition.ReplacementEventId = ScenarioLibraryYamlUtility.Unquote(value); break;
        }
    }

    private static void ApplyCondition(TriggerConditionDefinition definition, string key, string value)
    {
        switch (key)
        {
            case "name": definition.DisplayNameKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "description": definition.DescriptionKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "usage": definition.UsageKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "sentence": definition.SentenceTemplateKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "category": definition.Category = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "tags": definition.Tags = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "aliases": definition.Aliases = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "contexts": definition.RequiredContexts = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "modes": definition.AllowedPrimaryModes = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "icon": definition.IconId = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "accent": definition.AccentHex = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "deprecated": definition.Deprecated = ScenarioLibraryYamlUtility.ParseBool(value); break;
            case "replacement": definition.ReplacementConditionId = ScenarioLibraryYamlUtility.Unquote(value); break;
        }
    }

    private static void ApplyField(TriggerFieldDefinition field, string key, string value)
    {
        switch (key)
        {
            case "name": field.DisplayNameKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "description": field.DescriptionKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "type": field.TypeId = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "control": field.EditorControlId = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "required": field.Required = ScenarioLibraryYamlUtility.ParseBool(value); break;
            case "default": field.DefaultValueJson = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "placeholder": field.PlaceholderKo = ScenarioLibraryYamlUtility.Unquote(value); break;
            case "sources": field.ValueSources = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "options": field.Options = ScenarioLibraryYamlUtility.ParseList(value); break;
            case "min": field.HasMinimum = true; field.Minimum = ScenarioLibraryYamlUtility.ParseDouble(value); break;
            case "max": field.HasMaximum = true; field.Maximum = ScenarioLibraryYamlUtility.ParseDouble(value); break;
            case "unit": field.UnitKo = ScenarioLibraryYamlUtility.Unquote(value); break;
        }
    }

    private static void ApplyDocumentDefaults(TriggerLibrarySourceDocument document)
    {
        for (int i = 0; i < document.Events.Count; i++)
        {
            ScenarioEventDefinition definition = document.Events[i];
            if (string.IsNullOrWhiteSpace(definition.Category))
            {
                definition.Category = document.Category;
            }

            if (string.IsNullOrWhiteSpace(definition.AccentHex))
            {
                definition.AccentHex = document.AccentHex;
            }
        }

        for (int i = 0; i < document.Conditions.Count; i++)
        {
            TriggerConditionDefinition definition = document.Conditions[i];
            if (string.IsNullOrWhiteSpace(definition.Category))
            {
                definition.Category = document.Category;
            }

            if (string.IsNullOrWhiteSpace(definition.AccentHex))
            {
                definition.AccentHex = document.AccentHex;
            }
        }
    }
}

internal static class TriggerLibrarySourceValidation
{
    public static ScenarioValidationResult Validate(TriggerLibrarySourceDocument document)
    {
        var result = new ScenarioValidationResult();
        if (document == null)
        {
            result.AddError("trigger_library.document.missing", "Trigger Library document is missing.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(document.LibraryId))
        {
            result.AddError("trigger_library.id.required", "Trigger Library requires libraryId.", document.SourcePath);
        }

        if (string.IsNullOrWhiteSpace(document.Category))
        {
            result.AddError("trigger_library.category.required", "Trigger Library requires category.", document.SourcePath);
        }

        if ((document.Events == null || document.Events.Count == 0)
            && (document.Conditions == null || document.Conditions.Count == 0))
        {
            result.AddError(
                "trigger_library.entries.required",
                "Trigger Library source requires at least one Event or Condition.",
                document.SourcePath);
            return result;
        }

        TriggerLibraryAsset temporary = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        temporary.LibraryId = document.LibraryId;
        Copy(document.Events, temporary.Events);
        Copy(document.Conditions, temporary.Conditions);
        result.Merge(TriggerLibraryContractValidator.Validate(temporary));
        UnityEngine.Object.DestroyImmediate(temporary);
        return result;
    }

    private static void Copy(
        IList<ScenarioEventDefinition> source,
        List<ScenarioEventDefinition> target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            target.Add(TriggerLibraryContractCopy.Event(source[i]));
        }
    }

    private static void Copy(
        IList<TriggerConditionDefinition> source,
        List<TriggerConditionDefinition> target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            target.Add(TriggerLibraryContractCopy.Condition(source[i]));
        }
    }
}
