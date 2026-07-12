public interface ITriggerConditionEvaluator
{
    string ConditionId { get; }

    bool TryEvaluate(
        ScenarioTriggerConditionNodeData condition,
        ScenarioTriggerEvaluationContext context,
        out bool matched,
        out string error);
}
