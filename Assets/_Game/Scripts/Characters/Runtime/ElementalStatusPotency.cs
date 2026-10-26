using System;

/// <summary>누적량에 따른 상태 피해 단계만 계산합니다. 부여·저항·소모 규칙은 변경하지 않습니다.</summary>
public static class ElementalStatusPotency
{
    public static int GetTier(int buildup, int firstThreshold = 10, int secondThreshold = 25,
        int thirdThreshold = 50)
    {
        int first = Math.Max(1, firstThreshold);
        int second = Math.Max(first, secondThreshold);
        int third = Math.Max(second, thirdThreshold);
        if (buildup >= third) return 3;
        if (buildup >= second) return 2;
        return buildup >= first ? 1 : 0;
    }

    public static float GetMultiplier(int tier, float firstMultiplier = 1.25f,
        float secondMultiplier = 1.5f, float thirdMultiplier = 2f, float maximumMultiplier = 2f)
    {
        if (tier <= 0) return 1f;
        float first = Math.Max(1f, FiniteOr(firstMultiplier, 1.25f));
        float second = Math.Max(first, FiniteOr(secondMultiplier, 1.5f));
        float third = Math.Max(second, FiniteOr(thirdMultiplier, 2f));
        float maximum = Math.Max(1f, FiniteOr(maximumMultiplier, 2f));
        return Math.Min(maximum, tier == 1 ? first : tier == 2 ? second : third);
    }

    private static float FiniteOr(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
}
