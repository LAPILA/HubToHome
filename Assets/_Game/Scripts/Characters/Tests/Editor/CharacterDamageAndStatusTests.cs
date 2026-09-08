using NUnit.Framework;
using UnityEngine;

public sealed class CharacterDamageAndStatusTests
{
    private TestCharacter _attacker;
    private TestCharacter _target;

    [SetUp]
    public void SetUp()
    {
        _attacker = CreateCharacter("Attacker");
        _target = CreateCharacter("Target");
    }

    [TearDown]
    public void TearDown()
    {
        if (_attacker != null)
            Object.DestroyImmediate(_attacker.gameObject);
        if (_target != null)
            Object.DestroyImmediate(_target.gameObject);
    }

    [Test]
    public void PhysicalDamageUsesDefence()
    {
        _target.SetStats(new StatBlock
        {
            MaxHP = 100,
            DEF = 100,
            FireResistance = 1f,
        });
        _target.ResetResources();

        DamageResult result = _target.TakeDamage(100, DamageElement.Physical, _attacker);

        Assert.That(result.Applied, Is.True);
        Assert.That(result.Element, Is.EqualTo(DamageElement.Physical));
        Assert.That(result.FinalDamage, Is.EqualTo(50));
        Assert.That(_target.CurrentHP, Is.EqualTo(50));
    }

    [Test]
    public void AttributeDamageUsesResistanceWithoutDefence()
    {
        _target.SetStats(new StatBlock
        {
            MaxHP = 100,
            DEF = 1000,
            FireResistance = 0.5f,
        });
        _target.ResetResources();

        DamageResult result = _target.TakeDamage(100, DamageElement.Fire, _attacker);

        Assert.That(result.FinalDamage, Is.EqualTo(50));
        Assert.That(_target.CurrentHP, Is.EqualTo(50));
    }

    [Test]
    public void DamageResultIncludesOutgoingMultiplier()
    {
        _attacker.SetStats(new StatBlock
        {
            MaxHP = 100,
            OutgoingDamageMultiplier = 2f,
        });
        _attacker.ResetResources();
        _target.SetStats(new StatBlock { MaxHP = 100, DEF = 0 });
        _target.ResetResources();
        Assert.That(_target.DEF, Is.EqualTo(0));

        DamageResult result = _target.TakeDamage(10, DamageElement.Physical, _attacker);

        Assert.That(result.FinalDamage, Is.EqualTo(20));
    }

    [Test]
    public void CurrentResourcesStayOnCharacterBaseWhenMaximumsRecalculate()
    {
        _target.SetStats(new StatBlock { MaxHP = 100, MaxAP = 50 });
        _target.SetCurrentResources(80, 40);

        _target.SetStats(new StatBlock { MaxHP = 50, MaxAP = 20 });

        Assert.That(_target.CurrentHP, Is.EqualTo(50));
        Assert.That(_target.CurrentAP, Is.EqualTo(20));
        Assert.That(_target.Stats.ResolvedStats.MaxHP, Is.EqualTo(50));
        Assert.That(_target.Stats.ResolvedStats.MaxAP, Is.EqualTo(20));
    }

    [Test]
    public void StatusResistanceCanBlockApplication()
    {
        _target.SetStats(new StatBlock
        {
            MaxHP = 100,
        });
        _target.Stats.BaseStats.SetStatusResistance(StatusEffectIds.Stun, 0f);
        _target.SetProgressedStats(_target.Stats.BaseStats);
        _target.ResetResources();

        StatusApplicationResult result =
            _target.TryApplyStatusEffect(new StunEffect(2));

        Assert.That(result.Applied, Is.False);
        Assert.That(result.Status, Is.EqualTo(StatusApplicationStatus.BlockedByResistance));
        Assert.That(_target.HasEffect(StatusEffectIds.Stun), Is.False);
    }

