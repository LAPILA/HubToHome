using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlotPointMarker : AreaMarkerBase
{
    [TitleGroup("Plot Point 설정/기본")]
    [SerializeField, LabelText("플롯 ID")] private string plotId;
    [TitleGroup("Plot Point 설정/기본")]
    [SerializeField, LabelText("발동 방식")]
    private AreaPlotTriggerMode triggerMode = AreaPlotTriggerMode.OnEnter;

    [TitleGroup("Plot Point 설정/표시")]
    [SerializeField, Tooltip("플롯 이벤트와 함께 보여줄 DialogueData입니다. 비어 있으면 fallbackDialogueText를 사용합니다."), LabelText("DialogueData")]
    private DialogueData dialogueData;
    [TitleGroup("Plot Point 설정/표시")]
    [TextArea(2, 6)] [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback 대사")]
    private string fallbackDialogueText;
    [TitleGroup("Plot Point 설정/표시")]
    [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;
    [TitleGroup("Plot Point 설정/표시")]
    [SerializeField, ShowIf(nameof(UseFallbackDialogue)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    private bool UseFallbackDialogue => dialogueData == null;

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

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(plotId))
            issues.Add("plotId가 비어 있습니다.");
        if (dialogueData == null && string.IsNullOrWhiteSpace(fallbackDialogueText))
            issues.Add("DialogueData 또는 fallbackDialogueText 중 하나는 필요합니다.");
    }
}