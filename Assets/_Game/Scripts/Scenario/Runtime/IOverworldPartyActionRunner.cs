using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime-only presentation commands; never changes the saved battle roster.</summary>
public interface IOverworldPartyActionRunner
{
    bool TrySetMode(int slot, OverworldFollowerMode mode, out string error);
    bool TrySetVisible(int slot, bool visible, out string error);
    bool TryGather(out string error);
    bool TryBeginMove(int slot, IReadOnlyList<Vector2> waypoints, float duration, bool rejoin,
        out IOverworldPartyMoveOperation operation, out string error);
}

/// <summary>Cancel affects only this request, not a newer move on the same follower.</summary>
public interface IOverworldPartyMoveOperation
{
    bool IsDone { get; }
    bool WasCompleted { get; }
    bool IsCancelled { get; }
    string Error { get; }
    void Cancel();
}
