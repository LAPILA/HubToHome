using System;
using System.Collections.Generic;

/// <summary>HP 진행 표시 전용 계산. 페이즈나 피해 규칙은 판단하지 않습니다.</summary>
public static class BattleBossProgressPolicy
{
    public static float Ratio(int current, int maximum)
        => maximum <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)current / maximum));

    public static float[] NormalizeMarkers(IReadOnlyList<float> markers)
    {
        var result = new List<float>(8);
        for (int i = 0; markers != null && i < markers.Count && result.Count < 8; i++)
        {
            float value = markers[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value >= 1f) continue;
            bool duplicate = false;
            for (int j = 0; j < result.Count; j++)
                if (Math.Abs(result[j] - value) < .0001f) { duplicate = true; break; }
            if (!duplicate) result.Add(value);
        }
        result.Sort();
        return result.ToArray();
    }
}
