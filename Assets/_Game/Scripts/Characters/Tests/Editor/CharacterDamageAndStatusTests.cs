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
