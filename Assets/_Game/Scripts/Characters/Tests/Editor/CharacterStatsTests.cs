using System.Collections.Generic;
using NUnit.Framework;

public sealed class CharacterStatsTests
{
    [Test]
    public void MissingStatusMapAndNonFiniteResistance_AreSafeWithoutChangingSource()
    {
        var input = new StatBlock { StatusResistances = null, FireResistance = float.NaN, IceResistance = -1f };
        Assert.That(input.Clone().GetStatusResistance("burn"), Is.EqualTo(1f));
        StatBlock resolved = CharacterStatsCalculator.Resolve(input, null, null);
        Assert.That(resolved.FireResistance, Is.EqualTo(1f));
        Assert.That(resolved.IceResistance, Is.Zero);
        Assert.That(input.StatusResistances, Is.Null);
        Assert.That(float.IsNaN(input.FireResistance), Is.True);
        input.SetStatusResistance("burn", 0.5f);
        Assert.That(input.GetStatusResistance("burn"), Is.EqualTo(0.5f));
    }

    [Test]
    public void NonFiniteModifiers_AreIgnoredAndFiniteOverflowCannotWrapPrimaryStat()
    {
        var modifiers = new[]
        {
            StatModifier.ForElementResistance(StatLayer.Equipment, DamageElement.Fire, float.NaN),
            StatModifier.ForStatusResistance(StatLayer.Equipment, "burn", float.PositiveInfinity),
            StatModifier.ForIncomingDamageMultiplier(StatLayer.Equipment, additivePercent: float.NaN),
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.ATK, float.NaN)
        };
        StatBlock resolved = CharacterStatsCalculator.Resolve(new StatBlock(), modifiers, null);
        Assert.That(resolved.ATK, Is.EqualTo(10));
        Assert.That(resolved.FireResistance, Is.EqualTo(1f));
        Assert.That(resolved.GetStatusResistance("burn"), Is.EqualTo(1f));
        Assert.That(resolved.IncomingDamageMultiplier, Is.EqualTo(1f));
        Assert.That(CharacterStatsCalculator.ApplyPrimaryStatLayer(int.MaxValue, StatType.ATK,
            StatLayer.Equipment, new[] { StatModifier.ForPrimary(StatLayer.Equipment, StatType.ATK, int.MaxValue) }),
            Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void ZeroDefense_RemainsZeroWhenUnrelatedEquipmentIsApplied()
    {
        var input = new StatBlock { DEF = 0 };
        StatBlock resolved = CharacterStatsCalculator.Resolve(input,
            new[] { StatModifier.ForPrimary(StatLayer.Equipment, StatType.ATK, 7f) }, null);
        Assert.That(resolved.DEF, Is.Zero);
    }

    [TestCase(3f, 0.5f)]
    [TestCase(-30f, -1.2f)]
    [TestCase(0f, 0f)]
    public void SinglePrimaryProjectionMatchesFullLayerCalculation(float flat, float percent)
    {
        var input = new StatBlock { MaxHP = 101, MaxAP = 0, ATK = 11, DEF = 0, SPD = 13 };
        var modifiers = new List<StatModifier>
        {
            StatModifier.ForPrimary(StatLayer.Battle, StatType.SPD, flat, percent),
            StatModifier.ForPrimary(StatLayer.Battle, StatType.ATK, 2f, 0.1f),
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.SPD, 5f, 0.25f),
            StatModifier.ForIncomingDamageMultiplier(StatLayer.Battle, -0.2f),
        };
        foreach (StatLayer layer in new[] { StatLayer.Equipment, StatLayer.Battle })
        {
            StatBlock full = CharacterStatsCalculator.ApplyLayer(input, layer, modifiers);
            foreach (StatType stat in new[] { StatType.MaxHP, StatType.MaxAP, StatType.ATK, StatType.DEF, StatType.SPD })
                Assert.That(CharacterStatsCalculator.ApplyPrimaryStatLayer(input.GetPrimaryStat(stat), stat, layer, modifiers),
                    Is.EqualTo(full.GetPrimaryStat(stat)));
            input = full;
        }
        Assert.That(CharacterStatsCalculator.ApplyPrimaryStatLayer(0, StatType.DEF, StatLayer.Battle, null), Is.Zero);
    }

