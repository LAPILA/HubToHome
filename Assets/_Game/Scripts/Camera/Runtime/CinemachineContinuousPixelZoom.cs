using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>전투 숏 동안만 렌즈 양자화를 완화합니다. 출력 PixelPerfectCamera는 끄지 않습니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-79)]
public sealed class CinemachineContinuousPixelZoom : CinemachineExtension
{
    private CinemachinePixelPerfect _pixelExtension;
    private PixelPerfectCamera _outputPixelCamera;
    private bool _ownsPixelExtension;
    private bool _hold;
    private CameraState _heldState;

    public void Begin(CinemachineCamera camera)
    {
        if (_ownsPixelExtension || camera == null) return;
        _pixelExtension = camera.GetComponent<CinemachinePixelPerfect>();
        var brain = CinemachineCore.FindPotentialTargetBrain(camera);
        _outputPixelCamera = brain != null && brain.OutputCamera != null
            ? brain.OutputCamera.GetComponent<PixelPerfectCamera>() : null;
        if (_pixelExtension == null || !_pixelExtension.enabled
            || _outputPixelCamera == null || !_outputPixelCamera.isActiveAndEnabled) return;
        _ownsPixelExtension = true;
        _pixelExtension.enabled = false;
    }

    public float RestingSize(float requested)
    {
        return _ownsPixelExtension && _outputPixelCamera != null && _outputPixelCamera.isActiveAndEnabled
            ? _outputPixelCamera.CorrectCinemachineOrthoSize(requested) : requested;
    }

    public void Hold(CinemachineCamera camera, bool hold)
    {
        if (hold && !_hold && camera != null) _heldState = camera.State;
        _hold = hold;
    }

    public void Release()
    {
        _hold = false;
        if (_ownsPixelExtension && _pixelExtension != null) _pixelExtension.enabled = true;
        _ownsPixelExtension = false;
        _outputPixelCamera = null;
        _pixelExtension = null;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body && _ownsPixelExtension
            && _outputPixelCamera != null && _outputPixelCamera.isActiveAndEnabled)
        {
            // URP가 렌더 직전 크기를 재설정하지 않도록 Cinemachine 호환 모드는 유지합니다.
            // 반올림된 반환값은 의도적으로 사용하지 않습니다.
            _outputPixelCamera.CorrectCinemachineOrthoSize(state.Lens.OrthographicSize);
        }
        if (stage == CinemachineCore.Stage.Finalize && _hold) state = _heldState;
    }

    private void OnDisable() => Release();
    protected override void OnDestroy() { Release(); base.OnDestroy(); }
}
