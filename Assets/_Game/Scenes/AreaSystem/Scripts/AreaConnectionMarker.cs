using UnityEngine;
using UnityEngine.SceneManagement;

public class AreaConnectionMarker : AreaMarkerBase
{
    [Header("Connection")]
    [SerializeField, Tooltip("이동할 Unity Scene 이름입니다.")]
    private string targetSceneName;
    [SerializeField, Tooltip("도착 SpawnPoint ID입니다.")]
    private string targetSpawnId;
    [SerializeField, Tooltip("켜면 상호작용 키로 이동하고, 끄면 TriggerEnter로 이동합니다.")]
    private bool interactToUse = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    [Header("Room Map Connection")]
    [SerializeField, Tooltip("Room 기반 맵 이동 요청입니다. 유효하면 위 Scene 이름보다 우선합니다.")]
    private MapTransitionRequest mapTransition = new MapTransitionRequest();
    [SerializeField, Tooltip("Room/Scene 이동 발동 방식입니다.")]
    private DoorActivationMode activationMode = DoorActivationMode.OnInteract;
    [SerializeField, Tooltip("Trigger 내부에 머무르는 동안 같은 이동을 한 번만 실행합니다.")]
    private bool oneShotUntilExit = true;

    private bool _isPlayerInside;
    private bool _usedWhileInside;
    private float _nextAllowedTransitionTime;

    public MapTransitionRequest MapTransition => mapTransition;
    public DoorActivationMode ActivationMode => activationMode;
    public bool HasSceneTarget => !string.IsNullOrWhiteSpace(targetSceneName);

    protected override void Reset()
    {
        markerType = AreaMarkerType.Connection;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        interactionRange = 1.5f;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (activationMode == DoorActivationMode.OnTriggerEnter) return;
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        RequestConnection(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _isPlayerInside = true;

        bool triggerEnabled = activationMode == DoorActivationMode.OnTriggerEnter
            || activationMode == DoorActivationMode.TriggerOrInteract
            || !interactToUse;

        if (triggerEnabled && base.CanInteract(player))
            RequestConnection(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        _isPlayerInside = false;
        _usedWhileInside = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player)) return false;
        if (activationMode == DoorActivationMode.OnTriggerEnter) return false;
        return player == null || _isPlayerInside || activationMode == DoorActivationMode.OnInteract || IsPlayerInRange(player);
    }

    protected virtual void RequestConnection(PlayerController player)
    {
        if (Time.unscaledTime < _nextAllowedTransitionTime) return;
        if (oneShotUntilExit && _usedWhileInside) return;

        if (TryRequestMapTransition(player))
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[AreaConnectionMarker] TargetSceneName이 비어 있습니다. Marker={MarkerId}", this);
            return;
        }

        player?.SavePositionToGlobal();
        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.SpawnScene = targetSceneName;
            GlobalDataManager.Instance.SpawnPointId = targetSpawnId;
            if (player != null)
            {
                GlobalDataManager.Instance.SpawnX = player.transform.position.x;
                GlobalDataManager.Instance.SpawnY = player.transform.position.y;
            }
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(targetSceneName, fadeDuration);
        else
            SceneManager.LoadScene(targetSceneName);

        Debug.Log($"[AreaConnectionMarker] 이동 요청: scene={targetSceneName}, spawn={targetSpawnId}", this);
        if (isOneShot) CompleteMarker();
    }

    private bool TryRequestMapTransition(PlayerController player)
    {
        if (mapTransition == null || !mapTransition.IsValid(out string error))
            return false;

        if (MapTransitionService.Instance == null)
        {
            Debug.LogError("[AreaConnectionMarker] MapTransitionService가 씬에 없어 Room 이동을 실행할 수 없습니다.", this);
            return true;
        }

        _usedWhileInside = true;
        MapTransitionService.Instance.RequestTransition(mapTransition, player);
        Debug.Log($"[AreaConnectionMarker] 맵 이동 요청: type={mapTransition.TransitionType}, room={mapTransition.TargetRoom}, scene={mapTransition.TargetSceneName}, spawn={mapTransition.TargetSpawnPointId}", this);
        if (isOneShot) CompleteMarker();
        return true;
    }

    public void SuppressForSeconds(float seconds)
    {
        _nextAllowedTransitionTime = Time.unscaledTime + Mathf.Max(0f, seconds);
        _usedWhileInside = true;
    }
}