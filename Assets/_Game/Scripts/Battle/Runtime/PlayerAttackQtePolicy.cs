using UnityEngine;

/// <summary>공격 QTE는 정상 피해를 복구하는 판정이며 추가 피해/자원을 생성하지 않습니다.</summary>
public static class PlayerAttackQtePolicy
{
    public const float DefaultFailureMultiplier = 0.5f;

    public static float DamageMultiplier(float successRatio, float failureMultiplier = DefaultFailureMultiplier)
    {
        float ratio = float.IsNaN(successRatio) || float.IsInfinity(successRatio) ? 0f : Mathf.Clamp01(successRatio);
        float failure = float.IsNaN(failureMultiplier) || float.IsInfinity(failureMultiplier)
            ? DefaultFailureMultiplier : Mathf.Clamp(failureMultiplier, 0f, 0.99f);
        return Mathf.Lerp(failure, 1f, ratio);
    }
}
