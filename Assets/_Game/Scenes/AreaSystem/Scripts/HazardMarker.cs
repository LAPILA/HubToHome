using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class HazardMarker : AreaMarkerBase
{
    [TitleGroup("Hazard 설정")]
    [InfoBox("현재 Hazard는 플레이어를 밀어내는 연출 seam만 연결되어 있습니다. damage 값은 로그/기획 수치이며 실제 HP는 아직 감소하지 않습니다.")]
    [SerializeField, Min(0), LabelText("디자인 피해량(HP 미연동)")] private int damage = 10;
    [TitleGroup("Hazard 설정")]
    [SerializeField, Min(0f), LabelText("넉백")] private float knockback = 0.5f;
    [TitleGroup("Hazard 설정")]
    [SerializeField, LabelText("접촉 즉시 발동")] private bool triggerOnEnter = true;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Hazard;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        ApplyHazard(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter || !CanInteract()) return;
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) ApplyHazard(player);
    }

    private void ApplyHazard(PlayerController player)
    {
        AreaMarkerRuntimeService.ApplyHazard(this, player, damage, knockback);
        if (isOneShot) CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (damage <= 0)
            issues.Add("damage는 1 이상이어야 합니다.");
    }
}