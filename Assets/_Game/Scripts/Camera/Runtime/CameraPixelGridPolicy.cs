using System;

/// <summary>출력 PPC의 기준 렌즈와 원본 픽셀 격자를 공유하는 순수 계산 정책입니다.</summary>
public static class CameraPixelGridPolicy
{
    private const int DefaultReferenceHeight = 480;
    private const int DefaultPixelsPerUnit = 32;
    private const float DefaultReferenceSize = 7.5f;
    private const double ZoomBoundaryTolerance = 0.000001d;

    public static float ReferenceSize(int referenceResolutionY, int pixelsPerUnit)
    {
        int height = referenceResolutionY > 0 ? referenceResolutionY : DefaultReferenceHeight;
        int ppu = pixelsPerUnit > 0 ? pixelsPerUnit : DefaultPixelsPerUnit;
        return (float)((double)height / (2d * ppu));
    }

    /// <summary>
    /// 필요한 시야보다 좁아지지 않는 정수 확대 렌즈를 선택합니다.
    /// 기준 렌즈보다 더 넓은 시야는 원본 픽셀 축소가 필요하므로 기준 렌즈에서 제한합니다.
    /// </summary>
    public static float SnapSizeAtLeast(float requiredSize, float referenceSize)
    {
        double native = PositiveFinite(referenceSize) ? referenceSize : DefaultReferenceSize;
        double required = PositiveFinite(requiredSize) ? requiredSize : native;
        double zoom = Math.Max(1d, Math.Floor(native / required + ZoomBoundaryTolerance));
        return (float)(native / zoom);
    }

    /// <summary>게임 Transform 대신 최종 카메라 출력 좌표를 원본 1픽셀 단위로 맞춥니다.</summary>
    public static float SnapCoordinate(float value, int pixelsPerUnit)
    {
        if (!Finite(value)) return 0f;
        int ppu = pixelsPerUnit > 0 ? pixelsPerUnit : DefaultPixelsPerUnit;
        return (float)(Math.Round((double)value * ppu, MidpointRounding.ToEven) / ppu);
    }

    private static bool PositiveFinite(float value) => Finite(value) && value > 0f;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
