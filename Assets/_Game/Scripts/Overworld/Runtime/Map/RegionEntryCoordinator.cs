using System;
using System.Collections.Generic;
using UnityEngine;

public enum RegionEntryStatus
{
    NotStarted,
    Preparing,
    Succeeded,
    MissingDependency,
    InvalidRoomConfiguration,
    RoomLoadFailed,
    ArrivalFailed,
    CameraBindingFailed
}

/// <summary>
/// Builds the destination Room and applies arrival state before SceneLoader reveals the scene.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class RegionEntryCoordinator : MonoBehaviour, ISceneRevealGateFailureSource
{
    [Header("Region Entry")]
    [SerializeField] private RoomContainer _roomContainer;
    [SerializeField] private PlayerController _player;
    [SerializeField] private RoomDefinition _defaultRoom;
    [SerializeField] private List<RoomDefinition> _rooms = new List<RoomDefinition>();
    [SerializeField] private bool _prepareOnAwake = true;
    [SerializeField] private bool _requireCameraBinding = true;

    private bool _isReadyToReveal;

    public bool IsReadyToReveal => _isReadyToReveal;
    public bool HasFailed => IsTerminalFailure(Status);
    public string FailureReason => HasFailed ? LastError : string.Empty;
    public RegionEntryStatus Status { get; private set; } = RegionEntryStatus.NotStarted;
    public string LastError { get; private set; } = string.Empty;
    public RoomDefinition ResolvedRoom { get; private set; }
    public bool UsedDefaultFallback { get; private set; }

    private void Awake()
    {
        if (_prepareOnAwake)
            TryPrepare(out _);
        else
            _isReadyToReveal = true;
    }

    public void Configure(
        RoomContainer roomContainer,
        PlayerController player,
        RoomDefinition defaultRoom,
        IEnumerable<RoomDefinition> rooms,
        bool requireCameraBinding = true)
    {
        _roomContainer = roomContainer;
        _player = player;
        _defaultRoom = defaultRoom;
        _requireCameraBinding = requireCameraBinding;
        _rooms.Clear();
        if (rooms != null)
            _rooms.AddRange(rooms);
    }

    public bool TryPrepare(out string error)
    {
        _isReadyToReveal = false;
        Status = RegionEntryStatus.Preparing;
        LastError = string.Empty;
        ResolvedRoom = null;
        UsedDefaultFallback = false;

        GlobalDataManager global = GlobalDataManager.Instance
            ?? FindFirstObjectByType<GlobalDataManager>(FindObjectsInactive.Include);
        if (global == null)
            return Fail(RegionEntryStatus.MissingDependency, "GlobalDataManager가 없습니다.", out error);

        if (_roomContainer == null)
            _roomContainer = FindFirstObjectByType<RoomContainer>(FindObjectsInactive.Include);
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (_roomContainer == null || _player == null)
            return Fail(RegionEntryStatus.MissingDependency, "RoomContainer 또는 Player가 없습니다.", out error);

        if (!TryResolveRoom(global.CurrentRoomId, out RoomDefinition roomDefinition, out error))
            return Fail(RegionEntryStatus.InvalidRoomConfiguration, error, out error);

        ResolvedRoom = roomDefinition;
        var arrivalRequest = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Room,
            TargetRoom = roomDefinition,
            TargetSpawnPointId = global.SpawnPointId,
            FacingAfterEnter = ResolveFacing(global.LookingDir),
            FallbackPosition = new Vector2(global.SpawnX, global.SpawnY),
            UseFallbackPosition = global.SpawnFallbackAllowed || string.IsNullOrWhiteSpace(global.SpawnPointId),
            FadeDuration = 0f
        };

        string arrivalValidationError = string.Empty;
        bool roomLoaded = _roomContainer.TryLoadRoom(
            roomDefinition,
            candidate => MapTransitionService.TryValidateArrival(
                arrivalRequest,
                candidate != null ? candidate.transform : null,
                out arrivalValidationError),
            out RoomInstance room,
            out string roomError);
        if (!roomLoaded || room == null)
        {
            string message = string.IsNullOrEmpty(arrivalValidationError)
                ? roomError
                : arrivalValidationError;
            return Fail(RegionEntryStatus.RoomLoadFailed, message, out error);
        }

        if (!MapTransitionService.TryApplyArrival(
                _player,
                arrivalRequest,
                room.transform,
                out string arrivalError))
        {
            return Fail(RegionEntryStatus.ArrivalFailed, arrivalError, out error);
        }

        bool cameraBound = room.OnRoomEntered(_player);
        if (_requireCameraBinding && !cameraBound)
            return Fail(RegionEntryStatus.CameraBindingFailed, "오버월드 카메라 연결에 실패했습니다.", out error);

        global.CurrentRoomId = roomDefinition.RoomId;
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.IsValid())
            global.SpawnScene = activeScene.name;

        Status = RegionEntryStatus.Succeeded;
        LastError = string.Empty;
        _isReadyToReveal = true;
        error = string.Empty;
        return true;
    }

    private bool TryResolveRoom(
        string requestedRoomId,
        out RoomDefinition roomDefinition,
        out string error)
    {
        roomDefinition = null;
        if (_defaultRoom == null || !_defaultRoom.IsValid)
        {
            error = "기본 RoomDefinition이 유효하지 않습니다.";
            return false;
        }

        var byId = new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);
        if (!TryAddRoom(byId, _defaultRoom, out error))
            return false;

        for (int i = 0; i < _rooms.Count; i++)
        {
            RoomDefinition candidate = _rooms[i];
            if (candidate == null)
                continue;
            if (!TryAddRoom(byId, candidate, out error))
                return false;
        }

        string normalizedId = Normalize(requestedRoomId);
        if (!string.IsNullOrEmpty(normalizedId)
            && byId.TryGetValue(normalizedId, out roomDefinition))
        {
            error = string.Empty;
            return true;
        }

        roomDefinition = _defaultRoom;
        UsedDefaultFallback = !string.IsNullOrEmpty(normalizedId);
        if (UsedDefaultFallback)
        {
            Debug.LogWarning(
                "[RegionEntryCoordinator] 저장된 Room ID를 찾지 못해 기본 Room을 사용합니다. RoomId="
                + normalizedId,
                this);
        }

        error = string.Empty;
        return true;
    }

    private static bool TryAddRoom(
        Dictionary<string, RoomDefinition> byId,
        RoomDefinition room,
        out string error)
    {
        if (room == null || !room.IsValid)
        {
            error = "Region RoomDefinition 중 유효하지 않은 항목이 있습니다.";
            return false;
        }

        string roomId = Normalize(room.RoomId);
        if (byId.TryGetValue(roomId, out RoomDefinition existing))
        {
            if (ReferenceEquals(existing, room))
            {
                error = string.Empty;
                return true;
            }

            error = "중복 Room ID가 있습니다. RoomId=" + roomId;
            return false;
        }

        byId.Add(roomId, room);
        error = string.Empty;
        return true;
    }

    private bool Fail(RegionEntryStatus status, string message, out string error)
    {
        Status = status;
        LastError = string.IsNullOrWhiteSpace(message) ? "Region 진입 준비에 실패했습니다." : message;
        _isReadyToReveal = false;
        error = LastError;
        Debug.LogError("[RegionEntryCoordinator] " + LastError, this);
        return false;
    }

    private static bool IsTerminalFailure(RegionEntryStatus status)
    {
        return status == RegionEntryStatus.MissingDependency
            || status == RegionEntryStatus.InvalidRoomConfiguration
            || status == RegionEntryStatus.RoomLoadFailed
            || status == RegionEntryStatus.ArrivalFailed
            || status == RegionEntryStatus.CameraBindingFailed;
    }

    private static FacingDirection ResolveFacing(int value)
    {
        return value >= (int)FacingDirection.Down && value <= (int)FacingDirection.Right
            ? (FacingDirection)value
            : FacingDirection.Keep;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}