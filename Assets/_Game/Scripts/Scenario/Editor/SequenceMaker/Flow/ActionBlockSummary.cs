using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class ActionBlockQuickValue
{
    public ActionBlockQuickValue(string parameterName, string label, string value)
    {
        ParameterName = parameterName ?? string.Empty;
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
    }

    public string ParameterName { get; }
    public string Label { get; }
    public string Value { get; }
}

public sealed class ActionBlockSummary
{
    private static readonly Regex TemplateToken =
        new Regex("\\{([A-Za-z0-9_.-]+)\\}", RegexOptions.Compiled);

    private ActionBlockSummary()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Note { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string AccentHex { get; private set; } = string.Empty;
    public string IconId { get; private set; } = string.Empty;
    public bool IsStructural { get; private set; }
    public bool HasParameterError { get; private set; }
    public IReadOnlyList<ActionBlockQuickValue> QuickValues { get; private set; } =
        Array.Empty<ActionBlockQuickValue>();

    public static ActionBlockSummary Build(
        ScenarioActionData action,
        ActionCatalogEntry entry)
    {
        action = action ?? new ScenarioActionData();
        var result = new ActionBlockSummary
        {
            Title = ResolveTitle(action, entry),
            Note = Normalize(action.Note),
            Category = Normalize(entry?.Category),
            AccentHex = Normalize(entry?.AccentHex),
            IconId = Normalize(entry?.IconId),
            IsStructural = string.Equals(
                Normalize(action.ActionId),
                ActionDirector.ParallelActionId,
                StringComparison.Ordinal)
        };

        if (!TryParseParameters(action.ParametersJson, out JObject parameters))
        {
            result.HasParameterError = true;
            result.Summary = "파라미터 JSON 오류";
            result.QuickValues = BuildQuickValues(entry, new JObject());
            return result;
        }

        if (result.IsStructural)
        {
            result.Title = string.IsNullOrWhiteSpace(action.DesignerLabel)
                ? "동시 실행"
                : action.DesignerLabel.Trim();
            result.Category = string.IsNullOrEmpty(result.Category)
                ? "flow"
                : result.Category;
            result.Summary = BuildParallelSummary(action, parameters);
        }
        else
        {
            result.Summary = BuildSummary(action, entry, parameters);
        }

        result.QuickValues = BuildQuickValues(entry, parameters);
        return result;
    }

    private static string ResolveTitle(
        ScenarioActionData action,
        ActionCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(action.DesignerLabel))
        {
            return action.DesignerLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry?.DisplayNameKo))
        {
            return entry.DisplayNameKo.Trim();
        }

        return string.IsNullOrWhiteSpace(action.ActionId)
            ? "액션"
            : action.ActionId.Trim();
    }

    private static string BuildSummary(
        ScenarioActionData action,
        ActionCatalogEntry entry,
        JObject parameters)
    {
        string template = Normalize(entry?.SummaryTemplateKo);
        if (!string.IsNullOrEmpty(template))
        {
            return TemplateToken.Replace(template, match =>
            {
                string path = match.Groups[1].Value;
                JToken token = SelectToken(parameters, path);
                return token == null ? "값 없음" : FormatToken(token);
            });
        }

        string compact = parameters.Count > 0
            ? parameters.ToString(Formatting.None)
            : string.Empty;
        if (!string.IsNullOrEmpty(compact))
        {
            return compact.Length <= 110
                ? compact
                : compact.Substring(0, 107) + "...";
        }

        if (!string.IsNullOrWhiteSpace(entry?.UsageKo))
        {
            return entry.UsageKo.Trim();
        }

        return Normalize(action.ActionId);
    }

    private static string BuildParallelSummary(
        ScenarioActionData action,
        JObject parameters)
    {
        string policy = Normalize(parameters.Value<string>("policy"));
        string policyKo;
        switch (policy)
        {
            case "any":
                policyKo = "하나 성공 시 완료";
                break;
            case "race":
                policyKo = "먼저 끝난 결과 사용";
                break;
            default:
                policyKo = "모두 완료 대기";
                break;
        }

        int count = action.Children != null ? action.Children.Count : 0;
        return count + "개 블록 · " + policyKo;
    }

    private static IReadOnlyList<ActionBlockQuickValue> BuildQuickValues(
        ActionCatalogEntry entry,
        JObject parameters)
    {
        var result = new List<ActionBlockQuickValue>();
        if (entry?.Parameters == null)
        {
            return result;
        }

        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter == null || !parameter.QuickEdit)
            {
                continue;
            }

            JToken token = SelectToken(parameters, parameter.Name);
            string value = token != null ? FormatToken(token) : "값 없음";
            if (token != null
                && !IsBinding(token)
                && !string.IsNullOrWhiteSpace(parameter.UnitKo))
            {
                value += parameter.UnitKo.Trim();
            }

            result.Add(new ActionBlockQuickValue(
                parameter.Name,
                string.IsNullOrWhiteSpace(parameter.DisplayNameKo)
                    ? parameter.Name
                    : parameter.DisplayNameKo,
                value));
        }

        return result;
    }

    private static bool TryParseParameters(string json, out JObject parameters)
    {
        parameters = new JObject();
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            parameters = JObject.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JToken SelectToken(JObject parameters, string path)
    {
        if (parameters == null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        JToken current = parameters;
        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            current = current?[parts[i]];
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static string FormatToken(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return "값 없음";
        }

        if (IsBinding(token))
        {
            return "${" + token["$bind"].Value<string>() + "}";
        }

        switch (token.Type)
        {
            case JTokenType.String:
                return token.Value<string>() ?? string.Empty;
            case JTokenType.Integer:
                return token.Value<long>().ToString(CultureInfo.InvariantCulture);
            case JTokenType.Float:
                return token.Value<double>().ToString("0.###", CultureInfo.InvariantCulture);
            case JTokenType.Boolean:
                return token.Value<bool>() ? "켜짐" : "꺼짐";
            case JTokenType.Array:
                var values = new List<string>();
                foreach (JToken child in token.Children())
                {
                    values.Add(FormatToken(child));
                }

                return string.Join(", ", values);
            default:
                return token.ToString(Formatting.None);
        }
    }

    private static bool IsBinding(JToken token)
    {
        return token is JObject obj
            && obj["$bind"] != null
            && obj["$bind"].Type == JTokenType.String;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
