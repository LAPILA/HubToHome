using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.U2D;

public partial class CameraController
{
    private ScreenPassFramingScope _screenPassFraming;

    /// <summary>관통 이동 동안만 현재 출력 구도를 보존합니다. Timeline/새 카메라 명령의 제어권은 빼앗지 않습니다.</summary>
    public bool TryBeginScreenPassFraming(CharacterBase actor, out ScreenPassFramingScope scope)
    {
        scope = null;
        BattleUIController ui = BattleUIController.Instance;
        if (actor == null || !isActiveAndEnabled || !_battleViewportActive || _timelineLease.IsValid
            || ui == null || !ui.TryGetBattlePlayfieldScreenRect(out Camera output, out Rect safe)
            || output == null || !output.orthographic
            || Vector3.Dot(output.transform.right, Vector3.right) < 0.999f
            || Vector3.Dot(output.transform.up, Vector3.up) < 0.999f) return false;

        float depth = Vector3.Dot(actor.transform.position - output.transform.position, output.transform.forward);
        if (depth <= 0f || float.IsNaN(depth) || float.IsInfinity(depth)) return false;
        Vector3 low = output.ScreenToWorldPoint(new Vector3(safe.xMin, safe.yMin, depth));
        Vector3 high = output.ScreenToWorldPoint(new Vector3(safe.xMax, safe.yMax, depth));
        Rect world = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        if (world.width <= 0f || world.height <= 0f) return false;

        SpriteRenderer body = actor.GetComponent<SpriteRenderer>();
        if (body == null) body = actor.GetComponentInChildren<SpriteRenderer>(true);
        Bounds bounds = body != null && body.sprite != null ? body.bounds
            : new Bounds(actor.transform.position, new Vector3(1.3f, 2f, 0f));
        PixelPerfectCamera pixel = output.GetComponent<PixelPerfectCamera>();
        int ppu = pixel != null && pixel.assetsPPU > 0 ? pixel.assetsPPU : 32;
        var next = new ScreenPassFramingScope(this, _commandVersion, output.transform.position,
            output.orthographicSize, world, (Vector2)(bounds.center - actor.transform.position),
            new Vector2(Mathf.Max(0.01f, bounds.extents.x), Mathf.Max(0.01f, bounds.extents.y)),
            ppu, StabilizeBattleDefense());
        ScreenPassFramingScope previous = _screenPassFraming;
        _screenPassFraming = next;
        previous?.Dispose();
        scope = next;
        return true;
    }

    private bool TryConstrainScreenPassFrame(ref CameraState state)
    {
        ScreenPassFramingScope scope = _screenPassFraming;
        if (scope == null || !scope.IsActive) return false;
        Vector3 current = state.GetFinalPosition();
        Vector3 held = scope.CameraPosition;
        held.z = current.z;
        state.PositionCorrection += held - current;
        state.Lens.OrthographicSize = scope.CameraSize;
        return true;
    }

    public sealed class ScreenPassFramingScope : IDisposable
    {
        private CameraController _owner;
        private readonly int _version;
        private IDisposable _motionHold;
        internal readonly Vector3 CameraPosition;
        internal readonly float CameraSize;
        public readonly Rect SafeWorld;
        public readonly Vector2 BodyOffset;
        public readonly Vector2 BodyHalfSize;
        public readonly int PixelsPerUnit;
        public float WorldUnitsPerReferencePixel => 2f * CameraSize / 480f;
        public bool IsActive => _owner != null && _owner.isActiveAndEnabled && _owner._battleViewportActive
            && !_owner._timelineLease.IsValid && _owner._commandVersion == _version
            && ReferenceEquals(_owner._screenPassFraming, this);

        internal ScreenPassFramingScope(CameraController owner, int version, Vector3 cameraPosition,
            float cameraSize, Rect safeWorld, Vector2 bodyOffset, Vector2 bodyHalfSize, int pixelsPerUnit,
            IDisposable motionHold)
        {
            _owner = owner;
            _version = version;
            CameraPosition = cameraPosition;
            CameraSize = cameraSize;
            SafeWorld = safeWorld;
            BodyOffset = bodyOffset;
            BodyHalfSize = bodyHalfSize;
            PixelsPerUnit = pixelsPerUnit;
            _motionHold = motionHold;
        }

        public void Dispose()
        {
            CameraController owner = _owner;
            _owner = null;
            if (owner != null && ReferenceEquals(owner._screenPassFraming, this)) owner._screenPassFraming = null;
            IDisposable hold = _motionHold;
            _motionHold = null;
            hold?.Dispose();
        }
    }
}
