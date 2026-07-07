using UnityEngine;

public class SignMarker : AreaMarkerBase
{
    [Header("Sign")]
    [SerializeField, Tooltip("표지판 전용 DialogueData입니다. 비어 있으면 signText를 1노드 대사로 표시합니다.")]
    private DialogueData dialogueData;
    [TextArea(2, 6)] [SerializeField] private string signText;
    [SerializeField] private SpeakerData fallbackSpeaker;
    [SerializeField] private EmotionType fallbackEmotion = EmotionType.Normal;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Sign;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = true;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;

        bool started = TryStartDialogue(
            dialogueData,
            signText,
            fallbackSpeaker,
            fallbackEmotion,
            isOneShot ? CompleteMarker : null);

        if (!started)
            Debug.LogWarning($"[SignMarker] 표지판 대화 시작 실패: {DisplayName}", this);
    }
}