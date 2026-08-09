using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InventoryConsumptionTests
{
    private GameObject _globalObject;
    private GameObject _inventoryObject;
    private GameObject _targetObject;
    private ItemData _createdItem;
    private CharacterData _characterData;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("GlobalDataManager");
        GlobalDataManager global = _globalObject.AddComponent<GlobalDataManager>();
        SetSingleton(typeof(GlobalDataManager), global);
        _inventoryObject = new GameObject("InventoryManager");
        InventoryManager inventory = _inventoryObject.AddComponent<InventoryManager>();
        SetSingleton(typeof(InventoryManager), inventory);
        _characterData = ScriptableObject.CreateInstance<CharacterData>();
        _targetObject = new GameObject("Target");
        PlayerCharacter target = _targetObject.AddComponent<PlayerCharacter>();
        target.SetCharacterData(_characterData);
    }

    [TearDown]
    public void TearDown()
    {
        SetSingleton(typeof(InventoryManager), null);
        SetSingleton(typeof(GlobalDataManager), null);
        Object.DestroyImmediate(_targetObject);
        Object.DestroyImmediate(_inventoryObject);
        Object.DestroyImmediate(_globalObject);
        if (_createdItem != null) Object.DestroyImmediate(_createdItem);
        if (_characterData != null) Object.DestroyImmediate(_characterData);
    }

    private static void SetSingleton(System.Type type, object value)
    {
        PropertyInfo property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property.GetSetMethod(true).Invoke(null, new[] { value });
    }

    [Test]
    public void SuccessfulUseConsumesExactlyOneItem()
    {
        const string itemId = "consumable.small_potion";
        PlayerCharacter target = _targetObject.GetComponent<PlayerCharacter>();
        target.HealHP(target.MaxHP);
        target.TakePureDamage(50);
        GlobalDataManager.Instance.AddItem(itemId, 2);

        bool used = InventoryManager.Instance.UseItem(itemId, target);

        Assert.That(used, Is.True);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(1));
        Assert.That(target.CurrentHP, Is.EqualTo(80));
    }

    [Test]
    public void InvalidTargetDoesNotConsumeItem()
    {
        const string itemId = "consumable.small_potion";
        GlobalDataManager.Instance.AddItem(itemId, 1);

        bool used = InventoryManager.Instance.UseItem(itemId, null);

        Assert.That(used, Is.False);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(1));
    }
    [Test]
    public void InvalidEffectConfigurationDoesNotConsumeItem()
    {
        const string itemId = "test.invalid_heal";
        _createdItem = ScriptableObject.CreateInstance<ItemData>();
        _createdItem.ItemID = itemId;
        _createdItem.Type = ItemType.Consumable;
        _createdItem.UsableInOverworld = true;
        _createdItem.ActionType = EffectActionType.Heal;
        _createdItem.TargetStat = TargetStatType.None;
        _createdItem.EffectValue = 30;

        FieldInfo field = typeof(InventoryManager).GetField("_itemDict", BindingFlags.Instance | BindingFlags.NonPublic);
        var items = (Dictionary<string, ItemData>)field.GetValue(InventoryManager.Instance);
        items.Add(itemId, _createdItem);
        GlobalDataManager.Instance.AddItem(itemId, 1);

        bool used = InventoryManager.Instance.UseItem(itemId, _targetObject.GetComponent<PlayerCharacter>());

        Assert.That(used, Is.False);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(1));
    }

    [Test]
    public void ItemUseReservesOwnedItemBeforeEffectCallbacks()
    {
        const string itemId = "consumable.small_potion";
        PlayerCharacter target = _targetObject.GetComponent<PlayerCharacter>();
        target.HealHP(target.MaxHP);
        target.TakePureDamage(50);
        GlobalDataManager.Instance.AddItem(itemId, 1);
        target.OnHPChanged += (_, _, _) => GlobalDataManager.Instance.RemoveItem(itemId, 1);

        bool used = InventoryManager.Instance.UseItem(itemId, target);

        Assert.That(used, Is.True);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.Zero);
        Assert.That(target.CurrentHP, Is.EqualTo(80));
    }

    [Test]
    public void EffectExceptionRestoresReservedItem()
    {
        const string itemId = "consumable.small_potion";
        PlayerCharacter target = _targetObject.GetComponent<PlayerCharacter>();
        target.HealHP(target.MaxHP);
        target.TakePureDamage(50);
        GlobalDataManager.Instance.AddItem(itemId, 1);
        target.OnHPChanged += (_, _, _) =>
            throw new System.InvalidOperationException("test effect callback failure");

        bool used = false;
        Assert.DoesNotThrow(() => used = InventoryManager.Instance.UseItem(itemId, target));

        Assert.That(used, Is.False);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(1));
    }

    [Test]
    public void InventoryRejectsInvalidAmountsAndClampsKnownStackSize()
    {
        const string itemId = "consumable.small_potion";

        int added = GlobalDataManager.Instance.AddItemAndGetAddedAmount(itemId, 200);
        int overflow = GlobalDataManager.Instance.AddItemAndGetAddedAmount(itemId, 1);
        GlobalDataManager.Instance.AddItem(itemId, -1);

        Assert.That(added, Is.EqualTo(99));
        Assert.That(overflow, Is.Zero);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(99));
        Assert.That(GlobalDataManager.Instance.RemoveItem(itemId, -1), Is.False);
        Assert.That(GlobalDataManager.Instance.GetItemCount(itemId), Is.EqualTo(99));
    }
}
