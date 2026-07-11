using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class ScenarioValueBinding
{
    public const string PropertyName = "$bind";

    private static readonly HashSet<string> SupportedRoots = new HashSet<string>(StringComparer.Ordinal)
    {
        "input",
        "event",
        "session",
        "memory",
        "flag",
        "context",
        "result"
    };

    public static JObject Create(string path)
    {
        if (!TryValidatePath(path, out string normalized, out string error))
        {
            throw new ArgumentException(error, nameof(path));
        }

        return new JObject { [PropertyName] = normalized };
    }

    public static bool HasMarker(JToken token)
    {
        return token is JObject objectToken && objectToken.Property(PropertyName) != null;
    }

    public static bool TryRead(JToken token, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;

        if (!(token is JObject objectToken) || objectToken.Property(PropertyName) == null)
        {
            return false;
        }

        if (objectToken.Count != 1)
        {
            error = "A value binding may only contain the '" + PropertyName + "' property.";
            return false;
        }

        JToken pathToken = objectToken[PropertyName];
        if (pathToken == null || pathToken.Type != JTokenType.String)
        {
            error = "A value binding path must be a string.";
            return false;
        }

        return TryValidatePath(pathToken.Value<string>(), out path, out error);
    }

    public static bool TryParseExpression(string expression, out JObject binding)
    {
        binding = null;
        string value = expression == null ? string.Empty : expression.Trim();
        if (value.Length < 4
            || !value.StartsWith("${", StringComparison.Ordinal)
            || !value.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        string path = value.Substring(2, value.Length - 3).Trim();
        if (!TryValidatePath(path, out string normalized, out _))
        {
            return false;
        }

        binding = new JObject { [PropertyName] = normalized };
        return true;
    }

    public static string ToExpression(string path)
    {
        return TryValidatePath(path, out string normalized, out _)
            ? "${" + normalized + "}"
            : string.Empty;
    }

    public static bool TryValidatePath(string path, out string normalized, out string error)
    {
        normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        error = string.Empty;
        int separator = normalized.IndexOf('.');
        if (separator <= 0 || separator == normalized.Length - 1)
        {
            error = "Binding path '" + normalized + "' must use '<root>.<name>' format.";
            return false;
        }

        string root = normalized.Substring(0, separator);
        if (!SupportedRoots.Contains(root))
        {
            error = "Binding path '" + normalized + "' uses unsupported root '" + root + "'.";
            return false;
        }

        string[] segments = normalized.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            if (!IsValidSegment(segments[i]))
            {
                error = "Binding path '" + normalized + "' contains an invalid segment.";
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        for (int i = 0; i < segment.Length; i++)
        {
            char character = segment[i];
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}
