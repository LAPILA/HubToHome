using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatAPItemPolicyTests
{
    private ItemTarget _target;
    private ItemData _item;

    [SetUp]
    public void SetUp()
    {
        _target = new GameObject("CombatItemPayloadTarget").AddComponent<ItemTarget>();
        _target.Configure(new StatBlock { MaxHP = 500, MaxAP = 40, DEF = 0 });
        _item = ScriptableObject.CreateInstance<ItemData>();
        _item.ActionType = EffectActionType.None;
        _item.UsableInOverworld = false;
        _item.ElementalPayload = new ElementalItemPayload
        {
            Kind = ElementalStatusKind.Electric, Operation = ElementalItemOperation.AddBuildup, Amount = 4
        };
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_target.gameObject);
        UnityEngine.Object.DestroyImmediate(_item);
    }

    [Test]
    public void BaseParryBudgetIsPerAllyAndResetsForNextAction()
    {
        var budget = new BattleParryRewardBudget<object>();
        object first = new object(), second = new object();
        int total = 0;
        for (int i = 0; i < 10; i++) total += budget.Claim(first, 2);
        Assert.That(total, Is.EqualTo(6));
        Assert.That(budget.Claim(second, 2), Is.EqualTo(2));
        Assert.That(budget.Claim(null, 2), Is.Zero);
        Assert.That(budget.Claim(second, -1), Is.Zero);
        budget.Reset();
        Assert.That(budget.Claim(first, int.MaxValue), Is.EqualTo(6));
        budget.Reset(-1);
        Assert.That(budget.Claim(first, 2), Is.Zero);
    }

    [Test]
    public void OldEquipmentTriggerValuesAreStable()
    {
        Assert.That((int)EquipmentEffectTrigger.BattleStart, Is.Zero);
        Assert.That((int)EquipmentEffectTrigger.Counter, Is.EqualTo(4));
        Assert.That((int)EquipmentEffectTrigger.ItemUsed, Is.EqualTo(5));
    }

    [Test]
    public void AdditionIsRejectedAtCapAndByImmunityWithoutChangingResources()
    {
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.True);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(4));
        _target.Elements.Add(ElementalStatusKind.Electric, 9999);
        Assert.That(ItemEffectService.CanApply(_item, _target, true, out _), Is.False);
        _target.Elements.Reset();
        var stats = new StatBlock { MaxHP = 500, MaxAP = 40 };
        stats.SetStatusResistance("Electric", 0f);
        _target.Configure(stats);
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.False);
        Assert.That(_target.CurrentHP, Is.EqualTo(500));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.Zero);
    }

    [Test]
    public void FractionalResistedAdditionIsSuccessfulAndRetainsCarry()
    {
        var stats = new StatBlock { MaxHP = 500, MaxAP = 40 };
        stats.SetStatusResistance("Electric", .1f);
        _target.Configure(stats);
        for (int i = 0; i < 3; i++)
            Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.True);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(1));
        Assert.That(_target.Elements.GetBuildupRemainder(ElementalStatusKind.Electric), Is.EqualTo(.2d).Within(.000001d));
    }

    [Test]
    public void ConsumptionUsesOnlyAvailableBuildupAndReportsNoOrdinaryDamage()
    {
        _item.ElementalPayload.Operation = ElementalItemOperation.ConsumeForDamage;
        _item.ElementalPayload.Amount = 6;
        _item.ElementalPayload.DamagePerStack = 18;
        Assert.That(ItemEffectService.CanApply(_item, _target, true, out _), Is.False);
        _target.Elements.Add(ElementalStatusKind.Electric, 8);
        int feedback = 0;
        _target.OnElementalDamage += (_, __, ___) => feedback++;
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _, out int mainDamage), Is.True);
        Assert.That(mainDamage, Is.Zero);
        Assert.That(_target.CurrentHP, Is.EqualTo(392));
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(2));
        Assert.That(feedback, Is.EqualTo(1));
    }

    [Test]
    public void DeadTargetAndBattleOnlyFieldUseAreRejected()
    {
        _item.UsableInOverworld = true; // 제작자가 실수로 켜도 부분 적용하지 않습니다.
        Assert.That(ItemEffectService.TryApply(_item, _target, false, out _), Is.False);
        Assert.That(ItemEffectService.TryApplyToSavedMember(_item,
            new CharacterSaveData { HP = 100, MaxHP = 100, AP = 5, MaxAP = 20 }, out _), Is.False);
        _target.SetHP(0);
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.False);
    }

    [Test]
    public void OrdinaryLethalDamageDoesNotConsumeBuildupAfterDeath()
    {
        _target.Elements.Add(ElementalStatusKind.Electric, 8);
        _item.ActionType = EffectActionType.Damage;
        _item.TargetStat = TargetStatType.HP;
        _item.EffectValue = 9999;
        _item.ElementalPayload.Operation = ElementalItemOperation.ConsumeForDamage;
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.True);
        Assert.That(_target.IsAlive, Is.False);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.EqualTo(8));
    }

    [Test]
    public void PayloadDamageSaturatesInsteadOfOverflowing()
    {
        _target.Elements.Add(ElementalStatusKind.Electric, 10);
        _item.ElementalPayload.Operation = ElementalItemOperation.ConsumeForDamage;
        _item.ElementalPayload.Amount = 10;
        _item.ElementalPayload.DamagePerStack = int.MaxValue;
        Assert.That(ItemEffectService.TryApply(_item, _target, true, out _), Is.True);
        Assert.That(_target.CurrentHP, Is.Zero);
        Assert.That(_target.Elements.GetValue(ElementalStatusKind.Electric), Is.Zero);
    }

    private sealed class ItemTarget : CharacterBase
    {
        public void Configure(StatBlock stats)
        {
            SetBaseStats(stats);
            SetCurrentHPValue(MaxHP);
            SetCurrentAPValue(MaxAP);
        }
        public void SetHP(int value) => SetCurrentHPValue(value);
        protected override void OnDie() { }
    }
}
