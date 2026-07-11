using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class PuzzleMarker : AreaMarkerBase
{
    [TitleGroup("Puzzle 설정")]
    [InfoBox("현재 PuzzleMarker는 퍼즐 미니게임을 열지 않고, 상호작용 시 solvedFlag를 즉시 세팅하는 임시 seam입니다.")]
    [SerializeField, LabelText("퍼즐 ID (연결용)")] private string puzzleId;
    [TitleGroup("Puzzle 설정")]
    [SerializeField, LabelText("즉시 완료 플래그")] private string solvedFlag;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Puzzle;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player)) return false;
        if (!string.IsNullOrWhiteSpace(solvedFlag) && GlobalDataManager.Instance != null)
            return GlobalDataManager.Instance.GetFlag(solvedFlag, 0) == 0;
        return true;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        AreaMarkerRuntimeService.CompletePuzzle(this, puzzleId, solvedFlag);
        CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(puzzleId))
            issues.Add("puzzleId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(solvedFlag))
            issues.Add("solvedFlag가 비어 있습니다.");
    }
}