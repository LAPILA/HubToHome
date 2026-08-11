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
    public const int HandheldMinTargetFps = MinTargetFps;
    public const int HandheldMaxTargetFps = 60;

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

    public static int NormalizeTargetFps(int value, bool isHandheld)
    {
        if (!isHandheld)
            return NormalizeTargetFps(value);

        int midpoint = (HandheldMinTargetFps + HandheldMaxTargetFps) / 2;
        return value < midpoint ? HandheldMinTargetFps : HandheldMaxTargetFps;
    }

    public static int StepTargetFps(int current, int direction, bool isHandheld)
    {
        int normalized = NormalizeTargetFps(current, isHandheld);
        if (direction == 0)
            return normalized;

        int step = direction < 0 ? -30 : 30;
        return NormalizeTargetFps(normalized + step, isHandheld);
    }

    public static bool IsHandheldPlatform(RuntimePlatform platform, DeviceType deviceType)
    {
        return platform == RuntimePlatform.Android
            || platform == RuntimePlatform.IPhonePlayer
            || deviceType == DeviceType.Handheld;
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
