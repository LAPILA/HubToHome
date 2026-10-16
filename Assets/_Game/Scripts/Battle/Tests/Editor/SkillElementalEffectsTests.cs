using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillElementalEffectsTests
{
    private TestCharacter _actor, _target, _second;
    private SkillData _skill;
    private SkillContext _context;

    [SetUp]
    public void SetUp()
    {
        _actor = Create("Actor");
        _target = Create("Target");
        _second = Create("Second");
        _skill = ScriptableObject.CreateInstance<SkillData>();
        _skill.ActionTimeline.Add(new Action_Wait());
        _context = new SkillContext { Actor = _actor, Targets = new List<CharacterBase> { _target, _second } };
        _context.BeginSkillResources(_skill);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_actor.gameObject);
        Object.DestroyImmediate(_target.gameObject);
        Object.DestroyImmediate(_second.gameObject);
        Object.DestroyImmediate(_skill);
    }

    [Test]
    public void OnceAndPerHitAreIndependentAndScopedPerTarget()
    {
        _skill.ElementalApplications = new[]
        {
            Application(ElementalStatusKind.Burn, 3, ElementalApplicationMode.OncePerSkill),
            Application(ElementalStatusKind.Poison, 2, ElementalApplicationMode.PerHit)
        };
        for (int i = 0; i < 2; i++)
        {
            _context.ApplySkillHit(_target, 10, DamageElement.Physical);
            _context.ApplySkillHit(_second, 10, DamageElement.Physical);
        }
        foreach (CharacterBase target in _context.Targets)
        {
            Assert.That(target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(3));
            Assert.That(target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(4));
        }
    }

    [Test]
    public void EvadedHitDoesNotSpendTheOncePerSkillApplication()
    {
        _skill.ElementalApplications = new[] { Application(ElementalStatusKind.Burn, 6) };
        _target.IsInvincible = true;
        Assert.That(_context.ApplySkillHit(_target, 10, DamageElement.Fire).Applied, Is.False);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
        _target.IsInvincible = false;
        _context.ApplySkillHit(_target, 10, DamageElement.Fire);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(6));
    }

    [Test]
    public void GuardBuildupIsSeparateFromOffensiveQteAndResourceMultipliers()
    {
        _skill.ElementalApplications = new[] { Application(ElementalStatusKind.Poison, 5) };
        _context.CurrentStatusBuildupMultiplier = .5f;
        _context.CurrentDamageMultiplier = 2f;
        _context.ApplySkillHit(_target, 10, DamageElement.Physical);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(2));
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Poison), Is.EqualTo(.5d));
    }

    [Test]
    public void FiftyHitPayloadAccumulatesTenthsWithoutMinimumOneInflation()
    {
        _skill.StatusProcCoefficient = .1f;
        _skill.ElementalApplications = new[] { Application(ElementalStatusKind.Burn, .2f, ElementalApplicationMode.PerHit) };
        for (int i = 0; i < 50; i++) _context.ApplySkillHit(_target, 1, DamageElement.Physical);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(10));
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Burn), Is.LessThan(.001d));
    }

    [Test]
    public void FreezeCompletingHitDoesNotShatterButFollowingHitDoes()
    {
        _skill.ElementalApplications = new[] { Application(ElementalStatusKind.Chill, 10) };
        Assert.That(_context.ApplySkillHit(_target, 100, DamageElement.Ice).FinalDamage, Is.EqualTo(100));
        Assert.That(_target.Elements.IsFrozen, Is.True);
        Assert.That(_context.ApplySkillHit(_target, 100, DamageElement.Ice).FinalDamage, Is.EqualTo(150));
        Assert.That(_target.Elements.IsFrozen, Is.False);
        Assert.That(_target.Elements.RecoveryLocked, Is.True);
    }

    [Test]
    public void StoppedTimelineCannotDealDamageOrApplyPayload()
    {
        _skill.ElementalApplications = new[] { Application(ElementalStatusKind.Burn, 6) };
        _context.StopTimelineExecution = true;
        Assert.That(_context.ApplySkillHit(_target, 100, DamageElement.Fire).Applied, Is.False);
        Assert.That(_target.CurrentHP, Is.EqualTo(_target.MaxHP));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
    }

    [Test]
    public void DetonationDoesNotNaturallyGrowPoisonAndCleanseKeepsLegacyBuffs()
    {
        _target.Elements.Add(ElementalStatusKind.Poison, 8);
        _target.Elements.Add(ElementalStatusKind.Burn, 3);
        var detonate = new Action_ElementalUtility { Operation = ElementalUtilityOperation.Detonate, Kind = ElementalStatusKind.Poison };
        Assert.That(detonate.Execute(_context).MoveNext(), Is.False);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(8));
        Assert.That(_target.ElementalDamageTotal, Is.EqualTo(4));
        _target.TryApplyStatusEffect(new BerserkEffect(2));
        var cleanse = new Action_ElementalUtility { Operation = ElementalUtilityOperation.Cleanse };
        Assert.That(cleanse.Execute(_context).MoveNext(), Is.False);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Poison), Is.Zero);
        Assert.That(_target.HasEffect(StatusEffectIds.Berserk), Is.True);
    }

    [Test]
    public void AccumulationDoesNotInvalidateTurnSpeedForecast()
    {
        uint revision = _target.BattleStatsRevision;
        _target.Elements.Add(ElementalStatusKind.Electric, 80);
        Assert.That(_target.BattleStatsRevision, Is.EqualTo(revision));
    }

    private static ElementalStatusApplication Application(ElementalStatusKind kind, float amount,
        ElementalApplicationMode mode = ElementalApplicationMode.OncePerSkill)
        => new ElementalStatusApplication { Kind = kind, Amount = amount, Mode = mode };

    private static TestCharacter Create(string name)
    {
        var actor = new GameObject(name).AddComponent<TestCharacter>();
        actor.Initialize();
        return actor;
    }

    private sealed class TestCharacter : CharacterBase
    {
        public void Initialize()
        {
            SetBaseStats(new StatBlock { MaxHP = 10000, ATK = 10, SPD = 10 });
            SetCurrentHPValue(MaxHP);
        }
        protected override void OnDie() { }
    }
}