    // 계산 순서와 런타임 자원 분리는 이 테스트를 기준 계약으로 유지한다.
    [Test]
    public void Resolve_AppliesFlatThenAdditivePercentPerLayer()
    {
        var baseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 50,
            ATK = 10,
            DEF = 5,
            SPD = 10,
        };

        var equipment = new List<StatModifier>
        {
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.ATK, flatValue: 5f, additivePercent: 0.1f),
        };
        var battle = new List<StatModifier>
        {
            StatModifier.ForPrimary(StatLayer.Battle, StatType.ATK, flatValue: 2f, additivePercent: 0.2f),
        };

        StatBlock resolved = CharacterStatsCalculator.Resolve(baseStats, equipment, battle);

        Assert.That(resolved.ATK, Is.EqualTo(23));
    }

    [Test]
    public void ApplyLayer_SumsPercentModifiersBeforeApplyingThem()
    {
        var input = new StatBlock { ATK = 100, MaxHP = 1, MaxAP = 0 };
        var modifiers = new List<StatModifier>
        {
            StatModifier.ForPrimary(StatLayer.Battle, StatType.ATK, additivePercent: 0.1f),
            StatModifier.ForPrimary(StatLayer.Battle, StatType.ATK, additivePercent: 0.2f),
        };

        StatBlock resolved = CharacterStatsCalculator.ApplyLayer(input, StatLayer.Battle, modifiers);

        Assert.That(resolved.ATK, Is.EqualTo(130));
    }

    [Test]
    public void Resolve_OnlyChangesLayeredStats()
    {
        var baseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 50,
        };
        var equipment = new List<StatModifier>
        {
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.MaxHP, flatValue: -90f),
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.MaxAP, flatValue: -40f),
        };

        StatBlock resolved = CharacterStatsCalculator.Resolve(baseStats, equipment, null);

        Assert.That(resolved.MaxHP, Is.EqualTo(10));
        Assert.That(resolved.MaxAP, Is.EqualTo(10));
    }

    [Test]
    public void ApplyLayer_ResolvesElementAndStatusResistanceInTheSameBlock()
    {
        var input = new StatBlock();
        input.SetStatusResistance("Burn", 1f);
        var modifiers = new List<StatModifier>
        {
            StatModifier.ForElementResistance(StatLayer.Battle, DamageElement.Fire, flatValue: -0.5f),
            StatModifier.ForStatusResistance(StatLayer.Battle, "Burn", flatValue: -0.25f),
        };

        StatBlock resolved = CharacterStatsCalculator.ApplyLayer(input, StatLayer.Battle, modifiers);

        Assert.That(resolved.GetElementResistance(DamageElement.Fire), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(resolved.GetStatusResistance("Burn"), Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void ApplyLayer_ResolvesIncomingAndOutgoingDamageMultipliers()
    {
        var input = new StatBlock();
        var modifiers = new List<StatModifier>
        {
            StatModifier.ForIncomingDamageMultiplier(StatLayer.Battle, flatValue: -0.2f),
            StatModifier.ForOutgoingDamageMultiplier(StatLayer.Battle, flatValue: 0.5f),
        };

        StatBlock resolved = CharacterStatsCalculator.ApplyLayer(input, StatLayer.Battle, modifiers);

        Assert.That(resolved.IncomingDamageMultiplier, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(resolved.OutgoingDamageMultiplier, Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void CharacterStats_RecalculateKeepsResolvedStatsAfterModifierChanges()
    {
        var stats = new CharacterStats();
        stats.SetBaseStats(new StatBlock { MaxHP = 100, MaxAP = 50 });
        stats.SetEquipmentModifiers(new[]
        {
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.MaxHP, flatValue: 50f),
            StatModifier.ForPrimary(StatLayer.Equipment, StatType.MaxAP, flatValue: 10f),
        });

        Assert.That(stats.ResolvedStats.MaxHP, Is.EqualTo(150));
        Assert.That(stats.ResolvedStats.MaxAP, Is.EqualTo(60));
    }

    [Test]
    public void StatsReaderReturnsResolvedSnapshotWithoutExposingMutableState()
    {
        var stats = new CharacterStats();
        stats.SetBaseStats(new StatBlock { ATK = 12 });

        ICharacterStatsReader reader = stats;
        StatBlock snapshot = reader.GetResolvedSnapshot();
        snapshot.ATK = 999;

        Assert.That(reader.GetPrimaryStat(StatType.ATK), Is.EqualTo(12));
    }
}
