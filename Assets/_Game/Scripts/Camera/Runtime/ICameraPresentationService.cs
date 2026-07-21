using System;
using UnityEngine;

public readonly struct CameraCommandToken : IEquatable<CameraCommandToken>
{
    public CameraCommandToken(int version) { Version = version; }
    public int Version { get; }
    public bool IsValid => Version > 0;
    public bool Equals(CameraCommandToken other) => Version == other.Version;
    public override bool Equals(object obj) => obj is CameraCommandToken other && Equals(other);
    public override int GetHashCode() => Version;
}

public readonly struct CameraControlLease : IEquatable<CameraControlLease>
{
    public static readonly CameraControlLease None = default;
    public CameraControlLease(int version) { Version = version; }
    public int Version { get; }
    public bool IsValid => Version > 0;
    public bool Equals(CameraControlLease other) => Version == other.Version;
    public override bool Equals(object obj) => obj is CameraControlLease other && Equals(other);
    public override int GetHashCode() => Version;
}

public readonly struct CameraDefaultTargetSnapshot
{
    public CameraDefaultTargetSnapshot(Transform target, bool useGameplaySafeReset)
    {
        Target = target;
        UseGameplaySafeReset = useGameplaySafeReset;
    }

    public Transform Target { get; }
    public bool UseGameplaySafeReset { get; }
    public bool IsValid => Target != null;
}

public interface IScreenShakeScaleProvider
{
    float Scale { get; }
}

public sealed class GameConfigScreenShakeScaleProvider : IScreenShakeScaleProvider
{
    public float Scale
    {
        get
        {
            float value = GameConfigManager.Instance != null ? GameConfigManager.Instance.ScreenShake : 1f;
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }
    }
}

public interface ICameraPresentationService
{
    bool IsReady { get; }
    Transform DefaultTarget { get; }
    void SetDefaultTarget(Transform target, bool useGameplaySafeReset = false);
    CameraDefaultTargetSnapshot CaptureDefaultTarget();
    void RestoreDefaultTarget(CameraDefaultTargetSnapshot snapshot, float duration);
    bool TryAcquireTimelineControl(object owner, out CameraControlLease lease, out string error);
    void ReleaseTimelineControl(CameraControlLease lease);
    bool TryFocus(Transform target, float zoom, CameraShotStyle style, float duration,
        CameraControlLease lease, out CameraCommandToken token, out string error);
    bool TryReset(float duration, CameraShotStyle style, CameraControlLease lease,
        out CameraCommandToken token, out string error);
    bool TryImpulse(Vector3 direction, float intensity, float duration,
        CameraShakeSafety safety, out string error);
    bool IsCurrent(CameraCommandToken token);
    void Cancel(CameraCommandToken token, bool restoreDefault);
}
