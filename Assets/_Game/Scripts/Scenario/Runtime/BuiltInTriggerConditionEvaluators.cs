using System;
using Newtonsoft.Json.Linq;

public sealed class ValueEqualsTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.ValueEquals;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "path", out string path, out error)
            || !TriggerConditionParameters.TryGetRequiredToken(parameters, "value", out JToken expected, out error))
        {
            return false;
        }

        matched = context != null
            && context.TryGetValue(path, out JToken actual)
            && TriggerConditionParameters.ValuesEqual(actual, expected);
        return true;
    }
}

public sealed class NumberCompareTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.NumberCompare;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "path", out string path, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "operator", out string comparison, out error)
            || !TriggerConditionParameters.TryGetRequiredDouble(parameters, "value", out double expected, out error))
        {
            return false;
        }

        if (context == null
            || !context.TryGetValue(path, out JToken token)
            || !TriggerConditionParameters.TryConvertDouble(token, out double actual))
        {
            error = "Numeric value was not available at path '" + path + "'.";
            return false;
        }

        return TriggerConditionParameters.TryCompare(actual, expected, comparison, out matched, out error);
    }
}

public sealed class NumberCrossedBelowTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.NumberCrossedBelow;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "previousPath", out string previousPath, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "currentPath", out string currentPath, out error)
            || !TriggerConditionParameters.TryGetRequiredDouble(parameters, "threshold", out double threshold, out error))
        {
            return false;
        }

        if (context == null
            || !context.TryGetValue(previousPath, out JToken previousToken)
            || !context.TryGetValue(currentPath, out JToken currentToken)
            || !TriggerConditionParameters.TryConvertDouble(previousToken, out double previous)
            || !TriggerConditionParameters.TryConvertDouble(currentToken, out double current))
        {
            error = "Crossed-below condition requires numeric previous and current values.";
            return false;
        }

        matched = previous > threshold && current <= threshold;
        return true;
    }
}

public sealed class EventParticipantTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.EventParticipant;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "participant", out string participant, out error))
        {
            return false;
        }

        matched = context != null
            && context.TryGetValue("event.subject", out JToken subject)
            && subject.Type == JTokenType.String
            && string.Equals(subject.Value<string>(), participant, StringComparison.Ordinal);
        return true;
    }
}

public sealed class ModuleOutcomeTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.ModuleOutcome;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "module", out string module, out error))
        {
            return false;
        }

        if (context == null
            || !context.TryGetValue("event.module", out JToken moduleToken)
            || moduleToken.Type != JTokenType.String
            || !string.Equals(moduleToken.Value<string>(), module, StringComparison.Ordinal))
        {
            return true;
        }

        string expectedOutcome = parameters.Value<string>("outcome") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedOutcome))
        {
            matched = true;
            return true;
        }

        matched = context.TryGetValue("event.outcome", out JToken outcomeToken)
            && outcomeToken.Type == JTokenType.String
            && string.Equals(outcomeToken.Value<string>(), expectedOutcome.Trim(), StringComparison.Ordinal);
        return true;
    }
}

public sealed class EncounterMeetCountTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.EncounterMeetCount;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "operator", out string comparison, out error)
            || !TriggerConditionParameters.TryGetRequiredDouble(parameters, "value", out double expected, out error))
        {
            return false;
        }

        string path = parameters.Value<string>("path") ?? "memory.meetCount";
        if (context == null
            || !context.TryGetValue(path, out JToken token)
            || !TriggerConditionParameters.TryConvertDouble(token, out double actual))
        {
            error = "Encounter meet count was not available at path '" + path + "'.";
            return false;
        }

        return TriggerConditionParameters.TryCompare(actual, expected, comparison, out matched, out error);
    }
}

public sealed class FlagStateTriggerConditionEvaluator : ITriggerConditionEvaluator
{
    public string ConditionId => BuiltInTriggerConditionIds.FlagState;

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        if (!TriggerConditionParameters.TryParse(condition, out JObject parameters, out error)
            || !TriggerConditionParameters.TryGetRequiredString(parameters, "flag", out string flag, out error))
        {
            return false;
        }

        JToken expected = parameters["value"] ?? new JValue("true");
        matched = context != null
            && context.TryGetValue("flag." + flag, out JToken actual)
            && TriggerConditionParameters.ValuesEqual(actual, expected);
        return true;
    }
}

internal static class TriggerConditionParameters
{
    public static bool TryParse(
        ScenarioTriggerConditionNodeData condition,
        out JObject parameters,
        out string error)
    {
        parameters = null;
        error = string.Empty;
        try
        {
            parameters = string.IsNullOrWhiteSpace(condition?.ParametersJson)
                ? new JObject()
                : JObject.Parse(condition.ParametersJson);
            return true;
        }
        catch (Exception exception)
        {
            error = "Condition parameters must be a JSON object: " + exception.Message;
            return false;
        }
    }

    public static bool TryGetRequiredString(
        JObject parameters,
        string key,
        out string value,
        out string error)
    {
        value = parameters?.Value<string>(key)?.Trim() ?? string.Empty;
        error = string.Empty;
        if (!string.IsNullOrEmpty(value))
        {
            return true;
        }

        error = "Condition requires string parameter '" + key + "'.";
        return false;
    }

    public static bool TryGetRequiredToken(
        JObject parameters,
        string key,
        out JToken value,
        out string error)
    {
        value = parameters?[key];
        error = string.Empty;
        if (value != null)
        {
            value = value.DeepClone();
            return true;
        }

        error = "Condition requires parameter '" + key + "'.";
        return false;
    }

    public static bool TryGetRequiredDouble(
        JObject parameters,
        string key,
        out double value,
        out string error)
    {
        value = 0d;
        error = string.Empty;
        if (parameters != null
            && parameters.TryGetValue(key, out JToken token)
            && TryConvertDouble(token, out value))
        {
            return true;
        }

        error = "Condition requires numeric parameter '" + key + "'.";
        return false;
    }

    public static bool TryConvertDouble(JToken token, out double value)
    {
        value = 0d;
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
        {
            return false;
        }

        value = token.Value<double>();
        return true;
    }

    public static bool ValuesEqual(JToken left, JToken right)
    {
        if (TryConvertDouble(left, out double leftNumber)
            && TryConvertDouble(right, out double rightNumber))
        {
            return Math.Abs(leftNumber - rightNumber) < 0.000001d;
        }

        return JToken.DeepEquals(left, right);
    }

    public static bool TryCompare(
        double actual,
        double expected,
        string comparison,
        out bool matched,
        out string error)
    {
        matched = false;
        error = string.Empty;
        switch ((comparison ?? string.Empty).Trim())
        {
            case "less": matched = actual < expected; return true;
            case "less_or_equal": matched = actual <= expected; return true;
            case "equal": matched = Math.Abs(actual - expected) < 0.000001d; return true;
            case "not_equal": matched = Math.Abs(actual - expected) >= 0.000001d; return true;
            case "greater_or_equal": matched = actual >= expected; return true;
            case "greater": matched = actual > expected; return true;
            default:
                error = "Unknown numeric comparison operator: " + comparison;
                return false;
        }
    }
}
