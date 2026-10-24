using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public partial class CameraController
{
    [Title("적 근접 · 추적 준비와 돌파 컷")]
    [SerializeField, LabelText("근접 돌파 카메라 사용")]
    private bool _useEnemyMeleeCamera = true;
    [SerializeField, Range(0.04f, 0.25f), LabelText("적 중심 구도 진입(초)")]
    private float _enemyMeleeFocusDuration = 0.06f;
    [SerializeField, Range(0.5f, 0.8f), LabelText("준비 구도 적 비중")]
    private float _enemyMeleeActorWeight = 0.68f;
    [SerializeField, Range(0f, 0.7f), LabelText("돌파 방향 이동 거리")]
    private float _enemyMeleeImpactPan = 0.35f;
    [SerializeField, Range(0f, 0.08f), LabelText("돌파 순간 줌 강도")]
    private float _enemyMeleeImpactZoom = 0.04f;

    private EnemyMeleeCameraScope _enemyMeleeCameraScope;

    /// <summary>기존 준비 시간에 적 중심 구도로 이동합니다. 전조 판정의 hold와 명령 토큰은 그대로 유지합니다.</summary>
    public EnemyMeleeCameraScope BeginBattleEnemyMeleeShot(CameraCommandToken token,
        CharacterBase attacker, CharacterBase defender)
    {
        if (!_useEnemyMeleeCamera || !_useBattleImpactCuts || IsStaticBattlePresentation
            || attacker == null || defender == null || !IsCurrent(token) || !_battleShotActive
            || _battleShotVersion != _commandVersion || _timelineLease.IsValid || _authoredBattleBeat
            || _vCam == null || _battleShotTarget == null || IsDefenseCameraStable
            || ResolveShakeScale() <= 0f || GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f)
            return null;

        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
        var scope = new EnemyMeleeCameraScope(this, token,
            attacker.GetPivot(CharacterPivotId.Center), defender.GetPivot(CharacterPivotId.Center));
        _enemyMeleeCameraScope = scope;

        Vector3 point = Vector3.Lerp(scope.Defender.position, scope.Attacker.position,
            Mathf.Clamp(_enemyMeleeActorWeight, 0.5f, 0.8f));
        point.y += _actionHeightOffset;
        float lens = ResolveBattleShotLens(point);
        float duration = Mathf.Clamp(_enemyMeleeFocusDuration, 0.04f, 0.25f);
        scope.PreparationDuration = duration;
        _battlePositionTween?.Kill();
        _battleLensTween?.Kill();
        _battlePositionTween = _battleShotTarget.DOMove(point, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        _battleLensTween = DOTween.To(() => _vCam.Lens.OrthographicSize,
                value => { if (_vCam != null) _vCam.Lens.OrthographicSize = value; }, lens, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        return scope;
    }

    private bool OwnsEnemyMeleeCamera(EnemyMeleeCameraScope scope)
    {
        return ReferenceEquals(_enemyMeleeCameraScope, scope) && IsCurrent(scope.Token)
            && _battleShotActive && _battleShotVersion == _commandVersion && !_timelineLease.IsValid;
    }

    private void ResolveEnemyMeleeImpact(EnemyMeleeCameraScope scope, Vector3 attackDirection)
    {
        // 판정이 끝난 그 순간만 소비합니다. 막힌 요청은 늦게 재생하지 않습니다.
        if (!OwnsEnemyMeleeCamera(scope) || scope.IsDisposed || scope.ImpactResolved) return;
        scope.ImpactResolved = true;
        if (!_useEnemyMeleeCamera || !_useBattleImpactCuts || _defenseCameraHolds > 0
            || _authoredBattleBeat || _battleImpactActive
            || GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f || ResolveShakeScale() <= 0f
            || _vCam == null || _battleShotTarget == null || !IsFinite(attackDirection)) return;

        float direction = Mathf.Sign(attackDirection.x);
        if (Mathf.Approximately(attackDirection.x, 0f))
            direction = scope.Attacker != null && scope.Defender != null
                ? Mathf.Sign(scope.Defender.position.x - scope.Attacker.position.x) : 0f;
        float scale = ResolveShakeScale();
        float pan = Mathf.Clamp(_enemyMeleeImpactPan, 0f, 0.7f) * scale;
        float zoom = Mathf.Clamp(_enemyMeleeImpactZoom, 0f, 0.08f) * scale;
        if (pan <= 0f && zoom <= 0f) return;
        scope.HasImpactCut = true;
        StartBattleImpactCut(new Vector3(direction * pan, 0f, 0f), zoom,
            new BattleImpactCameraEnvelope(0.06f, 0.04f, 0.13f));
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private void EndEnemyMeleeCamera(EnemyMeleeCameraScope scope)
    {
        if (!OwnsEnemyMeleeCamera(scope)) return;
        // 돌파 이동이 먼저 끝나도 이미 시작한 컷은 유한한 복귀를 마친 뒤 짝 구도로 돌아갑니다.
        if (scope.HasImpactCut && _battleImpactActive) return;
        StopEnemyMeleeCamera(true);
    }

    private void CompleteEnemyMeleeCameraCut()
    {
        EnemyMeleeCameraScope scope = _enemyMeleeCameraScope;
        if (scope != null && scope.IsDisposed && OwnsEnemyMeleeCamera(scope))
            StopEnemyMeleeCamera(true);
    }

    private bool UpdateEnemyMeleeCamera()
    {
        EnemyMeleeCameraScope scope = _enemyMeleeCameraScope;
        if (scope == null) return false;
        if (!_useEnemyMeleeCamera || !_useBattleImpactCuts || ResolveShakeScale() <= 0f
            || !OwnsEnemyMeleeCamera(scope) || scope.Attacker == null || scope.Defender == null)
        {
            StopEnemyMeleeCamera(true);
            return false;
        }
        if (scope.IsDisposed && !_battleImpactActive)
        {
            StopEnemyMeleeCamera(true);
            return false;
        }
        return true;
    }

    private void StopEnemyMeleeCamera(bool restoreFrame)
    {
        EnemyMeleeCameraScope scope = _enemyMeleeCameraScope;
        _enemyMeleeCameraScope = null;
        if (scope == null) return;
        bool current = IsCurrent(scope.Token) && _battleShotActive && _battleShotVersion == _commandVersion
            && !_timelineLease.IsValid && _vCam != null && _battleShotTarget != null;
        scope.Invalidate();
        if (!restoreFrame || !current) return;
        ReframeBattleShot(_actionCameraSmoothTime);
    }

    /// <summary>한 근접 타격의 카메라 수명입니다. Dispose와 결과 요청은 더 새로운 연출을 덮지 않습니다.</summary>
    public sealed class EnemyMeleeCameraScope : IDisposable
    {
        private CameraController _owner;
        internal readonly CameraCommandToken Token;
        internal readonly Transform Attacker;
        internal readonly Transform Defender;
        internal bool ImpactResolved;
        internal bool HasImpactCut;
        internal bool IsDisposed;

        internal EnemyMeleeCameraScope(CameraController owner, CameraCommandToken token,
            Transform attacker, Transform defender)
        {
            _owner = owner;
            Token = token;
            Attacker = attacker;
            Defender = defender;
        }

        public bool IsActive => !IsDisposed && _owner != null && _owner.OwnsEnemyMeleeCamera(this);
        public float PreparationDuration { get; internal set; }

        public void ResolveImpact(Vector3 attackDirection)
        {
            if (_owner != null) _owner.ResolveEnemyMeleeImpact(this, attackDirection);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (_owner != null) _owner.EndEnemyMeleeCamera(this);
        }

        internal void Invalidate()
        {
            IsDisposed = true;
            _owner = null;
        }
    }
}
