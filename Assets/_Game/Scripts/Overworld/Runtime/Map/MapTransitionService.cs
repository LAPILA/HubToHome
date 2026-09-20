using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns Room/Scene transition locking, destination state, and failure recovery.
/// </summary>
public class MapTransitionService : MonoBehaviour
{
    public static MapTransitionService Instance { get; private set; }

    [SerializeField] private RoomContainer _roomContainer;
    [SerializeField] private bool _dontDestroyOnLoad;
    [SerializeField] private float _arrivalDoorSuppressSeconds = 0.25f;

    private bool _isTransitioning;
    private RoomTransitionContext _activeRoomTransition;

    public bool IsTransitioning => _isTransitioning;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            bool sameScene = Instance.gameObject.scene == gameObject.scene;
            if (sameScene || Instance._dontDestroyOnLoad)
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        if (_dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CancelRoomTransition(IsExternalSceneTransitionPending());
    }

    private void Update()
    {
        // 진행 중일 때만 확인하며 씬 오브젝트 검색은 하지 않습니다.
        if (_activeRoomTransition != null && IsExternalSceneTransitionPending())
            CancelRoomTransition(true);
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        if (_activeRoomTransition != null && current != _activeRoomTransition.OriginScene)
            CancelRoomTransition(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_activeRoomTransition != null && mode == LoadSceneMode.Single
            && scene != _activeRoomTransition.OriginScene)
            CancelRoomTransition(true);
    }

    private bool IsExternalSceneTransitionPending()
    {
        return _activeRoomTransition != null
            && ((SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
                || SceneManager.GetActiveScene() != _activeRoomTransition.OriginScene);
    }


    public void RequestTransition(MapTransitionRequest request, PlayerController player = null)
    {
        TryRequestTransition(request, player, null);
    }

    public bool TryRequestTransition(MapTransitionRequest request, PlayerController player = null)
    {
        return TryRequestTransition(request, player, null);
    }

    public bool TryRequestTransition(
        MapTransitionRequest request,
        PlayerController player,
        Action<SceneLoadResult> onCompleted)
    {
        if (_isTransitioning || !isActiveAndEnabled
            || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading))
            return false;

        if (request == null)
        {
            Debug.LogError("[MapTransitionService] TransitionRequest가 null입니다.");
            return false;
        }

        if (!request.IsValid(out string error))
        {
            Debug.LogError("[MapTransitionService] 잘못된 맵 전환 요청입니다. Error=" + error, this);
            return false;
        }

        // 상점의 임시 Cutscene을 전환 후 복귀 상태로 저장하지 않도록 먼저 닫습니다.
        ShopUI.Instance?.CloseForTransition();
        if (_isTransitioning || !isActiveAndEnabled
            || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading))
            return false;
        GameState previousState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;
        _isTransitioning = true;

        if (request.TransitionType == MapTransitionType.Scene)
        {
            GameStateManager.Instance?.ChangeState(GameState.Cutscene);
            BeginSceneTransition(request, player, previousState, onCompleted);
        }
        else
        {
            var transition = new RoomTransitionContext(request, player, previousState, onCompleted);
            _activeRoomTransition = transition;
            try
            {
                GameStateManager.Instance?.ChangeState(GameState.Cutscene);
                if (!transition.Completed)
                {
                    Coroutine routine = StartCoroutine(CoRoomTransition(transition));
                    // FadeDuration=0이면 StartCoroutine 안에서 완료/재진입할 수 있습니다.
                    if (!transition.Completed)
                        transition.Routine = routine;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FinishRoomTransition(transition, SceneLoadResult.LoadFailed, true);
                return false;
            }
        }

        return true;
    }

    private void BeginSceneTransition(
        MapTransitionRequest request,
        PlayerController player,
        GameState previousState,
        Action<SceneLoadResult> onCompleted)
    {
        player ??= FindFirstObjectByType<PlayerController>();

        DepartureState departureState = DepartureState.Capture(GlobalDataManager.Instance);
        SaveDepartureState(player, request);
        BeginSceneLoad(
            request,
            result => CompleteSceneTransition(
                this,
                result,
                departureState,
                previousState,
                onCompleted));
    }

