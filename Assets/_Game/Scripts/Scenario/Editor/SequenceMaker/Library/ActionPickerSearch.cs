using System;
using System.Collections.Generic;

public enum ActionPickerCompatibility
{
    Compatible,
    Deprecated,
    Unavailable
}

public sealed class ActionPickerContext
{
    private readonly HashSet<string> _availableContexts;

    public ActionPickerContext(
        string primaryMode,
        IEnumerable<string> availableContexts = null)
    {
        PrimaryMode = Normalize(primaryMode);
        HasKnownContexts = availableContexts != null;
        _availableContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (availableContexts == null)
        {
            return;
        }

        foreach (string context in availableContexts)
        {
            string normalized = Normalize(context);
            if (!string.IsNullOrEmpty(normalized))
            {
                _availableContexts.Add(normalized);
            }
        }
    }

    public string PrimaryMode { get; }
    public bool HasKnownContexts { get; }

    public bool HasContext(string contextId)
    {
        return _availableContexts.Contains(Normalize(contextId));
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class ActionPickerSearchResult
{
    internal ActionPickerSearchResult(
        ActionCatalogEntry entry,
        int score,
        ActionPickerCompatibility compatibility,
        string compatibilityReason,
        IReadOnlyList<string> matchedFields)
    {
        Entry = entry;
        Score = score;
        Compatibility = compatibility;
        CompatibilityReason = compatibilityReason ?? string.Empty;
        MatchedFields = matchedFields ?? Array.Empty<string>();
    }

    public ActionCatalogEntry Entry { get; }
    public int Score { get; }
    public ActionPickerCompatibility Compatibility { get; }
    public string CompatibilityReason { get; }
    public IReadOnlyList<string> MatchedFields { get; }
    public bool CanSelect => Compatibility == ActionPickerCompatibility.Compatible;
}

public static class ActionPickerSearch
{
    public static IReadOnlyList<ActionPickerSearchResult> Search(
        ActionCatalogAsset catalog,
        string query,
        ActionPickerContext context)
    {
        var results = new List<ActionPickerSearchResult>();
        if (catalog?.Entries == null)
        {
            return results;
        }

        string[] tokens = Tokens(query);
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            ActionCatalogEntry entry = catalog.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            var matchedFields = new List<string>();
            int score = Score(entry, tokens, matchedFields);
            if (score < 0)
            {
                continue;
            }
            score += FullQueryBonus(entry, Normalize(query));

            EvaluateCompatibility(
                entry,
                context,
                out ActionPickerCompatibility compatibility,
                out string reason);
            results.Add(new ActionPickerSearchResult(
                entry,
                score,
                compatibility,
                reason,
                matchedFields));
        }

        results.Sort(Compare);
        return results;
    }

    public static void EvaluateCompatibility(
        ActionCatalogEntry entry,
        ActionPickerContext context,
        out ActionPickerCompatibility compatibility,
        out string reason)
    {
        compatibility = ActionPickerCompatibility.Compatible;
        reason = string.Empty;
        if (entry == null)
        {
            compatibility = ActionPickerCompatibility.Unavailable;
            reason = "Action Library 항목이 없습니다.";
            return;
        }

        if (entry.Disabled)
        {
            compatibility = ActionPickerCompatibility.Unavailable;
            reason = "새 시퀀스에서 사용이 중지된 액션입니다.";
            return;
        }

        string primaryMode = context?.PrimaryMode ?? string.Empty;
        if (!string.IsNullOrEmpty(primaryMode)
            && entry.AllowedPrimaryModes != null
            && entry.AllowedPrimaryModes.Count > 0
            && !Contains(entry.AllowedPrimaryModes, primaryMode))
        {
            compatibility = ActionPickerCompatibility.Unavailable;
            reason = "현재 모드 " + primaryMode + "에서 사용할 수 없습니다. 사용 가능: "
                + string.Join(", ", entry.AllowedPrimaryModes);
            return;
        }

        if (context != null
            && context.HasKnownContexts
            && entry.RequiredContexts != null)
        {
            var missing = new List<string>();
            for (int i = 0; i < entry.RequiredContexts.Count; i++)
            {
                string required = Normalize(entry.RequiredContexts[i]);
                if (!string.IsNullOrEmpty(required) && !context.HasContext(required))
                {
                    missing.Add(required);
                }
            }

            if (missing.Count > 0)
            {
                compatibility = ActionPickerCompatibility.Unavailable;
                reason = "필요한 실행 기능이 없습니다: " + string.Join(", ", missing);
                return;
            }
        }

        if (entry.Deprecated)
        {
            compatibility = ActionPickerCompatibility.Deprecated;
            reason = string.IsNullOrWhiteSpace(entry.ReplacementActionId)
                ? "이전 데이터 호환용 액션입니다."
                : "호환용 액션입니다. 대신 " + entry.ReplacementActionId + " 사용을 권장합니다.";
        }
    }

    private static int Score(
        ActionCatalogEntry entry,
        IReadOnlyList<string> tokens,
        List<string> matchedFields)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            int best = -1;
            string bestField = string.Empty;
            Consider(entry.ActionId, token, 1000, 820, 620, "ID", ref best, ref bestField);
            Consider(entry.DisplayNameKo, token, 950, 860, 700, "이름", ref best, ref bestField);
            Consider(entry.Category, token, 680, 560, 440, "카테고리", ref best, ref bestField);
            Consider(entry.Subcategory, token, 660, 540, 420, "하위 카테고리", ref best, ref bestField);
            Consider(entry.DescriptionKo, token, 520, 460, 360, "설명", ref best, ref bestField);
            Consider(entry.UsageKo, token, 500, 440, 340, "사용 시점", ref best, ref bestField);
            Consider(entry.ExampleYaml, token, 340, 300, 240, "예시", ref best, ref bestField);
            ConsiderList(entry.Tags, token, 760, 620, 520, "태그", ref best, ref bestField);
            ConsiderList(entry.Aliases, token, 880, 740, 600, "별칭", ref best, ref bestField);
            ConsiderParameters(entry.Parameters, token, ref best, ref bestField);
            if (best < 0)
            {
                return -1;
            }

            total += best;
            AddUnique(matchedFields, bestField);
        }

