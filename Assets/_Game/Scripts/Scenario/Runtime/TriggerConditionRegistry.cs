using System;
using System.Collections.Generic;

public sealed class TriggerConditionRegistry
{
    private readonly Dictionary<string, ITriggerConditionEvaluator> _evaluators =
        new Dictionary<string, ITriggerConditionEvaluator>(StringComparer.Ordinal);

    public void Register(ITriggerConditionEvaluator evaluator)
    {
        if (evaluator == null || string.IsNullOrWhiteSpace(evaluator.ConditionId))
        {
            throw new ArgumentException("Trigger condition evaluator requires a stable condition ID.", nameof(evaluator));
        }

        _evaluators[evaluator.ConditionId.Trim()] = evaluator;
    }

    public bool TryEvaluate(
        ScenarioTriggerConditionNodeData node,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        matched = false;
        error = string.Empty;
        if (node == null)
        {
            error = "Trigger condition node is missing.";
            return false;
        }

        bool rawResult;
        if (node.Kind == ScenarioConditionNodeKind.Group)
        {
            if (!TryEvaluateGroup(node, context, out rawResult, out error))
            {
                return false;
            }
        }
        else
        {
            string conditionId = Normalize(node.ConditionId);
            if (!_evaluators.TryGetValue(conditionId, out ITriggerConditionEvaluator evaluator))
            {
                error = WithNode(node, "Unknown trigger condition ID: " + conditionId);
                return false;
            }

            try
            {
                if (!evaluator.TryEvaluate(node, context, out rawResult, out error))
                {
                    error = WithNode(node, error);
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = WithNode(node, "Trigger condition threw: " + exception.Message);
                return false;
            }
        }

        matched = node.Negate ? !rawResult : rawResult;
        return true;
    }

    public static TriggerConditionRegistry CreateDefault()
    {
        var registry = new TriggerConditionRegistry();
        registry.Register(new ValueEqualsTriggerConditionEvaluator());
        registry.Register(new NumberCompareTriggerConditionEvaluator());
        registry.Register(new NumberCrossedBelowTriggerConditionEvaluator());
        registry.Register(new EventParticipantTriggerConditionEvaluator());
        registry.Register(new ModuleOutcomeTriggerConditionEvaluator());
        registry.Register(new EncounterMeetCountTriggerConditionEvaluator());
        registry.Register(new FlagStateTriggerConditionEvaluator());
        return registry;
    }

    private bool TryEvaluateGroup(
        ScenarioTriggerConditionNodeData group,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error)
    {
        error = string.Empty;
        bool isAll = group.GroupMode == ScenarioConditionGroupMode.All;
        matched = isAll;
        if (group.Children == null || group.Children.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < group.Children.Count; i++)
        {
            if (!TryEvaluate(group.Children[i], context, out bool childMatched, out error))
            {
                return false;
            }

            if (isAll && !childMatched)
            {
                matched = false;
                return true;
            }

            if (!isAll && childMatched)
            {
                matched = true;
                return true;
            }
        }

        return true;
    }

    private static string WithNode(ScenarioTriggerConditionNodeData node, string message)
    {
        string nodeId = node == null || string.IsNullOrWhiteSpace(node.NodeId)
            ? "unassigned"
            : node.NodeId.Trim();
        return "Condition node '" + nodeId + "': " + message;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
