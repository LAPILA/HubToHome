using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class ScenarioTriggerEvaluator
{
    private readonly TriggerConditionRegistry _conditions;

    public ScenarioTriggerEvaluator(TriggerConditionRegistry conditions = null)
    {
        _conditions = conditions ?? TriggerConditionRegistry.CreateDefault();
    }

    public bool TryEvaluate(
        ScenarioTriggerRuleData rule,
        ScenarioEventData scenarioEvent,
        IScenarioTriggerHistory history,
        ActionExecutionContext values,
        out ScenarioTriggerMatch match,
        out string error,
        bool commitHistory = true)
    {
        match = null;
        error = string.Empty;
        if (rule == null || scenarioEvent == null || rule.Disabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.EventId)
            || !string.Equals(rule.EventId.Trim(), scenarioEvent.EventId, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.SequenceId))
        {
            error = WithRule(rule, "Target Sequence ID is required.");
            return false;
        }

        if (rule.Timing == ScenarioTriggerTiming.Checkpoint
            && string.IsNullOrWhiteSpace(rule.CheckpointId))
        {
            error = WithRule(rule, "Checkpoint timing requires CheckpointId.");
            return false;
        }

        string ruleKey = RuleKey(rule);
        if (rule.Once != ScenarioTriggerOnceScope.Always)
        {
            if (history == null)
            {
                error = WithRule(rule, "Trigger history is required for once scope " + rule.Once + ".");
                return false;
            }

            if (history.HasRuleFired(ruleKey, rule.Once))
            {
                return false;
            }
        }

        var context = new ScenarioTriggerEvaluationContext(scenarioEvent, values);
        if (!_conditions.TryEvaluate(rule.Conditions, context, out bool conditionsMatched, out error))
        {
            error = WithRule(rule, error);
            return false;
        }

        if (!conditionsMatched)
        {
            error = string.Empty;
            return false;
        }

        if (!TryResolveTargetInputs(rule.TargetInputsJson, context, out string targetInputsJson, out error))
        {
            error = WithRule(rule, error);
            return false;
        }

        match = new ScenarioTriggerMatch(
            rule.RuleId,
            rule.SequenceId,
            rule.Timing,
            rule.CheckpointId,
            targetInputsJson,
            ruleKey,
            rule.Once,
            scenarioEvent);
        if (commitHistory)
        {
            history?.MarkRuleFired(ruleKey, rule.Once);
        }

        return true;
    }

    private static bool TryResolveTargetInputs(
        string targetInputsJson,
        ScenarioTriggerEvaluationContext context,
        out string resolvedJson,
        out string error)
    {
        resolvedJson = "{}";
        error = string.Empty;
        JObject inputs;
        try
        {
            inputs = string.IsNullOrWhiteSpace(targetInputsJson)
                ? new JObject()
                : JObject.Parse(targetInputsJson);
        }
        catch (Exception exception)
        {
            error = "Target inputs must be a JSON object: " + exception.Message;
            return false;
        }

        ActionExecutionContext bindingValues = context.Values.CreateChild(
            new ActionExecutionHandle("trigger_input_binding"));
        JObject payload = context.Event?.Payload;
        if (payload != null)
        {
            foreach (JProperty property in payload.Properties())
            {
                bindingValues.SetValue("event." + property.Name, property.Value);
            }
        }

        if (!ScenarioValueResolver.TryResolveToken(inputs, bindingValues, out JToken resolved, out error)
            || !(resolved is JObject resolvedObject))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "Resolved target inputs must remain a JSON object.";
            }

            return false;
        }

        resolvedJson = resolvedObject.ToString(Formatting.None);
        return true;
    }

    private static string RuleKey(ScenarioTriggerRuleData rule)
    {
        return !string.IsNullOrWhiteSpace(rule.RuleId)
            ? rule.RuleId.Trim()
            : rule.SequenceId.Trim();
    }

    private static string WithRule(ScenarioTriggerRuleData rule, string message)
    {
        string id = rule == null || string.IsNullOrWhiteSpace(rule.RuleId)
            ? "unassigned"
            : rule.RuleId.Trim();
        return "Trigger Rule '" + id + "': " + (message ?? string.Empty);
    }
}
