public interface IActionSequenceLiveContextSource
{
    int LiveContextPriority { get; }

    string LiveContextLabel { get; }

    bool TryCreateLiveContext(
        BattleScenarioData battle,
        ActionSequenceAsset sequence,
        out ActionDirector director,
        out ActionExecutionContext context,
        out string error);
}
