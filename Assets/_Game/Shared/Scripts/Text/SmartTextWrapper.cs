using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared smart line wrapper for dialogue-like UI.
/// Use this before assigning text to TMP when the surrounding box is resized from preferred text size.
/// The wrapper keeps whitespace-delimited words together first, then falls back to character chunks
/// only when a single token is wider than the target line width.
/// </summary>
public static class SmartTextWrapper
{
    private const char LineBreak = '\n';
    private const string Space = " ";

    /// <summary>
    /// Wraps text using TMP as the width measurement source.
    /// This is the easiest entry point for runtime UI:
    /// <c>string wrapped = SmartTextWrapper.Wrap(label, rawText, maxTextWidth);</c>
    /// </summary>
    public static string Wrap(TMP_Text textComponent, string text, float maxWidth, SmartTextWrapOptions options = default)
    {
        if (textComponent == null)
            return text ?? string.Empty;

        return Wrap(
            text,
            maxWidth,
            value => textComponent.GetPreferredValues(value, Mathf.Infinity, Mathf.Infinity).x,
            options);
    }

    /// <summary>
    /// Wraps text using a caller-provided width measurement function.
    /// This overload is intentionally TMP-independent so editor tests and other UI systems can reuse it.
    /// </summary>
    public static string Wrap(string text, float maxWidth, Func<string, float> measureWidth, SmartTextWrapOptions options = default)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f || measureWidth == null)
            return text ?? string.Empty;

        SmartTextWrapOptions resolved = SmartTextWrapOptions.Resolve(options);
        if (!resolved.Enabled)
            return text;

        string normalized = NormalizeLineBreaks(text);
        string[] paragraphs = resolved.PreserveExistingLineBreaks
            ? normalized.Split(LineBreak)
            : new[] { normalized.Replace(LineBreak, ' ') };

        Dictionary<string, float> widthCache = new Dictionary<string, float>(StringComparer.Ordinal);
        StringBuilder result = new StringBuilder(normalized.Length + 8);

        for (int i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
                result.Append(LineBreak);

            result.Append(WrapParagraph(paragraphs[i], maxWidth, measureWidth, widthCache, resolved));
        }

        return result.ToString();
    }

    private static string WrapParagraph(
        string paragraph,
        float maxWidth,
        Func<string, float> measureWidth,
        Dictionary<string, float> widthCache,
        SmartTextWrapOptions options)
    {
        string[] tokens = Tokenize(paragraph, options.CollapseWhitespace);
        if (tokens.Length == 0)
            return string.Empty;

        StringBuilder result = new StringBuilder(paragraph.Length + 8);
        string currentLine = string.Empty;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            float tokenWidth = Measure(token, measureWidth, widthCache);

            if (tokenWidth > maxWidth)
            {
                AppendLongToken(token, maxWidth, measureWidth, widthCache, result, ref currentLine);
                continue;
            }

            if (currentLine.Length == 0)
            {
                currentLine = token;
                continue;
            }

            string candidate = currentLine + Space + token;
            if (Measure(candidate, measureWidth, widthCache) > maxWidth)
            {
                AppendLineBreak(result);
                result.Append(currentLine);
                currentLine = token;
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (currentLine.Length > 0)
        {
            AppendLineBreak(result);
            result.Append(currentLine);
        }

        return result.ToString();
    }

    private static void AppendLongToken(
        string token,
        float maxWidth,
        Func<string, float> measureWidth,
        Dictionary<string, float> widthCache,
        StringBuilder result,
        ref string currentLine)
    {
        int index = 0;
        while (index < token.Length)
        {
            string chunk = TakeLargestFittingChunk(token, index, maxWidth, measureWidth, widthCache);

            if (currentLine.Length == 0)
            {
                currentLine = chunk;
                index += chunk.Length;
                continue;
            }

            string candidate = currentLine + Space + chunk;
            if (Measure(candidate, measureWidth, widthCache) > maxWidth)
            {
                AppendLineBreak(result);
                result.Append(currentLine);
                currentLine = chunk;
            }
            else
            {
                currentLine = candidate;
            }

            index += chunk.Length;
        }
    }

    private static string TakeLargestFittingChunk(
        string token,
        int startIndex,
        float maxWidth,
        Func<string, float> measureWidth,
        Dictionary<string, float> widthCache)
    {
        int remaining = token.Length - startIndex;
        int low = 1;
        int high = remaining;
        int best = 1;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string candidate = token.Substring(startIndex, mid);
            if (Measure(candidate, measureWidth, widthCache) <= maxWidth)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return token.Substring(startIndex, best);
    }

    private static string[] Tokenize(string paragraph, bool collapseWhitespace)
    {
        if (string.IsNullOrWhiteSpace(paragraph))
            return Array.Empty<string>();

        if (collapseWhitespace)
            return paragraph.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        return paragraph.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    }

    private static float Measure(string text, Func<string, float> measureWidth, Dictionary<string, float> widthCache)
    {
        if (widthCache.TryGetValue(text, out float cached))
            return cached;

        float width = Mathf.Max(0f, measureWidth(text));
        widthCache[text] = width;
        return width;
    }

    private static void AppendLineBreak(StringBuilder builder)
    {
        if (builder.Length > 0)
            builder.Append(LineBreak);
    }

    private static string NormalizeLineBreaks(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}

/// <summary>
/// Options for SmartTextWrapper.
/// The default struct value resolves to Default, so callers can omit this argument in most cases.
/// </summary>
public readonly struct SmartTextWrapOptions
{
    public static readonly SmartTextWrapOptions Default = new SmartTextWrapOptions(
        enabled: true,
        preserveExistingLineBreaks: true,
        collapseWhitespace: true);

    private readonly bool _configured;

    public SmartTextWrapOptions(
        bool enabled = true,
        bool preserveExistingLineBreaks = true,
        bool collapseWhitespace = true)
    {
        Enabled = enabled;
        PreserveExistingLineBreaks = preserveExistingLineBreaks;
        CollapseWhitespace = collapseWhitespace;
        _configured = true;
    }

    public bool Enabled { get; }
    public bool PreserveExistingLineBreaks { get; }
    public bool CollapseWhitespace { get; }

    public static SmartTextWrapOptions Resolve(SmartTextWrapOptions options)
    {
        return options._configured ? options : Default;
    }
}
