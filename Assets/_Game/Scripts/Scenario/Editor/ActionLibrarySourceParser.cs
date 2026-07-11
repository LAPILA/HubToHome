using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ActionLibrarySourceParseResult
{
    public ActionLibrarySourceDocument Document;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => Document != null && !Validation.HasErrors;
}

public static class ActionLibrarySourceParser
{
    public static ActionLibrarySourceParseResult Parse(string text, string sourcePath = "")
    {
        var result = new ActionLibrarySourceParseResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Validation.AddError(
                "action_library.source.empty",
                "Action Library source text is required.",
                sourcePath);
            return result;
        }

        var document = new ActionLibrarySourceDocument { SourcePath = sourcePath ?? string.Empty };
        List<YamlLine> lines = ReadLines(text);
        int index = 0;
        while (index < lines.Count)
        {
            YamlLine line = lines[index];
            if (line.Indent != 0 || !TryReadKeyValue(line.Text, out string key, out string value))
            {
                result.Validation.AddError(
                    "action_library.source.top_level.invalid",
                    "Action Library top-level fields must use 'key: value'.",
                    document.SourcePath);
                index++;
                continue;
            }

            if (key == "actions")
            {
                index++;
                ParseActions(lines, ref index, document, result.Validation);
                continue;
            }

            ApplyHeader(document, key, value);
            index++;
        }

        for (int i = 0; i < document.Entries.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(document.Entries[i].Category))
            {
                document.Entries[i].Category = document.Category;
            }

