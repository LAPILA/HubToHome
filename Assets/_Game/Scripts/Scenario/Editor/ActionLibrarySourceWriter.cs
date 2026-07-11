using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public sealed class ActionLibrarySourceWriteResult
{
    public string Text = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => !Validation.HasErrors;
}

public static class ActionLibrarySourceWriter
{
    public static ActionLibrarySourceWriteResult Write(ActionLibrarySourceDocument document)
    {
        var result = new ActionLibrarySourceWriteResult();
        result.Validation.Merge(ActionLibrarySourceValidation.Validate(document));
        if (document == null)
        {
            return result;
        }

        var builder = new StringBuilder();
        Key(builder, 0, "libraryId", document.LibraryId);
        Key(builder, 0, "name", document.DisplayNameKo);
        Key(builder, 0, "description", document.DescriptionKo);
        Key(builder, 0, "category", document.Category);
        Raw(builder, 0, "order", document.SortOrder.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(document.AccentHex))
        {
            Key(builder, 0, "accent", document.AccentHex);
        }

        builder.AppendLine("actions:");
        var entries = new List<ActionCatalogEntry>();
        if (document.Entries != null)
        {
            for (int i = 0; i < document.Entries.Count; i++)
            {
                if (document.Entries[i] != null)
                {
                    entries.Add(document.Entries[i]);
                }
            }
        }

        entries.Sort((left, right) => string.Compare(left.ActionId, right.ActionId, StringComparison.Ordinal));
        for (int i = 0; i < entries.Count; i++)
        {
            WriteEntry(builder, document, entries[i]);
        }

        result.Text = builder.ToString();
        return result;
    }

    private static void WriteEntry(
        StringBuilder builder,
        ActionLibrarySourceDocument document,
        ActionCatalogEntry entry)
    {
        KeyOnly(builder, 1, entry.ActionId);
        Key(builder, 2, "name", entry.DisplayNameKo);
        Key(builder, 2, "description", entry.DescriptionKo);
        Key(builder, 2, "usage", entry.UsageKo);
        Key(builder, 2, "summary", entry.SummaryTemplateKo);
        if (!string.IsNullOrWhiteSpace(entry.Category) && entry.Category != document.Category)
        {
            Key(builder, 2, "category", entry.Category);
        }

        Optional(builder, 2, "subcategory", entry.Subcategory);
        Key(builder, 2, "runtimeAdapter", entry.RuntimeAdapterId);
        List(builder, 2, "tags", entry.Tags);
        if (entry.Aliases != null && entry.Aliases.Count > 0)
        {
            List(builder, 2, "aliases", entry.Aliases);
        }

        List(builder, 2, "contexts", entry.RequiredContexts);
        if (entry.AllowedPrimaryModes != null && entry.AllowedPrimaryModes.Count > 0)
        {
            List(builder, 2, "modes", entry.AllowedPrimaryModes);
        }

        Raw(builder, 2, "preview", Preview(entry.PreviewSupport));
        Raw(builder, 2, "preparation", Preparation(entry.PreparationPolicy));
        if (entry.Deprecated)
        {
            Raw(builder, 2, "deprecated", "true");
            Optional(builder, 2, "replacement", entry.ReplacementActionId);
        }

        Optional(builder, 2, "icon", entry.IconId);
        if (!string.IsNullOrWhiteSpace(entry.AccentHex) && entry.AccentHex != document.AccentHex)
        {
            Key(builder, 2, "accent", entry.AccentHex);
        }

        Key(builder, 2, "example", entry.ExampleYaml);
        if (entry.Disabled)
        {
            Raw(builder, 2, "disabled", "true");
        }

        if (entry.Parameters == null || entry.Parameters.Count == 0)
        {
            return;
        }

        KeyOnly(builder, 2, "parameters");
        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter != null)
            {
                WriteParameter(builder, parameter);
            }
        }
    }

    private static void WriteParameter(StringBuilder builder, ActionCatalogParameter parameter)
    {
        KeyOnly(builder, 3, parameter.Name);
        Key(builder, 4, "name", parameter.DisplayNameKo);
        Key(builder, 4, "description", parameter.DescriptionKo);
        Key(builder, 4, "type", parameter.Type);
        Key(builder, 4, "control", parameter.EditorControlId);
        if (parameter.Required)
        {
            Raw(builder, 4, "required", "true");
        }

        if (parameter.QuickEdit)
        {
            Raw(builder, 4, "quick", "true");
        }

        Optional(builder, 4, "default", parameter.DefaultValue);
        if (parameter.HasMinimum)
        {
            Raw(builder, 4, "min", Number(parameter.Minimum));
        }

        if (parameter.HasMaximum)
        {
            Raw(builder, 4, "max", Number(parameter.Maximum));
        }

        Optional(builder, 4, "unit", parameter.UnitKo);
        List(builder, 4, "sources", parameter.ValueSources);
        if (parameter.Options != null && parameter.Options.Count > 0)
        {
            List(builder, 4, "options", parameter.Options);
        }

        Optional(builder, 4, "placeholder", parameter.PlaceholderKo);
    }

    private static string Preview(ActionPreviewSupport value)
    {
        switch (value)
        {
            case ActionPreviewSupport.SafePreview: return "safe_preview";
            case ActionPreviewSupport.LiveOnly: return "live_only";
            default: return "unsupported";
        }
    }

    private static string Preparation(ActionPreparationPolicy value)
    {
        switch (value)
        {
            case ActionPreparationPolicy.ApplyFinalState: return "apply_final_state";
            case ActionPreparationPolicy.ExecuteIsolated: return "execute_isolated";
            case ActionPreparationPolicy.SkipPresentation: return "skip_presentation";
            case ActionPreparationPolicy.RequireInput: return "require_input";
            default: return "unsupported";
        }
    }

    private static string Number(double value)
    {
        return value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    private static void Optional(StringBuilder builder, int indent, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Key(builder, indent, key, value);
        }
    }

    private static void Key(StringBuilder builder, int indent, string key, string value)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(Quote(value));
    }

    private static void Raw(StringBuilder builder, int indent, string key, string value)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(value ?? string.Empty);
    }

    private static void KeyOnly(StringBuilder builder, int indent, string key)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.AppendLine(":");
    }

    private static void List(StringBuilder builder, int indent, string key, IList<string> values)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.Append(": [");
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Quote(values[i]));
            }
        }

        builder.AppendLine("]");
    }

    private static string Quote(string value)
    {
        string text = value ?? string.Empty;
        return "\"" + text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";
    }

    private static void Indent(StringBuilder builder, int level)
    {
        builder.Append(' ', level * 2);
    }
}
