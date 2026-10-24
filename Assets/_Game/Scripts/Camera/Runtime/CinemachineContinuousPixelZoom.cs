using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>기존 컴포넌트 이름은 보존하되 최종 렌즈·위치를 출력 Pixel Perfect 규칙에 맞춥니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-79)]
public sealed class CinemachineContinuousPixelZoom : CinemachineExtension
{
    private PixelPerfectCamera _outputPixelCamera;
    private bool _hold;
    private CameraState _heldState;
    private CameraController _battleViewportOwner;
    private CinemachineCamera _camera;
    private CinemachineBrain _outputBrain;
    private Camera _outputCamera;
    private bool _listeningForOutput;

    public void SetBattleViewportOwner(CameraController owner) => _battleViewportOwner = owner;
    public bool IsBattleViewportOwnedBy(CameraController owner) => owner != null && _battleViewportOwner == owner;
    public bool IsPixelPerfectActive => _outputPixelCamera != null && _outputPixelCamera.isActiveAndEnabled;
    public float PixelReferenceSize => IsPixelPerfectActive
        ? CameraPixelGridPolicy.ReferenceSize(_outputPixelCamera.refResolutionY, _outputPixelCamera.assetsPPU) : 7.5f;

    public void Begin(CinemachineCamera camera)
    {
        if (camera == null) return;
        _camera = camera;
        _outputBrain = CinemachineCore.FindPotentialTargetBrain(camera);
        _outputCamera = _outputBrain != null ? _outputBrain.OutputCamera : null;
        _outputPixelCamera = _outputCamera != null ? _outputCamera.GetComponent<PixelPerfectCamera>() : null;
        if (!_listeningForOutput && isActiveAndEnabled && _outputPixelCamera != null)
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(CorrectBlendedOutput);
            _listeningForOutput = true;
        }
        // 네이티브 CinemachinePixelPerfect의 enabled 상태를 빼앗지 않습니다.
    }

    public float RestingSize(float requested)
    {
        return IsPixelPerfectActive
            ? _outputPixelCamera.CorrectCinemachineOrthoSize(requested) : requested;
    }

    public void Hold(CinemachineCamera camera, bool hold)
    {
        if (hold && !_hold && camera != null) _heldState = camera.State;
        _hold = hold;
    }

    /// <summary>소유한 일회성 컷을 중단한 프레임에는 컷 이전의 최종 구도를 고정합니다.</summary>
    public void HoldState(CameraState state)
    {
        _heldState = state;
        _hold = true;
    }

    public void Release()
    {
        _hold = false;
        // 명령과 전투 시야 정책은 수명이 다릅니다. 정책은 명시적으로 분리할 때까지 유지합니다.
    }

    private void CorrectBlendedOutput(CinemachineBrain brain)
    {
        if (!isActiveAndEnabled || !IsPixelPerfectActive || brain != _outputBrain
            || _outputCamera == null || !_outputCamera.orthographic || _camera == null
            || !brain.IsLiveChild(_camera)) return;
        // Brain은 각 vcam의 Finalize 이후 렌즈를 다시 보간합니다. 그 최종 출력도 격자를 지킵니다.
        float size = _hold ? _heldState.Lens.OrthographicSize : _outputCamera.orthographicSize;
        if (!_hold && _battleViewportOwner != null
            && _battleViewportOwner.TryGetBattlePixelLockedSize(out float lockedSize)) size = lockedSize;
        _outputCamera.orthographicSize = _outputPixelCamera.CorrectCinemachineOrthoSize(size);
        // 최종 위치는 URP PPC가 렌더 직전 PixelSnap에서 정렬합니다. Transform/행렬은 중복 변경하지 않습니다.
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body && IsPixelPerfectActive)
        {
            state.Lens.OrthographicSize = _outputPixelCamera.CorrectCinemachineOrthoSize(state.Lens.OrthographicSize);
        }
        if (stage == CinemachineCore.Stage.Finalize)
        {
            if (_hold) state = _heldState;
            // 임의 카메라 회전은 원본 픽셀 격자와 양립하지 않습니다. 영상/UI 컷씬은 별도입니다.
            if (IsPixelPerfectActive) state.Lens.Dutch = 0f;
            if (!_hold && _battleViewportOwner != null) _battleViewportOwner.ConstrainBattleViewport(ref state);
            // 안전영역·Hold까지 적용한 최종값을 보정해야 Body 결과를 뒤에서 깨지 않습니다.
            if (IsPixelPerfectActive)
            {
                state.Lens.OrthographicSize = _outputPixelCamera.CorrectCinemachineOrthoSize(state.Lens.OrthographicSize);
                Vector3 position = state.GetFinalPosition();
                Vector3 snapped = _outputPixelCamera.RoundToPixel(position);
                snapped.z = position.z;
                state.PositionCorrection += snapped - position;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_camera != null) Begin(_camera);
    }

    private void DetachOutput()
    {
        Release();
        _battleViewportOwner = null;
        if (_listeningForOutput) CinemachineCore.CameraUpdatedEvent.RemoveListener(CorrectBlendedOutput);
        _listeningForOutput = false;
    }

    private void OnDisable() => DetachOutput();
    protected override void OnDestroy() { DetachOutput(); base.OnDestroy(); }
}
