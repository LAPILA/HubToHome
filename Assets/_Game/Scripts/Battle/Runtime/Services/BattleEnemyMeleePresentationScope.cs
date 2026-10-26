using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>공유 방어 시계의 접근·관통을 소유합니다. 연속 모드만 판정 유예 중에도 이동을 이어갑니다.</summary>
public sealed class BattleEnemyMeleePresentationScope : IDisposable
{
    private readonly EnemyCharacter _actor;
    private readonly Transform _root;
    private readonly CharacterGhostTrail _trail;
    private readonly Func<bool> _isActive;
    private readonly Func<bool> _isOwner;
    private readonly BattleEnemyMeleeDashPath _path;
    private readonly CameraController.EnemyMeleeCameraScope _camera;
    private readonly CameraController.ScreenPassFramingScope _framing;
    private readonly SpriteRenderer _body;
    private readonly bool _originalFlip;
    private readonly int _scene;
    private readonly bool _passThroughOnGuard;
    private Tween _movement;
    private bool _resolved;
    private bool _passThrough;
    private bool _completed;
    private bool _disposed;
    private bool _tailStarted;
    private float _tailElapsed;
    public float PreparationDuration => _camera?.PreparationDuration ?? 0f;
    public bool IsScreenWide => _path.IsScreenWide;
    public bool IsContinuous => _path.IsContinuous;
    public float ImpactTime => _path.ImpactTime;
    public bool IsPrepared { get; private set; }
    public bool IsActive => CanMove;

    public BattleEnemyMeleePresentationScope(EnemyCharacter actor, PlayerCharacter defender,
        BattleEnemyMeleeDashPath path, Func<bool> isActive, Func<bool> isOwner,
        CameraController.ScreenPassFramingScope framing = null, bool passThroughOnGuard = false)
    {
        _actor = actor;
        _root = actor.transform;
        _trail = actor.GetComponentInChildren<CharacterGhostTrail>();
        _path = path;
        _scene = actor.gameObject.scene.handle;
        _framing = framing;
        _passThroughOnGuard = passThroughOnGuard;
        _body = path.IsScreenWide || path.IsContinuous
            ? actor.GetComponent<SpriteRenderer>() ?? actor.GetComponentInChildren<SpriteRenderer>() : null;
        _originalFlip = _body != null && _body.flipX;
        _isActive = isActive;
        _isOwner = isOwner;
        if (!path.IsScreenWide && path.ApproachHeight <= 0f) { IsPrepared = true; CreateApproach(); }
        CameraController camera = CameraController.Instance;
        if (camera != null && !path.IsScreenWide)
            _camera = camera.BeginBattleEnemyMeleeShot(camera.BattleShotToken, actor, defender);
    }

    private bool OwnsMovement => !_disposed && (_isOwner == null || _isOwner());
    private bool CanMove => OwnsMovement && _actor != null && _actor.IsAlive
        && _actor.gameObject.activeInHierarchy && _actor.gameObject.scene.handle == _scene
        && (_isActive == null || _isActive()) && (_framing == null || _framing.IsActive);

    public IEnumerator Prepare()
    {
        if (!CanMove) yield break;
        _actor.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        if (IsPrepared)
        {
            if (_body != null) _body.flipX = _path.Direction > 0f ? !_originalFlip : _originalFlip;
            yield break;
        }
        bool needsEntry = !IsContinuous || (_root.position - _path.Entry).sqrMagnitude > 0.00000001f;
        if (needsEntry)
        {
            _movement = MoveTo(_path.Entry, _path.EntryDuration, IsContinuous ? Ease.Linear : Ease.OutCubic);
            float elapsed = 0f;
            while (elapsed < _path.EntryDuration && CanMove)
            {
                if (!Paused)
                {
                    elapsed = Mathf.Min(_path.EntryDuration, elapsed + Time.unscaledDeltaTime);
                    SetTrail(elapsed < _path.EntryDuration);
                    _movement.Goto(elapsed, false);
                }
                if (IsContinuous && elapsed >= _path.EntryDuration) break;
                yield return null;
            }
        }
        if (!CanMove) yield break;
        KillMovement();
        SetTrail(false);
        if (_body != null) _body.flipX = _path.Direction > 0f ? !_originalFlip : _originalFlip;
        IsPrepared = true;
        CreateApproach();
    }

    private static bool Paused => BattleDelay.IsPaused || GameInput.IsDefenseInputBlocked;

