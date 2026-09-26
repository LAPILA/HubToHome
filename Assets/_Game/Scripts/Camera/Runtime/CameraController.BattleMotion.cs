using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public partial class CameraController
{
    [Title("전투 카메라 · 짧은 구도 전환")]
    [SerializeField, Range(0.5f, 1f), LabelText("행동 줌 비율")]
    private float _actionZoomRatio = 0.70f;
    [SerializeField, Range(0.5f, 0.8f), LabelText("행동자 추적 비중")]
    private float _actionActorWeight = 0.62f;
    [SerializeField, Min(0.05f), LabelText("구도 전환 시간(초)")]
    private float _actionCameraSmoothTime = 0.16f;
    // 기존 프리팹 호환용. 상시 드리프트/호흡 줌은 더 이상 사용하지 않습니다.
    [SerializeField, HideInInspector] private float _actionCameraDrift;
    [SerializeField, HideInInspector] private float _actionZoomBreathing;
    [SerializeField, Min(0.1f), LabelText("재구도 최소 이동거리")]
    private float _actionReframeDistance = 0.75f;
    [SerializeField, Range(0f, 0.08f), LabelText("타격 줌 강도 (0 = 없음)")]
    private float _actionImpactZoom = 0.025f;
    [SerializeField, Range(0f, 1f), LabelText("특수 스킬 회전 강도 (0 = 끔)")]
    private float _skillRollScale = 1f;
    [SerializeField, Range(-2f, 2f), LabelText("구도 높이 보정")]
    private float _actionHeightOffset = -0.45f;
    [SerializeField, Range(0f, 0.3f), LabelText("방어 후 구도 유지(초)")]
    private float _defenseCameraHold = 0.12f;

    private Transform _battleShotTarget;
    private readonly List<Transform> _battleShotMembers = new List<Transform>(4);
    private Transform _battleShotActor;
    private CinemachineContinuousPixelZoom _continuousPixelZoom;
    private bool _ownsContinuousPixelZoom;
    private bool _battleShotActive;
    private int _battleShotVersion;
    private int _defenseCameraHolds;
    private float _defenseResumeAt;
    private float _battleShotLens;
    private Vector3 _battleFramePosition;
    private float _battleFrameLens;
    private float _nextBattleReframe;
    private Tween _battlePositionTween;
    private Tween _battleLensTween;
    private Tween _battleRollTween;
    private bool _authoredBattleBeat;
    private Transform _battleReturnTarget;
    private CameraTarget _preBattleCameraTarget;
    private bool IsDefenseCameraStable => _battleShotActive
        && (_defenseCameraHolds > 0 || Time.unscaledTime < _defenseResumeAt);
    public CameraCommandToken BattleShotToken => _battleShotActive
        ? new CameraCommandToken(_battleShotVersion) : default;

    public bool TryStartBattleShot(IReadOnlyList<Transform> targets,
        out CameraCommandToken token, out string error)
    {
        token = default;
        if (!EnsureReady(out error) || !CanUseCamera(CameraControlLease.None, out error)) return false;
        if (!CollectFrameTargets(targets, out error)) return false;
        StopBattleMotion();
        StopTargetFraming();
        KillCameraTweens();
        if (_battleShotTarget == null)
        {
            var target = new GameObject("BattleCameraTarget_Runtime") { hideFlags = HideFlags.HideAndDontSave };
            _battleShotTarget = target.transform;
        }
        _battleShotMembers.Clear();
        for (int i = 0; i < _validatedFrameTargets.Count; i++)
        {
            Transform target = _validatedFrameTargets[i];
            CharacterBase actor = target.GetComponent<CharacterBase>();
            _battleShotMembers.Add(actor != null ? actor.GetPivot(CharacterPivotId.Center) : target);
        }
        _battleShotActor = _battleShotMembers[0];
        // 이동과 줌은 같은 시간의 유한 Tween으로 끝냅니다. 후속 감쇠로 늘어지지 않습니다.
        _battleShotTarget.position = _vCam.Follow != null ? _vCam.Follow.position : ResolveBattleShotPosition();
        _battleShotLens = ResolveSettings(ResolveResetStyle(), _defaultLensSize, true).OrthographicSize
            * Mathf.Clamp(_actionZoomRatio, 0.5f, 1f);
        BeginContinuousBattleZoom();
        _preBattleCameraTarget = _vCam.Target;
        ApplyTrackingTarget(_battleShotTarget);
        float initialLens = _vCam.Lens.OrthographicSize;
        CameraShotSettings settings = ResolveSettings(CameraShotStyle.Dynamic, initialLens);
        settings.Damping = Vector3.zero;
        settings.EnableLookahead = false;
        ApplySettings(settings);
        _battleShotActive = true;
        _battleShotVersion = ++_commandVersion;
        _vCam.Lens.Dutch = 0f;
        ReframeBattleShot(_actionCameraSmoothTime);
        token = new CameraCommandToken(_commandVersion);
        error = string.Empty;
        return true;
    }

    public IDisposable StabilizeBattleDefense()
    {
        if (!_battleShotActive || _battleShotVersion != _commandVersion) return null;
        _defenseCameraHolds++;
        SetBattleTweensPaused(true);
        _continuousPixelZoom?.Hold(_vCam, true);
        return new DefenseCameraHold(this, _battleShotVersion);
    }

    public void TrackBattleCounter(CharacterBase defender)
    {
        if (!_battleShotActive || defender == null || _battleShotVersion != _commandVersion) return;
        _battleShotActor = defender.GetPivot(CharacterPivotId.Center);
        _defenseResumeAt = 0f;
        _nextBattleReframe = 0f;
    }

    private Vector3 ResolveBattleShotPosition()
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _battleShotMembers.Count; i++)
        {
            Transform member = _battleShotMembers[i];
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            center += member.position;
            count++;
        }
        if (count == 0) return ResolveDefaultTarget().position;
        center /= count;
        if (_battleShotActor != null)
            center = Vector3.Lerp(center, _battleShotActor.position, Mathf.Clamp01((_actionActorWeight - 0.5f) * 2f));
        center.y += _actionHeightOffset;
        return center;
    }

    private void UpdateBattleMotion()
    {
        if (!_battleShotActive || _battleShotVersion != _commandVersion || _vCam == null) return;
        if (_battleShotActor == null || !_battleShotActor.gameObject.activeInHierarchy)
        {
            ResetCamera(0.3f);
            return;
        }
        bool hold = IsDefenseCameraStable || GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f;
        _continuousPixelZoom?.Hold(_vCam, hold);
        SetBattleTweensPaused(hold);
        if (hold) return;
        if (_authoredBattleBeat || Time.unscaledTime < _nextBattleReframe) return;
        Vector3 point = ResolveBattleShotPosition();
        float lens = ResolveBattleShotLens(point);
        // 발밑의 미세한 모션은 추적하지 않습니다. 큰 위치 변화만 짧게 재구도합니다.
        if ((point - _battleFramePosition).sqrMagnitude >= _actionReframeDistance * _actionReframeDistance
            || Mathf.Abs(lens - _battleFrameLens) >= 0.5f)
            ReframeBattleShot(_actionCameraSmoothTime);
    }

    private float ResolveBattleShotLens(Vector3 point)
    {
        float lens = _battleShotLens;
        // 여러 대상이 있으면 전원 식별 가능한 화면 여백을 우선합니다.
        for (int i = 0; i < _battleShotMembers.Count; i++)
        {
            Transform member = _battleShotMembers[i];
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            Vector3 distance = member.position - point;
            float aspect = Mathf.Max(0.5f, _vCam.State.Lens.Aspect);
            float required = Mathf.Max((Mathf.Abs(distance.x) + 1.2f) / (aspect * 0.85f),
                (Mathf.Abs(distance.y) + 1.4f) / 0.65f);
            lens = Mathf.Max(lens, required);
        }
        return lens;
    }

    private void ReframeBattleShot(float duration, float zoomRatio = 1f)
    {
        _battleFramePosition = ResolveBattleShotPosition();
        _battleFrameLens = ResolveBattleShotLens(_battleFramePosition) * Mathf.Clamp(zoomRatio, 0.8f, 1.2f);
        _battlePositionTween?.Kill();
        _battleLensTween?.Kill();
        _battlePositionTween = _battleShotTarget.DOMove(_battleFramePosition, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        _battleLensTween = DOTween.To(() => _vCam.Lens.OrthographicSize,
                value => { if (_vCam != null) _vCam.Lens.OrthographicSize = value; }, _battleFrameLens, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        _nextBattleReframe = Time.unscaledTime + Mathf.Max(0.2f, duration);
    }

    /// <summary>행동 스코프의 명령 토큰을 유지하는 스킬 전용 구도. 각도는 누적값(180 → 360)입니다.</summary>
    public void PlayBattleSkillBeat(CameraCommandToken token, float zoomRatio, float rollDegrees, float duration = 0.18f)
    {
        if (!IsCurrent(token) || !_battleShotActive || _battleShotVersion != _commandVersion || _vCam == null
            || IsDefenseCameraStable) return;
        _authoredBattleBeat = true;
        ReframeBattleShot(Mathf.Max(0.05f, duration), zoomRatio);
        _battleRollTween?.Kill();
        // 한 바퀴를 마치고 일반 줌/베기로 넘어갈 때 360 → 0 역회전을 만들지 않습니다.
        // 회전 도중 취소된 경우도 동등한 각도에서 가장 짧게 정방향으로 돌아옵니다.
        if (Mathf.Approximately(rollDegrees, 0f))
            _vCam.Lens.Dutch = Mathf.DeltaAngle(0f, _vCam.Lens.Dutch);
        _battleRollTween = DOTween.To(() => _vCam.Lens.Dutch,
                value => { if (_vCam != null) _vCam.Lens.Dutch = value; },
                ResolveShakeScale() > 0f ? rollDegrees * _skillRollScale : 0f, Mathf.Max(0.05f, duration))
            .SetEase(Ease.InOutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
    }

    public void EndBattleSkillBeats(CameraCommandToken token)
    {
        if (!IsCurrent(token) || !_battleShotActive || !_authoredBattleBeat || _vCam == null) return;
        _authoredBattleBeat = false;
        _battleRollTween?.Kill();
        // 360도는 0도와 같으므로 역방향 한 바퀴가 발생하지 않도록 정규화합니다.
        _vCam.Lens.Dutch = Mathf.DeltaAngle(0f, _vCam.Lens.Dutch);
        _battleRollTween = DOTween.To(() => _vCam.Lens.Dutch,
                value => { if (_vCam != null) _vCam.Lens.Dutch = value; }, 0f, 0.14f)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        ReframeBattleShot(0.16f);
    }

    private void PlayBattleImpactBeat(float intensity)
    {
        if (!_battleShotActive || IsDefenseCameraStable || _authoredBattleBeat || _vCam == null
            || _actionImpactZoom <= 0f || GameInput.IsDefenseInputBlocked) return;
        _battleLensTween?.Kill();
        float lens = _battleFrameLens;
        _vCam.Lens.OrthographicSize = lens * (1f - _actionImpactZoom * Mathf.Clamp01(intensity) * ResolveShakeScale());
        _battleLensTween = DOTween.To(() => _vCam.Lens.OrthographicSize,
                value => { if (_vCam != null) _vCam.Lens.OrthographicSize = value; }, lens, 0.12f)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
    }

    private void SetBattleTweensPaused(bool paused)
    {
        SetPaused(_battlePositionTween, paused);
        SetPaused(_battleLensTween, paused);
        SetPaused(_battleRollTween, paused);
    }

    private static void SetPaused(Tween tween, bool paused)
    {
        if (tween == null || !tween.IsActive() || tween.IsComplete()) return;
        if (paused) tween.Pause();
        else if (!tween.IsPlaying()) tween.Play();
    }

    private void BeginContinuousBattleZoom()
    {
        if (_continuousPixelZoom == null)
        {
            _continuousPixelZoom = _vCam.GetComponent<CinemachineContinuousPixelZoom>();
            if (_continuousPixelZoom == null)
            {
                _continuousPixelZoom = _vCam.gameObject.AddComponent<CinemachineContinuousPixelZoom>();
                _ownsContinuousPixelZoom = true;
            }
        }
        _continuousPixelZoom.Begin(_vCam);
    }

    private void StopBattleMotion(bool releasePixelZoom = true)
    {
        _battlePositionTween?.Kill(); _battlePositionTween = null;
        _battleLensTween?.Kill(); _battleLensTween = null;
        _battleRollTween?.Kill(); _battleRollTween = null;
        if (_battleShotActive && _vCam != null) _vCam.Lens.Dutch = 0f;
        _authoredBattleBeat = false;
        if (_battleShotActive && _vCam != null && _vCam.Follow == _battleShotTarget)
            _vCam.Target = _preBattleCameraTarget;
        else if (_battleReturnTarget != null && _vCam != null && _vCam.Follow == _battleShotTarget)
            ApplyTrackingTarget(_battleReturnTarget);
        _battleReturnTarget = null;
        _battleShotActive = false;
        _battleShotMembers.Clear();
        _battleShotActor = null;
        _defenseCameraHolds = 0;
        _defenseResumeAt = 0f;
        if (_continuousPixelZoom != null)
        {
            _continuousPixelZoom.Hold(_vCam, false);
            if (releasePixelZoom) _continuousPixelZoom.Release();
        }
    }

    private void DisposeBattleMotion()
    {
        StopBattleMotion();
        if (_battleShotTarget != null) DestroySafe(_battleShotTarget.gameObject);
        if (_ownsContinuousPixelZoom && _continuousPixelZoom != null) DestroySafe(_continuousPixelZoom);
    }

    private sealed class DefenseCameraHold : IDisposable
    {
        private CameraController _owner;
        private readonly int _version;
        public DefenseCameraHold(CameraController owner, int version) { _owner = owner; _version = version; }
        public void Dispose()
        {
            CameraController owner = _owner;
            _owner = null;
            if (owner == null || !owner._battleShotActive || owner._battleShotVersion != _version) return;
            owner._defenseCameraHolds = Mathf.Max(0, owner._defenseCameraHolds - 1);
            owner._defenseResumeAt = Time.unscaledTime + owner._defenseCameraHold;
        }
    }
}
