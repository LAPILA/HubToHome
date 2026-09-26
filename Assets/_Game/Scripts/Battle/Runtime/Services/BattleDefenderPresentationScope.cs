using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>교전하는 아군/적의 중앙 위치와 방어 기준점을 한 행동 동안만 소유합니다.</summary>
public sealed class BattleDefenderPresentationScope : IDisposable
{
    public const float StepDistance = 1f;
    private const float MoveDuration = 0.2f;
    private readonly PlayerCharacter _player;
    private readonly PlayerController _controller;
    private readonly EnemyCharacter _enemy;
    private readonly Vector3 _enemyHomePosition;
    private readonly Vector3 _playerStagePosition;
    private readonly Vector3 _enemyStagePosition;
    private readonly float _moveDuration;
    private readonly bool _paired;
    private readonly Func<bool> _isActive;
    private Tween _movement;
    private bool _disposed;
    private bool _movementCompleted;
    public bool IsStaged { get; private set; }
    public PlayerCharacter Player => _player;
    public EnemyCharacter Enemy => _enemy;
    public Vector3 HomePosition { get; }
    public bool IsPaired => _paired;

    public BattleDefenderPresentationScope(PlayerCharacter player, Func<bool> isActive,
        EnemyCharacter enemy = null)
    {
        _player = player;
        _isActive = isActive;
        _controller = player != null ? player.GetComponent<PlayerController>() : null;
        if (_controller != null)
            _controller.ActiveDefensePresentation?.Dispose();
        HomePosition = _controller != null && _controller.State == PlayerController.PlayerState.InBattle
            ? _controller.BattleDefenseAnchorPosition : player != null ? player.transform.position : Vector3.zero;
        _enemy = enemy;
        _enemyHomePosition = enemy != null ? enemy.transform.position : Vector3.zero;
        PositionManager positions = PositionManager.Instance;
        _paired = enemy != null && positions != null && positions.CenterTransform != null;
        _moveDuration = _paired ? positions.DuelMoveDuration : MoveDuration;
        _playerStagePosition = _paired ? positions.GetDuelStagingPos(player)
            : HomePosition + Vector3.right * StepDistance;
        _enemyStagePosition = _paired ? positions.GetDuelStagingPos(enemy) : _enemyHomePosition;
        if (_controller != null)
            _controller.ActiveDefensePresentation = this;
    }

    private bool CanMove => !_disposed && _player != null && _player.IsAlive
        && _player.gameObject.activeInHierarchy && (_isActive == null || _isActive());

    public IEnumerator Enter()
    {
        if (!CanMove) yield break;
        if (_controller != null) _controller.ResetDefenseReactionLock();
        yield return MoveTo(_playerStagePosition, _paired ? _enemyStagePosition : (Vector3?)null);
        if (!CanMove || !_movementCompleted || (_paired && (_enemy == null || !_enemy.IsAlive)))
        {
            Dispose();
            yield break;
        }
        // 회피는 이 중앙 기준점에서 왼쪽으로 빠졌다가 되돌아옵니다.
        if (_controller != null) _controller.SnapToBattleAnchor(_playerStagePosition);
        else _player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        if (_paired && _enemy != null) _enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
        IsStaged = true;
    }

    public IEnumerator Return()
    {
        try
        {
            if (!CanMove) yield break;
            if (_controller != null) _controller.ResetDefenseReactionLock();
            yield return MoveTo(HomePosition, _paired ? _enemyHomePosition : (Vector3?)null);
        }
        finally { Dispose(); }
    }

    public bool TryGetStagingPosition(CharacterBase actor, out Vector3 position)
    {
        position = actor == _player ? _playerStagePosition : _enemyStagePosition;
        return IsStaged && _paired && actor != null && (actor == _player || actor == _enemy);
    }

    private IEnumerator MoveTo(Vector3 destination, Vector3? enemyDestination = null)
    {
        _movementCompleted = false;
        if (!CanMove) yield break;
        bool moveEnemy = enemyDestination.HasValue && _enemy != null && _enemy.IsAlive
            && _enemy.gameObject.activeInHierarchy;
        if ((_player.transform.position - destination).sqrMagnitude < 0.0001f
            && (!moveEnemy || (_enemy.transform.position - enemyDestination.Value).sqrMagnitude < 0.0001f))
        {
            _movementCompleted = true;
            yield break;
        }
        _player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        Sequence pair = DOTween.Sequence().Append(MoveActor(_player, destination));
        if (moveEnemy)
        {
            _enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
            pair.Join(MoveActor(_enemy, enemyDestination.Value));
        }
        _movement = pair.SetRecyclable(false).SetAutoKill(false)
            .SetLink(_player.gameObject, LinkBehaviour.KillOnDisable);
        try
        {
            while (CanMove && _movement.IsActive() && !_movement.IsComplete())
                yield return null;
            _movementCompleted = CanMove && _movement.IsActive() && _movement.IsComplete();
        }
        finally { ClearMovement(); }
    }

    private Tween MoveActor(CharacterBase actor, Vector3 destination)
    {
        // 한쪽이 피해 이벤트에서 먼저 파괴돼도 나머지 이동의 setter는 안전해야 합니다.
        return DOTween.To(() => actor != null ? actor.transform.position : destination,
            value => { if (actor != null) actor.transform.position = value; },
            destination, _moveDuration).SetEase(Ease.InOutSine).SetRecyclable(false);
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
        if (_paired && _enemy != null)
        {
            _enemy.transform.position = _enemyHomePosition;
            if (_enemy.IsAlive && _enemy.gameObject.activeInHierarchy)
                _enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
        }
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
