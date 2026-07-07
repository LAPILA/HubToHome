using UnityEngine;

public class VendorMarker : AreaMarkerBase
{
    [Header("Vendor")]
    [SerializeField] private string vendorId;
    [SerializeField] private string shopId;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Vendor;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        Debug.Log($"[VendorMarker] 상점 요청: vendorId={vendorId}, shopId={shopId}. Shop UI 연결 지점입니다.", this);
        if (isOneShot) CompleteMarker();
    }
}