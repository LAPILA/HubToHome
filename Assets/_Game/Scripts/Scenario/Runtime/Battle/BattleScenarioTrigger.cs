public sealed class BattleScenarioTrigger
{
    public BattleScenarioTrigger(
        string ruleId,
        string sequenceId,
        BattleRuleTiming timing,
        BattleEventData sourceEvent)
    {
        RuleId = ruleId ?? string.Empty;
        SequenceId = sequenceId ?? string.Empty;
        Timing = timing;
        ScenarioTiming = BattleTriggerRuleCompatibilityMapper.ToScenarioTiming(timing);
        SourceEvent = sourceEvent;
        ScenarioEvent = sourceEvent?.ToScenarioEvent();
        TargetInputsJson = "{}";
        CheckpointId = string.Empty;
        HistoryKey = ruleId ?? string.Empty;
        OnceScope = ScenarioTriggerOnceScope.Always;
    }

    public BattleScenarioTrigger(
        ScenarioTriggerMatch match,
        BattleEventData sourceEvent = null)
    {
        RuleId = match?.RuleId ?? string.Empty;
        SequenceId = match?.SequenceId ?? string.Empty;
        ScenarioTiming = match != null ? match.Timing : ScenarioTriggerTiming.Immediate;
        Timing = BattleTriggerRuleCompatibilityMapper.ToBattleTiming(ScenarioTiming);
        CheckpointId = match?.CheckpointId ?? string.Empty;
        TargetInputsJson = match?.TargetInputsJson ?? "{}";
        HistoryKey = match?.HistoryKey ?? string.Empty;
        OnceScope = match != null ? match.OnceScope : ScenarioTriggerOnceScope.Always;
        ScenarioEvent = match?.SourceEvent;
        SourceEvent = sourceEvent;
    }

    public string RuleId { get; }
    public string SequenceId { get; }
    public BattleRuleTiming Timing { get; }
    public ScenarioTriggerTiming ScenarioTiming { get; }
    public string CheckpointId { get; }
    public string TargetInputsJson { get; }
    public string HistoryKey { get; }
    public ScenarioTriggerOnceScope OnceScope { get; }
    public ScenarioEventData ScenarioEvent { get; }
    public BattleEventData SourceEvent { get; }
}
