using UnityEngine;

public static class BattleRunPolicy
{
    public static bool IsSuccessful(float successChance, float randomValue)
    {
        float chance = Mathf.Clamp01(successChance);
        float roll = Mathf.Clamp01(randomValue);
        return roll < chance;
    }
}

