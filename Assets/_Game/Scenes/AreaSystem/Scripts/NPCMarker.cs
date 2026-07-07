using UnityEngine;

public class NPCMarker : AreaMarkerBase
{
    [Header("NPC")]
    [SerializeField] private string npcId;
    [SerializeField] private string dialogueId;
    [SerializeField, Tooltip("실제 실행할 DialogueData입니다. 비어 있으면 fallbackDialogueText를 1노드 대사로 표시합니다.")]
    private DialogueData dialogueData;
    [SerializeField]
    private SpeakerData fallbackSpeaker;
    [SerializeField]
    private EmotionType fallbackEmotion = EmotionType.Normal;
    [TextArea(2, 6)] [SerializeField]
    private string fallbackDialogueText;

    protected override void Reset()
    {
        markerType = AreaMarkerType.NPC;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = true;
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
}