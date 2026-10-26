using NUnit.Framework;

public sealed class BattleImpactCameraEnvelopeTests
{
    [Test]
    public void CutHasFastEntryFiniteHoldAndExactReturn()
    {
        var envelope = new BattleImpactCameraEnvelope(0.025f, 0.025f, 0.075f);
        Assert.That(envelope.Duration, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(envelope.Evaluate(0f), Is.Zero);
        Assert.That(envelope.Evaluate(0.0125f), Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(envelope.Evaluate(0.025f), Is.EqualTo(1f));
        Assert.That(envelope.Evaluate(0.04f), Is.EqualTo(1f));
        Assert.That(envelope.Evaluate(0.0875f), Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(envelope.Evaluate(envelope.Duration), Is.Zero);
        Assert.That(envelope.Evaluate(1f), Is.Zero);
    }

    [Test]
    public void InvalidDurationsCannotLeaveCameraOffsetOrProduceNaN()
    {
        var envelope = new BattleImpactCameraEnvelope(float.NaN, float.PositiveInfinity, -1f);
        Assert.That(envelope.CutIn, Is.EqualTo(0.025f));
        Assert.That(envelope.Hold, Is.EqualTo(0.025f));
        Assert.That(envelope.Return, Is.EqualTo(0.001f));
        Assert.That(envelope.Evaluate(float.NaN), Is.Zero);
        Assert.That(envelope.Evaluate(float.PositiveInfinity), Is.Zero);
        Assert.That(envelope.Evaluate(-1f), Is.Zero);
        for (int i = 0; i < 1000; i++)
            Assert.That(envelope.Evaluate(envelope.Duration * i / 999f), Is.InRange(0f, 1f));
    }
}
