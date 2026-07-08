using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class VendorMarker : AreaMarkerBase
{
    [TitleGroup("Vendor 설정")]
    [InfoBox("현재 VendorMarker는 상점 UI를 직접 열지 않습니다. vendorId/shopId를 런타임 서비스에 전달하는 연결 지점만 제공합니다.")]
    [SerializeField, LabelText("Vendor ID (연결용)")] private string vendorId;
    [TitleGroup("Vendor 설정")]
    [SerializeField, LabelText("Shop ID (연결용)")] private string shopId;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Vendor;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        AreaMarkerRuntimeService.RequestVendor(this, vendorId, shopId);
        if (isOneShot) CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(vendorId))
            issues.Add("vendorId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(shopId))
            issues.Add("shopId가 비어 있습니다.");
    }
}