        return total;
    }

    private static void ConsiderParameters(
        IReadOnlyList<ActionCatalogParameter> parameters,
        string token,
        ref int best,
        ref string bestField)
    {
        if (parameters == null)
        {
            return;
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            ActionCatalogParameter parameter = parameters[i];
            if (parameter == null)
            {
                continue;
            }

            Consider(parameter.Name, token, 720, 600, 480, "파라미터", ref best, ref bestField);
            Consider(parameter.DisplayNameKo, token, 740, 620, 500, "파라미터", ref best, ref bestField);
            Consider(parameter.DescriptionKo, token, 420, 360, 300, "파라미터 설명", ref best, ref bestField);
        }
    }

    private static int FullQueryBonus(ActionCatalogEntry entry, string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }
        if (string.Equals(entry.ActionId, query, StringComparison.OrdinalIgnoreCase))
        {
            return 2400;
        }
        if (string.Equals(entry.DisplayNameKo, query, StringComparison.OrdinalIgnoreCase))
        {
            return 2200;
        }
        if (!string.IsNullOrWhiteSpace(entry.DisplayNameKo)
            && entry.DisplayNameKo.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 500;
        }
        return 0;
    }

    private static void ConsiderList(
        IReadOnlyList<string> values,
        string token,
        int exact,
        int starts,
        int contains,
        string field,
        ref int best,
        ref string bestField)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            Consider(values[i], token, exact, starts, contains, field, ref best, ref bestField);
        }
    }

    private static void Consider(
        string value,
        string token,
        int exact,
        int starts,
        int contains,
        string field,
        ref int best,
        ref string bestField)
    {
        string normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        int score = -1;
        if (string.Equals(normalized, token, StringComparison.OrdinalIgnoreCase))
        {
            score = exact;
        }
        else if (normalized.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            score = starts;
        }
        else if (normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score = contains;
        }

        if (score > best)
        {
            best = score;
            bestField = field;
        }
    }

    private static int Compare(ActionPickerSearchResult left, ActionPickerSearchResult right)
    {
        int compatibility = CompatibilityRank(left.Compatibility)
            .CompareTo(CompatibilityRank(right.Compatibility));
        if (compatibility != 0)
        {
            return compatibility;
        }

        int score = right.Score.CompareTo(left.Score);
        if (score != 0)
        {
            return score;
        }

        int category = StringComparer.OrdinalIgnoreCase.Compare(
            left.Entry.Category,
            right.Entry.Category);
        if (category != 0)
        {
            return category;
        }

        int subcategory = StringComparer.OrdinalIgnoreCase.Compare(
            left.Entry.Subcategory,
            right.Entry.Subcategory);
        return subcategory != 0
            ? subcategory
            : StringComparer.OrdinalIgnoreCase.Compare(
                DisplayName(left.Entry),
                DisplayName(right.Entry));
    }

    private static int CompatibilityRank(ActionPickerCompatibility value)
    {
        switch (value)
        {
            case ActionPickerCompatibility.Compatible: return 0;
            case ActionPickerCompatibility.Deprecated: return 1;
            default: return 2;
        }
    }

    private static string[] Tokens(string query)
    {
        return Normalize(query).Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool Contains(IReadOnlyList<string> values, string expected)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(Normalize(values[i]), expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string DisplayName(ActionCatalogEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry?.DisplayNameKo)
            ? entry.DisplayNameKo
            : entry?.ActionId ?? string.Empty;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!string.IsNullOrEmpty(value) && !values.Contains(value))
        {
            values.Add(value);
        }
    }
}
