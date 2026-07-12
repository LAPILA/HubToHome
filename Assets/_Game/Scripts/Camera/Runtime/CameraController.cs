using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class CameraController : MonoBehaviour, ICameraPresentationService
{
    public static CameraController Instance { get; private set; }

    private const string CameraZoomTweenId = "CameraZoom";
    private const string CameraImpactTweenId = "CameraImpact";
    private const string CameraDutchTweenId = "CameraDutch";
    private const string HitStopTweenId = "HitStop";

    [Title("컴포넌트 참조")]
    [SerializeField, Tooltip("시네마친 가상 카메라")]
    private CinemachineCamera _vCam;

    [SerializeField, Tooltip("카메라 흔들림 소스 (Impulse)")]
    private CinemachineImpulseSource _impulseSource;

    [SerializeField, Tooltip("기본 추적 타겟. 전투에서는 PositionManager.CenterTransform을 런타임 등록합니다.")]
    private Transform _centerTarget;

    [Title("기본 설정")]
    [SerializeField] private float _defaultLensSize = 5.5f;
    [SerializeField] private float _battleZoomSize = 4.0f;

    [Title("카메라 프리셋")]
    [SerializeField, AssetsOnly] private CameraShotProfile _staticProfile;
    [SerializeField, AssetsOnly] private CameraShotProfile _dynamicProfile;
    [SerializeField, AssetsOnly] private CameraShotProfile _gameplaySafeProfile;

    private CinemachinePositionComposer _positionComposer;
    private Transform _fallbackTarget;
    private Transform _startupTarget;
    private CameraShotSettings _startupSettings;
    private float _startupDutch;
    private int _commandVersion;
    private int _leaseVersion;
    private CameraControlLease _timelineLease;
    private bool _useGameplaySafeReset;
    private bool _warnedMissingCamera;
    private IScreenShakeScaleProvider _screenShakeScaleProvider;

    public CinemachineCamera VirtualCamera => _vCam;
    public Transform CenterTarget => _centerTarget;
    public Transform DefaultTarget => ResolveDefaultTarget();
    public bool IsReady => _vCam != null && _positionComposer != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _screenShakeScaleProvider = new GameConfigScreenShakeScaleProvider();
        InitializeCinemachine();
    }

    private void Start()
    {
        InitializeCinemachine();
        ApplyResetImmediate(ResolveResetStyle());
    }

    private void OnDisable()
    {
        KillCameraTweens();
        if (_positionComposer != null)
        {
            ApplySettings(_startupSettings);
        }
    }

    private void OnDestroy()
    {
        KillCameraTweens();
        if (_fallbackTarget != null)
        {
            DestroySafe(_fallbackTarget.gameObject);
            _fallbackTarget = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetScreenShakeScaleProvider(IScreenShakeScaleProvider provider)
    {
        _screenShakeScaleProvider = provider ?? new GameConfigScreenShakeScaleProvider();
    }

    public void SetDefaultTarget(Transform target, bool useGameplaySafeReset = false)
    {
        if (target == null)
        {
            return;
        }

        _centerTarget = target;
        _useGameplaySafeReset = useGameplaySafeReset;
    }

    public bool TryAcquireTimelineControl(object owner, out CameraControlLease lease, out string error)
    {
        lease = CameraControlLease.None;
        error = string.Empty;
        if (owner == null)
        {
            error = "Timeline camera owner is missing.";
            return false;
        }

        if (_timelineLease.IsValid)
        {
            error = "Camera is already controlled by an active Timeline.";
            return false;
        }

        _timelineLease = new CameraControlLease(++_leaseVersion);
        lease = _timelineLease;
        return true;
    }

    public void ReleaseTimelineControl(CameraControlLease lease)
    {
        if (!_timelineLease.IsValid || !_timelineLease.Equals(lease))
        {
            return;
        }

        _timelineLease = CameraControlLease.None;
    }

    public bool TryFocus(
        Transform target,
        float zoom,
        CameraShotStyle style,
        float duration,
        CameraControlLease lease,
        out CameraCommandToken token,
        out string error)
    {
        token = default;
        if (!ValidateCommand(target, zoom, duration, lease, out error))
        {
            return false;
        }

        CameraShotSettings settings = ResolveSettings(style, zoom);
        ApplyTrackingTarget(target);
        ApplySettings(settings);
        TweenLens(settings.OrthographicSize, duration);

        token = new CameraCommandToken(++_commandVersion);
        return true;
    }

    public bool TryReset(
        float duration,
        CameraShotStyle style,
        CameraControlLease lease,
        out CameraCommandToken token,
        out string error)
    {
        token = default;
        if (!EnsureReady(out error) || duration < 0f)
        {
            if (string.IsNullOrEmpty(error))
            {
                error = "Camera reset duration must be zero or greater.";
            }
            return false;
        }

        if (!CanUseCamera(lease, out error))
        {
            return false;
        }

        Transform target = ResolveDefaultTarget();
        if (target == null)
        {
            error = "Camera default target is missing.";
            return false;
        }

        CameraShotSettings settings = ResolveSettings(style, _defaultLensSize, true);
        ApplyTrackingTarget(target);
        ApplySettings(settings);
        TweenLens(settings.OrthographicSize, duration);
        TweenDutch(0f, duration);

        token = new CameraCommandToken(++_commandVersion);
        return true;
    }

    public bool TryImpulse(
        Vector3 direction,
        float intensity,
        float duration,
        CameraShakeSafety safety,
        out string error)
    {
        error = string.Empty;
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (_impulseSource == null || _impulseSource.ImpulseDefinition == null)
        {
            error = "Cinemachine Impulse Source is missing.";
            return false;
        }

        if (direction.sqrMagnitude <= 0.000001f)
        {
            error = "Camera impulse direction must not be zero.";
            return false;
        }

        if (intensity <= 0f || duration <= 0f)
        {
            error = "Camera impulse intensity and duration must be greater than zero.";
            return false;
        }

        CameraShotStyle style = safety == CameraShakeSafety.Cinematic
            ? CameraShotStyle.Dynamic
            : CameraShotStyle.GameplaySafe;
        CameraShotSettings settings = ResolveSettings(style, _vCam.Lens.OrthographicSize);
        float scale = ResolveShakeScale();
        float finalIntensity = Mathf.Min(intensity, settings.MaxImpulseIntensity) * scale;
        if (finalIntensity <= 0f)
        {
            return true;
        }

        Vector3 normalizedDirection = direction.normalized;
        CinemachineImpulseDefinition definition = CloneImpulseDefinition(
            _impulseSource.ImpulseDefinition,
            duration);
        definition.CreateAndReturnEvent(_impulseSource.transform.position, normalizedDirection * finalIntensity);

        float dutch = Mathf.Clamp(normalizedDirection.x * intensity, -settings.MaxDutch, settings.MaxDutch) * scale;
        _vCam.Lens.Dutch = dutch;
        TweenDutch(0f, duration);
        return true;
    }

    public bool IsCurrent(CameraCommandToken token)
    {
        return token.IsValid && token.Version == _commandVersion;
    }

    public void Cancel(CameraCommandToken token, bool restoreDefault)
    {
        if (!IsCurrent(token))
        {
            return;
        }

        KillCameraTweens();
        if (restoreDefault)
        {
            ApplyResetImmediate(ResolveResetStyle());
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (!EnsureReady(out string error) || newTarget == null)
        {
            return;
        }

        if (!CanUseCamera(CameraControlLease.None, out error))
        {
            WarnLegacy(error);
            return;
        }

        ApplyTrackingTarget(newTarget);
        _commandVersion++;
    }

    public void ZoomOnTransform(Transform target, float targetZoom, float duration = 0.3f)
    {
        if (!TryFocus(
                target,
                Mathf.Max(0.5f, targetZoom),
                CameraShotStyle.Dynamic,
                Mathf.Max(0f, duration),
                CameraControlLease.None,
                out _,
                out string error))
        {
            WarnLegacy(error);
        }
    }

    [Button("카메라 완전 리셋")]
    public void ResetCamera(float duration = 0.4f)
    {
        if (!TryReset(
                Mathf.Max(0f, duration),
                ResolveResetStyle(),
                CameraControlLease.None,
                out _,
                out string error))
        {
            WarnLegacy(error);
        }
    }

    public void ModePlayerAction(Transform playerTarget = null)
    {
        ZoomOnTransform(playerTarget != null ? playerTarget : ResolveDefaultTarget(), _battleZoomSize, 0.3f);
    }

    public void ModeEnemyAction() => ResetCamera(0.3f);

    public void PlayHeavySlam(Vector3 direction, float intensity = 1.0f, bool lockHorizontal = true)
    {
        Vector3 finalDirection = lockHorizontal
            ? new Vector3(direction.x, 0f, 0f)
            : new Vector3(direction.x, direction.y, 0f);
        if (finalDirection.sqrMagnitude <= 0.000001f)
        {
            finalDirection = Vector3.right;
        }

        if (!TryImpulse(finalDirection, Mathf.Max(0.001f, intensity), 0.3f, CameraShakeSafety.Cinematic, out string error))
        {
            WarnLegacy(error);
        }

        StopFrame(0.05f * Mathf.Max(0f, intensity));
    }

    public void PlayDashThroughImpact(float intensity = 1.0f)
    {
        if (!EnsureReady(out string error))
        {
            WarnLegacy(error);
            return;
        }

        float impactZoom = _defaultLensSize + 0.8f;
        DOTween.Kill(CameraZoomTweenId);
        DOTween.Kill(CameraImpactTweenId);
        DOTween.To(
                () => _vCam.Lens.OrthographicSize,
                value => _vCam.Lens.OrthographicSize = value,
                impactZoom,
                0.1f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late, true)
            .SetId(CameraImpactTweenId)
            .OnComplete(() => TweenLens(_defaultLensSize, 0.2f));

        TryImpulse(Vector3.right, Mathf.Max(0.001f, intensity), 0.2f, CameraShakeSafety.Cinematic, out _);
        StopFrame(0.06f);
    }

    private void InitializeCinemachine()
    {
        if (_vCam == null)
        {
            _vCam = GetComponent<CinemachineCamera>();
        }

        if (_impulseSource == null)
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        if (_vCam == null)
        {
            WarnLegacy("Cinemachine Camera is missing.");
            return;
        }

        _positionComposer = _vCam.GetComponent<CinemachinePositionComposer>();
        if (_positionComposer == null)
        {
            _positionComposer = _vCam.gameObject.AddComponent<CinemachinePositionComposer>();
        }

        _startupTarget = _vCam.Follow;
        _startupDutch = _vCam.Lens.Dutch;
        _startupSettings = CaptureSettings();
        if (_startupSettings.OrthographicSize <= 0f)
        {
            _startupSettings.OrthographicSize = Mathf.Max(0.5f, _vCam.Lens.OrthographicSize);
        }

        ResolveDefaultTarget();
    }

    private bool ValidateCommand(
        Transform target,
        float zoom,
        float duration,
        CameraControlLease lease,
        out string error)
    {
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (target == null)
        {
            error = "Camera focus target is missing.";
            return false;
        }

        if (zoom <= 0f)
        {
            error = "Camera focus zoom must be greater than zero.";
            return false;
        }

        if (duration < 0f)
        {
            error = "Camera focus duration must be zero or greater.";
            return false;
        }

        return CanUseCamera(lease, out error);
    }

    private bool EnsureReady(out string error)
    {
        if (_vCam != null && _positionComposer != null)
        {
            error = string.Empty;
            return true;
        }

        InitializeCinemachine();
        if (_vCam != null && _positionComposer != null)
        {
            error = string.Empty;
            return true;
        }

        error = "Cinemachine camera presentation is not ready.";
        return false;
    }

    private bool CanUseCamera(CameraControlLease lease, out string error)
    {
        if (!_timelineLease.IsValid || _timelineLease.Equals(lease))
        {
            error = string.Empty;
            return true;
        }

        error = "Camera focus/reset is locked by an active Timeline.";
        return false;
    }

    private Transform ResolveDefaultTarget()
    {
        if (_centerTarget != null)
        {
            return _centerTarget;
        }

        if (_startupTarget != null)
        {
            return _startupTarget;
        }

        if (_fallbackTarget == null && _vCam != null)
        {
            GameObject fallback = new GameObject("CameraDefaultTarget_Runtime");
            fallback.hideFlags = HideFlags.HideAndDontSave;
            Vector3 position = _vCam.transform.position;
            fallback.transform.position = new Vector3(position.x, position.y, 0f);
            _fallbackTarget = fallback.transform;
        }

        return _fallbackTarget;
    }

    private CameraShotStyle ResolveResetStyle()
    {
        return _useGameplaySafeReset ? CameraShotStyle.GameplaySafe : CameraShotStyle.Static;
    }

    private CameraShotSettings ResolveSettings(
        CameraShotStyle style,
        float lensSize,
        bool useProfileLens = false)
    {
        CameraShotProfile profile = style switch
        {
            CameraShotStyle.Dynamic => _dynamicProfile,
            CameraShotStyle.GameplaySafe => _gameplaySafeProfile,
            _ => _staticProfile
        };

        CameraShotSettings settings = profile != null
            ? profile.ToSettings(lensSize)
            : CameraShotSettings.CreateBuiltIn(style, lensSize);
        if (profile == null || !useProfileLens)
        {
            settings.OrthographicSize = Mathf.Max(0.5f, lensSize);
        }
        return settings;
    }

    private CameraShotSettings CaptureSettings()
    {
        CameraShotSettings settings = CameraShotSettings.CreateBuiltIn(
            CameraShotStyle.Static,
            _vCam != null ? _vCam.Lens.OrthographicSize : _defaultLensSize);
        if (_positionComposer == null)
        {
            return settings;
        }

        settings.Damping = _positionComposer.Damping;
        settings.ScreenPosition = _positionComposer.Composition.ScreenPosition;
        settings.EnableLookahead = _positionComposer.Lookahead.Enabled;
        settings.LookaheadTime = _positionComposer.Lookahead.Time;
        settings.LookaheadSmoothing = _positionComposer.Lookahead.Smoothing;
        return settings;
    }

    private void ApplyTrackingTarget(Transform target)
    {
        if (_vCam != null && target != null)
        {
            _vCam.Follow = target;
        }
    }

    private void ApplySettings(CameraShotSettings settings)
    {
        if (_positionComposer == null)
        {
            return;
        }

        _positionComposer.Damping = settings.Damping;
        var composition = _positionComposer.Composition;
        composition.ScreenPosition = settings.ScreenPosition;
        _positionComposer.Composition = composition;

        var lookahead = _positionComposer.Lookahead;
        lookahead.Enabled = settings.EnableLookahead;
        lookahead.Time = settings.LookaheadTime;
        lookahead.Smoothing = settings.LookaheadSmoothing;
        _positionComposer.Lookahead = lookahead;
    }

    private void ApplyResetImmediate(CameraShotStyle style)
    {
        if (!EnsureReady(out _))
        {
            return;
        }

        ApplyTrackingTarget(ResolveDefaultTarget());
        CameraShotSettings settings = style == CameraShotStyle.Static && !_useGameplaySafeReset && _staticProfile == null
            ? _startupSettings
            : ResolveSettings(style, _defaultLensSize, true);
        ApplySettings(settings);
        _vCam.Lens.OrthographicSize = settings.OrthographicSize;
        _vCam.Lens.Dutch = 0f;
        _commandVersion++;
    }

    private void TweenLens(float target, float duration)
    {
        DOTween.Kill(CameraZoomTweenId);
        if (_vCam == null)
        {
            return;
        }

        if (duration <= 0f)
        {
            _vCam.Lens.OrthographicSize = target;
            return;
        }

        DOTween.To(
                () => _vCam.Lens.OrthographicSize,
                value => _vCam.Lens.OrthographicSize = value,
                target,
                duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(UpdateType.Late, true)
            .SetId(CameraZoomTweenId);
    }

    private void TweenDutch(float target, float duration)
    {
        DOTween.Kill(CameraDutchTweenId);
        if (_vCam == null)
        {
            return;
        }

        if (duration <= 0f)
        {
            _vCam.Lens.Dutch = target;
            return;
        }

        DOTween.To(
                () => _vCam.Lens.Dutch,
                value => _vCam.Lens.Dutch = value,
                target,
                duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late, true)
            .SetId(CameraDutchTweenId);
    }

    private float ResolveShakeScale()
    {
        float value = _screenShakeScaleProvider != null ? _screenShakeScaleProvider.Scale : 1f;
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
    }

    private static CinemachineImpulseDefinition CloneImpulseDefinition(
        CinemachineImpulseDefinition source,
        float duration)
    {
        return new CinemachineImpulseDefinition
        {
            ImpulseChannel = source.ImpulseChannel,
            ImpulseShape = source.ImpulseShape,
            CustomImpulseShape = source.CustomImpulseShape != null
                ? new AnimationCurve(source.CustomImpulseShape.keys)
                : new AnimationCurve(),
            ImpulseDuration = Mathf.Max(0.001f, duration),
            ImpulseType = source.ImpulseType,
            DissipationRate = source.DissipationRate,
            RawSignal = source.RawSignal,
            AmplitudeGain = source.AmplitudeGain,
            FrequencyGain = source.FrequencyGain,
            RepeatMode = source.RepeatMode,
            Randomize = source.Randomize,
            TimeEnvelope = source.TimeEnvelope,
            ImpactRadius = source.ImpactRadius,
            DirectionMode = source.DirectionMode,
            DissipationMode = source.DissipationMode,
            DissipationDistance = source.DissipationDistance,
            PropagationSpeed = source.PropagationSpeed
        };
    }

    private void StopFrame(float duration)
    {
        DOTween.Kill(HitStopTweenId);
        Time.timeScale = 0.01f;
        DOVirtual.DelayedCall(duration, () => Time.timeScale = 1f)
            .SetUpdate(true)
            .SetId(HitStopTweenId);
    }

    private void KillCameraTweens()
    {
        DOTween.Kill(CameraZoomTweenId);
        DOTween.Kill(CameraImpactTweenId);
        DOTween.Kill(CameraDutchTweenId);
    }

    private void WarnLegacy(string message)
    {
        if (_warnedMissingCamera || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _warnedMissingCamera = true;
        Debug.LogWarning("[CameraController] " + message, this);
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
