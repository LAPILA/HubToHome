using System;
using UnityEngine;

public sealed class TrainTravelSession
{
    private readonly TrainNetworkDefinition _network;
    private readonly ITrainDepartureSequenceRunner _sequenceRunner;
    private readonly ITrainTransitionRequester _transitionRequester;
    private readonly ITrainTravelStateStore _stateStore;
    private readonly ITrainStopAccessPolicy _stopAccessPolicy;
    private readonly ITrainTravelFeedback _feedback;
    private readonly float _fadeDuration;
    private readonly string _requestFailedFallback;

    private int _generation;
    private bool _isBusy;
    private TrainTravelStateSnapshot _activeSnapshot;

    public TrainTravelSession(
        TrainNetworkDefinition network,
        ITrainDepartureSequenceRunner sequenceRunner,
        ITrainTransitionRequester transitionRequester,
        ITrainTravelStateStore stateStore,
        ITrainStopAccessPolicy stopAccessPolicy,
        ITrainTravelFeedback feedback,
        float fadeDuration,
        string requestFailedFallback)
    {
        _network = network;
        _sequenceRunner = sequenceRunner;
        _transitionRequester = transitionRequester;
        _stateStore = stateStore;
        _stopAccessPolicy = stopAccessPolicy;
        _feedback = feedback;
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _requestFailedFallback = string.IsNullOrWhiteSpace(requestFailedFallback)
            ? "* 열차가 지금은 출발할 수 없다."
            : requestFailedFallback.Trim();
    }

    public bool IsBusy => _isBusy;

    public bool TryTravel(
        TrainStopDefinition destination,
        PlayerController player,
        UnityEngine.Object feedbackOwner)
    {
        if (_isBusy)
            return false;
        if (!TryValidateDestination(destination, feedbackOwner))
            return false;

        TrainTravelStateSnapshot current = _stateStore.Capture(_network);
        if (string.Equals(current.StopId, destination.StopId, StringComparison.Ordinal))
        {
            _feedback?.Show(
                feedbackOwner,
                destination.AlreadyHereDialogue,
                "* 이미 이 정류소에 서 있다.");
            return false;
        }

        if (!_stopAccessPolicy.IsUnlocked(destination))
        {
            _feedback?.Show(
                feedbackOwner,
                destination.UnavailableDialogue,
                "* 아직 갈 수 없는 정류소다.");
            return false;
        }

        _isBusy = true;
        int generation = ++_generation;
        bool callbackObserved = false;
        bool started;
        try
        {
            started = _sequenceRunner.TryPlay(result =>
            {
                callbackObserved = true;
                CompleteSequence(
                    generation,
                    destination,
                    player,
                    feedbackOwner,
                    result);
            });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, feedbackOwner);
            FinishFailure(generation, feedbackOwner, restoreSnapshot: false);
            return false;
        }

        if (!started && !callbackObserved)
            FinishFailure(generation, feedbackOwner, restoreSnapshot: false);

        return started;
    }

    private bool TryValidateDestination(
        TrainStopDefinition destination,
        UnityEngine.Object feedbackOwner)
    {
        if (_network == null)
            return RejectConfiguration(feedbackOwner, "TrainNetworkDefinition이 없습니다.");

        if (!_network.TryValidateRuntime(out string error))
            return RejectConfiguration(feedbackOwner, error);
        if (destination == null || !_network.Contains(destination))
            return RejectConfiguration(feedbackOwner, "목적지가 Network에 등록되지 않았습니다.");
        if (!destination.TryValidateRuntime(out error))
            return RejectConfiguration(feedbackOwner, error);
        if (_sequenceRunner == null
            || !ReferenceEquals(_sequenceRunner.Sequence, _network.DepartureSequence))
        {
            return RejectConfiguration(feedbackOwner, "출발 Sequence 참조가 Network와 다릅니다.");
        }
        if (_transitionRequester == null
            || _stateStore == null
            || _stopAccessPolicy == null)
        {
            return RejectConfiguration(feedbackOwner, "열차 이동 서비스 구성이 누락됐습니다.");
        }

        return true;
    }

    private bool RejectConfiguration(UnityEngine.Object owner, string error)
    {
        Debug.LogError("[TrainTravelSession] " + error, owner);
        _feedback?.Show(owner, null, _requestFailedFallback);
        return false;
    }

    private void CompleteSequence(
        int generation,
        TrainStopDefinition destination,
        PlayerController player,
        UnityEngine.Object feedbackOwner,
        ActionExecutionResult result)
    {
        if (!IsCurrent(generation))
            return;

        if (result == null || result.Status != ActionExecutionStatus.Succeeded)
        {
            FinishFailure(generation, feedbackOwner, restoreSnapshot: false);
            return;
        }

        try
        {
            _activeSnapshot = _stateStore.Capture(_network);
            _stateStore.ApplyStop(_network, destination);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, feedbackOwner);
            FinishFailure(generation, feedbackOwner, restoreSnapshot: true);
            return;
        }

        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = destination.TargetSceneName,
            TargetRoomId = destination.TargetRoom.RoomId,
            TargetAreaId = destination.TargetRoom.RoomId,
            TargetSpawnPointId = destination.TargetSpawnPointId,
            FacingAfterEnter = destination.ArrivalFacing,
            FadeDuration = _fadeDuration
        };

        bool callbackObserved = false;
        bool accepted;
        try
        {
            accepted = _transitionRequester.TryRequest(
                request,
                player,
                sceneResult =>
                {
                    callbackObserved = true;
                    CompleteTransition(generation, feedbackOwner, sceneResult);
                });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, feedbackOwner);
            FinishFailure(generation, feedbackOwner, restoreSnapshot: true);
            return;
        }

        if (!accepted && !callbackObserved)
            FinishFailure(generation, feedbackOwner, restoreSnapshot: true);
    }

    private void CompleteTransition(
        int generation,
        UnityEngine.Object feedbackOwner,
        SceneLoadResult result)
    {
        if (!IsCurrent(generation))
            return;

        if (!SceneLoadResultUtility.WasDestinationActivated(result))
        {
            FinishFailure(generation, feedbackOwner, restoreSnapshot: true);
            return;
        }

        if (result != SceneLoadResult.Succeeded)
        {
            Debug.LogError(
                $"[TrainTravelSession] 목적 Scene은 활성화됐지만 준비에 실패했습니다. Result={result}",
                feedbackOwner);
        }

        Finish(generation);
    }

    private void FinishFailure(
        int generation,
        UnityEngine.Object feedbackOwner,
        bool restoreSnapshot)
    {
        if (!IsCurrent(generation))
            return;

        if (restoreSnapshot && _activeSnapshot != null)
        {
            try
            {
                _stateStore.Restore(_activeSnapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, feedbackOwner);
            }
        }

        _feedback?.Show(feedbackOwner, null, _requestFailedFallback);
        Finish(generation);
    }

    private void Finish(int generation)
    {
        if (!IsCurrent(generation))
            return;

        _activeSnapshot = null;
        _isBusy = false;
    }

    private bool IsCurrent(int generation)
    {
        return _isBusy && generation == _generation;
    }
}