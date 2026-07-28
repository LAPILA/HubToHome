using System;
using UnityEngine;

public interface ITrainTransitionRequester
{
    bool TryRequest(
        MapTransitionRequest request,
        PlayerController player,
        Action<SceneLoadResult> onCompleted);
}

public interface ITrainDepartureSequenceRunner
{
    ActionSequenceAsset Sequence { get; }
    bool IsPlaying { get; }
    bool TryPlay(Action<ActionExecutionResult> onFinished);
}

public interface ITrainTravelFeedback
{
    bool Show(UnityEngine.Object owner, DialogueData dialogue, string fallbackText);
}

public interface ITrainStopAccessPolicy
{
    bool IsUnlocked(TrainStopDefinition stop);
}

public sealed class MapTrainTransitionRequester : ITrainTransitionRequester
{
    public bool TryRequest(
        MapTransitionRequest request,
        PlayerController player,
        Action<SceneLoadResult> onCompleted)
    {
        MapTransitionService service = MapTransitionService.Instance;
        return service != null
            && service.TryRequestTransition(request, player, onCompleted);
    }
}

public sealed class SceneTrainDepartureSequenceRunner : ITrainDepartureSequenceRunner
{
    private readonly SceneActionSequencePlayer _player;

    public SceneTrainDepartureSequenceRunner(SceneActionSequencePlayer player)
    {
        _player = player;
    }

    public ActionSequenceAsset Sequence => _player != null ? _player.Sequence : null;
    public bool IsPlaying => _player != null && _player.IsPlaying;

    public bool TryPlay(Action<ActionExecutionResult> onFinished)
    {
        return _player != null && _player.TryPlay(onFinished);
    }
}

public sealed class AreaMarkerTrainTravelFeedback : ITrainTravelFeedback
{
    public bool Show(UnityEngine.Object owner, DialogueData dialogue, string fallbackText)
    {
        return AreaMarkerRuntimeService.TryStartDialogue(
            owner,
            dialogue,
            fallbackText,
            null,
            EmotionType.Normal);
    }
}

public sealed class GlobalDataTrainStopAccessPolicy : ITrainStopAccessPolicy
{
    private readonly GlobalDataManager _global;

    public GlobalDataTrainStopAccessPolicy(GlobalDataManager global)
    {
        _global = global;
    }

    public bool IsUnlocked(TrainStopDefinition stop)
    {
        return stop != null && stop.IsUnlocked(_global);
    }
}