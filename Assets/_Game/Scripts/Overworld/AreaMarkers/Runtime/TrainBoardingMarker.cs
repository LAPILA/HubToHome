using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class TrainBoardingMarker : AreaMarkerBase
{
    [TitleGroup("열차 승차")]
    [SerializeField] private TrainNetworkDefinition _network;
    [TitleGroup("열차 승차")]
    [SerializeField] private TrainStopDefinition _stop;
    [TitleGroup("열차 승차")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;

    private bool _isTravelPending;

    public TrainNetworkDefinition Network => _network;
    public TrainStopDefinition Stop => _stop;

    public void Configure(
        TrainNetworkDefinition network,
        TrainStopDefinition stop,
        float fadeDuration)
    {
        _network = network;
        _stop = stop;
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        EnsureDefaults();
    }

    protected override void EnsureDefaults()
    {
        markerType = AreaMarkerType.Sublocation;
        isOneShot = false;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.EnsureDefaults();
    }

    public override bool CanInteract(PlayerController player)
    {
        return !_isTravelPending && base.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;

        if (!TryValidateTravel(out string error))
        {
            Debug.LogError("[TrainBoardingMarker] " + error, this);
            return;
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        var stateStore = new GlobalDataTrainTravelStateStore(global);
        TrainTravelStateSnapshot snapshot = stateStore.Capture(_network);
        stateStore.ApplyStop(_network, _stop);

        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = _network.TrainSceneName,
            TargetRoomId = _network.TrainRoom.RoomId,
            TargetAreaId = _network.TrainRoom.RoomId,
            TargetSpawnPointId = _network.TrainEntrySpawnPointId,
            FacingAfterEnter = FacingDirection.Keep,
            FadeDuration = _fadeDuration
        };

        _isTravelPending = true;
        bool callbackObserved = false;
        bool accepted = new MapTrainTransitionRequester().TryRequest(
            request,
            player,
            result =>
            {
                callbackObserved = true;
                if (!SceneLoadResultUtility.WasDestinationActivated(result))
                    stateStore.Restore(snapshot);
                else if (result != SceneLoadResult.Succeeded)
                    Debug.LogError($"[TrainBoardingMarker] 열차 Scene 준비 실패: {result}", this);
                _isTravelPending = false;
            });

        if (!accepted && !callbackObserved)
        {
            stateStore.Restore(snapshot);
            _isTravelPending = false;
        }
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (_network == null)
            issues.Add("TrainNetworkDefinition이 없습니다.");
        else if (!_network.TryValidateRuntime(out string networkError))
            issues.Add(networkError);
        if (_stop == null)
            issues.Add("출발 정류소가 없습니다.");
        else if (_network != null && !_network.Contains(_stop))
            issues.Add("출발 정류소가 Network에 등록되지 않았습니다.");
        else if (!_stop.TryValidateRuntime(out string stopError))
            issues.Add(stopError);
        if (_stop != null
            && _stop.TargetRoom != null
            && !string.Equals(areaId, _stop.TargetRoom.RoomId, System.StringComparison.Ordinal))
        {
            issues.Add("areaId가 출발 정류소 Room ID와 다릅니다.");
        }
        if (!string.IsNullOrEmpty(areaId)
            && !string.Equals(markerId, areaId + ".train_boarding", System.StringComparison.Ordinal))
        {
            issues.Add("markerId는 '<areaId>.train_boarding'이어야 합니다.");
        }
    }

    private bool TryValidateTravel(out string error)
    {
        if (GlobalDataManager.Instance == null)
        {
            error = "GlobalDataManager가 없습니다.";
            return false;
        }
        if (_network == null)
        {
            error = "TrainNetworkDefinition이 없습니다.";
            return false;
        }
        if (!_network.TryValidateRuntime(out error))
            return false;
        if (_stop == null || !_network.Contains(_stop))
        {
            error = "출발 정류소가 Network에 등록되지 않았습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}