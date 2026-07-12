using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public enum TriggerRuleSimulationStatus
{
    Matched,
    NotMatched,
    Error
}

public sealed class TriggerConditionSimulationTrace
{
    public TriggerConditionSimulationTrace(
        string nodeId,
        string conditionId,
        bool evaluated,
        bool matched,
        string error)
    {
        NodeId = nodeId ?? string.Empty;
        ConditionId = conditionId ?? string.Empty;
        Evaluated = evaluated;
        Matched = matched;
        Error = error ?? string.Empty;
    }

    public string NodeId { get; }
    public string ConditionId { get; }
    public bool Evaluated { get; }
    public bool Matched { get; }
    public string Error { get; }
}

public sealed class TriggerRuleSimulationRequest
{
    public string EventId = string.Empty;
    public string PayloadJson = "{}";
    public string ContextValuesJson = "{}";
    public bool RuleAlreadyFired;
}

public sealed class TriggerRuleSimulationResult
{
    internal TriggerRuleSimulationResult(
        TriggerRuleSimulationStatus status,
        string message,
        string resolvedTargetInputsJson,
        IReadOnlyList<TriggerConditionSimulationTrace> traces)
    {
        Status = status;
        Message = message ?? string.Empty;
        ResolvedTargetInputsJson = resolvedTargetInputsJson ?? "{}";
        Traces = traces ?? Array.Empty<TriggerConditionSimulationTrace>();
    }

    public TriggerRuleSimulationStatus Status { get; }
    public string Message { get; }
    public string ResolvedTargetInputsJson { get; }
    public IReadOnlyList<TriggerConditionSimulationTrace> Traces { get; }
    public bool Matched => Status == TriggerRuleSimulationStatus.Matched;
}

public sealed class TriggerRuleSimulator
{
    private readonly TriggerConditionRegistry _conditions;
    private readonly ScenarioTriggerEvaluator _evaluator;

    public TriggerRuleSimulator(TriggerConditionRegistry conditions = null)
    {
        _conditions = conditions ?? TriggerConditionRegistry.CreateDefault();
        _evaluator = new ScenarioTriggerEvaluator(_conditions);
    }

    public TriggerRuleSimulationResult Simulate(
        ScenarioTriggerRuleData rule,
        TriggerRuleSimulationRequest request)
    {
        var traces = new List<TriggerConditionSimulationTrace>();
        if (rule == null)
        {
            return Error("시험할 Trigger Rule이 없습니다.", traces);
        }
        request = request ?? new TriggerRuleSimulationRequest();
        if (!TryObject(request.PayloadJson, out JObject payload, out string payloadError))
        {
            return Error("Event payload JSON 오류: " + payloadError, traces);
        }
        if (!TryObject(request.ContextValuesJson, out JObject values, out string valuesError))
        {
            return Error("Context values JSON 오류: " + valuesError, traces);
        }

        string eventId = string.IsNullOrWhiteSpace(request.EventId)
            ? rule.EventId
            : request.EventId.Trim();
        var scenarioEvent = new ScenarioEventData(eventId);
        foreach (JProperty property in payload.Properties())
        {
            scenarioEvent.SetPayloadValue(property.Name, property.Value);
        }
        var context = new ActionExecutionContext();
        ApplyContextValues(context, values, string.Empty);
        var evaluationContext = new ScenarioTriggerEvaluationContext(scenarioEvent, context);
        CollectTraces(rule.Conditions, evaluationContext, traces);

        var history = new SimulationHistory();
        if (request.RuleAlreadyFired && rule.Once != ScenarioTriggerOnceScope.Always)
        {
            history.MarkRuleFired(RuleKey(rule), rule.Once);
        }
        bool matched = _evaluator.TryEvaluate(
            rule,
            scenarioEvent,
            history,
            context,
            out ScenarioTriggerMatch match,
            out string error,
            false);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Error(error, traces);
        }
        if (!matched || match == null)
        {
            string message = request.RuleAlreadyFired && rule.Once != ScenarioTriggerOnceScope.Always
                ? "이미 실행된 once 규칙이라 이번 이벤트에서는 실행하지 않습니다."
                : !string.Equals(rule.EventId, eventId, StringComparison.Ordinal)
                    ? "시험 이벤트 ID가 규칙의 Event ID와 다릅니다."
                    : "이벤트는 맞지만 하나 이상의 조건이 성립하지 않았습니다.";
            return new TriggerRuleSimulationResult(
                TriggerRuleSimulationStatus.NotMatched,
                message,
                "{}",
                traces);
        }

        return new TriggerRuleSimulationResult(
            TriggerRuleSimulationStatus.Matched,
            "조건이 성립합니다. " + TriggerRuleSentenceFormatter.Timing(
                match.Timing,
                match.CheckpointId) + " 실행됩니다.",
            match.TargetInputsJson,
            traces);
    }

    private void CollectTraces(
        ScenarioTriggerConditionNodeData node,
        ScenarioTriggerEvaluationContext context,
        List<TriggerConditionSimulationTrace> traces)
    {
        if (node == null)
        {
            return;
        }
        bool evaluated = _conditions.TryEvaluate(
            node,
            context,
            out bool matched,
            out string error);
        traces.Add(new TriggerConditionSimulationTrace(
            node.NodeId,
            node.Kind == ScenarioConditionNodeKind.Group
                ? (node.GroupMode == ScenarioConditionGroupMode.All ? "all" : "any")
                : node.ConditionId,
            evaluated,
            matched,
            error));
        if (node.Children == null)
        {
            return;
        }
        for (int i = 0; i < node.Children.Count; i++)
        {
            CollectTraces(node.Children[i], context, traces);
        }
    }

    private static void ApplyContextValues(
        ActionExecutionContext context,
        JObject values,
        string prefix)
    {
        foreach (JProperty property in values.Properties())
        {
            string path = string.IsNullOrEmpty(prefix)
                ? property.Name
                : prefix + "." + property.Name;
            if (property.Value is JObject child)
            {
                ApplyContextValues(context, child, path);
            }
            else
            {
                context.SetValue(path, property.Value);
            }
        }
    }

    private static bool TryObject(string json, out JObject value, out string error)
    {
        try
        {
            value = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            value = new JObject();
            error = exception.Message;
            return false;
        }
    }

    private static TriggerRuleSimulationResult Error(
        string message,
        IReadOnlyList<TriggerConditionSimulationTrace> traces)
    {
        return new TriggerRuleSimulationResult(
            TriggerRuleSimulationStatus.Error,
            message,
            "{}",
            traces);
    }

    private static string RuleKey(ScenarioTriggerRuleData rule)
    {
        return !string.IsNullOrWhiteSpace(rule.RuleId)
            ? rule.RuleId.Trim()
            : rule.SequenceId?.Trim() ?? string.Empty;
    }

    private sealed class SimulationHistory : IScenarioTriggerHistory
    {
        private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

        public bool HasRuleFired(string ruleKey, ScenarioTriggerOnceScope scope)
        {
            return scope != ScenarioTriggerOnceScope.Always && _fired.Contains(Key(ruleKey, scope));
        }

        public void MarkRuleFired(string ruleKey, ScenarioTriggerOnceScope scope)
        {
            if (scope != ScenarioTriggerOnceScope.Always)
            {
                _fired.Add(Key(ruleKey, scope));
            }
        }

        private static string Key(string ruleKey, ScenarioTriggerOnceScope scope)
        {
            return scope + "|" + (ruleKey ?? string.Empty);
        }
    }
}