    private IEnumerator CoRoomTransition(RoomTransitionContext transition)
    {
        try
        {
            if (!CanContinueRoomTransition(transition))
                yield break;
            transition.Player ??= FindFirstObjectByType<PlayerController>();
            SaveDepartureState(transition.Player, transition.Request);
            if (transition.Request.FadeDuration > 0f)
            {
                transition.OverlayScope = transition.FadeRunner.CaptureRestorationScope(transition.FadeHandle);
                yield return transition.FadeRunner.Fade(
                    "out", "black", transition.Request.FadeDuration, transition.FadeHandle);
                if (!CanContinueRoomTransition(transition))
                    yield break;
            }

            SceneLoadResult result = LoadRoomSafely(transition);
            if (!CanContinueRoomTransition(transition))
                yield break;

            if (transition.Request.FadeDuration > 0f)
            {
                yield return transition.FadeRunner.Fade(
                    "in", "black", transition.Request.FadeDuration, transition.FadeHandle);
                if (!CanContinueRoomTransition(transition))
                    yield break;
            }

            FinishRoomTransition(transition, result, false);
        }
        finally
        {
            if (!transition.Completed)
            {
                transition.CancelRequested = true;
                // 활성화 콜백에서 소유자가 파괴되어 Dispose가 재진입해도 commit 판정은 먼저 끝냅니다.
                if (!transition.LoadingRoom)
                    FinishRoomTransition(transition, InterruptedRoomResult(transition), true);
            }
        }
    }

    private bool CanContinueRoomTransition(RoomTransitionContext transition)
    {
        if (transition.Completed)
            return false;
        if (IsExternalSceneTransitionPending())
            transition.ExternalSceneTransition = true;
        return !transition.CancelRequested && !transition.ExternalSceneTransition
            && !transition.FadeHandle.IsCancellationRequested && !transition.FadeHandle.IsDone
            && (transition.OverlayScope == null || transition.OverlayScope.IsCurrent);
    }

    private void CancelRoomTransition(bool externalSceneTransition)
    {
        RoomTransitionContext transition = _activeRoomTransition;
        if (transition == null || transition.Completed)
            return;

        transition.CancelRequested = true;
        transition.ExternalSceneTransition |= externalSceneTransition;
        // RoomContainer 교체 도중 OnDisable이 재진입하면 실제 commit 여부를 확인한 뒤 마감합니다.
        if (transition.LoadingRoom)
            return;

        Coroutine routine = transition.Routine;
        FinishRoomTransition(transition, InterruptedRoomResult(transition), true);
        if (routine != null)
            StopCoroutine(routine);
    }

    private static SceneLoadResult InterruptedRoomResult(RoomTransitionContext transition)
    {
        return transition.DestinationActivated
            ? SceneLoadResult.DestinationPreparationFailed
            : SceneLoadResult.CancelledBeforeActivation;
    }

    private void FinishRoomTransition(RoomTransitionContext transition, SceneLoadResult result, bool canceled)
    {
        if (transition.Completed)
            return;
        transition.Completed = true;
        if (ReferenceEquals(_activeRoomTransition, transition))
        {
            _activeRoomTransition = null;
            _isTransitioning = false;
        }

        try
        {
            if (canceled)
                transition.FadeHandle.Cancel("Room transition was interrupted.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            // Fade-in 자체의 prior는 검정입니다. 두 페이드 이전의 baseline으로 회수해야 합니다.
            transition.OverlayScope?.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            if (!transition.DestinationActivated && !transition.ExternalSceneTransition
                && ReferenceEquals(GlobalDataManager.Instance, transition.DataOwner))
                transition.Departure.Restore(transition.DataOwner);
            if (ReferenceEquals(GameStateManager.Instance, transition.StateOwner))
                RestoreGameStateIfTransitionOwned(transition.PreviousState);
        }
        finally
        {
            if (transition.DestinationActivated && result == SceneLoadResult.LoadFailed)
                result = SceneLoadResult.DestinationPreparationFailed;
            InvokeCompletionSafely(transition.OnCompleted, result, this);
        }
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
            Debug.LogError(
                "[MapTransitionService] Build Settings에서 씬을 찾을 수 없습니다. Scene="
                + request.TargetSceneName,
                this);
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
        GameState previousState,
        Action<SceneLoadResult> onCompleted)
    {
        try
        {
            if (!SceneLoadResultUtility.WasDestinationActivated(result))
                departureState.Restore(GlobalDataManager.Instance);

            RestoreGameStateIfTransitionOwned(previousState);
        }
        finally
        {
            if (owner != null)
                owner._isTransitioning = false;
            InvokeCompletionSafely(onCompleted, result, owner);
        }
    }

    private static void RestoreGameStateIfTransitionOwned(GameState previousState)
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager == null || stateManager.CurrentState != GameState.Cutscene)
            return;

