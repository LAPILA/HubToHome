using System;

/// <summary>실제 Z/X/C press 간격을 공통으로 기록합니다. 창/프레임 재생성으로 재준비를 초기화하지 않습니다.</summary>
public struct ParryRearmState
{
    public const float DefaultInterval = 0.18f;
    private bool _hasPress, _hasPrevious;
    private float _lastPress, _previousPress;

    public static float IntervalFor(float attackDuration, float configuredInterval = DefaultInterval)
    {
        if (!Finite(attackDuration) || !Finite(configuredInterval)) return DefaultInterval;
        return Math.Max(0f, Math.Min(Math.Max(0f, configuredInterval), Math.Max(0.01f, attackDuration) * 0.5f));
    }

    public void ObservePress(float pressedAt)
    {
        if (!Finite(pressedAt) || (_hasPress && pressedAt <= _lastPress)) return;
        _hasPrevious = _hasPress;
        _previousPress = _lastPress;
        _lastPress = pressedAt;
        _hasPress = true;
    }

    public bool IsPressRearmed(float pressedAt, float interval)
    {
        if (!Finite(pressedAt) || !Finite(interval)) return false;
        return _hasPress && Math.Abs(pressedAt - _lastPress) <= 0.00001f
            && (!_hasPrevious || _lastPress - _previousPress + 0.00001f >= Math.Max(0f, interval));
    }

    public float Remaining(float now, float interval)
    {
        if (!Finite(now) || !Finite(interval) || !_hasPrevious
            || _lastPress - _previousPress + 0.00001f >= interval) return 0f;
        return Math.Max(0f, _lastPress + Math.Max(0f, interval) - now);
    }

    public void Reset() => this = default;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
