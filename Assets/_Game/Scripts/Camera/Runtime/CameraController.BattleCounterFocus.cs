using System;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

public partial class CameraController
{
    private BattleCounterFocusScope _battleCounterFocus;

    /// <summary>C 성공 대기에서만 정수 두 배 확대합니다. 기본 전투 배율과 HUD는 변경하지 않습니다.</summary>
    public IDisposable BeginBattleCounterFocus(CharacterBase defender)
    {
        if (defender == null || !isActiveAndEnabled || !_useBattleViewportSafety || !_battleShotActive || _vCam == null
            || _battleShotVersion != _commandVersion || _timelineLease.IsValid) return null;

        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
        float baseline = _battlePixelLockedSize > 0f ? _battlePixelLockedSize : _vCam.Lens.OrthographicSize;
        float size = Mathf.Max(0.01f, baseline * 0.5f);
        if (IsStrictBattlePixelView)
            size = CameraPixelGridPolicy.SnapSizeAtLeast(size, _continuousPixelZoom.PixelReferenceSize);

        Vector3 from = _vCam.State.GetFinalPosition();
        Vector3 point = defender.GetPivot(CharacterPivotId.Center).position;
        BattleUIController ui = BattleUIController.Instance;
        if (ui != null && ui.TryGetBattlePlayfieldScreenRect(out Camera output, out Rect safe)
            && output != null && output.pixelRect.height > 0f && output.pixelRect.width > 0f)
        {
            Rect pixels = output.pixelRect;
            // 확대해도 배우를 화면 중앙이 아닌 HUD 위의 실제 전투 영역 중앙에 둡니다.
            point.x -= ((safe.center.x - pixels.xMin) / pixels.width - 0.5f) * 2f * size * output.aspect;
            point.y -= ((safe.center.y - pixels.yMin) / pixels.height - 0.5f) * 2f * size;
            from = output.transform.position;
        }
        point.z = from.z;
        var next = new BattleCounterFocusScope(this, defender, _commandVersion, from, point, size);
        BattleCounterFocusScope previous = _battleCounterFocus;
        _battleCounterFocus = next;
        previous?.Dispose();
        _defenseResumeAt = 0f;
        _continuousPixelZoom?.Hold(_vCam, false);
        SetBattleTweensPaused(true);
        next.Begin();
        return next;
    }

    private bool UpdateBattleCounterFocus()
    {
        BattleCounterFocusScope scope = _battleCounterFocus;
        if (scope == null) return false;
        if (!scope.IsActive)
        {
            scope.Dispose();
            return false;
        }
        _continuousPixelZoom?.Hold(_vCam, false);
        SetBattleTweensPaused(true);
        scope.SetPaused(BattleDelay.IsPaused || Time.timeScale <= 0f);
        return true;
    }

    private bool TryConstrainBattleCounterFocus(ref CameraState state)
    {
        BattleCounterFocusScope scope = _battleCounterFocus;
        if (scope == null || !scope.IsActive) return false;
        Vector3 current = state.GetFinalPosition();
        Vector3 focused = scope.Position;
        focused.z = current.z;
        state.PositionCorrection += focused - current;
        state.Lens.OrthographicSize = scope.Size;
        return true;
    }

    private sealed class BattleCounterFocusScope : IDisposable
    {
        private CameraController _owner;
        private readonly CharacterBase _defender;
        private readonly int _version;
        private readonly Vector3 _destination;
        private Tween _move;
        public Vector3 Position;
        public readonly float Size;
        public bool IsActive => _owner != null && _owner.isActiveAndEnabled && _defender != null
            && _defender.gameObject.activeInHierarchy && _owner._useBattleViewportSafety && _owner._battleShotActive
            && !_owner._timelineLease.IsValid && _owner._commandVersion == _version
            && ReferenceEquals(_owner._battleCounterFocus, this);

        public BattleCounterFocusScope(CameraController owner, CharacterBase defender, int version,
            Vector3 from, Vector3 destination, float size)
        {
            _owner = owner;
            _defender = defender;
            _version = version;
            Position = from;
            _destination = destination;
            Size = size;
        }

        public void Begin()
        {
            _move = DOTween.To(() => Position, value => { if (IsActive) Position = value; }, _destination, 0.14f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false);
            SetPaused(BattleDelay.IsPaused || Time.timeScale <= 0f);
        }

        public void SetPaused(bool paused)
        {
            if (_move == null || !_move.IsActive() || _move.IsComplete()) return;
            if (paused) _move.Pause();
            else if (!_move.IsPlaying()) _move.Play();
        }

        public void Dispose()
        {
            CameraController owner = _owner;
            bool restore = IsActive;
            _owner = null;
            _move?.Kill(false);
            _move = null;
            if (owner == null || !ReferenceEquals(owner._battleCounterFocus, this)) return;
            owner._battleCounterFocus = null;
            if (!restore) return;
            // 같은 행동의 전투 배율로 돌아갑니다. 새 명령/Timeline은 복구하지 않습니다.
            owner._continuousPixelZoom?.Hold(owner._vCam, false);
            owner._defenseResumeAt = 0f;
            owner._nextBattleReframe = 0f;
        }
    }
}
