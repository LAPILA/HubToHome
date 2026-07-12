using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal readonly struct ScenarioLibraryYamlLine
{
    public ScenarioLibraryYamlLine(int indent, string text)
    {
        Indent = indent;
        Text = text;
    }

    public int Indent { get; }
    public string Text { get; }
}

internal static class ScenarioLibraryYamlUtility
{
    public static List<ScenarioLibraryYamlLine> ReadLines(string text)
    {
        var result = new List<ScenarioLibraryYamlLine>();
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

            result.Add(new ScenarioLibraryYamlLine(indent, content.Trim()));
        }

        return result;
    }

    public static bool TryReadKeyValue(string text, out string key, out string value)
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

    public static string Unquote(string value)
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

    public static List<string> ParseList(string value)
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

    public static bool ParseBool(string value)
    {
        return string.Equals(Unquote(value), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseInt(string value)
    {
        return int.TryParse(Unquote(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    public static double ParseDouble(string value)
    {
        return double.TryParse(Unquote(value), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0d;
    }

    public static string Number(double value)
    {
        return value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    public static void Key(StringBuilder builder, int indent, string key, string value)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(Quote(value));
    }

    public static void Optional(StringBuilder builder, int indent, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Key(builder, indent, key, value);
        }
    }

    public static void Raw(StringBuilder builder, int indent, string key, string value)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(value ?? string.Empty);
    }

    public static void KeyOnly(StringBuilder builder, int indent, string key)
    {
        Indent(builder, indent);
        builder.Append(key);
        builder.AppendLine(":");
    }

    public static void List(StringBuilder builder, int indent, string key, IList<string> values)
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

    private static void AddListValue(List<string> result, StringBuilder builder)
    {
        string item = Unquote(builder.ToString());
        builder.Length = 0;
        if (!string.IsNullOrWhiteSpace(item))
        {
            result.Add(item);
        }
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