    [Test]
    public void ClearBattleStatusEffects_RemovesModifiersFlagsAndBleedSubscriptionWithoutHealing()
    {
        _target.SetStats(new StatBlock { MaxHP = 100, MaxAP = 50, ATK = 20, DEF = 10, SPD = 12 });
        _target.SetCurrentResources(80, 35);
        _target.TryApplyStatusEffect(new BerserkEffect(3));
        _target.TryApplyStatusEffect(new BindEffect(3));
        _target.TryApplyStatusEffect(new StunEffect(3));
        _target.TryApplyStatusEffect(new BleedEffect(3));
        _target.TryApplyStatusEffect(new PoisonEffect(3));
        _target.TryApplyStatusEffect(new FreezeEffect(3));
        _target.TryApplyStatusEffect(new IceShieldEffect(3));
        _target.IsDefending = true;
        _target.IsInvincible = true;
        int otherActionNotifications = 0;
        _target.OnActionExecuted += () => otherActionNotifications++;

        _target.ClearBattleStatusEffects();
        _target.NotifyActionExecuted();
        _target.ProcessEffects();

        Assert.That(_target.HasEffect(StatusEffectIds.Berserk), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.Bind), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.Stun), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.Bleed), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.Poison), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.Freeze), Is.False);
        Assert.That(_target.HasEffect(StatusEffectIds.IceShield), Is.False);
        Assert.That(_target.IsBound || _target.IsStunned || _target.IsBerserk, Is.False);
        Assert.That(_target.IsDefending || _target.IsInvincible, Is.False);
        Assert.That(_target.ATK, Is.EqualTo(20));
        Assert.That(_target.DEF, Is.EqualTo(10));
        Assert.That(_target.SPD, Is.EqualTo(12));
        Assert.That(_target.GetIncomingDamageMultiplier(), Is.EqualTo(1f));
        Assert.That(_target.CurrentHP, Is.EqualTo(80));
        Assert.That(_target.CurrentAP, Is.EqualTo(35));
        Assert.That(otherActionNotifications, Is.EqualTo(1), "Non-status subscribers must remain connected.");
    }

    [Test]
    public void ClearBattleStatusEffects_IsIdempotentForDeadInactiveCharacters()
    {
        var effect = new RemovalProbeEffect();
        _target.TryApplyStatusEffect(effect);
        _target.TryApplyStatusEffect(new StunEffect(3));
        _target.TakePureDamage(_target.CurrentHP);
        _target.gameObject.SetActive(false);

        _target.ClearBattleStatusEffects();
        _target.ClearBattleStatusEffects();

        Assert.That(effect.RemovalCalls, Is.EqualTo(1));
        Assert.That(_target.HasEffect(effect.EffectID), Is.False);
        Assert.That(_target.IsStunned, Is.False);
        Assert.That(_target.CurrentHP, Is.Zero);
        Assert.That(_target.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ClearBattleStatusEffects_ClampsResourcesToPersistentMaximums()
    {
        _target.TryApplyStatusEffect(new StatModifierEffect("temp.hp", 3, StatType.MaxHP, flatMod: 100));
        _target.TryApplyStatusEffect(new StatModifierEffect("temp.ap", 3, StatType.MaxAP, flatMod: 20));
        _target.SetCurrentResources(150, 65);

        _target.ClearBattleStatusEffects();

        Assert.That(_target.StatsReader.GetPrimaryStat(StatType.MaxHP), Is.EqualTo(100));
        Assert.That(_target.StatsReader.GetPrimaryStat(StatType.MaxAP), Is.EqualTo(50));
        Assert.That(_target.CurrentHP, Is.EqualTo(100));
        Assert.That(_target.CurrentAP, Is.EqualTo(50));
    }

    private sealed class RemovalProbeEffect : StatusEffect
    {
        public int RemovalCalls { get; private set; }

        public RemovalProbeEffect() : base("test.removal", 3) { }

        public override void OnRemove()
        {
            RemovalCalls++;
            base.OnRemove();
        }
    }

    private static TestCharacter CreateCharacter(string name)
    {
        GameObject gameObject = new GameObject(name);
        TestCharacter character = gameObject.AddComponent<TestCharacter>();
        character.SetStats(new StatBlock { MaxHP = 100, MaxAP = 50 });
        character.ResetResources();
        return character;
    }

    private sealed class TestCharacter : CharacterBase
    {
        public void SetStats(StatBlock stats) => SetBaseStats(stats);
        public void SetProgressedStats(StatBlock stats) => SetProgressedBaseStats(stats);
        public void ResetResources()
        {
            SetCurrentHPValue(MaxHP);
            SetCurrentAPValue(MaxAP);
        }

        public void SetCurrentResources(int hp, int ap)
        {
            SetCurrentHPValue(hp);
            SetCurrentAPValue(ap);
        }

        protected override void OnDie() { }
    }
}
