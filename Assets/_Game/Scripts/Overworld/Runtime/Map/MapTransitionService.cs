using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Room/Scene 맵 전환의 수명주기와 도착 상태를 통합 관리합니다.
/// </summary>
public class MapTransitionService : MonoBehaviour
{
    public static MapTransitionService Instance { get; private set; }

    [SerializeField] private RoomContainer _roomContainer;
    [SerializeField] private bool _dontDestroyOnLoad;
    [SerializeField] private float _arrivalDoorSuppressSeconds = 0.25f;

    private bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    public void RequestTransition(MapTransitionRequest request, PlayerController player = null)
    {
        TryRequestTransition(request, player);
    }

    public bool TryRequestTransition(MapTransitionRequest request, PlayerController player = null)
    {
        if (_isTransitioning)
            return false;

        if (request == null)
        {
            Debug.LogError("[MapTransitionService] TransitionRequest가 null입니다.");
            return false;
        }

        if (!request.IsValid(out string error))
        {
            Debug.LogError($"[MapTransitionService] 잘못된 맵 전환 요청입니다. Error={error}", this);
            return false;
        }

        GameState previousState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;
        _isTransitioning = true;
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);

        if (request.TransitionType == MapTransitionType.Scene)
            BeginSceneTransition(request, player, previousState);
        else
            StartCoroutine(CoRoomTransition(request, player, previousState));

