using System;
using System.Collections.Generic;
using System.Text;

public sealed class TriggerLibrarySourceWriteResult
{
    public string Text = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => !Validation.HasErrors;
}

public static class TriggerLibrarySourceWriter
{
    public static TriggerLibrarySourceWriteResult Write(TriggerLibrarySourceDocument document)
    {
        var result = new TriggerLibrarySourceWriteResult();
        result.Validation.Merge(TriggerLibrarySourceValidation.Validate(document));
        if (document == null)
        {
            return result;
        }

        var builder = new StringBuilder();
        ScenarioLibraryYamlUtility.Key(builder, 0, "libraryId", document.LibraryId);
        ScenarioLibraryYamlUtility.Key(builder, 0, "name", document.DisplayNameKo);
        ScenarioLibraryYamlUtility.Key(builder, 0, "description", document.DescriptionKo);
        ScenarioLibraryYamlUtility.Key(builder, 0, "category", document.Category);
        ScenarioLibraryYamlUtility.Raw(builder, 0, "order", document.SortOrder.ToString());
        ScenarioLibraryYamlUtility.Optional(builder, 0, "accent", document.AccentHex);
        WriteEvents(builder, document);
        WriteConditions(builder, document);
        result.Text = builder.ToString();
        return result;
    }

    private static void WriteEvents(StringBuilder builder, TriggerLibrarySourceDocument document)
    {
        List<ScenarioEventDefinition> entries = NonNull(document.Events);
        if (entries.Count == 0)
        {
            return;
        }

        entries.Sort((left, right) => string.Compare(left.EventId, right.EventId, StringComparison.Ordinal));
        builder.AppendLine("events:");
        for (int i = 0; i < entries.Count; i++)
        {
            ScenarioEventDefinition entry = entries[i];
            ScenarioLibraryYamlUtility.KeyOnly(builder, 1, entry.EventId);
            WriteCommon(builder, document, entry.Category, entry.DisplayNameKo, entry.DescriptionKo,
                entry.UsageKo, entry.SentenceTemplateKo, entry.Tags, entry.Aliases,
                entry.AllowedPrimaryModes, entry.IconId, entry.AccentHex, entry.Deprecated,
                entry.ReplacementEventId);
            WriteFields(builder, "payload", entry.Payload);
        }
    }

    private static void WriteConditions(StringBuilder builder, TriggerLibrarySourceDocument document)
    {
        List<TriggerConditionDefinition> entries = NonNull(document.Conditions);
        if (entries.Count == 0)
        {
            return;
        }

        entries.Sort((left, right) => string.Compare(left.ConditionId, right.ConditionId, StringComparison.Ordinal));
        builder.AppendLine("conditions:");
        for (int i = 0; i < entries.Count; i++)
        {
            TriggerConditionDefinition entry = entries[i];
            ScenarioLibraryYamlUtility.KeyOnly(builder, 1, entry.ConditionId);
            WriteCommon(builder, document, entry.Category, entry.DisplayNameKo, entry.DescriptionKo,
                entry.UsageKo, entry.SentenceTemplateKo, entry.Tags, entry.Aliases,
                entry.AllowedPrimaryModes, entry.IconId, entry.AccentHex, entry.Deprecated,
                entry.ReplacementConditionId);
            if (entry.RequiredContexts != null && entry.RequiredContexts.Count > 0)
            {
                ScenarioLibraryYamlUtility.List(builder, 2, "contexts", entry.RequiredContexts);
            }

            WriteFields(builder, "parameters", entry.Parameters);
        }
    }