    private Tween MoveTo(Vector3 destination, float duration, Ease ease)
    {
        Vector3 origin = _root.position;
        // Pause/Goto만 사용하며, 새로운 소유자가 생겼을 때 이전 setter가 위치를 덮지 않습니다.
        return DOTween.To(() => _root != null ? _root.position : origin,
            value => { if (CanMove && !Paused) _root.position = value; }, destination, duration)
            .SetEase(ease).SetAutoKill(false).SetRecyclable(false).Pause()
            .SetLink(_actor.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void CreateApproach()
        => _movement = MoveTo(_path.Contact, _path.DashDuration,
            _path.IsScreenWide || IsContinuous ? Ease.Linear : Ease.InCubic);

    public void AdvanceAttack(float authoredTime)
    {
        if (!CanMove || !IsPrepared || _completed || (_resolved && !IsContinuous) || Paused
            || _movement == null || !_movement.IsActive() || float.IsNaN(authoredTime)) return;
        if (IsContinuous && authoredTime >= _path.ImpactTime)
        {
            if (!_tailStarted) _root.position = _path.Contact;
            BeginContinuousTail();
            _tailElapsed = Mathf.Clamp(authoredTime - _path.ImpactTime, _tailElapsed, _path.ExitDuration);
            SetTrail(_tailElapsed < _path.ExitDuration);
            _movement.Goto(_tailElapsed, false);
            return;
        }
        if (_tailStarted) return;
        float elapsed = _path.DashElapsed(authoredTime);
        SetTrail(elapsed > 0f && elapsed < _path.DashDuration);
        _movement.Goto(elapsed, false);
    }

    public void ResolveImpact(DefenseQteResult result)
    {
        if (!CanMove || _resolved) return;
        _resolved = true;
        if (IsContinuous && !result.IsCounterSuccess)
        {
            // 늦은 입력을 기다리는 동안에도 관통했으므로 접점으로 되감지 않습니다.
            _passThrough = true;
            BeginContinuousTail();
            _camera?.ResolveImpact(new Vector3(_path.Direction, 0f, 0f));
            return;
        }
        _root.position = _path.Contact;
        KillMovement();
        SetTrail(false);
        _passThrough = !result.IsCounterSuccess
            && (_passThroughOnGuard || (!result.IsGuard && !result.IsPerfectParry));
        if (_passThrough)
            _movement = MoveTo(_path.Exit, _path.ExitDuration, _path.IsScreenWide ? Ease.Linear : Ease.OutCubic);
        if (!result.IsCounterSuccess)
            _camera?.ResolveImpact(new Vector3(_path.Direction, 0f, 0f));
    }

    private void BeginContinuousTail()
    {
        if (_tailStarted) return;
        KillMovement();
        _movement = MoveTo(_path.Exit, _path.ExitDuration, Ease.Linear);
        _tailStarted = true;
    }

    /// <summary>피해/VFX 소비 뒤 호출합니다. 접근과 별도의 결과 시계만 사용합니다.</summary>
    public IEnumerator Finish()
    {
        if (!_resolved || !CanMove) yield break;
        if (_passThrough)
        {
            float elapsed = IsContinuous ? _tailElapsed : 0f;
            while (elapsed < _path.ExitDuration && CanMove)
            {
                if (!Paused)
                {
                    if (_movement == null || !_movement.IsActive()) yield break;
                    elapsed = Mathf.Min(_path.ExitDuration, elapsed + Time.unscaledDeltaTime);
                    if (IsContinuous) _tailElapsed = elapsed;
                    SetTrail(elapsed < _path.ExitDuration);
                    _movement.Goto(elapsed, false);
                }
                if (IsContinuous && elapsed >= _path.ExitDuration) break;
                yield return null;
            }
            if (!CanMove) yield break;
        }
        _completed = true;
    }

    private void SetTrail(bool value)
    {
        if (OwnsMovement && _trail != null) _trail.SetTrailActive(value);
    }

    private void KillMovement()
    {
        _movement?.Kill();
        _movement = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        bool owns = OwnsMovement && (_framing == null || _framing.IsActive);
        KillMovement();
        if (owns)
        {
            SetTrail(false);
            bool sameScene = _actor != null && _actor.gameObject.scene.handle == _scene;
            if (sameScene && _body != null) _body.flipX = _originalFlip;
            if (!_completed && sameScene && _actor.IsAlive && _actor.gameObject.activeInHierarchy)
                _root.position = _path.Start;
        }
        _disposed = true;
        _camera?.Dispose();
        _framing?.Dispose();
    }
}
