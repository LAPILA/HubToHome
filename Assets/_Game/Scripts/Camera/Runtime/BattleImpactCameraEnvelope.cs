using UnityEngine;

/// <summary>게임 시계와 독립된 카메라 명중 컷의 진입·고정·복귀 곡선입니다.</summary>
public readonly struct BattleImpactCameraEnvelope
{
    public float CutIn { get; }
    public float Hold { get; }
    public float Return { get; }
    public float Duration => CutIn + Hold + Return;

    public BattleImpactCameraEnvelope(float cutIn, float hold, float returnDuration)
    {
        CutIn = Normalize(cutIn, 0.025f, 0.001f, 0.08f);
        Hold = Normalize(hold, 0.025f, 0f, 0.08f);
        Return = Normalize(returnDuration, 0.075f, 0.001f, 0.20f);
    }

    public float Evaluate(float elapsed)
    {
        if (float.IsNaN(elapsed) || elapsed <= 0f) return 0f;
        if (elapsed >= Duration) return 0f;
        if (elapsed < CutIn)
        {
            float remaining = 1f - elapsed / CutIn;
            return 1f - remaining * remaining * remaining;
        }
        if (elapsed <= CutIn + Hold) return 1f;
        float returning = 1f - (elapsed - CutIn - Hold) / Return;
        return returning * returning * returning;
    }

    private static float Normalize(float value, float fallback, float min, float max)
    {
        return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
