using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class TriggerRuleSentenceFormatter
{
    private static readonly Regex Placeholder = new Regex(
        "\\{([A-Za-z0-9_.-]+)\\}",
        RegexOptions.Compiled);

    public static string Format(
        ScenarioTriggerRuleData rule,
        TriggerLibraryAsset library,
        Func<string, string> sequenceName = null)
    {
        if (rule == null)
        {
            return "규칙 없음";
        }

        ScenarioEventDefinition eventDefinition = library?.FindEvent(rule.EventId);
        string condition = FormatCondition(rule.Conditions, library);
        string when;
        if (string.IsNullOrWhiteSpace(condition) || condition == "추가 조건 없음")
        {
            when = FormatEventSentence(eventDefinition, rule.EventId);
        }
        else
        {
            string eventName = DisplayName(
                eventDefinition?.DisplayNameKo,
                rule.EventId,
                "이벤트");
            when = eventName + " 이벤트가 발생하고 " + condition + "이면";
        }

        string resolvedSequenceName = sequenceName?.Invoke(rule.SequenceId);
        string target = DisplayName(resolvedSequenceName, rule.SequenceId, "대상 없음");
        return when
            + "  →  " + target + " 시퀀스 실행"
            + "  ·  " + Timing(rule.Timing, rule.CheckpointId)
            + "  ·  " + Once(rule.Once);
    }

    public static string FormatCompact(
        ScenarioTriggerRuleData rule,
        TriggerLibraryAsset library)
    {
        if (rule == null)
        {
            return "규칙 없음";
        }
        ScenarioEventDefinition definition = library?.FindEvent(rule.EventId);
        string eventName = DisplayName(definition?.DisplayNameKo, rule.EventId, "이벤트 없음");
        string condition = FormatCondition(rule.Conditions, library);
        string when = condition == "추가 조건 없음"
            ? eventName
            : eventName + " + " + condition;
        return when + "  →  " + DisplayName(null, rule.SequenceId, "시퀀스 없음");
    }

    public static string FormatCondition(
        ScenarioTriggerConditionNodeData node,
        TriggerLibraryAsset library)
    {
        if (node == null)
        {
            return "조건 없음";
        }
        string result;
        if (node.Kind == ScenarioConditionNodeKind.Group)
        {
            var children = new List<string>();
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    string child = FormatCondition(node.Children[i], library);
                    if (!string.IsNullOrWhiteSpace(child))
                    {
                        children.Add(child);
                    }
                }
            }
            if (children.Count == 0)
            {
                result = "추가 조건 없음";
            }
            else
            {
                string separator = node.GroupMode == ScenarioConditionGroupMode.All
                    ? " 그리고 "
                    : " 또는 ";
                result = children.Count > 1
                    ? "(" + string.Join(separator, children) + ")"
                    : children[0];
            }
        }
        else
        {
            TriggerConditionDefinition definition = library?.FindCondition(node.ConditionId);
            JObject parameters = ParseObject(node.ParametersJson);
            string template = !string.IsNullOrWhiteSpace(definition?.SentenceTemplateKo)
                ? definition.SentenceTemplateKo
                : DisplayName(definition?.DisplayNameKo, node.ConditionId, "알 수 없는 조건");
            result = Interpolate(template, parameters, null);
            result = TrimConnective(result);
        }

        return node.Negate && result != "추가 조건 없음"
            ? "'" + result + "'가 아님"
            : result;
    }

    public static string Timing(ScenarioTriggerTiming timing, string checkpointId)
    {
        switch (timing)
        {
            case ScenarioTriggerTiming.AfterCurrentAction: return "현재 액션 종료 후";
            case ScenarioTriggerTiming.AfterCurrentSkill: return "현재 스킬 종료 후";
            case ScenarioTriggerTiming.AfterCurrentModule: return "현재 모듈 종료 후";
            case ScenarioTriggerTiming.Checkpoint:
                return "체크포인트 " + DisplayName(null, checkpointId, "미지정") + "에서";
            default: return "즉시";
        }
    }

    public static string Once(ScenarioTriggerOnceScope once)
    {
        switch (once)
        {
            case ScenarioTriggerOnceScope.Always: return "매번";
            case ScenarioTriggerOnceScope.EncounterMemory: return "이 만남에서 한 번";
            case ScenarioTriggerOnceScope.Save: return "저장 데이터에서 한 번";
            default: return "현재 실행에서 한 번";
        }
    }

    private static string FormatEventSentence(
        ScenarioEventDefinition definition,
        string eventId)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.SentenceTemplateKo))
        {
            return DisplayName(definition?.DisplayNameKo, eventId, "이벤트") + " 이벤트가 발생하면";
        }
        var payloadNames = new JObject();
        if (definition.Payload != null)
        {
            for (int i = 0; i < definition.Payload.Count; i++)
            {
                TriggerFieldDefinition field = definition.Payload[i];
                if (field != null && !string.IsNullOrWhiteSpace(field.FieldId))
                {
                    payloadNames[field.FieldId] = DisplayName(
                        field.DisplayNameKo,
                        field.FieldId,
                        field.FieldId);
                }
            }
        }
        return Interpolate(definition.SentenceTemplateKo, payloadNames, null);
    }

    private static string Interpolate(
        string template,
        JObject values,
        Func<string, string> missing)
    {
        return Placeholder.Replace(template ?? string.Empty, match =>
        {
            string key = match.Groups[1].Value;
            if (values != null && values.TryGetValue(key, out JToken token))
            {
                return FriendlyValue(key, token);
            }
            return missing?.Invoke(key) ?? key;
        });
    }

    private static string FriendlyValue(string key, JToken token)
    {
        if (ValueSourceField.TryReadBinding(token, out string path))
        {
            return "${" + path + "}";
        }
        if (token == null || token.Type == JTokenType.Null)
        {
            return "비어 있음";
        }
        string value = token.Type == JTokenType.String
            ? token.Value<string>() ?? string.Empty
            : token.ToString(Formatting.None);
        if (key == "operator")
        {
            return Operator(value);
        }
        return string.IsNullOrWhiteSpace(value) ? "미지정" : value;
    }

    private static string Operator(string value)
    {
        switch (value)
        {
            case "equal": return "같음";
            case "not_equal": return "다름";
            case "less": return "보다 작음";
            case "less_or_equal": return "이하";
            case "greater": return "보다 큼";
            case "greater_or_equal": return "이상";
            default: return value;
        }
    }

    private static JObject ParseObject(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
        }
        catch
        {
            return new JObject();
        }
    }

    private static string TrimConnective(string value)
    {
        string result = value?.Trim() ?? string.Empty;
        string[] endings = { "이고", "이며", "하고" };
        for (int i = 0; i < endings.Length; i++)
        {
            if (result.EndsWith(endings[i], StringComparison.Ordinal))
            {
                return result.Substring(0, result.Length - endings[i].Length);
            }
        }
        return result;
    }

    private static string DisplayName(string preferred, string fallback, string empty)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : (!string.IsNullOrWhiteSpace(fallback) ? fallback.Trim() : empty);
    }
}
