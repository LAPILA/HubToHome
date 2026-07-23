using UnityEngine;

public interface IScreenFlashScaleProvider
{
    float Scale { get; }
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
            return GameConfigPolicy.NormalizeUnit(
                value,
                GameConfigManager.DefaultFlashIntensity);
        }
    }
}
