using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Keeps a 2D Cinemachine camera on its authored world-depth plane while allowing XY tracking.
/// </summary>
[DisallowMultipleComponent]
public sealed class CinemachineFixedDepthExtension : CinemachineExtension
{
    [SerializeField, Tooltip("Cinemachine 최종 출력에 적용할 월드 Z 깊이")]
    private float _worldDepth = -1f;

    public float WorldDepth => _worldDepth;

    public void SetWorldDepth(float worldDepth)
    {
        if (float.IsNaN(worldDepth) || float.IsInfinity(worldDepth)) return;
        _worldDepth = worldDepth;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase virtualCamera,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;

        Vector3 rawPosition = state.RawPosition;
        rawPosition.z = _worldDepth - state.PositionCorrection.z;
        state.RawPosition = rawPosition;
    }
}