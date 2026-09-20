using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>피격 대상의 전진 위치와 방어 기준점을 한 공격 동안만 소유합니다.</summary>
public sealed class BattleDefenderPresentationScope : IDisposable
{
    public const float StepDistance = 1f;
    private const float MoveDuration = 0.2f;
    private readonly PlayerCharacter _player;
    private readonly PlayerController _controller;
    private readonly Func<bool> _isActive;
    private Tween _movement;
    private bool _disposed;
    private bool _movementCompleted;
    public bool IsStaged { get; private set; }
    public PlayerCharacter Player => _player;
    public Vector3 HomePosition { get; }

    public BattleDefenderPresentationScope(PlayerCharacter player, Func<bool> isActive)
    {
        _player = player;
        _isActive = isActive;
        _controller = player != null ? player.GetComponent<PlayerController>() : null;
        if (_controller != null)
            _controller.ActiveDefensePresentation?.Dispose();
        HomePosition = _controller != null && _controller.State == PlayerController.PlayerState.InBattle
            ? _controller.BattleDefenseAnchorPosition : player != null ? player.transform.position : Vector3.zero;
        if (_controller != null)
            _controller.ActiveDefensePresentation = this;
    }

    private bool CanMove => !_disposed && _player != null && _player.IsAlive
        && _player.gameObject.activeInHierarchy && (_isActive == null || _isActive());

    public IEnumerator Enter()
    {
        if (!CanMove) yield break;
        if (_controller != null) _controller.ResetDefenseReactionLock();
        Vector3 forward = HomePosition + Vector3.right * StepDistance;
        yield return MoveTo(forward);
        if (!CanMove || !_movementCompleted)
        {
            Dispose();
            yield break;
        }
        // 이후 X 회피/피격/방어 종료도 이 전진 지점으로 돌아옵니다.
        if (_controller != null) _controller.SnapToBattleAnchor(forward);
        else _player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        IsStaged = true;
    }

    public IEnumerator Return()
    {
        try
        {
            if (!CanMove) yield break;
            if (_controller != null) _controller.ResetDefenseReactionLock();
            yield return MoveTo(HomePosition);
        }
        finally { Dispose(); }
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        _movementCompleted = false;
        if (!CanMove) yield break;
        if ((_player.transform.position - destination).sqrMagnitude < 0.0001f)
        {
            _movementCompleted = true;
            yield break;
        }
        _player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        _movement = _player.transform.DOMove(destination, MoveDuration).SetEase(Ease.OutQuad)
            .SetRecyclable(false).SetAutoKill(false);
        try
        {
            while (CanMove && _movement.IsActive() && !_movement.IsComplete())
                yield return null;
            _movementCompleted = CanMove && _movement.IsActive() && _movement.IsComplete();
        }
        finally { ClearMovement(); }
    }

    private void ClearMovement()
    {
        if (_movement != null && _movement.IsActive()) _movement.Kill(false);
        _movement = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsStaged = false;
        ClearMovement();
        if (_controller != null && ReferenceEquals(_controller.ActiveDefensePresentation, this))
        {
            _controller.ActiveDefensePresentation = null;
            _controller.CloseDefenseInputWindow();
            _controller.SnapToBattleAnchor(HomePosition, _player != null && _player.IsAlive
                && _player.gameObject.activeInHierarchy);
        }
        else if (_player != null)
        {
            _player.transform.position = HomePosition;
            if (_player.IsAlive && _player.gameObject.activeInHierarchy)
                _player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        }
    }
}
