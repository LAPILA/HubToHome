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
        float resetDuration = 0.18f)
    {
        CameraController controller = CameraController.Instance;
        PositionManager positions = PositionManager.Instance;
        if (controller != null && !controller.IsStaticBattlePresentation && HasOpposingCombatants(targets))
        {
            if (controller.TryStartBattleShot(targets, out CameraCommandToken actionToken, out _))
                return new BattleCameraActionScope(controller, actionToken, resetDuration);
            return new BattleCameraActionScope(null, default, resetDuration);
        }
        if (controller != null && positions != null && positions.CenterTransform != null
            && HasOpposingCombatants(targets))
        {
            // 캐릭터의 회피/공격 이동을 추적하지 않고 고정된 중앙을 부드럽게 확대합니다.
            // 명시적 Timeline 카메라 lease가 있으면 TryFocus가 거절하므로 빼앗지 않습니다.
            if (controller.TryFocusBattleCenter(positions.CenterTransform, out CameraCommandToken centerToken, out _))
                return new BattleCameraActionScope(controller, centerToken, resetDuration);
            return new BattleCameraActionScope(null, default, resetDuration);
        }
        if (controller == null
            || controller.IsStaticBattlePresentation
            || !controller.TryFrameBattleTargets(targets, out CameraCommandToken token, out _))
        {
            return new BattleCameraActionScope(null, default, resetDuration);
        }

        return new BattleCameraActionScope(controller, token, resetDuration);
    }

    private static bool HasOpposingCombatants(IReadOnlyList<Transform> targets)
    {
        bool player = false, enemy = false;
        for (int i = 0; targets != null && i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            player |= target.TryGetComponent<PlayerCharacter>(out _);
            enemy |= target.TryGetComponent<EnemyCharacter>(out _);
            if (player && enemy) return true;
        }
        return false;
    }

    public static BattleCameraActionScope Begin(
        Transform first,
        Transform second,
        float resetDuration = 0.18f)
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
