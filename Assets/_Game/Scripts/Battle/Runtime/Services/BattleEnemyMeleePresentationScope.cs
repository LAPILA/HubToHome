using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>방어 시계의 접근과 피해 소비 후의 돌파만 소유합니다. 전투 규칙은 바꾸지 않습니다.</summary>
public sealed class BattleEnemyMeleePresentationScope : IDisposable
{
    private readonly EnemyCharacter _actor;
    private readonly Transform _root;
    private readonly CharacterGhostTrail _trail;
    private readonly Func<bool> _isActive;
    private readonly Func<bool> _isOwner;
    private readonly BattleEnemyMeleeDashPath _path;
    private readonly CameraController.EnemyMeleeCameraScope _camera;
    private Tween _movement;
    private bool _resolved;
    private bool _passThrough;
    private bool _completed;
    private bool _disposed;
    public float PreparationDuration => _camera?.PreparationDuration ?? 0f;

    public BattleEnemyMeleePresentationScope(EnemyCharacter actor, PlayerCharacter defender,
        BattleEnemyMeleeDashPath path, Func<bool> isActive, Func<bool> isOwner)
    {
        _actor = actor;
        _root = actor.transform;
        _trail = actor.GetComponentInChildren<CharacterGhostTrail>();
        _path = path;
        _isActive = isActive;
        _isOwner = isOwner;
        _movement = _root.DOMove(path.Contact, path.DashDuration).SetEase(Ease.InCubic)
            .SetAutoKill(false).SetRecyclable(false).Pause().SetLink(actor.gameObject, LinkBehaviour.KillOnDisable);
        CameraController camera = CameraController.Instance;
        if (camera != null)
            _camera = camera.BeginBattleEnemyMeleeShot(camera.BattleShotToken, actor, defender);
    }

    private bool OwnsMovement => !_disposed && (_isOwner == null || _isOwner());
    private bool CanMove => OwnsMovement && _actor != null && _actor.IsAlive
        && _actor.gameObject.activeInHierarchy && (_isActive == null || _isActive());

    public void AdvanceAttack(float authoredTime)
    {
        if (!CanMove || _resolved || _movement == null || !_movement.IsActive()) return;
        float elapsed = _path.DashElapsed(authoredTime);
        SetTrail(elapsed > 0f && elapsed < _path.DashDuration);
        _movement.Goto(elapsed, false);
    }

    public void ResolveImpact(DefenseQteResult result)
    {
        if (!CanMove || _resolved) return;
        _resolved = true;
        _root.position = _path.Contact;
        KillMovement();
        SetTrail(false);
        _passThrough = !result.IsGuard && !result.IsPerfectParry && !result.IsCounterSuccess;
        if (_passThrough)
            _movement = _root.DOMove(_path.Exit, _path.ExitDuration).SetEase(Ease.OutCubic)
                .SetAutoKill(false).SetRecyclable(false).Pause().SetLink(_actor.gameObject, LinkBehaviour.KillOnDisable);
        if (!result.IsCounterSuccess)
            _camera?.ResolveImpact(new Vector3(_path.Direction, 0f, 0f));
    }

    /// <summary>피해/VFX 소비 뒤 호출합니다. 접근과 별도의 결과 시계만 사용합니다.</summary>
    public IEnumerator Finish()
    {
        if (!_resolved || !CanMove) yield break;
        if (_passThrough)
        {
            float elapsed = 0f;
            while (elapsed < _path.ExitDuration && CanMove)
            {
                if (!BattleDelay.IsPaused)
                {
                    if (_movement == null || !_movement.IsActive()) yield break;
                    elapsed = Mathf.Min(_path.ExitDuration, elapsed + Time.unscaledDeltaTime);
                    SetTrail(elapsed < _path.ExitDuration);
                    _movement.Goto(elapsed, false);
                }
                yield return null;
            }
            if (!CanMove) yield break;
        }
        _completed = true;
    }

    private void SetTrail(bool value)
    {
        if (_trail != null) _trail.SetTrailActive(value);
    }

    private void KillMovement()
    {
        _movement?.Kill();
        _movement = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        bool owns = OwnsMovement;
        KillMovement();
        if (owns)
        {
            SetTrail(false);
            if (!_completed && _actor != null && _actor.IsAlive && _actor.gameObject.activeInHierarchy)
                _root.position = _path.Start;
        }
        _disposed = true;
        _camera?.Dispose();
    }
}
