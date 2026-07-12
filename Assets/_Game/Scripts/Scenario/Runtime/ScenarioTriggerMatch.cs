public sealed class ScenarioTriggerMatch
{
    public ScenarioTriggerMatch(
        string ruleId,
        string sequenceId,
        ScenarioTriggerTiming timing,
        string checkpointId,
        string targetInputsJson,
        ScenarioEventData sourceEvent)
        : this(
            ruleId,
            sequenceId,
            timing,
            checkpointId,
            targetInputsJson,
            ruleId,
            ScenarioTriggerOnceScope.Always,
            sourceEvent)
    {
    }

    public ScenarioTriggerMatch(
        string ruleId,
        string sequenceId,
        ScenarioTriggerTiming timing,
        string checkpointId,
        string targetInputsJson,
        string historyKey,
        ScenarioTriggerOnceScope onceScope,
        ScenarioEventData sourceEvent)
    {
        RuleId = ruleId ?? string.Empty;
        SequenceId = sequenceId ?? string.Empty;
        Timing = timing;
        CheckpointId = checkpointId ?? string.Empty;
        TargetInputsJson = string.IsNullOrWhiteSpace(targetInputsJson) ? "{}" : targetInputsJson;
        HistoryKey = historyKey ?? string.Empty;
        OnceScope = onceScope;
        SourceEvent = sourceEvent;
    }

    public string RuleId { get; }
    public string SequenceId { get; }
    public ScenarioTriggerTiming Timing { get; }
    public string CheckpointId { get; }
    public string TargetInputsJson { get; }
    public string HistoryKey { get; }
    public ScenarioTriggerOnceScope OnceScope { get; }
    public ScenarioEventData SourceEvent { get; }
}
