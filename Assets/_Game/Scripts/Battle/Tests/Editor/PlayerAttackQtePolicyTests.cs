using NUnit.Framework;

public class PlayerAttackQtePolicyTests
{
    [TestCase(0f, 0.5f)]
    [TestCase(0.25f, 0.625f)]
    [TestCase(0.5f, 0.75f)]
    [TestCase(0.75f, 0.875f)]
    [TestCase(1f, 1f)]
    public void DamageMultiplier_SuccessRestoresNormalDamageWithoutBonus(float ratio, float expected)
    {
        Assert.That(PlayerAttackQtePolicy.DamageMultiplier(ratio), Is.EqualTo(expected).Within(0.000001f));
    }

    [TestCase(-1f, 0.5f)]
    [TestCase(2f, 1f)]
    [TestCase(float.NaN, 0.5f)]
    [TestCase(float.PositiveInfinity, 0.5f)]
    [TestCase(float.NegativeInfinity, 0.5f)]
    public void DamageMultiplier_InvalidRatioCannotCreateExtraDamage(float ratio, float expected)
    {
        Assert.That(PlayerAttackQtePolicy.DamageMultiplier(ratio), Is.EqualTo(expected));
    }

    [TestCase(0f, 0f)]
    [TestCase(0.25f, 0.25f)]
    [TestCase(0.8f, 0.8f)]
    [TestCase(-2f, 0f)]
    [TestCase(1f, 0.99f)]
    [TestCase(3f, 0.99f)]
    [TestCase(float.NaN, 0.5f)]
    [TestCase(float.PositiveInfinity, 0.5f)]
    [TestCase(float.NegativeInfinity, 0.5f)]
    public void DamageMultiplier_ConfiguredFailureStaysBelowNormalDamage(float configured, float expected)
    {
        Assert.That(PlayerAttackQtePolicy.DamageMultiplier(0f, configured), Is.EqualTo(expected));
        Assert.That(PlayerAttackQtePolicy.DamageMultiplier(1f, configured), Is.EqualTo(1f));
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(0.99f)]
    public void DamageMultiplier_IsMonotonicAndBoundedAcrossPartialSuccess(float failure)
    {
        float previous = failure;
        for (int i = 0; i <= 100; i++)
        {
            float multiplier = PlayerAttackQtePolicy.DamageMultiplier(i / 100f, failure);
            Assert.That(multiplier, Is.InRange(failure, 1f));
            Assert.That(multiplier, Is.GreaterThanOrEqualTo(previous));
            previous = multiplier;
        }
    }
}
