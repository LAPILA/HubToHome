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
        SourceEvent = sourceEvent;
    }

    public string RuleId { get; }
    public string SequenceId { get; }
    public BattleRuleTiming Timing { get; }
    public BattleEventData SourceEvent { get; }
}