            if (string.IsNullOrWhiteSpace(document.Entries[i].AccentHex))
            {
                document.Entries[i].AccentHex = document.AccentHex;
            }
        }

        result.Document = document;
        result.Validation.Merge(ActionLibrarySourceValidation.Validate(document));
        return result;
    }

    private static void ParseActions(
        List<YamlLine> lines,
        ref int index,
        ActionLibrarySourceDocument document,
        ScenarioValidationResult validation)
    {
        while (index < lines.Count && lines[index].Indent > 0)
        {
            YamlLine line = lines[index];
            if (line.Indent != 2 || !TryReadKeyValue(line.Text, out string actionId, out string inlineValue))
            {
                validation.AddError(
                    "action_library.action.invalid",
                    "Action entries must use two-space indentation and '<action.id>:'.",
                    document.SourcePath);
                index++;
                continue;
            }

            var entry = new ActionCatalogEntry
            {
                ActionId = Unquote(actionId),
                Category = document.Category,
                AccentHex = document.AccentHex
            };
            index++;
            while (index < lines.Count && lines[index].Indent > 2)
            {
                YamlLine fieldLine = lines[index];
                if (fieldLine.Indent != 4
                    || !TryReadKeyValue(fieldLine.Text, out string key, out string value))
                {
                    validation.AddError(
                        "action_library.action.field.invalid",
                        "Action fields must use four-space indentation.",
                        "action:" + entry.ActionId);
                    index++;
                    continue;
                }

                if (key == "parameters")
                {
                    index++;
                    ParseParameters(lines, ref index, entry, validation);
                    continue;
                }

                ApplyEntry(entry, key, value);
                index++;
            }

            document.Entries.Add(entry);
        }
    }

    private static void ParseParameters(
        List<YamlLine> lines,
        ref int index,
        ActionCatalogEntry entry,
        ScenarioValidationResult validation)
    {
        while (index < lines.Count && lines[index].Indent > 4)
        {
            YamlLine line = lines[index];
            if (line.Indent != 6 || !TryReadKeyValue(line.Text, out string parameterId, out string inlineValue))
            {
                validation.AddError(
                    "action_library.parameter.invalid",
                    "Parameter entries must use six-space indentation and '<parameterId>:'.",
                    "action:" + entry.ActionId);
                index++;
                continue;
            }

            var parameter = new ActionCatalogParameter { Name = Unquote(parameterId) };
            index++;
            while (index < lines.Count && lines[index].Indent > 6)
            {
                YamlLine fieldLine = lines[index];
                if (fieldLine.Indent != 8
                    || !TryReadKeyValue(fieldLine.Text, out string key, out string value))
                {
                    validation.AddError(
                        "action_library.parameter.field.invalid",
                        "Parameter fields must use eight-space indentation.",
                        "action:" + entry.ActionId + ".parameter:" + parameter.Name);
                    index++;
                    continue;
                }

                ApplyParameter(parameter, key, value);
                index++;
            }

            entry.Parameters.Add(parameter);
        }
    }

    private static void ApplyHeader(ActionLibrarySourceDocument document, string key, string value)
    {
        switch (key)
        {
            case "libraryId": document.LibraryId = Unquote(value); break;
            case "name": document.DisplayNameKo = Unquote(value); break;
            case "description": document.DescriptionKo = Unquote(value); break;
            case "category": document.Category = Unquote(value); break;
            case "order": document.SortOrder = ParseInt(value); break;
            case "accent": document.AccentHex = Unquote(value); break;
        }
    }

    private static void ApplyEntry(ActionCatalogEntry entry, string key, string value)
    {
        switch (key)
        {
            case "name": entry.DisplayNameKo = Unquote(value); break;
            case "description": entry.DescriptionKo = Unquote(value); break;
            case "usage": entry.UsageKo = Unquote(value); break;
            case "summary": entry.SummaryTemplateKo = Unquote(value); break;
            case "category": entry.Category = Unquote(value); break;
            case "subcategory": entry.Subcategory = Unquote(value); break;
            case "runtimeAdapter": entry.RuntimeAdapterId = Unquote(value); break;
            case "tags": entry.Tags = ParseList(value); break;
            case "aliases": entry.Aliases = ParseList(value); break;
            case "contexts": entry.RequiredContexts = ParseList(value); break;
            case "modes": entry.AllowedPrimaryModes = ParseList(value); break;
            case "preview": entry.PreviewSupport = ParsePreview(value); break;
            case "preparation": entry.PreparationPolicy = ParsePreparation(value); break;
            case "deprecated": entry.Deprecated = ParseBool(value); break;
            case "replacement": entry.ReplacementActionId = Unquote(value); break;
            case "icon": entry.IconId = Unquote(value); break;
            case "accent": entry.AccentHex = Unquote(value); break;
            case "example": entry.ExampleYaml = Unquote(value); break;
            case "disabled": entry.Disabled = ParseBool(value); break;
        }
    }

    private static void ApplyParameter(ActionCatalogParameter parameter, string key, string value)
    {
        switch (key)
        {
            case "name": parameter.DisplayNameKo = Unquote(value); break;
            case "description": parameter.DescriptionKo = Unquote(value); break;
            case "type": parameter.Type = Unquote(value); break;
            case "control": parameter.EditorControlId = Unquote(value); break;
            case "required": parameter.Required = ParseBool(value); break;
            case "quick": parameter.QuickEdit = ParseBool(value); break;
            case "default": parameter.DefaultValue = Unquote(value); break;
            case "min": parameter.HasMinimum = true; parameter.Minimum = ParseDouble(value); break;
            case "max": parameter.HasMaximum = true; parameter.Maximum = ParseDouble(value); break;
            case "unit": parameter.UnitKo = Unquote(value); break;
            case "sources": parameter.ValueSources = ParseList(value); break;
            case "options": parameter.Options = ParseList(value); break;
            case "placeholder": parameter.PlaceholderKo = Unquote(value); break;
        }
    }

    private static ActionPreviewSupport ParsePreview(string value)
    {
        switch (Unquote(value))
        {
            case "safe_preview": return ActionPreviewSupport.SafePreview;
            case "live_only": return ActionPreviewSupport.LiveOnly;
            default: return ActionPreviewSupport.Unsupported;
        }
    }

    private static ActionPreparationPolicy ParsePreparation(string value)
    {
        switch (Unquote(value))
        {
            case "apply_final_state": return ActionPreparationPolicy.ApplyFinalState;
            case "execute_isolated": return ActionPreparationPolicy.ExecuteIsolated;
            case "skip_presentation": return ActionPreparationPolicy.SkipPresentation;
            case "require_input": return ActionPreparationPolicy.RequireInput;
            default: return ActionPreparationPolicy.Unsupported;
        }
    }

    private static List<YamlLine> ReadLines(string text)
    {
        var result = new List<YamlLine>();
        string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string content = StripComment(lines[i]);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            int indent = 0;
            while (indent < content.Length && content[indent] == ' ')
            {
                indent++;
            }

            result.Add(new YamlLine(indent, content.Trim()));
        }

        return result;
    }

    private static string StripComment(string line)
    {
        bool quoted = false;
        bool escaped = false;
        for (int i = 0; i < (line ?? string.Empty).Length; i++)
        {
            char character = line[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\' && quoted)
            {
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == '#' && !quoted)
            {
                return line.Substring(0, i);
            }
        }

        return line ?? string.Empty;
    }

    private static bool TryReadKeyValue(string text, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int separator = (text ?? string.Empty).IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        key = Unquote(text.Substring(0, separator).Trim());
        value = text.Substring(separator + 1).Trim();
        return !string.IsNullOrEmpty(key);
    }

    internal static string Unquote(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length < 2 || text[0] != '"' || text[text.Length - 1] != '"')
        {
            return text;
        }

        var builder = new StringBuilder();
        bool escaped = false;
        for (int i = 1; i < text.Length - 1; i++)
        {
            char character = text[i];
            if (!escaped && character == '\\')
            {
                escaped = true;
                continue;
            }

            if (escaped)
            {
                switch (character)
                {
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    default: builder.Append(character); break;
                }

                escaped = false;
                continue;
            }

            builder.Append(character);
        }

        if (escaped)
        {
            builder.Append('\\');
        }

        return builder.ToString();
    }

    internal static List<string> ParseList(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
        {
            text = text.Substring(1, text.Length - 2);
        }

        var result = new List<string>();
        var builder = new StringBuilder();
        bool quoted = false;
        bool escaped = false;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && quoted)
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
                builder.Append(character);
                continue;
            }

            if (character == ',' && !quoted)
            {
                AddListValue(result, builder);
                continue;
            }

            builder.Append(character);
        }

        AddListValue(result, builder);
        return result;
    }

    private static void AddListValue(List<string> result, StringBuilder builder)
    {
        string item = Unquote(builder.ToString());
        builder.Length = 0;
        if (!string.IsNullOrWhiteSpace(item))
        {
            result.Add(item);
        }
    }

    private static bool ParseBool(string value)
    {
        return string.Equals(Unquote(value), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(Unquote(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(Unquote(value), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0d;
    }

    private readonly struct YamlLine
    {
        public YamlLine(int indent, string text)
        {
            Indent = indent;
            Text = text;
        }

        public int Indent { get; }
        public string Text { get; }
    }
}

internal static class ActionLibrarySourceValidation
{
    public static ScenarioValidationResult Validate(ActionLibrarySourceDocument document)
    {
        var result = new ScenarioValidationResult();
        if (document == null)
        {
            result.AddError("action_library.document.missing", "Action Library document is missing.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(document.LibraryId))
        {
            result.AddError("action_library.id.required", "Action Library requires libraryId.", document.SourcePath);
        }

        if (string.IsNullOrWhiteSpace(document.Category))
        {
            result.AddError("action_library.category.required", "Action Library requires category.", document.SourcePath);
        }

        if (document.Entries == null || document.Entries.Count == 0)
        {
            result.AddError("action_library.actions.required", "Action Library requires at least one Action.", document.SourcePath);
            return result;
        }

        ActionCatalogAsset temporary = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        temporary.CatalogId = document.LibraryId;
        for (int i = 0; i < document.Entries.Count; i++)
        {
            temporary.Entries.Add(ActionCatalogContractCopy.Entry(document.Entries[i]));
        }

        result.Merge(ScenarioCatalogValidator.Validate(temporary));
        UnityEngine.Object.DestroyImmediate(temporary);
        return result;
    }
}
