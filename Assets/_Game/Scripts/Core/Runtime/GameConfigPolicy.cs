using System;
using UnityEngine;

public static class GameConfigPolicy
{
    public const int ReferenceWidth = 640;
    public const int ReferenceHeight = 480;
    public const int WorldPixelsPerUnit = 32;
    public const int MinWindowScale = 1;
    public const int MaxWindowScale = 2;
    public const int MinTargetFps = 30;
    public const int MaxTargetFps = 240;

    public static float NormalizeFinite(
        float value,
        float minimum,
        float maximum,
        float fallback)
    {
        if (minimum > maximum)
            (minimum, maximum) = (maximum, minimum);

        if (float.IsNaN(fallback) || float.IsInfinity(fallback))
            fallback = minimum;
        if (float.IsNaN(value) || float.IsInfinity(value))
            value = fallback;

        return Mathf.Clamp(value, minimum, maximum);
    }

    public static float NormalizeUnit(float value, float fallback)
    {
        return NormalizeFinite(value, 0f, 1f, fallback);
    }

    public static int NormalizeWindowScale(int value)
    {
        return Mathf.Clamp(value, MinWindowScale, MaxWindowScale);
    }

    public static int NormalizeTargetFps(int value)
    {
        return Mathf.Clamp(value, MinTargetFps, MaxTargetFps);
    }

    public static LanguageType NormalizeLanguage(int value, LanguageType fallback)
    {
        return Enum.IsDefined(typeof(LanguageType), value)
            ? (LanguageType)value
            : fallback;
    }

    public static Vector2Int ResolveWindowSize(int scale)
    {
        int normalizedScale = NormalizeWindowScale(scale);
        return new Vector2Int(
            ReferenceWidth * normalizedScale,
            ReferenceHeight * normalizedScale);
    }

    public static Vector2 ReferenceResolution =>
        new Vector2(ReferenceWidth, ReferenceHeight);
}
