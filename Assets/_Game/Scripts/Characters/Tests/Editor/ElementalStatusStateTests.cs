using NUnit.Framework;
using UnityEngine;

public sealed class ElementalStatusStateTests
{
    private TestCharacter _target;
    private ElementalStatusProfile _profile;

    [SetUp]
    public void SetUp()
    {
        _target = new GameObject("ElementalTarget").AddComponent<TestCharacter>();
        _target.SetStats(new StatBlock { MaxHP = 100000, DEF = 0 });
        _target.FillHP();
        _profile = ScriptableObject.CreateInstance<ElementalStatusProfile>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_target.gameObject);
        Object.DestroyImmediate(_profile);
    }

    [TestCase(ElementalStatusKind.Burn, 99)]
    [TestCase(ElementalStatusKind.Electric, 999)]
    [TestCase(ElementalStatusKind.Bleed, 99)]
    [TestCase(ElementalStatusKind.Poison, 99)]
    public void DefaultsMatchAuthoringProfileAndCaps(ElementalStatusKind kind, int maximum)
    {
        Assert.That(_target.Elements.StatusDamageUnit, Is.EqualTo(3f));
        _target.Elements.Add(kind, 2000f);
        Assert.That(_target.Elements.GetValue(kind), Is.EqualTo(maximum));
        Assert.That(_target.Elements.GetBuildupRemainder(kind), Is.Zero);
        _target.ConfigureElementalStatus(_profile);
        Assert.That(_target.Elements.StatusDamageUnit, Is.EqualTo(_profile.StatusDamageUnit));
        Assert.That(_target.Elements.GetValue(kind), Is.EqualTo(maximum), "Configuration must not reset a live battle.");
    }

    [Test]
    public void ResistanceAndGuardMultiplierAccumulateFractionsInsteadOfRollingChance()
    {
        SetResistance("Burn", .5f);
        for (int i = 0; i < 3; i++) _target.Elements.Add(ElementalStatusKind.Burn, 1f, .5f);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Burn), Is.EqualTo(.75d).Within(.00001d));
        _target.Elements.Add(ElementalStatusKind.Burn, 1f, .5f);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(1));
    }

    [Test]
    public void ImmuneAndPerfectGuardApplicationsAddNeitherValueNorRemainder()
    {
        SetResistance("Burn", 0f);
        _target.Elements.Add(ElementalStatusKind.Burn, 99);
        _target.Elements.Add(ElementalStatusKind.Poison, 99, 0f);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.Zero);
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Poison), Is.Zero);
    }

    [Test]
    public void FiftyFractionalApplicationsAccumulateIntoOneValue()
    {
        for (int i = 0; i < 50; i++) _target.Elements.Add(ElementalStatusKind.Burn, 1f, .02f);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(1));
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Burn), Is.LessThan(.00001d));
    }

    [Test]
    public void BurnAndPoisonOnlyTickAtTurnEndAndPoisonGrowthIgnoresApplicationResistance()
    {
        SetResistance("Poison", .5f);
        _target.Elements.Add(ElementalStatusKind.Burn, 3);
        _target.Elements.Add(ElementalStatusKind.Poison, 10);
        int before = _target.CurrentHP;
        _target.ProcessEffects();
        Assert.That(_target.CurrentHP, Is.EqualTo(before));
        _target.ProcessEffects(endOfTurn: true);
        Assert.That(_target.CurrentHP, Is.EqualTo(before - 9 - 3));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(2));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(8));
    }

    [Test]
    public void BleedOnlyTicksOnCompletedAttackOrSkillNotTurnBoundariesOrItems()
    {
        _target.Elements.Add(ElementalStatusKind.Bleed, 5);
        int before = _target.CurrentHP;
        _target.ProcessEffects();
        _target.ProcessEffects(endOfTurn: true);
        _target.NotifyActionExecuted(triggerElementalBleed: false);
        Assert.That(_target.CurrentHP, Is.EqualTo(before));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Bleed), Is.EqualTo(5));
        _target.NotifyActionExecuted();
        Assert.That(_target.CurrentHP, Is.EqualTo(before - 18));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Bleed), Is.EqualTo(4));
    }

    [Test]
    public void PoisonDetonationDoesNotGrowOrConsumePoison()
    {
        _target.Elements.Add(ElementalStatusKind.Poison, 10);
        Assert.That(_target.Elements.Trigger(ElementalStatusKind.Poison), Is.EqualTo(6));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(10));
    }

    [Test]
    public void StatusDamageBypassesGuardInvincibilityAndResistancesWithoutTriggeringReactions()
    {
        _target.SetStats(new StatBlock { MaxHP = 100000, DEF = 99999, FireResistance = 0f, IncomingDamageMultiplier = 0f });
        _target.FillHP();
        _target.Elements.Add(ElementalStatusKind.Chill, 10);
        _target.Elements.Add(ElementalStatusKind.Electric, 99);
        _target.IsInvincible = true;
        _target.IsDefending = true;
        int before = _target.CurrentHP;
        int events = 0;
        _target.OnElementalDamage += (actor, damage, kind) => events++;
        Assert.That(_target.ApplyStatusDamage(15, ElementalStatusKind.Burn), Is.EqualTo(15));
        Assert.That(_target.CurrentHP, Is.EqualTo(before - 15));
        Assert.That(events, Is.EqualTo(1));
        Assert.That(_target.ElementalDamageTotal, Is.EqualTo(15));
        Assert.That(_target.Elements.IsFrozen, Is.True);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(99));
    }

    [Test]
    public void ChillVulnerabilityThenFrozenConversionUsesOnlyOneShatter()
    {
        _target.Elements.Add(ElementalStatusKind.Chill, 9);
        Assert.That(_target.TakeDamage(100, DamageElement.Physical).FinalDamage, Is.EqualTo(109));
        _target.Elements.Add(ElementalStatusKind.Chill, 4);
        Assert.That(_target.Elements.IsFrozen, Is.True);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Chill), Is.Zero);
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Chill), Is.Zero);
        Assert.That(_target.TakeDamage(100, DamageElement.Physical).FinalDamage, Is.EqualTo(150));
        Assert.That(_target.Elements.IsFrozen, Is.False);
        Assert.That(_target.Elements.RecoveryLocked, Is.True);
        Assert.That(_target.TakeDamage(100, DamageElement.Physical).FinalDamage, Is.EqualTo(100));
    }

    [Test]
    public void FrozenRejectsBuildupAndCannotBeRemovedByConsumingChill()
    {
        _target.Elements.Add(ElementalStatusKind.Chill, 12.5f);
        _target.Elements.Add(ElementalStatusKind.Chill, 9.5f);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Chill), Is.Zero);
        Assert.That(_target.Elements.Consume(ElementalStatusKind.Chill, 99), Is.Zero);
        Assert.That(_target.Elements.IsFrozen, Is.True);
        Assert.That(_target.CanTakeTurn(), Is.False);
        Assert.That(_target.CanDodgeOrJump(), Is.False);
    }

    [Test]
    public void FrozenSkipsOneTurnAndRecoveryRequiresARealCompletedAction()
    {
        _target.Elements.Add(ElementalStatusKind.Chill, 10);
        bool canActAtTurnEntry = _target.CanTakeTurn();
        _target.ProcessEffects();
        Assert.That(canActAtTurnEntry, Is.False, "Controller snapshots this before ProcessEffects.");
        Assert.That(_target.Elements.IsFrozen, Is.False);
        _target.ProcessEffects(endOfTurn: true);
        _target.Elements.Add(ElementalStatusKind.Chill, 100);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Chill), Is.EqualTo(9));
        Assert.That(_target.Elements.IsFrozen, Is.False);
        _target.NotifyActionExecuted(triggerElementalBleed: false);
        _target.Elements.Add(ElementalStatusKind.Chill, 1);
        Assert.That(_target.Elements.IsFrozen, Is.True);
    }

    [Test]
    public void LegacyStunEndProtectsAgainstImmediateRefreeze()
    {
        _target.TryApplyStatusEffect(new StunEffect(1));
        Assert.That(_target.Elements.CanApplyCrowdControl, Is.False);
        _target.ProcessEffects();
        Assert.That(_target.IsStunned, Is.False);
        Assert.That(_target.Elements.RecoveryLocked, Is.True);
        _target.Elements.Add(ElementalStatusKind.Chill, 100);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Chill), Is.EqualTo(9));
        _target.NotifyActionExecuted();
        Assert.That(_target.Elements.CanApplyCrowdControl, Is.True);
    }

    [Test]
    public void EvadedDirectHitDoesNotShatterOrDischarge()
    {
        _target.Elements.Add(ElementalStatusKind.Chill, 10);
        _target.Elements.Add(ElementalStatusKind.Electric, 50);
        _target.IsInvincible = true;
        Assert.That(_target.TakeDamage(100, DamageElement.Physical).Applied, Is.False);
        Assert.That(_target.Elements.IsFrozen, Is.True);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(50));
    }

    [Test]
    public void ElectricCarriesFractionalDamageAndDischargesOncePerHit()
    {
        _target.Elements.Add(ElementalStatusKind.Electric, 10);
        int before = _target.CurrentHP;
        _target.TakeDamage(1, DamageElement.Physical);
        Assert.That(_target.CurrentHP, Is.EqualTo(before - 1));
        _target.TakeDamage(1, DamageElement.Physical);
        Assert.That(_target.CurrentHP, Is.EqualTo(before - 3));
        Assert.That(_target.ElementalDamageTotal, Is.EqualTo(1));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(8));
    }

    [Test]
    public void FiftyHitsUseExplicitProcCoefficientAndNeverRecursivelyTriggerElectric()
    {
        _target.Elements.Add(ElementalStatusKind.Electric, 999);
        int expectedValue = 999;
        double expectedDamage = 0d;
        for (int hit = 0; hit < 50; hit++)
        {
            expectedDamage += expectedValue * 3d * .02d * .1d;
            expectedValue -= (int)System.Math.Ceiling(expectedValue * .01d);
            _target.TakeDamage(1, DamageElement.Physical, null, .1f);
        }
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(expectedValue));
        Assert.That(_target.ElementalDamageTotal, Is.EqualTo((int)System.Math.Floor(expectedDamage + .000001d)));
    }

    [Test]
    public void LethalDirectHitDoesNotReactAndLethalStatusDamageDiesOnce()
    {
        _target.SetHP(1);
        _target.Elements.Add(ElementalStatusKind.Electric, 999);
        DamageResult result = _target.TakeDamage(2, DamageElement.Physical);
        Assert.That(result.TargetDefeated, Is.True);
        Assert.That(_target.ElementalDamageTotal, Is.Zero);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(999));
        Assert.That(_target.DeathCount, Is.EqualTo(1));
        _target.SetHP(1);
        Assert.That(_target.ApplyStatusDamage(50, ElementalStatusKind.Poison), Is.EqualTo(1));
        Assert.That(_target.ApplyStatusDamage(50, ElementalStatusKind.Poison), Is.Zero);
        Assert.That(_target.DeathCount, Is.EqualTo(2));
    }

    [Test]
    public void CleanseClearsEveryRemainderAndFrozenButRetainsRecoveryFence()
    {
        _target.Elements.Add(ElementalStatusKind.Burn, .75f);
        _target.Elements.Add(ElementalStatusKind.Electric, 10);
        _target.TakeDamage(1, DamageElement.Physical);
        _target.Elements.Add(ElementalStatusKind.Chill, 10);
        _target.Elements.Cleanse();
        for (int i = 0; i < 5; i++)
        {
            Assert.That(_target.Elements.GetValue((ElementalStatusKind)i), Is.Zero);
            Assert.That(_target.Elements.GetBuildupRemainder((ElementalStatusKind)i), Is.Zero);
        }
        Assert.That(_target.Elements.IsFrozen, Is.False);
        Assert.That(_target.Elements.RecoveryLocked, Is.True);
        _target.Elements.Add(ElementalStatusKind.Electric, 10);
        _target.TakeDamage(1, DamageElement.Physical);
        Assert.That(_target.ElementalDamageTotal, Is.Zero, "Old fractional electric damage must not survive cleanse.");
    }

    [Test]
    public void BattleCleanupWorksForDeadInactiveActorsWithoutChangingProfileOrHealing()
    {
        _target.ConfigureElementalStatus(_profile);
        _target.Elements.Add(ElementalStatusKind.Chill, 10);
        _target.Elements.Add(ElementalStatusKind.Bleed, 7);
        _target.ApplyStatusDamage(_target.CurrentHP, ElementalStatusKind.Burn);
        _target.gameObject.SetActive(false);
        _target.ClearBattleStatusEffects();
        _target.ClearBattleStatusEffects();
        Assert.That(_target.Elements.IsFrozen || _target.Elements.RecoveryLocked, Is.False);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Bleed), Is.Zero);
        Assert.That(_target.Elements.Profile, Is.SameAs(_profile));
        Assert.That(_target.CurrentHP, Is.Zero);
    }

    [Test]
    public void SharedProfileDoesNotShareLiveValuesAndForecastDoesNotTickStates()
    {
        TestCharacter other = new GameObject("OtherTarget").AddComponent<TestCharacter>();
        try
        {
            other.SetStats(new StatBlock { MaxHP = 100 });
            other.FillHP();
            other.ConfigureElementalStatus(_profile);
            _target.ConfigureElementalStatus(_profile);
            _target.Elements.Add(ElementalStatusKind.Poison, 10);
            _target.Elements.Add(ElementalStatusKind.Chill, 10);
            int hp = _target.CurrentHP;
            _target.GetForecastSpeed(20, true);
            Assert.That(_target.CurrentHP, Is.EqualTo(hp));
            Assert.That(_target.Elements.IsFrozen, Is.True);
            Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(10));
            Assert.That(other.Elements.GetValue(ElementalStatusKind.Poison), Is.Zero);
            Assert.That(_profile.StatusDamageUnit, Is.EqualTo(3f));
        }
        finally { Object.DestroyImmediate(other.gameObject); }
    }

    private void SetResistance(string id, float value)
    {
        var stats = new StatBlock { MaxHP = 100000, DEF = 0 };
        stats.SetStatusResistance(id, value);
        _target.SetStats(stats);
        _target.FillHP();
    }

    private sealed class TestCharacter : CharacterBase
    {
        public int DeathCount { get; private set; }
        public void SetStats(StatBlock stats) => SetBaseStats(stats);
        public void FillHP() => SetCurrentHPValue(MaxHP);
        public void SetHP(int hp) => SetCurrentHPValue(hp);
        protected override void OnDie() => DeathCount++;
    }
}
