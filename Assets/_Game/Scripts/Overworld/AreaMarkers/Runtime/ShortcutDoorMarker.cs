using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ShortcutDoorMarker : AreaConnectionMarker
{
    [TitleGroup("Shortcut Door 설정/기본")]
    [SerializeField, LabelText("문 ID")]
    private string doorId;

    [TitleGroup("Shortcut Door 설정/기본")]
    [SerializeField, LabelText("연결 문 ID")]
    private string linkedDoorId;

    [TitleGroup("Shortcut Door 설정/잠금")]
    [SerializeField, LabelText("잠금 사용")]
    private bool isLocked = true;

    [TitleGroup("Shortcut Door 설정/잠금")]
    [SerializeField, ShowIf(nameof(isLocked)), LabelText("해제 플래그")]
    private string unlockFlag;

    [TitleGroup("Shortcut Door 설정/잠금 안내")]
    [SerializeField, ShowIf(nameof(isLocked)), LabelText("잠금 DialogueData")]
    private DialogueData lockedDialogue;

    [TitleGroup("Shortcut Door 설정/잠금 안내")]
    [SerializeField, ShowIf(nameof(UseLockedFallback)), LabelText("Fallback Speaker")]
    private SpeakerData lockedFallbackSpeaker;

    [TitleGroup("Shortcut Door 설정/잠금 안내")]
    [SerializeField, ShowIf(nameof(UseLockedFallback)), LabelText("Fallback Emotion")]
    private EmotionType lockedFallbackEmotion = EmotionType.Normal;

    [TitleGroup("Shortcut Door 설정/잠금 안내")]
    [TextArea(2, 4)]
    [SerializeField, ShowIf(nameof(UseLockedFallback)), LabelText("Fallback 안내")]
    private string lockedFallbackText = "잠겨 있다.";

    public bool IsUnlocked
    {
        get
        {
            if (!isLocked)
                return true;
            if (string.IsNullOrWhiteSpace(unlockFlag))
                return false;

            GlobalDataManager global = ResolveLockGlobalData();
            return global != null && global.GetFlag(unlockFlag, 0) != 0;
        }
    }

    private bool UseLockedFallback => isLocked && lockedDialogue == null;

    protected override void Reset()
    {
        base.Reset();
        markerType = AreaMarkerType.ShortcutDoor;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (string.IsNullOrWhiteSpace(doorId))
            doorId = markerId;
        if (string.IsNullOrWhiteSpace(lockedFallbackText))
            lockedFallbackText = "잠겨 있다.";
    }

    public override bool CanInteract(PlayerController player)
    {
        // A locked door remains interactable so it can explain why it cannot move.
        return base.CanInteract(player);
    }

    protected virtual GlobalDataManager ResolveLockGlobalData()
    {
        return GlobalDataManager.Instance;
    }
    protected override void RequestConnection(PlayerController player)
    {
        if (!IsUnlocked)
        {
            ShowLockedFeedback();
            return;
        }

        RequestUnlockedConnection(player);
    }

    protected virtual bool ShowLockedFeedback()
    {
        bool started = TryStartDialogue(
            lockedDialogue,
            lockedFallbackText,
            lockedFallbackSpeaker,
            lockedFallbackEmotion);
        if (!started)
        {
            Debug.LogWarning(
                $"[ShortcutDoorMarker] 잠금 안내 실패: door={doorId}, linked={linkedDoorId}, unlockFlag={unlockFlag}",
                this);
        }

        return started;
    }

    protected virtual void RequestUnlockedConnection(PlayerController player)
    {
        base.RequestConnection(player);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(doorId))
            issues.Add("doorId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(linkedDoorId))
            issues.Add("linkedDoorId가 비어 있습니다.");
        if (isLocked && string.IsNullOrWhiteSpace(unlockFlag))
            issues.Add("잠금 사용 시 unlockFlag가 필요합니다.");
        if (isLocked && lockedDialogue == null && string.IsNullOrWhiteSpace(lockedFallbackText))
            issues.Add("잠금 DialogueData 또는 fallback 안내가 필요합니다.");
    }
}