    private static void WriteCommon(
        StringBuilder builder,
        TriggerLibrarySourceDocument document,
        string category,
        string name,
        string description,
        string usage,
        string sentence,
        IList<string> tags,
        IList<string> aliases,
        IList<string> modes,
        string icon,
        string accent,
        bool deprecated,
        string replacement)
    {
        ScenarioLibraryYamlUtility.Key(builder, 2, "name", name);
        ScenarioLibraryYamlUtility.Key(builder, 2, "description", description);
        ScenarioLibraryYamlUtility.Key(builder, 2, "usage", usage);
        ScenarioLibraryYamlUtility.Key(builder, 2, "sentence", sentence);
        if (!string.IsNullOrWhiteSpace(category) && category != document.Category)
        {
            ScenarioLibraryYamlUtility.Key(builder, 2, "category", category);
        }

        ScenarioLibraryYamlUtility.List(builder, 2, "tags", tags);
        if (aliases != null && aliases.Count > 0)
        {
            ScenarioLibraryYamlUtility.List(builder, 2, "aliases", aliases);
        }

        if (modes != null && modes.Count > 0)
        {
            ScenarioLibraryYamlUtility.List(builder, 2, "modes", modes);
        }

        ScenarioLibraryYamlUtility.Optional(builder, 2, "icon", icon);
        if (!string.IsNullOrWhiteSpace(accent) && accent != document.AccentHex)
        {
            ScenarioLibraryYamlUtility.Key(builder, 2, "accent", accent);
        }

        if (deprecated)
        {
            ScenarioLibraryYamlUtility.Raw(builder, 2, "deprecated", "true");
            ScenarioLibraryYamlUtility.Optional(builder, 2, "replacement", replacement);
        }
    }

    private static void WriteFields(
        StringBuilder builder,
        string key,
        IList<TriggerFieldDefinition> fields)
    {
        if (fields == null || fields.Count == 0)
        {
            return;
        }

        ScenarioLibraryYamlUtility.KeyOnly(builder, 2, key);
        for (int i = 0; i < fields.Count; i++)
        {
            TriggerFieldDefinition field = fields[i];
            if (field == null)
            {
                continue;
            }

            ScenarioLibraryYamlUtility.KeyOnly(builder, 3, field.FieldId);
            ScenarioLibraryYamlUtility.Key(builder, 4, "name", field.DisplayNameKo);
            ScenarioLibraryYamlUtility.Key(builder, 4, "description", field.DescriptionKo);
            ScenarioLibraryYamlUtility.Key(builder, 4, "type", field.TypeId);
            ScenarioLibraryYamlUtility.Key(builder, 4, "control", field.EditorControlId);
            if (field.Required)
            {
                ScenarioLibraryYamlUtility.Raw(builder, 4, "required", "true");
            }

            ScenarioLibraryYamlUtility.Optional(builder, 4, "default", field.DefaultValueJson);
            ScenarioLibraryYamlUtility.Optional(builder, 4, "placeholder", field.PlaceholderKo);
            if (field.ValueSources != null && field.ValueSources.Count > 0)
            {
                ScenarioLibraryYamlUtility.List(builder, 4, "sources", field.ValueSources);
            }

            if (field.Options != null && field.Options.Count > 0)
            {
                ScenarioLibraryYamlUtility.List(builder, 4, "options", field.Options);
            }

            if (field.HasMinimum)
            {
                ScenarioLibraryYamlUtility.Raw(builder, 4, "min", ScenarioLibraryYamlUtility.Number(field.Minimum));
            }

            if (field.HasMaximum)
            {
                ScenarioLibraryYamlUtility.Raw(builder, 4, "max", ScenarioLibraryYamlUtility.Number(field.Maximum));
            }

            ScenarioLibraryYamlUtility.Optional(builder, 4, "unit", field.UnitKo);
        }
    }

    private static List<ScenarioEventDefinition> NonNull(IList<ScenarioEventDefinition> source)
    {
        var result = new List<ScenarioEventDefinition>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    result.Add(source[i]);
                }
            }
        }

        return result;
    }

    private static List<TriggerConditionDefinition> NonNull(IList<TriggerConditionDefinition> source)
    {
        var result = new List<TriggerConditionDefinition>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    result.Add(source[i]);
                }
            }
        }

        return result;
    }
}
