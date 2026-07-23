using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public partial class CameraController
{
    [Title("전투 자동 프레이밍")]
    [SerializeField, InlineProperty, HideLabel]
    private CameraFramingSettings _battleFramingSettings = CameraFramingSettings.CreateBattleDefault();

    private readonly List<Transform> _validatedFrameTargets = new List<Transform>();
    private CinemachineTargetGroup _framingTargetGroup;
    private CinemachineGroupFraming _groupFraming;
    private CameraTarget _initialCameraTarget;
    private CameraTarget _preFramingCameraTarget;
    private GroupFramingSnapshot _groupFramingSnapshot;
    private bool _initialCameraTargetCaptured;
    private bool _ownsGroupFraming;
    private bool _isFramingTargets;

    public bool IsFramingTargets => _isFramingTargets;

    public bool TryFrameBattleTargets(
        IReadOnlyList<Transform> targets,
        out CameraCommandToken token,
        out string error)
    {
        return TryFrameTargetsCore(
            targets,
            _battleFramingSettings,
            CameraControlLease.None,
            out token,
            out error);
    }

    private bool TryFrameTargetsCore(
        IReadOnlyList<Transform> targets,
        CameraFramingSettings settings,
        CameraControlLease lease,
        out CameraCommandToken token,
        out string error)
    {
        token = default;
        if (!EnsureReady(out error) || !CanUseCamera(lease, out error))
        {
            return false;
        }

        if (!CollectFrameTargets(targets, out error))
        {
            return false;
        }

        EnsureFramingRig();
        if (_framingTargetGroup == null || _groupFraming == null)
        {
            error = "Cinemachine target-group framing is not ready.";
            return false;
        }

        CameraFramingSettings normalized = settings.Normalized();
        BeginFramingOwnership();

        _framingTargetGroup.Targets.Clear();
        for (int i = 0; i < _validatedFrameTargets.Count; i++)
        {
            _framingTargetGroup.AddMember(
                _validatedFrameTargets[i],
                1f,
                normalized.TargetRadius);
        }
        _framingTargetGroup.DoUpdate();

        _groupFraming.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
        _groupFraming.FramingSize = normalized.FramingSize;
        _groupFraming.CenterOffset = normalized.CenterOffset;
        _groupFraming.Damping = normalized.Damping;
        _groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.ZoomOnly;
        _groupFraming.LateralAdjustment = CinemachineGroupFraming.LateralAdjustmentModes.ChangePosition;
        _groupFraming.OrthoSizeRange = new Vector2(
            normalized.MinOrthographicSize,
            normalized.MaxOrthographicSize);
        _groupFraming.enabled = true;

        KillCameraTweens();
        ApplySettings(ResolveSettings(
            normalized.Style,
            normalized.MinOrthographicSize));
        _vCam.Lens.OrthographicSize = Mathf.Clamp(
            _vCam.Lens.OrthographicSize,
            normalized.MinOrthographicSize,
            normalized.MaxOrthographicSize);
        _vCam.Lens.Dutch = 0f;

        CameraTarget groupTarget = _vCam.Target;
        groupTarget.TrackingTarget = _framingTargetGroup.transform;
        groupTarget.CustomLookAtTarget = true;
        groupTarget.LookAtTarget = _framingTargetGroup.transform;
        _vCam.Target = groupTarget;

        token = new CameraCommandToken(++_commandVersion);
        error = string.Empty;
        return true;
    }

    private bool CollectFrameTargets(IReadOnlyList<Transform> targets, out string error)
    {
        _validatedFrameTargets.Clear();
        if (targets == null)
        {
            error = "Camera framing targets are missing.";
            return false;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null
                || !target.gameObject.activeInHierarchy
                || _validatedFrameTargets.Contains(target))
            {
                continue;
            }

            _validatedFrameTargets.Add(target);
        }

        if (_validatedFrameTargets.Count < 2)
        {
            error = "Camera framing requires at least two unique active targets.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void EnsureFramingRig()
    {
        if (_framingTargetGroup == null)
        {
            GameObject targetGroupObject = new GameObject("CameraTargetGroup_Runtime");
            targetGroupObject.hideFlags = HideFlags.HideAndDontSave;
            _framingTargetGroup = targetGroupObject.AddComponent<CinemachineTargetGroup>();
            _framingTargetGroup.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
            _framingTargetGroup.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
            _framingTargetGroup.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;
        }

        if (_groupFraming != null || _vCam == null)
        {
            return;
        }

        _groupFraming = _vCam.GetComponent<CinemachineGroupFraming>();
        if (_groupFraming == null)
        {
            _groupFraming = _vCam.gameObject.AddComponent<CinemachineGroupFraming>();
            _groupFraming.enabled = false;
            _ownsGroupFraming = true;
        }
    }

    private void BeginFramingOwnership()
    {
        if (_isFramingTargets)
        {
            return;
        }

        _preFramingCameraTarget = _vCam.Target;
        _groupFramingSnapshot = GroupFramingSnapshot.Capture(_groupFraming);
        _isFramingTargets = true;
    }

    private void StopTargetFraming()
    {
        if (!_isFramingTargets)
        {
            return;
        }

        _isFramingTargets = false;
        if (_groupFraming != null)
        {
            _groupFramingSnapshot.Restore(_groupFraming);
        }
        if (_framingTargetGroup != null)
        {
            _framingTargetGroup.Targets.Clear();
        }
        if (_vCam != null)
        {
            _vCam.Target = _preFramingCameraTarget;
        }

        _groupFramingSnapshot = default;
    }

    private void CaptureInitialCameraTarget()
    {
        if (_initialCameraTargetCaptured || _vCam == null)
        {
            return;
        }

        _initialCameraTarget = _vCam.Target;
        _initialCameraTargetCaptured = true;
    }

    private void PrepareForTimelineControl()
    {
        KillCameraTweens();
        StopTargetFraming();
        _commandVersion++;
    }

    private void ReleaseFramingStateOnDisable()
    {
        StopTargetFraming();
        _timelineLease = CameraControlLease.None;
        _commandVersion++;
        if (_vCam != null && _initialCameraTargetCaptured)
        {
            _vCam.Target = _initialCameraTarget;
        }
    }

    private void DisposeFramingRuntime()
    {
        StopTargetFraming();
        if (_framingTargetGroup != null)
        {
            DestroySafe(_framingTargetGroup.gameObject);
            _framingTargetGroup = null;
        }
        if (_ownsGroupFraming && _groupFraming != null)
        {
            DestroySafe(_groupFraming);
        }

        _groupFraming = null;
        _ownsGroupFraming = false;
    }

    private void LateUpdate()
    {
        if (!_isFramingTargets || _framingTargetGroup == null)
        {
            return;
        }

        int validTargetCount = 0;
        for (int i = 0; i < _framingTargetGroup.Targets.Count; i++)
        {
            CinemachineTargetGroup.Target member = _framingTargetGroup.Targets[i];
            if (member != null
                && member.Object != null
                && member.Weight > 0f
                && member.Object.gameObject.activeInHierarchy)
            {
                validTargetCount++;
            }
        }

        if (validTargetCount > 0)
        {
            return;
        }

        StopTargetFraming();
        if (!_timelineLease.IsValid)
        {
            ApplyResetImmediate(ResolveResetStyle());
        }
    }

    private struct GroupFramingSnapshot
    {
        public bool IsValid;
        public bool Enabled;
        public CinemachineGroupFraming.FramingModes FramingMode;
        public float FramingSize;
        public Vector2 CenterOffset;
        public float Damping;
        public CinemachineGroupFraming.SizeAdjustmentModes SizeAdjustment;
        public CinemachineGroupFraming.LateralAdjustmentModes LateralAdjustment;
        public Vector2 FovRange;
        public Vector2 DollyRange;
        public Vector2 OrthoSizeRange;

        public static GroupFramingSnapshot Capture(CinemachineGroupFraming framing)
        {
            if (framing == null)
            {
                return default;
            }

            return new GroupFramingSnapshot
            {
                IsValid = true,
                Enabled = framing.enabled,
                FramingMode = framing.FramingMode,
                FramingSize = framing.FramingSize,
                CenterOffset = framing.CenterOffset,
                Damping = framing.Damping,
                SizeAdjustment = framing.SizeAdjustment,
                LateralAdjustment = framing.LateralAdjustment,
                FovRange = framing.FovRange,
                DollyRange = framing.DollyRange,
                OrthoSizeRange = framing.OrthoSizeRange
            };
        }

        public void Restore(CinemachineGroupFraming framing)
        {
            if (!IsValid || framing == null)
            {
                return;
            }

            framing.enabled = false;
            framing.FramingMode = FramingMode;
            framing.FramingSize = FramingSize;
            framing.CenterOffset = CenterOffset;
            framing.Damping = Damping;
            framing.SizeAdjustment = SizeAdjustment;
            framing.LateralAdjustment = LateralAdjustment;
            framing.FovRange = FovRange;
            framing.DollyRange = DollyRange;
            framing.OrthoSizeRange = OrthoSizeRange;
            framing.enabled = Enabled;
        }
    }
}
