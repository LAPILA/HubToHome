using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleCameraActionScope : IDisposable
{
    private CameraController _controller;
    private readonly CameraCommandToken _token;
    private readonly float _resetDuration;
    private bool _disposed;

    private BattleCameraActionScope(
        CameraController controller,
        CameraCommandToken token,
        float resetDuration)
    {
        _controller = controller;
        _token = token;
        _resetDuration = Mathf.Max(0f, resetDuration);
    }

    public CameraCommandToken Token => _token;
    public bool IsActive => !_disposed
        && _controller != null
        && _controller.IsCurrent(_token);

    public static BattleCameraActionScope Begin(
        IReadOnlyList<Transform> targets,
        float resetDuration = 0.4f)
    {
        CameraController controller = CameraController.Instance;
        if (controller == null
            || !controller.TryFrameBattleTargets(targets, out CameraCommandToken token, out _))
        {
            return new BattleCameraActionScope(null, default, resetDuration);
        }

        return new BattleCameraActionScope(controller, token, resetDuration);
    }

    public static BattleCameraActionScope Begin(
        Transform first,
        Transform second,
        float resetDuration = 0.4f)
    {
        return Begin(new[] { first, second }, resetDuration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CameraController controller = _controller;
        _controller = null;
        if (controller != null && controller.IsCurrent(_token))
        {
            controller.ResetCamera(_resetDuration);
        }
    }
}
