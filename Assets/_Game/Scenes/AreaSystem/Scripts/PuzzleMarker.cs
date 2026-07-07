using UnityEngine;

public class PuzzleMarker : AreaMarkerBase
{
    [Header("Puzzle")]
    [SerializeField] private string puzzleId;
    [SerializeField] private string solvedFlag;

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
        Debug.Log($"[PuzzleMarker] 퍼즐 시작/해결 요청: puzzleId={puzzleId}, solvedFlag={solvedFlag}", this);
        if (!string.IsNullOrWhiteSpace(solvedFlag)) GlobalDataManager.Instance?.SetFlag(solvedFlag, 1);
        CompleteMarker();
    }
}