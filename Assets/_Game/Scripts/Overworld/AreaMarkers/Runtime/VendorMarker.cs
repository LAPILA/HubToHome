using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class VendorMarker : AreaMarkerBase
{
    [TitleGroup("Vendor 설정")]
    [InfoBox("ShopDefinition과 Shop Session Launcher가 있으면 실제 세션을 열고, 없으면 기존 연결 로그 동작을 유지합니다.")]
    [SerializeField, LabelText("Vendor ID (연결용)")] private string vendorId;
    [TitleGroup("Vendor 설정")]
    [SerializeField, LabelText("Shop ID (연결용)")] private string shopId;
    [TitleGroup("Vendor 설정")]
    [SerializeField, LabelText("Shop Definition")] private ShopDefinition shopDefinition;

    private bool _sessionOpen;
    private int _sessionGeneration;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Vendor;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (shopDefinition != null && string.IsNullOrWhiteSpace(shopId))
            shopId = shopDefinition.ShopId;
    }

    public override bool CanInteract(PlayerController player)
    {
        return !_sessionOpen && base.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;

        if (shopDefinition == null)
        {
            AreaMarkerRuntimeService.RequestVendor(this, vendorId, shopId);
            if (isOneShot)
                CompleteMarker();
            return;
        }

        int generation = ++_sessionGeneration;
        _sessionOpen = true;
        bool opened = AreaMarkerRuntimeService.RequestVendor(
            this,
            vendorId,
            shopId,
            shopDefinition,
            result => HandleSessionClosed(generation, result));
        if (!opened && generation == _sessionGeneration)
            _sessionOpen = false;
    }

    private void OnDisable()
    {
        _sessionGeneration++;
        _sessionOpen = false;
    }

    private void HandleSessionClosed(int generation, ShopSessionResult result)
    {
        if (generation != _sessionGeneration)
            return;

        _sessionOpen = false;
        if (isOneShot && result.HasSuccessfulPurchase)
            CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(vendorId))
            issues.Add("vendorId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(shopId))
            issues.Add("shopId가 비어 있습니다.");
        if (shopDefinition != null)
        {
            if (!shopDefinition.TryValidate(out string shopError))
                issues.Add("ShopDefinition 오류: " + shopError);
            if (!string.Equals(
                    shopId?.Trim(),
                    shopDefinition.ShopId,
                    System.StringComparison.Ordinal))
            {
                issues.Add("shopId와 ShopDefinition.ShopId가 일치하지 않습니다.");
            }
        }
    }
}