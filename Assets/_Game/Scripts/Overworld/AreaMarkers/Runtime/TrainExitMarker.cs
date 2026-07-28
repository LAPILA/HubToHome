using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class TrainExitMarker : AreaMarkerBase
{
    [TitleGroup("열차 하차")]
    [SerializeField] private TrainNetworkDefinition _network;
    [TitleGroup("열차 하차")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;

    private bool _isTravelPending;

    public TrainNetworkDefinition Network => _network;

    public void Configure(TrainNetworkDefinition network, float fadeDuration)
    {
        _network = network;
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

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || _network == null
            || !_network.TryGetStop(global.CurrentTrainStopId, out TrainStopDefinition stop))
        {
            Debug.LogWarning("[TrainExitMarker] 현재 정류소를 찾을 수 없어 하차할 수 없습니다.", this);
            return;
        }

        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = stop.TargetSceneName,
            TargetRoomId = stop.TargetRoom.RoomId,
            TargetAreaId = stop.TargetRoom.RoomId,
            TargetSpawnPointId = stop.TargetSpawnPointId,
            FacingAfterEnter = stop.ArrivalFacing,
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
                if (result != SceneLoadResult.Succeeded)
                    Debug.LogWarning($"[TrainExitMarker] 하차 결과: {result}", this);
                _isTravelPending = false;
            });
        if (!accepted && !callbackObserved)
            _isTravelPending = false;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (_network == null)
            issues.Add("TrainNetworkDefinition이 없습니다.");
        else if (!_network.TryValidateRuntime(out string error))
            issues.Add(error);
        if (!string.Equals(areaId, "travel_train.main_car", System.StringComparison.Ordinal))
            issues.Add("areaId는 travel_train.main_car여야 합니다.");
        if (!string.Equals(markerId, "travel_train.main_car.train_exit", System.StringComparison.Ordinal))
            issues.Add("markerId는 travel_train.main_car.train_exit여야 합니다.");
    }
}