using UnityEngine;

public class ShortcutDoorMarker : AreaConnectionMarker
{
    [Header("Shortcut Door")]
    [SerializeField] private string doorId;
    [SerializeField] private string linkedDoorId;
    [SerializeField] private bool isLocked = true;
    [SerializeField] private string unlockFlag;

    protected override void Reset()
    {
        base.Reset();
        markerType = AreaMarkerType.ShortcutDoor;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (string.IsNullOrWhiteSpace(doorId)) doorId = markerId;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!AreaMarkerBaseCanInteract(player)) return false;
        if (!isLocked) return true;
        if (!string.IsNullOrWhiteSpace(unlockFlag) && GlobalDataManager.Instance != null && GlobalDataManager.Instance.GetFlag(unlockFlag, 0) != 0)
            return true;

        return true;
    }

    protected override void RequestConnection(PlayerController player)
    {
        if (isLocked && (string.IsNullOrWhiteSpace(unlockFlag) || GlobalDataManager.Instance == null || GlobalDataManager.Instance.GetFlag(unlockFlag, 0) == 0))
        {
            Debug.Log($"[ShortcutDoorMarker] 잠긴 문: door={doorId}, linked={linkedDoorId}, unlockFlag={unlockFlag}", this);
            return;
        }
        Debug.Log($"[ShortcutDoorMarker] 문 이동 요청: door={doorId}, linked={linkedDoorId}", this);
        base.RequestConnection(player);
    }

    private bool AreaMarkerBaseCanInteract(PlayerController player)
    {
        return base.CanInteract(player);
    }
}