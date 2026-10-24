using UnityEngine;

/// <summary>Presentation-only geometry, shared by the weather sample and its regression tests.</summary>
public static class WeatherSampleMath
{
    public static Vector2 CalculateSpawn(Vector2 landing, Vector2 velocity, float lifetime)
    {
        return landing - velocity * lifetime;
    }

    public static bool Contains(Vector2 point, Vector2 center, Vector2 size)
    {
        Vector2 half = size * .5f;
        return Mathf.Abs(point.x - center.x) <= half.x && Mathf.Abs(point.y - center.y) <= half.y;
    }

    public static float FogVisibility(float distance, float radius, float feather)
    {
        float t = Mathf.Clamp01((distance - radius) / Mathf.Max(.001f, feather));
        return t * t * (3f - 2f * t);
    }

    public static int AccumulateEmission(ref float remainder, float rate, float deltaTime, int frameLimit)
    {
        remainder += Mathf.Max(0f, rate) * Mathf.Clamp(deltaTime, 0f, .1f);
        int count = Mathf.Min(Mathf.FloorToInt(remainder), frameLimit);
        // Do not accumulate a burst backlog after a stall or an authored rate above the budget.
        remainder = Mathf.Min(remainder - count, .999f);
        return count;
    }
}
