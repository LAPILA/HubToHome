using System;
using System.Collections.Generic;

/// <summary>한 적 행동 동안 아군별 기본 저스트 AP 보상을 제한합니다. 장비·고유 자원은 별도입니다.</summary>
public sealed class BattleParryRewardBudget<T> where T : class
{
    public const int DefaultCap = 6;
    private readonly Dictionary<T, int> _granted = new Dictionary<T, int>();
    private int _cap = DefaultCap;

    public void Reset(int cap = DefaultCap)
    {
        _granted.Clear();
        _cap = Math.Max(0, cap);
    }

    public int Claim(T actor, int requested)
    {
        if (actor == null || requested <= 0) return 0;
        _granted.TryGetValue(actor, out int previous);
        int amount = Math.Min(requested, Math.Max(0, _cap - previous));
        if (amount > 0) _granted[actor] = previous + amount;
        return amount;
    }
}
