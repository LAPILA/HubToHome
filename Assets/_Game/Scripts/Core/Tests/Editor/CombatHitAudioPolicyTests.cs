using NUnit.Framework;

public sealed class CombatHitAudioPolicyTests
{
    [Test]
    public void SameFrameAndDenseAreaHitsShareOneContactSound()
    {
        var policy = new CombatHitAudioPolicy();
        Assert.That(policy.TryBegin(0d, false, out CombatHitAudioRequest first), Is.True);
        Assert.That(first.VoiceIndex, Is.Zero);
        for (int i = 0; i < 12; i++)
            Assert.That(policy.TryBegin(i * 0.002d, false, out _), Is.False);
        Assert.That(policy.TryBegin(0.04d, false, out CombatHitAudioRequest second), Is.True);
        Assert.That(second.VoiceIndex, Is.EqualTo(1));
    }

    [Test]
    public void TenHzRapidStrikesKeepEveryHitWithinFourReusableVoices()
    {
        var policy = new CombatHitAudioPolicy();
        for (int i = 0; i < 50; i++)
        {
            Assert.That(policy.TryBegin(i * 0.1d, false, out CombatHitAudioRequest request), Is.True);
            Assert.That(request.VoiceIndex, Is.EqualTo(i % CombatHitAudioPolicy.VoiceCount));
            Assert.That(request.Pitch, Is.InRange(0.98f, 1.03f));
            Assert.That(request.Gain, Is.LessThanOrEqualTo(1f));
        }
    }

    [Test]
    public void EmphasisChangesOnlyContactPitchAndDoesNotRaiseGainAboveOne()
    {
        var policy = new CombatHitAudioPolicy();
        Assert.That(policy.TryBegin(1d, true, out CombatHitAudioRequest request), Is.True);
        Assert.That(request.Pitch, Is.EqualTo(0.92f));
        Assert.That(request.Gain, Is.EqualTo(1f));
        Assert.That(policy.TryBegin(1d, true, out _), Is.False);
    }

    [Test]
    public void RejectedAndInvalidTimesDoNotConsumeAVoice()
    {
        var policy = new CombatHitAudioPolicy();
        Assert.That(policy.TryBegin(double.NaN, false, out _), Is.False);
        Assert.That(policy.TryBegin(double.PositiveInfinity, false, out _), Is.False);
        Assert.That(policy.TryBegin(double.NegativeInfinity, false, out _), Is.False);
        Assert.That(policy.TryBegin(10d, false, out CombatHitAudioRequest first), Is.True);
        Assert.That(first.VoiceIndex, Is.Zero);
        Assert.That(policy.TryBegin(10.01d, false, out _), Is.False);
        Assert.That(policy.TryBegin(10.1d, false, out CombatHitAudioRequest second), Is.True);
        Assert.That(second.VoiceIndex, Is.EqualTo(1));
    }

    [Test]
    public void ResetAndNewClockAllowTheFirstHitImmediately()
    {
        var policy = new CombatHitAudioPolicy();
        policy.TryBegin(10d, false, out _);
        policy.Reset();
        Assert.That(policy.TryBegin(10d, false, out CombatHitAudioRequest reset), Is.True);
        Assert.That(reset.VoiceIndex, Is.Zero);
        Assert.That(reset.Pitch, Is.EqualTo(1f));
        Assert.That(policy.TryBegin(0d, false, out CombatHitAudioRequest newClock), Is.True);
        Assert.That(newClock.VoiceIndex, Is.Zero);
    }

    [Test]
    public void ExactMinimumIntervalDoesNotLoseAHitsVoiceToFloatingPointRounding()
    {
        var policy = new CombatHitAudioPolicy();
        Assert.That(policy.TryBegin(1d, false, out _), Is.True);
        Assert.That(policy.TryBegin(1d + CombatHitAudioPolicy.MinimumInterval, false, out _), Is.True);
    }

    [TestCase(0.5f, 1f, 1f)]
    [TestCase(1f, 1f, 1f)]
    [TestCase(4f, 1f, 0.25f)]
    [TestCase(0.5f, 0.25f, 0.5f)]
    [TestCase(0f, 1f, 0f)]
    [TestCase(1f, 0f, 0f)]
    public void OwnedVoiceMixStaysWithinItsVolumeBudget(float total, float maximum, float expected)
    {
        float scale = CombatHitAudioPolicy.ResolveMixScale(total, maximum);
        Assert.That(scale, Is.EqualTo(expected).Within(0.00001f));
        Assert.That(total * scale, Is.LessThanOrEqualTo(maximum));
    }
}
