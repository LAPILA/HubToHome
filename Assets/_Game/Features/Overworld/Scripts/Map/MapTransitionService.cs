using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Room 기반 맵 전환의 중심 서비스입니다.
/// DoorTransition, 컷씬, 이벤트는 이 서비스에 요청만 보내고 실제 전환 규칙은 여기서 통합 관리합니다.
/// </summary>
public class MapTransitionService : MonoBehaviour
{
    public static MapTransitionService Instance { get; private set; }

    [SerializeField] private RoomContainer _roomContainer;
    [SerializeField] private bool _dontDestroyOnLoad;
    [SerializeField] private float _arrivalDoorSuppressSeconds = 0.25f;

    private bool _isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    public void RequestTransition(MapTransitionRequest request, PlayerController player = null)
    {
        if (_isTransitioning) return;
        if (request == null)
        {
            Debug.LogError("[MapTransitionService] TransitionRequest가 null입니다.");
            return;
        }

        if (!request.IsValid(out string error))
        {
            Debug.LogError($"[MapTransitionService] 잘못된 맵 전환 요청입니다. Error={error}", this);
            return;
        }

        StartCoroutine(CoTransition(request, player));
    }

    private IEnumerator CoTransition(MapTransitionRequest request, PlayerController player)
    {
        _isTransitioning = true;
        player ??= FindFirstObjectByType<PlayerController>();

        GameState previousState = GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState : GameState.Exploration;
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);

        SaveDepartureState(player, request);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, request.FadeDuration));

        if (request.TransitionType == MapTransitionType.Scene)
            yield return CoLoadScene(request);
        else
            yield return CoLoadRoom(request, player);

        GameStateManager.Instance?.ChangeState(previousState == GameState.Paused ? GameState.Exploration : previousState);
        _isTransitioning = false;
    }

    private IEnumerator CoLoadScene(MapTransitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetSceneName))
        {
            Debug.LogError("[MapTransitionService] TargetSceneName이 비어 있습니다.");
            yield break;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(request.TargetSceneName, request.FadeDuration);
            yield break;
        }

        SceneManager.LoadScene(request.TargetSceneName);
        yield return null;
    }

    private IEnumerator CoLoadRoom(MapTransitionRequest request, PlayerController player)
    {
        if (_roomContainer == null)
            _roomContainer = FindFirstObjectByType<RoomContainer>();

        if (_roomContainer == null)
        {
            Debug.LogError("[MapTransitionService] RoomContainer가 씬에 없습니다.");
            yield break;
        }

        RoomInstance room = _roomContainer.LoadRoom(request.TargetRoom, player);
        ApplyArrival(player, request);
        room?.ConfigureCamera(player != null ? player : FindFirstObjectByType<PlayerController>());
        SuppressArrivalDoor(request.TargetSpawnPointId);
        yield return null;

        ApplyRoomPresentation(request.TargetRoom, room);
    }

    private static void SaveDepartureState(PlayerController player, MapTransitionRequest request)
    {
        if (GlobalDataManager.Instance == null) return;

        if (player != null)
            player.SavePositionToGlobal();

        GlobalDataManager.Instance.SpawnScene = request.TransitionType == MapTransitionType.Scene
            ? request.TargetSceneName
            : SceneManager.GetActiveScene().name;
        GlobalDataManager.Instance.CurrentRoomId = request.TargetRoom != null ? request.TargetRoom.RoomId : string.Empty;
        GlobalDataManager.Instance.SpawnPointId = request.TargetSpawnPointId;

        if (request.FacingAfterEnter != FacingDirection.Keep)
            GlobalDataManager.Instance.LookingDir = (int)request.FacingAfterEnter;
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

        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.SpawnX = player.transform.position.x;
            GlobalDataManager.Instance.SpawnY = player.transform.position.y;
            GlobalDataManager.Instance.LookingDir = player.FacingDirection;
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

        PlayerController player = FindFirstObjectByType<PlayerController>();
        room?.ConfigureCamera(player);
    }

    private void SuppressArrivalDoor(string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(spawnPointId)) return;
        if (!SpawnPoint.TryFind(spawnPointId, out SpawnPoint spawnPoint) || spawnPoint == null) return;

        DoorTransition[] doors = FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;

            float distance = Vector2.Distance(doors[i].transform.position, spawnPoint.transform.position);
            if (distance <= 1.5f)
                doors[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
        }

        AreaConnectionMarker[] connectionMarkers = FindObjectsByType<AreaConnectionMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < connectionMarkers.Length; i++)
        {
            if (connectionMarkers[i] == null) continue;

            float distance = Vector2.Distance(connectionMarkers[i].transform.position, spawnPoint.transform.position);
            if (distance <= 1.5f)
                connectionMarkers[i].SuppressForSeconds(_arrivalDoorSuppressSeconds);
        }
    }
}
