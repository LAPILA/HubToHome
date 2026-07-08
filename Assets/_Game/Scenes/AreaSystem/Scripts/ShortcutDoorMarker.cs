using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ShortcutDoorMarker : AreaConnectionMarker
{
    [TitleGroup("Shortcut Door 설정")]
    [SerializeField, LabelText("문 ID")] private string doorId;
    [TitleGroup("Shortcut Door 설정")]
    [SerializeField, LabelText("연결 문 ID")] private string linkedDoorId;
    [TitleGroup("Shortcut Door 설정")]
    [SerializeField, LabelText("잠금 상태")] private bool isLocked = true;
    [TitleGroup("Shortcut Door 설정")]
    [SerializeField, ShowIf(nameof(isLocked)), LabelText("해제 플래그")] private string unlockFlag;

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

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(doorId))
            issues.Add("doorId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(linkedDoorId))
            issues.Add("linkedDoorId가 비어 있습니다.");
        if (isLocked && string.IsNullOrWhiteSpace(unlockFlag))
            issues.Add("잠긴 문이면 unlockFlag를 지정하는 것을 권장합니다.");
    }
}