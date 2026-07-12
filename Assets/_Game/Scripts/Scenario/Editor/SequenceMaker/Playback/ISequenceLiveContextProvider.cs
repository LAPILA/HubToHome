using UnityEngine;

public sealed class SequenceLiveContext
{
    public SequenceLiveContext(
        string label,
        MonoBehaviour coroutineHost,
        ActionDirector director,
        ActionExecutionContext executionContext)
    {
        Label = label ?? string.Empty;
        CoroutineHost = coroutineHost;
        Director = director;
        ExecutionContext = executionContext;
    }

    public string Label { get; }
    public MonoBehaviour CoroutineHost { get; }
    public ActionDirector Director { get; }
    public ActionExecutionContext ExecutionContext { get; }
}

public interface ISequenceLiveContextProvider
{
    int Priority { get; }

    bool TryCreate(
        BattleScenarioData battle,
        ActionSequenceAsset sequence,
        out SequenceLiveContext context,
        out string error);
}
