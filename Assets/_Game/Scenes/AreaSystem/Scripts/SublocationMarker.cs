using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SublocationMarker : AreaMarkerBase
{
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("서브로케이션 ID")] private string sublocationId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 Scene")] private string targetSceneName;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 Area ID")] private string targetAreaId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 SpawnPoint")] private string targetSpawnId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, Min(0f), LabelText("페이드 시간")] private float fadeDuration = 0.2f;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Sublocation;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        if (!AreaMarkerRuntimeService.RequestSublocation(this, targetSceneName, targetAreaId, targetSpawnId, fadeDuration))
            return;

        if (isOneShot)
            CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(sublocationId))
            issues.Add("sublocationId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(targetSceneName))
            issues.Add("targetSceneName이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(targetAreaId))
            issues.Add("targetAreaId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(targetSpawnId))
            issues.Add("targetSpawnId가 비어 있습니다.");
    }
}