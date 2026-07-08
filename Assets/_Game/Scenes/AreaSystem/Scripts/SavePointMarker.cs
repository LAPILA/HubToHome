using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SavePointMarker : AreaMarkerBase
{
    [TitleGroup("SAVE Point 설정")]
    [SerializeField, LabelText("저장 지점 ID")] private string savePointId;
    [TitleGroup("SAVE Point 설정")]
    [SerializeField, Min(0), LabelText("수동 저장 슬롯")] private int quickSaveSlot = 0;
    [TitleGroup("SAVE Point 설정")]
    [SerializeField, LabelText("접촉 자동 저장")] private bool autoSaveOnPass;
    [TitleGroup("SAVE Point 설정")]
    [SerializeField, Min(0), ShowIf(nameof(autoSaveOnPass)), LabelText("자동 저장 슬롯")] private int autoSaveSlot = 99;

    private bool _hasAutoSavedThisVisit;

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
        if (!AreaMarkerRuntimeService.RequestSavePoint(this, player, savePointId, quickSaveSlot))
            return;

        if (isOneShot) CompleteMarker();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoSaveOnPass || _hasAutoSavedThisVisit || !CanInteract())
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            return;

        if (!AreaMarkerRuntimeService.RequestSavePoint(this, player, savePointId, autoSaveSlot))
            return;

        _hasAutoSavedThisVisit = true;
        if (isOneShot)
            CompleteMarker();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!autoSaveOnPass || other.GetComponent<PlayerController>() == null)
            return;

        _hasAutoSavedThisVisit = false;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(savePointId))
            issues.Add("savePointId가 비어 있습니다.");
        if (quickSaveSlot < 0)
            issues.Add("quickSaveSlot은 0 이상이어야 합니다.");
        if (autoSaveOnPass && autoSaveSlot < 0)
            issues.Add("autoSaveSlot은 0 이상이어야 합니다.");
    }
}