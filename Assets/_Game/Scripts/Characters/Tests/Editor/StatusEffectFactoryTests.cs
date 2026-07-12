using NUnit.Framework;

public class StatusEffectFactoryTests
{
    [TestCase(StatusEffectIds.Burn, typeof(BurnEffect))]
    [TestCase(StatusEffectIds.Freeze, typeof(FreezeEffect))]
    [TestCase(StatusEffectIds.Bleed, typeof(BleedEffect))]
    [TestCase(StatusEffectIds.Poison, typeof(PoisonEffect))]
    [TestCase(StatusEffectIds.Bind, typeof(BindEffect))]
    [TestCase(StatusEffectIds.Stun, typeof(StunEffect))]
    [TestCase(StatusEffectIds.Berserk, typeof(BerserkEffect))]
    [TestCase(StatusEffectIds.IceShield, typeof(IceShieldEffect))]
    [TestCase(StatusEffectIds.Wet, typeof(WetEffect))]
    public void TryCreateReturnsRegisteredEffect(string effectId, System.Type expectedType)
    {
        bool created = StatusEffectFactory.TryCreate(effectId, 3, out StatusEffect effect);

        Assert.That(created, Is.True);
        Assert.That(effect, Is.TypeOf(expectedType));
        Assert.That(effect.EffectID, Is.EqualTo(effectId));
        Assert.That(effect.DurationTurns, Is.EqualTo(3));
    }

    [Test]
    public void TryCreateRejectsUnknownId()
    {
        bool created = StatusEffectFactory.TryCreate("Unknown", 3, out StatusEffect effect);

        Assert.That(created, Is.False);
        Assert.That(effect, Is.Null);
    }

    [Test]
    public void TryCreateClampsNegativeDuration()
    {
        StatusEffectFactory.TryCreate(StatusEffectIds.Burn, -1, out StatusEffect effect);

        Assert.That(effect.DurationTurns, Is.Zero);
    }
}

