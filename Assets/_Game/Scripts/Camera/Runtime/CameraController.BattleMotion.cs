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
    private float _actionZoomRatio = 1f;
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
    [Tooltip("출력 Pixel Perfect가 활성화된 일반 전투에서는 줌을 적용하지 않습니다.")]
    private float _actionImpactZoom = 0.025f;
    [Title("명중 컷 · 준비 고정 후 빠른 전환")]
    [SerializeField, LabelText("명중 컷 사용")]
    private bool _useBattleImpactCuts = true;
    [SerializeField, Range(0f, 0.12f), LabelText("공격 직전 구도 고정(초)")]
    private float _actionImpactPreparation = 0.05f;
    [SerializeField, Range(0f, 0.20f), LabelText("명중 방향 이동 거리")]
    private float _actionImpactOffset = 0.10f;
    [SerializeField, Range(0.001f, 0.08f), LabelText("명중 컷 진입(초)")]
    private float _actionImpactCutIn = 0.025f;
    [SerializeField, Range(0f, 0.08f), LabelText("명중 구도 유지(초)")]
    private float _actionImpactHold = 0.025f;
    [SerializeField, Range(0.001f, 0.20f), LabelText("명중 컷 복귀(초)")]
    private float _actionImpactReturn = 0.075f;
    [SerializeField, Range(0.06f, 0.30f), LabelText("연타 컷 최소 간격(초)")]
    private float _actionImpactMinInterval = 0.12f;
    [SerializeField, Range(0f, 1f), LabelText("특수 스킬 회전 강도 (0 = 끔)")]
    [Tooltip("픽셀 격자를 보존하는 동안 카메라 회전은 적용하지 않습니다.")]
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
    private Tween _battleImpactTween;
    private bool _battleImpactActive;
    private int _battleImpactVersion;
    private Vector3 _battleImpactOrigin;
    private float _battleImpactOriginLens;
    private CameraState _battleImpactOriginState;
    private float _battleImpactPreparationRemaining;
    private float _nextBattleImpactAt;
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
        StopBattleImpactBeat();
        _defenseCameraHolds++;
        SetBattleTweensPaused(true);
        _continuousPixelZoom?.Hold(_vCam, true);
        return new DefenseCameraHold(this, _battleShotVersion);
    }

    public void TrackBattleCounter(CharacterBase defender)
    {
        if (!_battleShotActive || defender == null || _battleShotVersion != _commandVersion) return;
        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
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
        if (UpdateBattleCounterFocus()) return;
        if (_battleShotActor == null || !_battleShotActor.gameObject.activeInHierarchy)
        {
            ResetCamera(0.3f);
            return;
        }
        bool inputHold = _defenseCameraHolds > 0 || GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f;
        if (!_useBattleImpactCuts || ResolveShakeScale() <= 0f || inputHold)
            StopBattleImpactBeat();
        if (!inputHold)
            _battleImpactPreparationRemaining = Mathf.Max(0f,
                _battleImpactPreparationRemaining - Time.unscaledDeltaTime);
        bool preparation = _battleImpactPreparationRemaining > 0f;
        // 판정 창 자체는 절대 움직이지 않습니다. 결과 확정 후의 grace에는 명중 컷만 허용합니다.
        bool hold = inputHold || IsDefenseCameraStable || preparation || _battleImpactActive;
        _continuousPixelZoom?.Hold(_vCam, inputHold || !_battleImpactActive && (IsDefenseCameraStable || preparation));
        SetBattleTweensPaused(hold);
        if (hold) return;
        if (UpdateEnemyMeleeCamera()) return;
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
        if (IsStrictBattlePixelView && _battlePixelLockedSize > 0f) return _battlePixelLockedSize;
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
        _battleFrameLens = ResolveBattleShotLens(_battleFramePosition)
            * (IsStrictBattlePixelView ? 1f : Mathf.Clamp(zoomRatio, 0.8f, 1.2f));
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
        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
        _authoredBattleBeat = true;
        ReframeBattleShot(Mathf.Max(0.05f, duration), zoomRatio);
        _battleRollTween?.Kill();
        // 한 바퀴를 마치고 일반 줌/베기로 넘어갈 때 360 → 0 역회전을 만들지 않습니다.
        // 회전 도중 취소된 경우도 동등한 각도에서 가장 짧게 정방향으로 돌아옵니다.
        if (Mathf.Approximately(rollDegrees, 0f))
            _vCam.Lens.Dutch = Mathf.DeltaAngle(0f, _vCam.Lens.Dutch);
        _battleRollTween = DOTween.To(() => _vCam.Lens.Dutch,
                value => { if (_vCam != null) _vCam.Lens.Dutch = value; },
                ResolveShakeScale() > 0f && !IsStrictBattlePixelView ? rollDegrees * _skillRollScale : 0f, Mathf.Max(0.05f, duration))
            .SetEase(Ease.InOutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
    }

    /// <summary>동일 행동의 토큰 안에서 공격 준비 대상을 확대합니다. 방어/Timeline 소유권은 빼앗지 않습니다.</summary>
    public bool TryFocusBattleAttacker(CameraCommandToken token, CharacterBase attacker, float zoomRatio, float duration)
    {
        if (attacker == null || !IsCurrent(token) || !_battleShotActive || _vCam == null
            || _battleShotVersion != _commandVersion || IsDefenseCameraStable) return false;
        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
        _authoredBattleBeat = true;
        _battlePositionTween?.Kill();
        _battleLensTween?.Kill();
        Vector3 point = attacker.GetPivot(CharacterPivotId.Center).position;
        point.y += _actionHeightOffset;
        _battlePositionTween = _battleShotTarget.DOMove(point, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        float lens = IsStrictBattlePixelView && _battlePixelLockedSize > 0f ? _battlePixelLockedSize
            : Mathf.Max(1f, _battleShotLens * Mathf.Clamp(zoomRatio, 0.5f, 1f));
        _battleLensTween = DOTween.To(() => _vCam.Lens.OrthographicSize,
                value => { if (_vCam != null) _vCam.Lens.OrthographicSize = value; }, lens, duration)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        return true;
    }

    public void EndBattleSkillBeats(CameraCommandToken token, float returnDuration = 0.16f)
    {
        if (!IsCurrent(token) || !_battleShotActive || !_authoredBattleBeat || _vCam == null) return;
        _authoredBattleBeat = false;
        _battleRollTween?.Kill();
        // 360도는 0도와 같으므로 역방향 한 바퀴가 발생하지 않도록 정규화합니다.
        _vCam.Lens.Dutch = Mathf.DeltaAngle(0f, _vCam.Lens.Dutch);
        _battleRollTween = DOTween.To(() => _vCam.Lens.Dutch,
                value => { if (_vCam != null) _vCam.Lens.Dutch = value; }, 0f, 0.14f)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        ReframeBattleShot(Mathf.Max(0.05f, returnDuration));
    }

    private void PlayBattleImpactBeat(float intensity)
    {
        RequestBattleHitCut(null, _battleShotActor, intensity);
    }

    /// <summary>기존 공격 시계는 그대로 두고 카메라 구도만 잠깐 고정합니다. 별도 대기를 만들지 않습니다.</summary>
    public void RequestBattleImpactPreparation(float duration = -1f)
    {
        if (!CanPlayBattleImpactCut() || IsDefenseCameraStable || _battleImpactActive) return;
        float requested = duration < 0f ? _actionImpactPreparation : duration;
        if (float.IsNaN(requested) || float.IsInfinity(requested)) return;
        _battleImpactPreparationRemaining = Mathf.Clamp(requested, 0f, 0.12f);
        if (_battleImpactPreparationRemaining <= 0f) return;
        SetBattleTweensPaused(true);
        _continuousPixelZoom?.Hold(_vCam, true);
    }

    /// <summary>현재 행동의 소유권 안에서 한 번만 튀는 명중 컷입니다. 연타/AoE는 중복 정지나 지연 재생을 하지 않습니다.</summary>
    public void RequestBattleHitCut(Transform source, Transform target, float strength = 1f)
    {
        if (!CanPlayBattleImpactCut() || target == null || !target.gameObject.activeInHierarchy
            || _defenseCameraHolds > 0 || _battleImpactActive || Time.unscaledTime < _nextBattleImpactAt
            || float.IsNaN(strength) || float.IsInfinity(strength) || strength <= 0f) return;
        float scale = Mathf.Clamp01(strength) * ResolveShakeScale();
        float zoom = Mathf.Clamp(_actionImpactZoom, 0f, 0.08f) * scale;
        float distance = Mathf.Clamp(_actionImpactOffset, 0f, 0.20f) * scale;
        if (zoom <= 0f && distance <= 0f) return;
        Vector3 direction = target.position - (source != null ? source.position : _battleShotTarget.position);
        direction.z = 0f;
        Vector3 offset = direction.sqrMagnitude > 0.000001f ? direction.normalized * distance : Vector3.zero;
        // 상하 이동이 시야를 흔들지 않도록 일반 컷은 좌우 방향을 중심으로 합니다.
        offset.y = Mathf.Clamp(offset.y, -distance * 0.35f, distance * 0.35f);
        StartBattleImpactCut(offset, zoom,
            new BattleImpactCameraEnvelope(_actionImpactCutIn, _actionImpactHold, _actionImpactReturn));
    }

    private void StartBattleImpactCut(Vector3 offset, float zoom, BattleImpactCameraEnvelope envelope)
    {
        _battleImpactPreparationRemaining = 0f;
        _continuousPixelZoom?.Hold(_vCam, false);
        // 진행 중이던 구도의 목적지를 보존하고 컷 복귀 뒤 같은 Tween을 재개합니다.
        SetPaused(_battlePositionTween, true);
        SetPaused(_battleLensTween, true);
        _battleImpactOrigin = _battleShotTarget.position;
        _battleImpactOriginLens = _vCam.Lens.OrthographicSize;
        _battleImpactOriginState = _vCam.State;
        Vector3 cutPosition = _battleImpactOrigin + offset;
        // 명중의 쾌감은 방향 컷으로 남기고, 일반 전투에서 렌즈 펌프는 만들지 않습니다.
        float cutLens = IsStrictBattlePixelView ? _battleImpactOriginLens : _battleImpactOriginLens * (1f - zoom);
        _battleImpactVersion = _commandVersion;
        _battleImpactActive = true;
        _nextBattleImpactAt = Time.unscaledTime + Mathf.Clamp(_actionImpactMinInterval, 0.06f, 0.30f);
        int version = _battleImpactVersion;
        float elapsed = 0f;
        _battleImpactTween = DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                if (!_battleImpactActive || version != _commandVersion || _vCam == null || _battleShotTarget == null) return;
                float amount = envelope.Evaluate(value);
                _battleShotTarget.position = Vector3.LerpUnclamped(_battleImpactOrigin, cutPosition, amount);
                _vCam.Lens.OrthographicSize = Mathf.LerpUnclamped(_battleImpactOriginLens, cutLens, amount);
            }, envelope.Duration, envelope.Duration)
            .SetEase(Ease.Linear).SetUpdate(true).SetRecyclable(false).SetLink(gameObject)
            .OnComplete(() =>
            {
                if (version != _commandVersion || !_battleImpactActive) return;
                _battleImpactTween = null;
                _battleImpactActive = false;
                CompleteEnemyMeleeCameraCut();
            });
    }

    private bool CanPlayBattleImpactCut()
    {
        return _useBattleImpactCuts && !IsStaticBattlePresentation && _battleShotActive
            && _battleShotVersion == _commandVersion && !_timelineLease.IsValid && !_authoredBattleBeat
            && _enemyMeleeCameraScope == null
            && _vCam != null && _battleShotTarget != null && ResolveShakeScale() > 0f
            && !GameInput.IsDefenseInputBlocked && Time.timeScale > 0f;
    }

    private void StopBattleImpactBeat()
    {
        _battleImpactTween?.Kill();
        _battleImpactTween = null;
        if (_battleImpactActive && _battleImpactVersion == _commandVersion && _vCam != null && _battleShotTarget != null)
        {
            _battleShotTarget.position = _battleImpactOrigin;
            _vCam.Lens.OrthographicSize = _battleImpactOriginLens;
            // 새 방어/모달이 같은 프레임에 열려도 직전 렌더의 컷 구도를 고정하지 않습니다.
            _continuousPixelZoom?.HoldState(_battleImpactOriginState);
        }
        _battleImpactActive = false;
        _battleImpactPreparationRemaining = 0f;
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
        BattleCounterFocusScope counterFocus = _battleCounterFocus;
        _battleCounterFocus = null;
        counterFocus?.Dispose();
        StopEnemyMeleeCamera(false);
        StopBattleImpactBeat();
        _nextBattleImpactAt = 0f;
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
