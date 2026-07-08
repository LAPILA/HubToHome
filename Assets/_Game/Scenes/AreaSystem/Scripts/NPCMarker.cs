using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class NPCMarker : AreaMarkerBase
{
    [TitleGroup("NPC 설정/기본")]
    [InfoBox("NPC는 기본적으로 반복 대화 가능하게 두는 편이 자연스럽습니다. 1회성 대화가 필요할 때만 '1회성'을 켜세요.")]
    [SerializeField, LabelText("NPC ID")] private string npcId;
    [TitleGroup("NPC 설정/기본")]
    [SerializeField, LabelText("대화 ID")]
    private string dialogueId;

    [TitleGroup("NPC 설정/대화")]
    [SerializeField, Tooltip("실제 실행할 DialogueData입니다. 비어 있으면 fallbackDialogueText를 1노드 대사로 표시합니다."), LabelText("DialogueData")]
    private DialogueData dialogueData;
    [TitleGroup("NPC 설정/대화")]
    [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;
    [TitleGroup("NPC 설정/대화")]
    [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;
    [TitleGroup("NPC 설정/대화")]
    [TextArea(2, 6)] [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback 대사")]
    private string fallbackDialogueText;

    private bool UseFallbackDialogue => dialogueData == null;

    protected override void Reset()
    {
        markerType = AreaMarkerType.NPC;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = false;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;

        bool started = TryStartDialogue(
            dialogueData,
            fallbackDialogueText,
            fallbackSpeaker,
            fallbackEmotion,
            isOneShot ? CompleteMarker : null);

        if (!started)
            Debug.LogWarning($"[NPCMarker] 대화 시작 실패: npcId={npcId}, dialogueId={dialogueId}", this);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(npcId))
            issues.Add("npcId가 비어 있습니다.");
        if (dialogueData == null && string.IsNullOrWhiteSpace(fallbackDialogueText))
            issues.Add("DialogueData 또는 fallbackDialogueText 중 하나는 필요합니다.");
    }
}