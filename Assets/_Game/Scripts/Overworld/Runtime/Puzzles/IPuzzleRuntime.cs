/// <summary>
/// Contract between a PuzzleMarker and any puzzle-specific runtime implementation.
/// The implementation owns rules, progress, persistence, reset, and completion effects.
/// </summary>
public interface IPuzzleRuntime
{
    string PuzzleId { get; }
    bool IsCompleted { get; }
    bool CanInteract(PlayerController player);
    bool TryHandleMarkerInteraction(PlayerController player);
    bool TryValidate(out string error);
}