using System;
using System.Collections.Generic;

/// <summary>최근 집중을 완화하는 전투 단위 랜덤 선정. 입력/카메라/턴 예고는 읽지 않습니다.</summary>
public sealed class BattleEnemyTargetSelector<T> where T : class
{
    private readonly T[] _recent = new T[4];
    private int _nextHistory;
    private T _last;
    private int _consecutive;

    public int SelectIndex(IReadOnlyList<T> candidates, Func<T, bool> eligible,
        Func<T, float> priority, float sample)
    {
        if (candidates == null || eligible == null) return -1;
        int count = 0;
        for (int i = 0; i < candidates.Count; i++)
            if (eligible(candidates[i])) count++;
        if (count == 0) return -1;

        double total = 0;
        for (int i = 0; i < candidates.Count; i++)
            if (eligible(candidates[i])) total += Weight(candidates[i], priority, count);
        if (total <= 0) return -1;
        double cursor = (float.IsNaN(sample) ? 0.5 : Math.Max(0, Math.Min(1, sample))) * total;
        int selected = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            T candidate = candidates[i];
            if (!eligible(candidate)) continue;
            double weight = Weight(candidate, priority, count);
            if (weight <= 0) continue;
            selected = i;
            cursor -= weight;
            if (cursor < 0) break;
        }
        if (selected < 0) return -1;
        T target = candidates[selected];
        _consecutive = ReferenceEquals(_last, target) ? Math.Min(2, _consecutive + 1) : 1;
        _last = target;
        _recent[_nextHistory] = target;
        _nextHistory = (_nextHistory + 1) % _recent.Length;
        return selected;
    }

    private double Weight(T candidate, Func<T, float> priority, int eligibleCount)
    {
        bool same = ReferenceEquals(_last, candidate);
        if (eligibleCount > 1 && same && _consecutive >= 2) return 0;
        float authored = priority != null ? priority(candidate) : 1f;
        double weight = float.IsNaN(authored) || float.IsInfinity(authored)
            ? 1 : Math.Max(1, Math.Min(1.25, authored));
        int recentCount = 0;
        for (int i = 0; i < _recent.Length; i++)
            if (ReferenceEquals(_recent[i], candidate)) recentCount++;
        weight /= 1 + recentCount * 2;
        return same && eligibleCount > 1 ? weight * 0.35 : weight;
    }
}
