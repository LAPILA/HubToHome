using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public class PuzzleMarker : AreaMarkerBase
{
    [SerializeField, HideInInspector]
    private string puzzleId;

    [TitleGroup("Puzzle 설정/기본")]
    [InfoBox("Puzzle Runtime은 IPuzzleRuntime을 구현한 MonoBehaviour여야 합니다. 규칙·진행·저장·완료 효과는 각 Runtime이 소유합니다.")]
    [SerializeField, LabelText("Puzzle Runtime")]
    [FormerlySerializedAs("sequenceController")]
    private MonoBehaviour puzzleRuntimeSource;

    [SerializeField, HideInInspector]
    private string solvedFlag;

    [TitleGroup("Puzzle 설정/안내")]
    [SerializeField, LabelText("안내 DialogueData")]
    private DialogueData instructionDialogue;

    [TitleGroup("Puzzle 설정/안내")]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;

    [TitleGroup("Puzzle 설정/안내")]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    [TitleGroup("Puzzle 설정/안내")]
    [TextArea(2, 6)]
    [SerializeField, ShowIf(nameof(UsesFallbackInstruction)), LabelText("Fallback 안내")]
    private string fallbackInstructionText;

    public IPuzzleRuntime PuzzleRuntime =>
        puzzleRuntimeSource != null ? puzzleRuntimeSource as IPuzzleRuntime : null;
    public bool HasPuzzleRuntime => PuzzleRuntime != null;
    private bool UsesFallbackInstruction => HasPuzzleRuntime && instructionDialogue == null;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Puzzle;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (HasPuzzleRuntime)
            isOneShot = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player))
            return false;

        IPuzzleRuntime runtime = PuzzleRuntime;
        return runtime != null
            && !runtime.IsCompleted
            && runtime.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;

        IPuzzleRuntime runtime = PuzzleRuntime;
        if (runtime == null || !runtime.TryHandleMarkerInteraction(player))
            ShowInstruction();
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
                $"[PuzzleMarker] 퍼즐 안내를 시작하지 못했습니다: puzzleId={PuzzleRuntime?.PuzzleId}",
                this);
        }

        return started;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (puzzleRuntimeSource == null)
        {
            issues.Add("IPuzzleRuntime 구현 컴포넌트가 필요합니다.");
            return;
        }

        IPuzzleRuntime runtime = PuzzleRuntime;
        if (runtime == null)
        {
            issues.Add($"{puzzleRuntimeSource.GetType().Name}은 IPuzzleRuntime을 구현하지 않습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(runtime.PuzzleId))
            issues.Add("Puzzle Runtime의 PuzzleId가 비어 있습니다.");
        if (instructionDialogue == null && string.IsNullOrWhiteSpace(fallbackInstructionText))
            issues.Add("안내 DialogueData 또는 fallback 안내가 필요합니다.");
        if (!runtime.TryValidate(out string error))
            issues.Add("Puzzle Runtime 오류: " + error);
    }
}