        GameState restoreState = previousState == GameState.Paused
            ? GameState.Exploration
            : previousState;
        stateManager.ChangeState(restoreState);
    }

    private SceneLoadResult LoadRoomSafely(RoomTransitionContext transition)
    {
        transition.LoadingRoom = true;
        try
        {
            return LoadRoom(transition);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return SceneLoadResult.LoadFailed;
        }
        finally
        {
            transition.LoadingRoom = false;
            if (transition.CancelRequested)
                FinishRoomTransition(transition, InterruptedRoomResult(transition), true);
        }
    }

    private SceneLoadResult LoadRoom(RoomTransitionContext transition)
    {
        MapTransitionRequest request = transition.Request;
        PlayerController player = transition.Player;
        if (player == null)
        {
            Debug.LogError("[MapTransitionService] 도착 위치를 적용할 Player가 없습니다.", this);
            return SceneLoadResult.LoadFailed;
        }

        if (_roomContainer == null)
            _roomContainer = FindFirstObjectByType<RoomContainer>();

        if (_roomContainer == null)
        {
            Debug.LogError("[MapTransitionService] RoomContainer가 씬에 없습니다.", this);
            return SceneLoadResult.LoadFailed;
        }

        string arrivalValidationError = string.Empty;
        RoomContainer container = _roomContainer;
        RoomInstance previousRoom = container.CurrentRoom;
        RoomInstance room;
        string roomError;
        bool loaded;
        try
        {
            loaded = container.TryLoadRoom(
                request.TargetRoom,
                candidate => !transition.CancelRequested && TryResolveArrival(
                    request,
                    candidate != null ? candidate.transform : null,
                    out _,
                    out arrivalValidationError),
                out room,
                out roomError);
        }
        finally
        {
            // commit 뒤 정리 콜백에서 실패하거나 Unity 오브젝트가 파괴돼도 이전 방으로 되돌리지 않습니다.
            transition.DestinationActivated = !ReferenceEquals(container.CurrentRoom, null)
                && !ReferenceEquals(container.CurrentRoom, previousRoom)
                && ReferenceEquals(container.CurrentDefinition, request.TargetRoom);
        }
        if (!loaded || room == null)
        {
            string error = string.IsNullOrEmpty(arrivalValidationError)
                ? roomError
                : arrivalValidationError;
            Debug.LogError("[MapTransitionService] Room 전환 준비에 실패했습니다. " + error, this);
            return SceneLoadResult.LoadFailed;
        }

        if (transition.ExternalSceneTransition)
            return SceneLoadResult.DestinationPreparationFailed;

        if (!TryApplyArrival(player, request, room.transform, out string arrivalError))
        {
            Debug.LogError("[MapTransitionService] Room 도착 적용에 실패했습니다. " + arrivalError, this);
            return SceneLoadResult.LoadFailed;
        }

        room.OnRoomEntered(player);
        SuppressArrivalDoor(request.TargetSpawnPointId);
        _roomContainer.ApplyCurrentRoomAudio();
        return SceneLoadResult.Succeeded;
    }

    private static void SaveDepartureState(PlayerController player, MapTransitionRequest request)
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
            return;

        if (player != null)
            player.SavePositionToGlobal();

        global.SpawnScene = request.TransitionType == MapTransitionType.Scene
            ? request.TargetSceneName
            : SceneManager.GetActiveScene().name;
        global.CurrentRoomId = request.TransitionType == MapTransitionType.Scene
            ? request.ResolvedTargetRoomId
            : request.TargetRoom != null ? request.TargetRoom.RoomId : string.Empty;
        global.SpawnPointId = request.TargetSpawnPointId ?? string.Empty;
        global.SpawnFallbackAllowed = request.UseFallbackPosition;
        if (request.UseFallbackPosition)
        {
            global.SpawnX = request.FallbackPosition.x;
            global.SpawnY = request.FallbackPosition.y;
        }

        if (request.FacingAfterEnter != FacingDirection.Keep)
            global.LookingDir = (int)request.FacingAfterEnter;
    }

    public static bool TryValidateArrival(
        MapTransitionRequest request,
        Transform searchRoot,
        out string error)
    {
        return TryResolveArrival(request, searchRoot, out _, out error);
    }

    public static bool TryApplyArrival(
        PlayerController player,
        MapTransitionRequest request,
        Transform searchRoot,
        out string error)
    {
        if (player == null)
        {
            error = "Player가 없습니다.";
            return false;
        }

        if (!TryResolveArrival(request, searchRoot, out ResolvedArrival arrival, out error))
            return false;

        player.transform.position = arrival.Position;
        ApplyFacing(player, arrival.Facing);

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global != null)
        {
            global.SpawnX = player.transform.position.x;
            global.SpawnY = player.transform.position.y;
            global.LookingDir = player.FacingDirection;
            global.SpawnPointId = string.Empty;
            global.SpawnFallbackAllowed = false;
        }

        return true;
    }

    private static bool TryResolveArrival(
        MapTransitionRequest request,
        Transform searchRoot,
        out ResolvedArrival arrival,
        out string error)
    {
        arrival = default;
        error = string.Empty;
        if (request == null)
        {
            error = "TransitionRequest가 null입니다.";
            return false;
        }

        string spawnPointId = string.IsNullOrWhiteSpace(request.TargetSpawnPointId)
            ? string.Empty
            : request.TargetSpawnPointId.Trim();
        if (!string.IsNullOrEmpty(spawnPointId)
            && TryFindSpawnPoint(spawnPointId, searchRoot, out SpawnPoint spawnPoint, out error))
        {
            FacingDirection facing = request.FacingAfterEnter != FacingDirection.Keep
                ? request.FacingAfterEnter
                : spawnPoint.DefaultFacing;
            arrival = new ResolvedArrival(spawnPoint.transform.position, facing);
            error = string.Empty;
            return true;
        }

        if (request.UseFallbackPosition)
        {
            arrival = new ResolvedArrival(request.FallbackPosition, request.FacingAfterEnter);
            error = string.Empty;
            return true;
        }

        if (string.IsNullOrEmpty(error))
            error = "SpawnPoint를 찾지 못했습니다. Id=" + spawnPointId;
        return false;
    }

    private static bool TryFindSpawnPoint(
        string spawnPointId,
        Transform searchRoot,
        out SpawnPoint spawnPoint,
        out string error)
    {
        spawnPoint = null;
        error = string.Empty;
        SpawnPoint[] points = searchRoot != null
            ? searchRoot.GetComponentsInChildren<SpawnPoint>(true)
            : FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < points.Length; i++)
        {
            SpawnPoint candidate = points[i];
            if (candidate == null
                || !string.Equals(candidate.SpawnPointId, spawnPointId, StringComparison.Ordinal))
            {
                continue;
            }

            if (spawnPoint != null)
            {
                error = "중복 SpawnPoint ID가 있습니다. Id=" + spawnPointId;
                spawnPoint = null;
                return false;
            }

            spawnPoint = candidate;
        }

        if (spawnPoint != null)
            return true;

        error = "SpawnPoint를 찾지 못했습니다. Id=" + spawnPointId;
        return false;
    }

    private static void ApplyFacing(PlayerController player, FacingDirection facing)
    {
        if (player == null || facing == FacingDirection.Keep)
            return;
        player.SetFacingDirection((int)facing);
    }

    private void SuppressArrivalDoor(string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(spawnPointId))
            return;
        if (!SpawnPoint.TryFind(spawnPointId, out SpawnPoint spawnPoint) || spawnPoint == null)
            return;

        DoorTransition[] doors = FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null
                && Vector2.Distance(doors[i].transform.position, spawnPoint.transform.position) <= 1.5f)
            {
                doors[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
            }
        }

        AreaConnectionMarker[] markers = FindObjectsByType<AreaConnectionMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null
                && Vector2.Distance(markers[i].transform.position, spawnPoint.transform.position) <= 1.5f)
            {
                markers[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
            }
        }
    }

    private static void InvokeCompletionSafely(
        Action<SceneLoadResult> callback,
        SceneLoadResult result,
        UnityEngine.Object context)
    {
        if (callback == null)
            return;

        try
        {
            callback(result);
        }
        catch (Exception exception)
        {
            if (context != null)
                Debug.LogException(exception, context);
            else
                Debug.LogException(exception);
        }
    }

    private void OnDestroy()
    {
        CancelRoomTransition(IsExternalSceneTransitionPending());
        if (Instance == this)
            Instance = null;
    }

    private sealed class RoomTransitionContext
    {
        public RoomTransitionContext(MapTransitionRequest request, PlayerController player,
            GameState previousState, Action<SceneLoadResult> onCompleted)
        {
            Request = request;
            Player = player;
            PreviousState = previousState;
            OnCompleted = onCompleted;
            OriginScene = SceneManager.GetActiveScene();
            DataOwner = GlobalDataManager.Instance;
            StateOwner = GameStateManager.Instance;
            Departure = DepartureState.Capture(DataOwner);
        }

        public readonly MapTransitionRequest Request;
        public PlayerController Player;
        public readonly GameState PreviousState;
        public readonly Action<SceneLoadResult> OnCompleted;
        public readonly Scene OriginScene;
        public readonly GlobalDataManager DataOwner;
        public readonly GameStateManager StateOwner;
        public readonly DepartureState Departure;
        public readonly ActionExecutionHandle FadeHandle = new ActionExecutionHandle("room_transition");
        public readonly ScreenTransitionRunner FadeRunner = new ScreenTransitionRunner();
        public ScreenTransitionOverlay.RestorationScope OverlayScope;
        public Coroutine Routine;
        public bool LoadingRoom;
        public bool DestinationActivated;
        public bool CancelRequested;
        public bool ExternalSceneTransition;
        public bool Completed;
    }

    private readonly struct ResolvedArrival
    {
        public ResolvedArrival(Vector3 position, FacingDirection facing)
        {
            Position = position;
            Facing = facing;
        }

        public Vector3 Position { get; }
        public FacingDirection Facing { get; }
    }

    private readonly struct DepartureState
    {
        private readonly string _spawnScene;
        private readonly string _roomId;
        private readonly string _spawnPointId;
        private readonly bool _spawnFallbackAllowed;
        private readonly float _spawnX;
        private readonly float _spawnY;
        private readonly int _lookingDir;

        private DepartureState(GlobalDataManager global)
        {
            _spawnScene = global != null ? global.SpawnScene : string.Empty;
            _roomId = global != null ? global.CurrentRoomId : string.Empty;
            _spawnPointId = global != null ? global.SpawnPointId : string.Empty;
            _spawnFallbackAllowed = global != null && global.SpawnFallbackAllowed;
            _spawnX = global != null ? global.SpawnX : 0f;
            _spawnY = global != null ? global.SpawnY : 0f;
            _lookingDir = global != null ? global.LookingDir : 0;
        }

        public static DepartureState Capture(GlobalDataManager global)
        {
            return new DepartureState(global);
        }

        public void Restore(GlobalDataManager global)
        {
            if (global == null)
                return;

            global.SpawnScene = _spawnScene;
            global.CurrentRoomId = _roomId;
            global.SpawnPointId = _spawnPointId;
            global.SpawnFallbackAllowed = _spawnFallbackAllowed;
            global.SpawnX = _spawnX;
            global.SpawnY = _spawnY;
            global.LookingDir = _lookingDir;
        }
    }
}
