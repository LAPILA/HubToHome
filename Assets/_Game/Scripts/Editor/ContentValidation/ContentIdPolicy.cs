#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

public static class ContentIdPolicy
{
    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            return false;

        if (!IsLowerAsciiLetterOrDigit(value[0]))
            return false;

        for (int i = 1; i < value.Length; i++)
        {
            char character = value[i];
            if (!IsLowerAsciiLetterOrDigit(character)
                && character != '.'
                && character != '_'
                && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    public static string CreateGeneratedId(
        string prefix,
        string displayName,
        string stableSuffix,
        ISet<string> reservedIds)
    {
        string normalizedPrefix = ToAsciiSlug(prefix);
        if (string.IsNullOrEmpty(normalizedPrefix))
            throw new ArgumentException("ID prefix must contain an ASCII letter or digit.", nameof(prefix));

        string slug = ToAsciiSlug(displayName);
        if (string.IsNullOrEmpty(slug))
            slug = "content";

        string suffix = ToAsciiSlug(stableSuffix);
        if (string.IsNullOrEmpty(suffix))
            suffix = "00000000";

        string candidateBase = normalizedPrefix + "_" + slug + "_" + suffix;
        if (reservedIds == null || !reservedIds.Contains(candidateBase))
            return candidateBase;

        int collisionIndex = 2;
        while (reservedIds.Contains(candidateBase + "_" + collisionIndex))
            collisionIndex++;

        return candidateBase + "_" + collisionIndex;
    }

    private static string ToAsciiSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        bool separatorPending = false;
        string lower = value.Trim().ToLowerInvariant();
        for (int i = 0; i < lower.Length; i++)
        {
            char character = lower[i];
            if (IsLowerAsciiLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                    builder.Append('_');

                builder.Append(character);
                separatorPending = false;
            }
            else if (builder.Length > 0)
            {
                separatorPending = true;
            }
        }

        return builder.ToString();
    }

    private static bool IsLowerAsciiLetterOrDigit(char character)
    {
        return (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9');
    }
}
#endif
