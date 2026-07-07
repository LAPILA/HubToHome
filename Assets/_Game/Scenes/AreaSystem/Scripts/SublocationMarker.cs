using UnityEngine;
using UnityEngine.SceneManagement;

public class SublocationMarker : AreaMarkerBase
{
    [Header("Sublocation")]
    [SerializeField] private string sublocationId;
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetAreaId;
    [SerializeField] private string targetSpawnId;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Sublocation;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.CurrentRoomId = targetAreaId;
            GlobalDataManager.Instance.SpawnPointId = targetSpawnId;
        }

        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(targetSceneName, fadeDuration);
            else SceneManager.LoadScene(targetSceneName);
        }

        Debug.Log($"[SublocationMarker] 내부맵 이동 요청: sublocation={sublocationId}, scene={targetSceneName}, area={targetAreaId}, spawn={targetSpawnId}", this);
        if (isOneShot) CompleteMarker();
    }
}