using UnityEngine;

public readonly struct BattleCameraViewportFrame
{
    public Vector2 Center { get; }
    public float Size { get; }
    public BattleCameraViewportFrame(Vector2 center, float size) { Center = center; Size = size; }
}

/// <summary>카메라 평면의 몸체 경계를 HUD를 제외한 정규화 화면 안에 넣습니다.</summary>
public static class BattleCameraViewportFraming
{
    public static Rect DefaultSafeViewport => new Rect(8f / 640f, 170f / 480f, 624f / 640f, 254f / 480f);

    /// <summary>확정된 픽셀 렌즈는 변경하지 않고 HUD 안전영역에 대한 중심만 보정합니다.</summary>
    public static BattleCameraViewportFrame FitAtFixedSize(Rect bounds, Rect safeViewport,
        Vector2 requestedCenter, float fixedSize, float aspect)
    {
        if (!Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height)
            || !Finite(bounds.xMax) || !Finite(bounds.yMax) || bounds.width < 0f || bounds.height < 0f)
            bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
        if (!Finite(safeViewport.x) || !Finite(safeViewport.y)
            || !Finite(safeViewport.width) || !Finite(safeViewport.height))
            safeViewport = DefaultSafeViewport;
        safeViewport = Rect.MinMaxRect(Mathf.Clamp01(safeViewport.xMin), Mathf.Clamp01(safeViewport.yMin),
            Mathf.Clamp01(safeViewport.xMax), Mathf.Clamp01(safeViewport.yMax));
        if (safeViewport.width < 0.1f || safeViewport.height < 0.1f) safeViewport = DefaultSafeViewport;
        aspect = Finite(aspect) && aspect > 0f ? Mathf.Max(0.1f, aspect) : 4f / 3f;
        float size = Finite(fixedSize) && fixedSize > 0f ? fixedSize : 4f;
        if (!Finite(requestedCenter.x)) requestedCenter.x = bounds.center.x;
        if (!Finite(requestedCenter.y)) requestedCenter.y = bounds.center.y;

        double left = bounds.xMax - (2d * safeViewport.xMax - 1d) * size * aspect;
        double right = bounds.xMin - (2d * safeViewport.xMin - 1d) * size * aspect;
        double bottom = bounds.yMax - (2d * safeViewport.yMax - 1d) * size;
        double top = bounds.yMin - (2d * safeViewport.yMin - 1d) * size;
        return new BattleCameraViewportFrame(new Vector2(
            ClampFixedCenter(requestedCenter.x, left, right),
            ClampFixedCenter(requestedCenter.y, bottom, top)), size);
    }

    public static BattleCameraViewportFrame Fit(Rect bounds, Rect safeViewport, Vector2 requestedCenter,
        float requestedSize, float aspect, float minimumSize)
    {
        if (!Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height)
            || bounds.width < 0f || bounds.height < 0f)
            bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
        if (!Finite(safeViewport.x) || !Finite(safeViewport.y)
            || !Finite(safeViewport.width) || !Finite(safeViewport.height))
            safeViewport = DefaultSafeViewport;
        safeViewport = Rect.MinMaxRect(Mathf.Clamp01(safeViewport.xMin), Mathf.Clamp01(safeViewport.yMin),
            Mathf.Clamp01(safeViewport.xMax), Mathf.Clamp01(safeViewport.yMax));
        if (safeViewport.width < 0.1f || safeViewport.height < 0.1f) safeViewport = DefaultSafeViewport;
        aspect = Finite(aspect) && aspect > 0f ? Mathf.Max(0.1f, aspect) : 4f / 3f;
        minimumSize = Finite(minimumSize) && minimumSize > 0f ? minimumSize : 4f;
        float size = Finite(requestedSize) && requestedSize > 0f ? requestedSize : minimumSize;
        size = Mathf.Max(Mathf.Max(size, minimumSize),
            Mathf.Max(bounds.width / (2f * aspect * safeViewport.width), bounds.height / (2f * safeViewport.height)));
        if (!Finite(requestedCenter.x)) requestedCenter.x = bounds.center.x;
        if (!Finite(requestedCenter.y)) requestedCenter.y = bounds.center.y;

        float left = bounds.xMax - (2f * safeViewport.xMax - 1f) * size * aspect;
        float right = bounds.xMin - (2f * safeViewport.xMin - 1f) * size * aspect;
        float bottom = bounds.yMax - (2f * safeViewport.yMax - 1f) * size;
        float top = bounds.yMin - (2f * safeViewport.yMin - 1f) * size;
        return new BattleCameraViewportFrame(new Vector2(
            ClampCenter(requestedCenter.x, left, right), ClampCenter(requestedCenter.y, bottom, top)), size);
    }

    private static float ClampCenter(float requested, float min, float max)
        => min <= max ? Mathf.Clamp(requested, min, max) : (min + max) * 0.5f;
    private static float ClampFixedCenter(float requested, double min, double max)
    {
        double center = min <= max ? System.Math.Max(min, System.Math.Min(requested, max)) : (min + max) * 0.5d;
        return (float)System.Math.Max(-float.MaxValue, System.Math.Min(center, float.MaxValue));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