        return true;
    }

    private void BeginSceneTransition(
        MapTransitionRequest request,
        PlayerController player,
        GameState previousState)
    {
        player ??= FindFirstObjectByType<PlayerController>();

        DepartureState departureState = DepartureState.Capture(GlobalDataManager.Instance);
        SaveDepartureState(player, request);
        BeginSceneLoad(
            request,
            result => CompleteSceneTransition(this, result, departureState, previousState));
    }

    private IEnumerator CoRoomTransition(
        MapTransitionRequest request,
        PlayerController player,
        GameState previousState)
    {
        player ??= FindFirstObjectByType<PlayerController>();

        DepartureState departureState = DepartureState.Capture(GlobalDataManager.Instance);
        SaveDepartureState(player, request);

        if (request.FadeDuration > 0f)
            yield return new WaitForSecondsRealtime(request.FadeDuration);

        SceneLoadResult result = CoLoadRoom(request, player);
        if (result != SceneLoadResult.Succeeded)
            departureState.Restore(GlobalDataManager.Instance);

        RestoreGameState(previousState);
        _isTransitioning = false;
    }

    protected virtual void BeginSceneLoad(
        MapTransitionRequest request,
        Action<SceneLoadResult> onCompleted)
    {
        if (string.IsNullOrWhiteSpace(request.TargetSceneName))
        {
            onCompleted?.Invoke(SceneLoadResult.InvalidScene);
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadSceneWithResult(
                request.TargetSceneName,
                request.FadeDuration,
                onCompleted);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(request.TargetSceneName))
        {
            Debug.LogError($"[MapTransitionService] Build Settings에서 씬을 찾을 수 없습니다. Scene={request.TargetSceneName}", this);
            onCompleted?.Invoke(SceneLoadResult.InvalidScene);
            return;
        }

        UnityAction<Scene, LoadSceneMode> sceneLoaded = null;
        sceneLoaded = (scene, _) =>
        {
            if (!string.Equals(scene.name, request.TargetSceneName, StringComparison.Ordinal))
                return;

            SceneManager.sceneLoaded -= sceneLoaded;
            onCompleted?.Invoke(SceneLoadResult.Succeeded);
        };
        SceneManager.sceneLoaded += sceneLoaded;

        AsyncOperation loadOperation = null;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(request.TargetSceneName);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (loadOperation != null)
            return;

        SceneManager.sceneLoaded -= sceneLoaded;
        onCompleted?.Invoke(SceneLoadResult.LoadFailed);
    }

    private static void CompleteSceneTransition(
        MapTransitionService owner,
        SceneLoadResult result,
        DepartureState departureState,
        GameState previousState)
    {
        if (result != SceneLoadResult.Succeeded)
            departureState.Restore(GlobalDataManager.Instance);

        RestoreGameState(previousState);
        if (owner != null)
            owner._isTransitioning = false;
    }

    private static void RestoreGameState(GameState previousState)
    {
        GameState restoreState = previousState == GameState.Paused
            ? GameState.Exploration
            : previousState;
        GameStateManager.Instance?.ChangeState(restoreState);
    }

    private SceneLoadResult CoLoadRoom(MapTransitionRequest request, PlayerController player)
    {
        if (_roomContainer == null)
            _roomContainer = FindFirstObjectByType<RoomContainer>();

        if (_roomContainer == null)
        {
            Debug.LogError("[MapTransitionService] RoomContainer가 씬에 없습니다.");
            return SceneLoadResult.LoadFailed;
        }

        RoomInstance room = _roomContainer.LoadRoom(request.TargetRoom, player);
        ApplyArrival(player, request);
        room?.ConfigureCamera(player != null ? player : FindFirstObjectByType<PlayerController>());
        SuppressArrivalDoor(request.TargetSpawnPointId);
        ApplyRoomPresentation(request.TargetRoom, room);
        return SceneLoadResult.Succeeded;
    }

    private static void SaveDepartureState(PlayerController player, MapTransitionRequest request)
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null) return;

        if (player != null)
            player.SavePositionToGlobal();

        global.SpawnScene = request.TransitionType == MapTransitionType.Scene
            ? request.TargetSceneName
            : SceneManager.GetActiveScene().name;
        global.CurrentRoomId = request.TransitionType == MapTransitionType.Scene
            ? request.TargetAreaId ?? string.Empty
            : request.TargetRoom != null ? request.TargetRoom.RoomId : string.Empty;
        global.SpawnPointId = request.TargetSpawnPointId;

        if (request.FacingAfterEnter != FacingDirection.Keep)
            global.LookingDir = (int)request.FacingAfterEnter;
    }

    private static void ApplyArrival(PlayerController player, MapTransitionRequest request)
    {
        if (player == null) return;

        if (SpawnPoint.TryFind(request.TargetSpawnPointId, out SpawnPoint spawnPoint))
        {
            player.transform.position = spawnPoint.transform.position;
            FacingDirection facing = request.FacingAfterEnter != FacingDirection.Keep
                ? request.FacingAfterEnter
                : spawnPoint.DefaultFacing;
            ApplyFacing(player, facing);
        }
        else if (request.UseFallbackPosition)
        {
            player.transform.position = request.FallbackPosition;
            ApplyFacing(player, request.FacingAfterEnter);
        }
        else
        {
            Debug.LogWarning($"[MapTransitionService] SpawnPoint를 찾지 못했습니다. Id={request.TargetSpawnPointId}");
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global != null)
        {
            global.SpawnX = player.transform.position.x;
            global.SpawnY = player.transform.position.y;
            global.LookingDir = player.FacingDirection;
            global.SpawnPointId = string.Empty;
        }
    }

    private static void ApplyFacing(PlayerController player, FacingDirection facing)
    {
        if (player == null || facing == FacingDirection.Keep) return;
        player.SetFacingDirection((int)facing);
    }

    private static void ApplyRoomPresentation(RoomDefinition definition, RoomInstance room)
    {
        if (definition == null) return;

        if (definition.BgmOverride != null)
            AudioManager.Instance?.CrossFadeBGM(definition.BgmOverride, definition.BgmFadeDuration);
        else if (!definition.KeepCurrentBgm)
            AudioManager.Instance?.FadeOutBGM(definition.BgmFadeDuration);

        room?.ConfigureCamera(FindFirstObjectByType<PlayerController>());
    }

    private void SuppressArrivalDoor(string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(spawnPointId)) return;
        if (!SpawnPoint.TryFind(spawnPointId, out SpawnPoint spawnPoint) || spawnPoint == null) return;

        DoorTransition[] doors = FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;
            if (Vector2.Distance(doors[i].transform.position, spawnPoint.transform.position) <= 1.5f)
                doors[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
        }

        AreaConnectionMarker[] markers = FindObjectsByType<AreaConnectionMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null) continue;
            if (Vector2.Distance(markers[i].transform.position, spawnPoint.transform.position) <= 1.5f)
                markers[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private readonly struct DepartureState
    {
        private readonly string _spawnScene;
        private readonly string _roomId;
        private readonly string _spawnPointId;
        private readonly float _spawnX;
        private readonly float _spawnY;
        private readonly int _lookingDir;

        private DepartureState(GlobalDataManager global)
        {
            _spawnScene = global != null ? global.SpawnScene : string.Empty;
            _roomId = global != null ? global.CurrentRoomId : string.Empty;
            _spawnPointId = global != null ? global.SpawnPointId : string.Empty;
            _spawnX = global != null ? global.SpawnX : 0f;
            _spawnY = global != null ? global.SpawnY : 0f;
            _lookingDir = global != null ? global.LookingDir : 0;
        }

        public static DepartureState Capture(GlobalDataManager global) => new DepartureState(global);

        public void Restore(GlobalDataManager global)
        {
            if (global == null) return;
            global.SpawnScene = _spawnScene;
            global.CurrentRoomId = _roomId;
            global.SpawnPointId = _spawnPointId;
            global.SpawnX = _spawnX;
            global.SpawnY = _spawnY;
            global.LookingDir = _lookingDir;
        }
    }
}
