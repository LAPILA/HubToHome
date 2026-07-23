using UnityEngine;

public interface IScreenFlashScaleProvider
{
    float Scale { get; }
}

public static class VisualAccessibilityPolicy
{
    public static float NormalizeScale(float value)
    {
        return GameConfigPolicy.NormalizeUnit(value, 1f);
    }

    public static Color ScaleFlashColor(
        Color safeColor,
        Color authoredColor,
        float scale)
    {
        return Color.Lerp(
            safeColor,
            authoredColor,
            NormalizeScale(scale));
    }
}

public sealed class GameConfigScreenFlashScaleProvider : IScreenFlashScaleProvider
{
    public float Scale
    {
        get
        {
            float value = GameConfigManager.Instance != null
                ? GameConfigManager.Instance.FlashIntensity
                : GameConfigManager.DefaultFlashIntensity;
            return VisualAccessibilityPolicy.NormalizeScale(value);
        }
    }
}
