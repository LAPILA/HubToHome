using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ChapterOneItemEquipmentTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private object _oldItems;
    private object _oldEquipment;
    private object _oldCharacters;
    private GlobalDataManager _global;
    private GlobalDataManager _oldGlobal;

    [SetUp]
    public void SetUp()
    {
        _oldItems = Cache(typeof(ItemDatabase)).GetValue(null);
        _oldEquipment = Cache(typeof(EquipmentDatabase)).GetValue(null);
        _oldCharacters = Cache(typeof(CharacterDatabase)).GetValue(null);
        Cache(typeof(ItemDatabase)).SetValue(null, new Dictionary<string, ItemData>(StringComparer.Ordinal));
        Cache(typeof(EquipmentDatabase)).SetValue(null, new Dictionary<string, EquipmentData>(StringComparer.Ordinal));
        Cache(typeof(CharacterDatabase)).SetValue(null, new Dictionary<string, CharacterData>(StringComparer.Ordinal));
        _oldGlobal = GlobalDataManager.Instance;
        SetGlobal(null);
        var go = new GameObject("ItemEquipmentTests_Global");
        _created.Add(go);
        _global = go.AddComponent<GlobalDataManager>();
        SetGlobal(_global);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
        SetGlobal(_oldGlobal);
        Cache(typeof(ItemDatabase)).SetValue(null, _oldItems);
        Cache(typeof(EquipmentDatabase)).SetValue(null, _oldEquipment);
        Cache(typeof(CharacterDatabase)).SetValue(null, _oldCharacters);
    }

    [Test]
    public void EquipmentProductRoutesToEquipmentInventoryAndCapsAt99()
    {
        ItemData product = EquipmentProduct();
        product.MaxStackSize = 500;
        Assert.That(InventoryEntryService.Add(_global, product, 150), Is.EqualTo(99));
        Assert.That(_global.GetItemCount(product.ItemID), Is.Zero);
        Assert.That(_global.GetEquipmentCount(product.Equipment.ItemID), Is.EqualTo(99));
        Assert.That(InventoryEntryService.Add(_global, product, 1), Is.Zero);
    }

    [Test]
    public void EquipmentSaleKeepsEquippedCopyAndRejectsSpecialEquipment()
    {
        ItemData product = EquipmentProduct();
        product.Price = 100;
        InventoryEntryService.Add(_global, product, 2);
        _global.Party.Add(new CharacterSaveData
        {
            CharacterDataID = "wizel", CharacterID = "member",
            EquippedEquipmentIDs = new List<string> { product.Equipment.ItemID }
        });
        var store = new GlobalDataShopTransactionStore(_global);
        Assert.That(ShopSellTransactionService.TrySell(store, product).Succeeded, Is.True);
        Assert.That(_global.GetEquipmentCount(product.Equipment.ItemID), Is.EqualTo(1));
        Assert.That(ShopSellTransactionService.TrySell(store, product).Status, Is.EqualTo(ShopSellStatus.InsufficientQuantity));
        InventoryEntryService.Add(_global, product, 1);
        product.Equipment.IsSellable = false;
        Assert.That(ShopSellTransactionService.TrySell(store, product).Status, Is.EqualTo(ShopSellStatus.NotSellable));
    }

    [Test]
    public void MissingEquipmentBridgeCannotConsumeMoney()
    {
        ItemData item = Item("broken", EffectActionType.None);
        item.Type = ItemType.Equipment;
        var shop = ScriptableObject.CreateInstance<ShopDefinition>();
        _created.Add(shop);
        shop.Configure("shop", "Shop", new[] { new ShopEntry("entry", item, 5, 1, 0, "shop.count") });
        _global.AddMoney(20);
        Assert.That(ShopTransactionService.TryPurchase(new GlobalDataShopTransactionStore(_global), shop, "entry").Succeeded, Is.False);
        Assert.That(_global.Money, Is.EqualTo(20));
    }

    [Test]
    public void SavedPartyReviveOnlyConsumesForDeadMemberWithoutSceneActor()
    {
        ItemData item = Item("revive", EffectActionType.Revive);
        item.EffectValue = 25;
        CharacterSaveData member = SavedMember(0);
        _global.Party.Add(member);
        _global.AddItem(item.ItemID, 2);
        Assert.That(InventoryManager.TryUsePartyItem(item.ItemID, member, out _), Is.True);
        Assert.That(member.HP, Is.EqualTo(25));
        Assert.That(_global.GetItemCount(item.ItemID), Is.EqualTo(1));
        Assert.That(InventoryManager.TryUsePartyItem(item.ItemID, member, out _), Is.False);
        Assert.That(_global.GetItemCount(item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void SavedPartyAoeHealsApplicableMembersWithOneConsumption()
    {
        ItemData item = Item("party_heal", EffectActionType.Heal);
        item.EffectValue = 20;
        item.IsAoE = true;
        CharacterSaveData first = SavedMember(10);
        CharacterSaveData second = SavedMember(20);
        CharacterSaveData dead = SavedMember(0);
        _global.Party.AddRange(new[] { first, second, dead });
        _global.AddItem(item.ItemID, 1);
        Assert.That(InventoryManager.TryUsePartyItem(item.ItemID, first, out _), Is.True);
        Assert.That(first.HP, Is.EqualTo(30));
        Assert.That(second.HP, Is.EqualTo(40));
        Assert.That(dead.HP, Is.Zero);
        Assert.That(_global.GetItemCount(item.ItemID), Is.Zero);
    }

    [Test]
    public void FullHealthAndFieldCleanseDoNotConsume()
    {
        ItemData item = Item("heal", EffectActionType.Heal);
        item.EffectValue = 20;
        CharacterSaveData member = SavedMember(100);
        _global.Party.Add(member);
        _global.AddItem(item.ItemID, 1);
        Assert.That(InventoryManager.TryUsePartyItem(item.ItemID, member, out _), Is.False);
        item.ActionType = EffectActionType.Cleanse;
        Assert.That(InventoryManager.TryUsePartyItem(item.ItemID, member, out _), Is.False);
        Assert.That(_global.GetItemCount(item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void CleansePreservesHelpfulBuffAndSingleChillRecoveryFence()
    {
        PlayerCharacter actor = Actor();
        actor.TryApplyStatusEffect(new HasteEffect(3));
        actor.Elements.Add(ElementalStatusKind.Chill, 10);
        actor.Elements.Add(ElementalStatusKind.Burn, 2);
        ItemData item = Item("cleanse", EffectActionType.Cleanse);
        item.CleanseAllHarmful = false;
        item.CleanseElement = ElementalStatusKind.Chill;
        Assert.That(ItemEffectService.TryApply(item, actor, true, out _), Is.True);
        Assert.That(actor.Elements.IsFrozen, Is.False);
        Assert.That(actor.Elements.RecoveryLocked, Is.True);
        Assert.That(actor.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(2));
        Assert.That(actor.HasEffect(StatusEffectIds.Haste), Is.True);
        item.CleanseAllHarmful = true;
        Assert.That(ItemEffectService.TryApply(item, actor, true, out _), Is.True);
        Assert.That(actor.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
        Assert.That(actor.HasEffect(StatusEffectIds.Haste), Is.True);
    }

    [TestCase(TargetStatType.HP)]
    [TestCase(TargetStatType.AP)]
    public void BattleRecoveryRejectsFullResourceAndAcceptsMissingResource(TargetStatType stat)
    {
        PlayerCharacter actor = Actor();
        ItemData item = Item("battle_recovery", EffectActionType.Heal);
        item.TargetStat = stat;
        item.EffectValue = 20;
        Assert.That(ItemEffectService.CanApply(item, actor, true, out _), Is.False);
        Assert.That(ItemEffectService.TryApply(item, actor, true, out _), Is.False);

        if (stat == TargetStatType.HP) actor.TakePureDamage(1);
        else actor.ConsumeAP(1);

        Assert.That(ItemEffectService.CanApply(item, actor, true, out _), Is.True);
        Assert.That(ItemEffectService.TryApply(item, actor, true, out _), Is.True);
        Assert.That(stat == TargetStatType.HP ? actor.CurrentHP : actor.CurrentAP,
            Is.EqualTo(stat == TargetStatType.HP ? actor.MaxHP : actor.MaxAP));
        Assert.That(ItemEffectService.CanApply(item, actor, true, out _), Is.False);
    }

    [Test]
    public void EquipmentEffectExecutesOnlyMatchingTriggerAndRespectsResourceCap()
    {
        PlayerCharacter actor = Actor();
        EquipmentData equipment = EquipmentProduct().Equipment;
        equipment.TriggeredEffects.Add(new EquipmentTriggeredEffect
        {
            Trigger = EquipmentEffectTrigger.JustGuard, Effect = EquipmentEffectKind.RestoreAP,
            Calculation = ValueCalcType.Flat, Amount = 10
        });
        actor.WeaponSlot = equipment;
        actor.ConsumeAP(5);
        int before = actor.CurrentAP;
        EquipmentEffectService.Apply(actor, EquipmentEffectTrigger.Dodge);
        Assert.That(actor.CurrentAP, Is.EqualTo(before));
        EquipmentEffectService.Apply(actor, EquipmentEffectTrigger.JustGuard);
        Assert.That(actor.CurrentAP, Is.EqualTo(actor.MaxAP));
    }

    private ItemData Item(string id, EffectActionType action)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        _created.Add(item);
        item.ItemID = id;
        item.ActionType = action;
        ((Dictionary<string, ItemData>)Cache(typeof(ItemDatabase)).GetValue(null))[id] = item;
        return item;
    }

    private ItemData EquipmentProduct()
    {
        EquipmentData equipment = ScriptableObject.CreateInstance<EquipmentData>();
        _created.Add(equipment);
        equipment.ItemID = "weapon.test";
        equipment.AllowedCharacterIDs.Add("wizel");
        ((Dictionary<string, EquipmentData>)Cache(typeof(EquipmentDatabase)).GetValue(null))[equipment.ItemID] = equipment;
        ItemData product = Item("product.weapon", EffectActionType.None);
        product.Type = ItemType.Equipment;
        product.Equipment = equipment;
        return product;
    }

    private PlayerCharacter Actor()
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        _created.Add(data);
        data.CharacterID = "wizel";
        var go = new GameObject("ItemEquipmentTests_Actor");
        _created.Add(go);
        PlayerCharacter actor = go.AddComponent<PlayerCharacter>();
        actor.SetCharacterData(data);
        return actor;
    }

    private static CharacterSaveData SavedMember(int hp) => new CharacterSaveData
    {
        CharacterID = Guid.NewGuid().ToString("N"), CharacterDataID = "not.spawned",
        HP = hp, AP = 10, MaxHP = 100, MaxAP = 20, ATK = 10, DEF = 0, SPD = 10
    };

    private static FieldInfo Cache(Type type) => type.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
    private static void SetGlobal(GlobalDataManager value) => typeof(GlobalDataManager).GetProperty("Instance")
        .GetSetMethod(true).Invoke(null, new object[] { value });
}
