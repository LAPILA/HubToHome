using NUnit.Framework;

public class BattleRunPolicyTests
{
    [TestCase(0.6f, 0.59f, true)]
    [TestCase(0.6f, 0.6f, false)]
    [TestCase(-1f, 0f, false)]
    [TestCase(2f, 0.999f, true)]
    public void IsSuccessfulUsesClampedExclusiveThreshold(float chance, float roll, bool expected)
    {
        Assert.That(BattleRunPolicy.IsSuccessful(chance, roll), Is.EqualTo(expected));
    }
}

