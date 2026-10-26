using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One scene-owner lookup per context, never a per-action or per-frame scene search.</summary>
public sealed class OverworldPartyActionRunner : IOverworldPartyActionRunner
{
    private readonly OverworldPartyFollowerController _controller;

    public OverworldPartyActionRunner(OverworldPartyFollowerController controller)
    {
        _controller = controller;
    }

    public static IOverworldPartyActionRunner Resolve(Scene scene)
    {
        OverworldPartyFollowerController found = null;
        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null || !player.isActiveAndEnabled || player.gameObject.scene != scene)
                continue;

            // Only the registered field leader is eligible. Battle clones/lab actors must not
            // acquire a controller merely because an Action Sequence asks for this capability.
            OverworldPartyFollowerController candidate = OverworldPartyFollowerController.For(player);
            if (candidate == null)
                continue;
            if (found != null && found != candidate)
                return null; // Ambiguous scene ownership fails closed at the adapter.
            found = candidate;
        }

        return found != null ? new OverworldPartyActionRunner(found) : null;
    }

    public bool TrySetMode(int slot, OverworldFollowerMode mode, out string error)
    {
        return IsAvailable(out error) && _controller.TrySetMode(slot, mode, out error);
    }

    public bool TrySetVisible(int slot, bool visible, out string error)
    {
        return IsAvailable(out error) && _controller.TrySetVisible(slot, visible, out error);
    }

    public bool TryGather(out string error)
    {
        return IsAvailable(out error) && _controller.TryGather(out error);
    }

    public bool TryBeginMove(int slot, IReadOnlyList<Vector2> waypoints, float duration, bool rejoin,
        out IOverworldPartyMoveOperation operation, out string error)
    {
        operation = null;
        if (!IsAvailable(out error))
            return false;
        if (waypoints == null || waypoints.Count == 0)
        {
            error = "동료 이동에는 하나 이상의 명시 경유점/목표가 필요합니다.";
            return false;
        }
        if (!_controller.TryGetFollower(slot, out OverworldFollowerActor actor) || actor == null)
        {
            error = "해당 번호의 필드 동료가 없습니다: " + slot;
            return false;
        }

        var positions = new Vector3[waypoints.Count];
        float z = actor.transform.position.z;
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector3(waypoints[i].x, waypoints[i].y, z);
        if (!_controller.TryBeginMove(slot, positions, duration, rejoin,
                out OverworldFollowerMoveCommand command, out error))
            return false;
        operation = new MoveOperation(command);
        return true;
    }

    private bool IsAvailable(out string error)
    {
        bool available = _controller != null && _controller.isActiveAndEnabled;
        error = available ? string.Empty : "현재 씬의 필드 동료 제어기가 없거나 비활성 상태입니다.";
        return available;
    }

    private sealed class MoveOperation : IOverworldPartyMoveOperation
    {
        private readonly OverworldFollowerMoveCommand _command;
        public MoveOperation(OverworldFollowerMoveCommand command) { _command = command; }
        public bool IsDone => _command.IsDone;
        public bool WasCompleted => _command.WasCompleted;
        public bool IsCancelled => _command.IsCancelled;
        public string Error => _command.Error;
        public void Cancel() { _command.Cancel(); }
    }
}
