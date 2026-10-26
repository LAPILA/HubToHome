using UnityEngine;

/// <summary>관람 출발 영역의 선분 통과 검사입니다. 경로 탐색이나 추종 커서는 소유하지 않습니다.</summary>
public static class OverworldFollowerObservationRoute
{
    public static bool CrossesBox(Vector2 from, Vector2 to, Vector2 center, Vector2 size)
    {
        if (!Finite(from.x) || !Finite(from.y) || !Finite(to.x) || !Finite(to.y)
            || !Finite(center.x) || !Finite(center.y) || !Finite(size.x) || !Finite(size.y)
            || size.x <= 0f || size.y <= 0f) return false;
        Vector2 half = size * 0.5f;
        float enter = 0f;
        float exit = 1f;
        return Clip(from.x, to.x - from.x, center.x - half.x, center.x + half.x, ref enter, ref exit)
            && Clip(from.y, to.y - from.y, center.y - half.y, center.y + half.y, ref enter, ref exit);
    }

    private static bool Clip(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.000001f) return start >= min && start <= max;
        float first = (min - start) / delta;
        float last = (max - start) / delta;
        if (first > last) { float swap = first; first = last; last = swap; }
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, last);
        return enter <= exit;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
