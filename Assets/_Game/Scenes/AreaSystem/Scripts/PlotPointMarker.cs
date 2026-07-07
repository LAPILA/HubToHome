using UnityEngine;

public class PlotPointMarker : AreaMarkerBase
{
    [Header("Plot Point")]
    [SerializeField] private string plotId;
    [SerializeField] private AreaPlotTriggerMode triggerMode = AreaPlotTriggerMode.OnEnter;
    [SerializeField, Tooltip("플롯 이벤트와 함께 보여줄 DialogueData입니다. 비어 있으면 fallbackDialogueText를 사용합니다.")]
    private DialogueData dialogueData;
    [TextArea(2, 6)] [SerializeField]
    private string fallbackDialogueText;
    [SerializeField] private SpeakerData fallbackSpeaker;
    [SerializeField] private EmotionType fallbackEmotion = EmotionType.Normal;

    protected override void Reset()
    {
        markerType = AreaMarkerType.PlotPoint;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (triggerMode != AreaPlotTriggerMode.OnInteract) return;
        TriggerPlot(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerMode != AreaPlotTriggerMode.OnEnter) return;
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) TriggerPlot(player);
    }

    private void TriggerPlot(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        Debug.Log($"[PlotPointMarker] 플롯 이벤트 요청: plotId={plotId}, triggerMode={triggerMode}", this);

        bool started = TryStartDialogue(
            dialogueData,
            fallbackDialogueText,
            fallbackSpeaker,
            fallbackEmotion,
            isOneShot ? CompleteMarker : null);

        if (!started && isOneShot)
            CompleteMarker();
    }
}