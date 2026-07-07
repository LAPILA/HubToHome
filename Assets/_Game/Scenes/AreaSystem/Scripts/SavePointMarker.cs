using UnityEngine;

public class SavePointMarker : AreaMarkerBase
{
    [Header("Save Point")]
    [SerializeField] private string savePointId;

    protected override void Reset()
    {
        markerType = AreaMarkerType.SavePoint;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (string.IsNullOrWhiteSpace(savePointId)) savePointId = markerId;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        player?.SavePositionToGlobal();
        Debug.Log($"[SavePointMarker] 저장 지점 요청: savePointId={savePointId}. 실제 슬롯 UI/SaveData 연결 지점입니다.", this);
        if (isOneShot) CompleteMarker();
    }
}