using UnityEngine;

/// <summary>공격 하나의 실제 시간과 애니메이션 시간을 연결합니다. 전역 시간은 바꾸지 않습니다.</summary>
public readonly struct BattleImpactTiming
{
    public float AuthoredDuration { get; }
    public float Duration { get; }
    public float SlowStart { get; }
    public float SlowDuration { get; }
    public const float SlowRate = 0.5f;

    public BattleImpactTiming(float authoredDuration, float cueWindow, bool enabled = true)
    {
        AuthoredDuration = Mathf.Max(0.01f, authoredDuration);
        SlowDuration = enabled ? Mathf.Min(0.10f, AuthoredDuration) : 0f;
        Duration = AuthoredDuration + SlowDuration * (1f - SlowRate);
        SlowStart = Mathf.Clamp(Duration - cueWindow, 0f, Duration - SlowDuration);
    }

    public float AttackTimeAt(float elapsed)
    {
        elapsed = Mathf.Clamp(elapsed, 0f, Duration);
        return elapsed - Mathf.Clamp(elapsed - SlowStart, 0f, SlowDuration) * (1f - SlowRate);
    }

    public float RateAt(float elapsed) => elapsed >= SlowStart && elapsed < SlowStart + SlowDuration
        ? SlowRate : 1f;
}
