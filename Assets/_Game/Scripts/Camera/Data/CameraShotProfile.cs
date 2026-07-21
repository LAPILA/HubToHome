using System;
using Sirenix.OdinInspector;
using UnityEngine;

public static class CameraLensDefaults
{
    public const float GameplayOrthographicSize = 4f;
    public const float BattleActionOrthographicSize = 3.5f;
}

public enum CameraShotStyle
{
    Static,
    Dynamic,
    GameplaySafe
}

public enum CameraShakeSafety
{
    GameplaySafe = 0,
    Cinematic = 1
}

[Serializable]
public struct CameraShotSettings
{
    public float OrthographicSize;
    public Vector3 Damping;
    public Vector2 ScreenPosition;
    public bool EnableLookahead;
    public float LookaheadTime;
    public float LookaheadSmoothing;
    public float MaxImpulseIntensity;
    public float MaxDutch;

    public static CameraShotSettings CreateBuiltIn(CameraShotStyle style, float lensSize)
    {
        float safeLens = Mathf.Max(0.5f, lensSize);
        switch (style)
        {
            case CameraShotStyle.Static:
                return Create(safeLens, Vector3.zero, false, 0f, 0f, 0.35f, 0f);
            case CameraShotStyle.Dynamic:
                return Create(safeLens, new Vector3(0.35f, 0.28f, 0f), true, 0.12f, 8f, 1.2f, 4f);
            default:
                return Create(safeLens, new Vector3(0.12f, 0.1f, 0f), false, 0f, 0f, 0.55f, 1.25f);
        }
    }

    private static CameraShotSettings Create(
        float lensSize,
        Vector3 damping,
        bool lookahead,
        float lookaheadTime,
        float lookaheadSmoothing,
        float maxImpulse,
        float maxDutch)
    {
        return new CameraShotSettings
        {
            OrthographicSize = lensSize,
            Damping = damping,
            ScreenPosition = Vector2.zero,
            EnableLookahead = lookahead,
            LookaheadTime = lookaheadTime,
            LookaheadSmoothing = lookaheadSmoothing,
            MaxImpulseIntensity = maxImpulse,
            MaxDutch = maxDutch
        };
    }
}

[CreateAssetMenu(fileName = "CameraShotProfile", menuName = "HubToHome/Camera/Shot Profile")]
public sealed class CameraShotProfile : ScriptableObject
{
    [Title("Camera Shot")]
    [LabelText("스타일")]
    public CameraShotStyle Style = CameraShotStyle.Static;

    [LabelText("기본 줌"), MinValue(0.5f)]
    public float OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;

    [LabelText("위치 감쇠")]
    public Vector3 Damping = Vector3.zero;

    [LabelText("화면 위치")]
    public Vector2 ScreenPosition = Vector2.zero;

    [LabelText("예측 이동")]
    public bool EnableLookahead;

    [ShowIf(nameof(EnableLookahead)), Range(0f, 1f), LabelText("예측 시간")]
    public float LookaheadTime = 0.12f;

    [ShowIf(nameof(EnableLookahead)), Range(0f, 30f), LabelText("예측 안정화")]
    public float LookaheadSmoothing = 8f;

    [LabelText("최대 흔들림"), MinValue(0f)]
    public float MaxImpulseIntensity = 0.55f;

    [LabelText("최대 화면 기울기"), MinValue(0f)]
    public float MaxDutch = 1.25f;

    public CameraShotSettings ToSettings(float fallbackLensSize)
    {
        CameraShotSettings settings = CameraShotSettings.CreateBuiltIn(Style, fallbackLensSize);
        settings.OrthographicSize = Mathf.Max(0.5f, OrthographicSize);
        settings.Damping = new Vector3(
            Mathf.Max(0f, Damping.x),
            Mathf.Max(0f, Damping.y),
            Mathf.Max(0f, Damping.z));
        settings.ScreenPosition = ScreenPosition;
        settings.EnableLookahead = EnableLookahead;
        settings.LookaheadTime = Mathf.Clamp(LookaheadTime, 0f, 1f);
        settings.LookaheadSmoothing = Mathf.Clamp(LookaheadSmoothing, 0f, 30f);
        settings.MaxImpulseIntensity = Mathf.Max(0f, MaxImpulseIntensity);
        settings.MaxDutch = Mathf.Max(0f, MaxDutch);
        return settings;
    }
}
