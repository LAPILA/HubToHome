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
    [SerializeField, HideInInspector] private int autoSaveSlot = 99; // 기존 데이터 호환용. 실행 시 99 고정.

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
        SaveManager.TryPromptManualSave(this, player, quickSaveSlot, saved =>
        {
            if (saved && this != null && isActiveAndEnabled && isOneShot) CompleteMarker();
        });
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoSaveOnPass || _hasAutoSavedThisVisit || !CanInteract())
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            return;

        if (!AreaMarkerRuntimeService.RequestSavePoint(this, player, savePointId, SaveManager.AutoSlotIndex))
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
        if (!SaveManager.IsManualSlot(quickSaveSlot))
            issues.Add("수동 저장 슬롯은 0~2입니다.");
        if (autoSaveOnPass && autoSaveSlot != SaveManager.AutoSlotIndex)
            issues.Add("자동 저장은 전용 슬롯 99를 사용합니다. 기존 슬롯 값은 무시됩니다.");
    }
}
