using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class PuzzleMarker : AreaMarkerBase
{
    [TitleGroup("Puzzle 설정/기본")]
    [InfoBox("Controller가 있으면 안내만 담당합니다. 완료 판정과 Flag 저장은 Controller가 소유합니다. Controller가 없으면 기존 즉시 완료 동작을 유지합니다.")]
    [SerializeField, LabelText("퍼즐 ID (연결용)")]
    private string puzzleId;

    [TitleGroup("Puzzle 설정/기본")]
    [SerializeField, LabelText("Sequence Controller")]
    private SequencePuzzleController sequenceController;

    [TitleGroup("Puzzle 설정/호환 모드")]
    [SerializeField, HideIf(nameof(UsesSequenceController)), LabelText("즉시 완료 플래그")]
    private string solvedFlag;

    [TitleGroup("Puzzle 설정/Controller 안내")]
    [SerializeField, ShowIf(nameof(UsesSequenceController)), LabelText("안내 DialogueData")]
    private DialogueData instructionDialogue;

    [TitleGroup("Puzzle 설정/Controller 안내")]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;

    [TitleGroup("Puzzle 설정/Controller 안내")]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    [TitleGroup("Puzzle 설정/Controller 안내")]
    [TextArea(2, 6)]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback 안내")]
    private string fallbackInstructionText;

    public bool UsesSequenceController => sequenceController != null;
    private bool UsesFallbackInstruction => UsesSequenceController && instructionDialogue == null;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Puzzle;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (sequenceController != null)
            isOneShot = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player))
            return false;

        if (sequenceController != null)
            return !sequenceController.IsCompleted;

        if (!string.IsNullOrWhiteSpace(solvedFlag) && GlobalDataManager.Instance != null)
            return GlobalDataManager.Instance.GetFlag(solvedFlag, 0) == 0;
        return true;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;

        if (sequenceController != null)
        {
            ShowInstruction();
            return;
        }

        AreaMarkerRuntimeService.CompletePuzzle(this, puzzleId, solvedFlag);
        CompleteMarker();
    }

    protected virtual bool ShowInstruction()
    {
        bool started = TryStartDialogue(
            instructionDialogue,
            fallbackInstructionText,
            fallbackSpeaker,
            fallbackEmotion);
        if (!started)
        {
            Debug.LogWarning(
                $"[PuzzleMarker] 퍼즐 안내를 시작하지 못했습니다: puzzleId={puzzleId}",
                this);
        }

        return started;
    }
    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(puzzleId))
            issues.Add("puzzleId가 비어 있습니다.");

        if (sequenceController == null)
        {
            if (string.IsNullOrWhiteSpace(solvedFlag))
                issues.Add("호환 모드에서는 solvedFlag가 필요합니다.");
            return;
        }

        if (sequenceController.gameObject == gameObject)
            issues.Add("Sequence Controller는 PuzzleMarker와 별도 GameObject에 배치해야 합니다.");
        if (instructionDialogue == null && string.IsNullOrWhiteSpace(fallbackInstructionText))
            issues.Add("Controller 모드에는 안내 DialogueData 또는 fallback 안내가 필요합니다.");
        if (!sequenceController.TryValidate(out string error))
            issues.Add("Sequence Controller 오류: " + error);
    }
}