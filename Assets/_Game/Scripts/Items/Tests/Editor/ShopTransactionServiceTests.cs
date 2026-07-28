using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ShopTransactionServiceTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private GlobalDataManager _previousGlobal;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }
        _createdObjects.Clear();
        ItemDatabase.InvalidateCache();
        SetGlobalInstance(_previousGlobal);
    }

    [Test]
    public void ValidPurchaseCommitsMoneyItemAndPurchaseCount()
    {
        ItemData item = Item("item.patch");
        ShopDefinition shop = Shop(Entry("patch", item, 10, 2));
        var store = new FakeShopTransactionStore(item, money: 30);

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch", 1);

        Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.Succeeded));
        Assert.That(result.TotalPrice, Is.EqualTo(20));
        Assert.That(result.ItemAmount, Is.EqualTo(2));
        Assert.That(store.Money, Is.EqualTo(10));
        Assert.That(store.GetItemCount(item.ItemID), Is.EqualTo(2));
        Assert.That(store.GetFlag("shop.test.patch.purchases"), Is.EqualTo(1));
    }

    [Test]
    public void EntryMustBelongToShopExactlyOnce()
    {
        ItemData item = Item("item.patch");
        var store = new FakeShopTransactionStore(item, money: 100);
        ShopDefinition missing = Shop(Entry("other", item, 1, 1));
        ShopDefinition duplicate = Shop(
            Entry("patch", item, 1, 1),
            Entry("patch", item, 1, 1));

        Assert.That(
            ShopTransactionService.TryPurchase(store, missing, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.EntryNotFound));
        Assert.That(
            ShopTransactionService.TryPurchase(store, duplicate, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.DuplicateEntryId));
        Assert.That(store.Money, Is.EqualTo(100));
    }

    [Test]
    public void PreflightRejectsInsufficientFundsCapacityAndUnregisteredItem()
    {
        ItemData item = Item("item.patch");
        ShopDefinition shop = Shop(Entry("patch", item, 10, 2));

        var poor = new FakeShopTransactionStore(item, money: 19);
        Assert.That(
            ShopTransactionService.TryPurchase(poor, shop, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.InsufficientFunds));

        var full = new FakeShopTransactionStore(item, money: 100) { MaxItemCount = 1 };
        Assert.That(
            ShopTransactionService.TryPurchase(full, shop, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.InventoryCapacityExceeded));

        var unregistered = new FakeShopTransactionStore(null, money: 100);
        Assert.That(
            ShopTransactionService.TryPurchase(unregistered, shop, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.ItemNotRegistered));
    }

    [Test]
    public void InvalidCountsPricesLimitsAndOverflowDoNotMutateStore()
    {
        ItemData item = Item("item.patch");
        var store = new FakeShopTransactionStore(item, money: int.MaxValue);

        ShopDefinition negativePrice = Shop(Entry("patch", item, -1, 1));
        Assert.That(
            ShopTransactionService.TryPurchase(store, negativePrice, "patch").Succeeded,
            Is.False);

        ShopDefinition overflow = Shop(Entry("patch", item, int.MaxValue, 2));
        Assert.That(
            ShopTransactionService.TryPurchase(store, overflow, "patch").Status,
            Is.EqualTo(ShopPurchaseStatus.PriceOverflow));

        ShopDefinition limited = Shop(Entry("patch", item, 1, 1, limit: 2));
        store.SetFlag("shop.test.patch.purchases", 1);
        Assert.That(
            ShopTransactionService.TryPurchase(store, limited, "patch", 2).Status,
            Is.EqualTo(ShopPurchaseStatus.PurchaseLimitReached));
        Assert.That(
            ShopTransactionService.TryPurchase(store, limited, "patch", 0).Status,
            Is.EqualTo(ShopPurchaseStatus.InvalidPurchaseCount));
        Assert.That(store.Money, Is.EqualTo(int.MaxValue));
        Assert.That(store.GetItemCount(item.ItemID), Is.Zero);
    }

    [Test]
    public void ItemCommitFailureRefundsMoneyExactly()
    {
        ItemData item = Item("item.patch");
        ShopDefinition shop = Shop(Entry("patch", item, 10, 2));
        var store = new FakeShopTransactionStore(item, money: 30) { FailItemAdd = true };

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch");

        Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.ItemCommitFailed));
        Assert.That(result.RollbackAttempted, Is.True);
        Assert.That(result.RollbackSucceeded, Is.True);
        Assert.That(store.Money, Is.EqualTo(30));
        Assert.That(store.GetItemCount(item.ItemID), Is.Zero);
        Assert.That(store.HasFlag("shop.test.patch.purchases"), Is.False);
    }

    [Test]
    public void FlagWriteExceptionRollsBackItemAndMoney()
    {
        ItemData item = Item("item.patch");
        ShopDefinition shop = Shop(Entry("patch", item, 10, 2));
        var store = new FakeShopTransactionStore(item, money: 30) { ThrowOnFlagSet = true };

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch");

        Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.StoreException));
        Assert.That(result.RollbackSucceeded, Is.True);
        Assert.That(store.Money, Is.EqualTo(30));
        Assert.That(store.GetItemCount(item.ItemID), Is.Zero);
        Assert.That(store.HasFlag("shop.test.patch.purchases"), Is.False);
    }

    [Test]
    public void RollbackFailureIsReportedWithoutHidingOriginalCause()
    {
        ItemData item = Item("item.patch");
        ShopDefinition shop = Shop(Entry("patch", item, 10, 1));
        var store = new FakeShopTransactionStore(item, money: 30)
        {
            FailFlagSet = true,
            FailItemRemove = true
        };

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch");

        Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.RollbackFailed));
        Assert.That(result.FailureCause, Is.EqualTo(ShopPurchaseStatus.FlagCommitFailed));
        Assert.That(result.RollbackSucceeded, Is.False);
    }

    [Test]
    public void GlobalDataAdapterCommitsOnceEvenWhenFlagObserverThrows()
    {
        ItemData item = Item("item.global_adapter");
        SetItemDatabase(item);
        SetGlobalInstance(null);
        GameObject globalObject = new GameObject("ShopTransactionServiceTests_Global");
        _createdObjects.Add(globalObject);
        GlobalDataManager global = globalObject.AddComponent<GlobalDataManager>();
        SetGlobalInstance(global);
        global.AddMoney(20);
        int successfulObserverCalls = 0;
        global.FlagChanged += (_, _, _) => throw new InvalidOperationException("shop observer failure");
        global.FlagChanged += (_, _, _) => successfulObserverCalls++;
        ShopDefinition shop = Shop(Entry("patch", item, 5, 2));
        var store = new GlobalDataShopTransactionStore(global);
        LogAssert.Expect(LogType.Exception, new Regex("shop observer failure"));

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(global.Money, Is.EqualTo(10));
        Assert.That(global.GetItemCount(item.ItemID), Is.EqualTo(2));
        Assert.That(global.GetFlag("shop.test.patch.purchases"), Is.EqualTo(1));
        Assert.That(successfulObserverCalls, Is.EqualTo(1));
    }

    [Test]
    public void ValidSaleCommitsItemAndHalfPriceMoney()
    {
        ItemData item = Item("item.scrap");
        item.Price = 9;
        var store = new FakeShopTransactionStore(item, money: 2);
        store.SeedItem(item.ItemID, 3);

        ShopSellResult result = ShopSellTransactionService.TrySell(store, item, 2);

        Assert.That(result.Status, Is.EqualTo(ShopSellStatus.Succeeded));
        Assert.That(result.UnitPrice, Is.EqualTo(4));
        Assert.That(result.TotalPrice, Is.EqualTo(8));
        Assert.That(store.GetItemCount(item.ItemID), Is.EqualTo(1));
        Assert.That(store.Money, Is.EqualTo(10));
    }

    [Test]
    public void SaleRejectsProtectedOrUnavailableItemsWithoutMutation()
    {
        ItemData item = Item("item.protected");
        var store = new FakeShopTransactionStore(item, money: 5);
        store.SeedItem(item.ItemID, 1);

        item.Type = ItemType.KeyItem;
        Assert.That(
            ShopSellTransactionService.TrySell(store, item).Status,
            Is.EqualTo(ShopSellStatus.KeyItemProtected));

        item.Type = ItemType.Consumable;
        item.IsSellable = false;
        Assert.That(
            ShopSellTransactionService.TrySell(store, item).Status,
            Is.EqualTo(ShopSellStatus.NotSellable));
        Assert.That(store.GetItemCount(item.ItemID), Is.EqualTo(1));
        Assert.That(store.Money, Is.EqualTo(5));
    }

    [Test]
    public void SaleMoneyFailureRestoresRemovedItem()
    {
        ItemData item = Item("item.rollback");
        var store = new FakeShopTransactionStore(item, money: 10)
        {
            FailMoneyRefund = true
        };
        store.SeedItem(item.ItemID, 2);

        ShopSellResult result = ShopSellTransactionService.TrySell(store, item);

        Assert.That(result.Status, Is.EqualTo(ShopSellStatus.MoneyCommitFailed));
        Assert.That(result.RollbackAttempted, Is.True);
        Assert.That(result.RollbackSucceeded, Is.True);
        Assert.That(store.GetItemCount(item.ItemID), Is.EqualTo(2));
        Assert.That(store.Money, Is.EqualTo(10));
    }

    [Test]
    public void SaleRollbackFailureIsReportedExplicitly()
    {
        ItemData item = Item("item.rollback_failure");
        var store = new FakeShopTransactionStore(item, money: 10)
        {
            FailMoneyRefund = true,
            FailItemAdd = true
        };
        store.SeedItem(item.ItemID, 1);

        ShopSellResult result = ShopSellTransactionService.TrySell(store, item);

        Assert.That(result.Status, Is.EqualTo(ShopSellStatus.RollbackFailed));
        Assert.That(result.FailureCause, Is.EqualTo(ShopSellStatus.MoneyCommitFailed));
        Assert.That(result.RollbackSucceeded, Is.False);
    }
    private ItemData Item(string id)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.ItemID = id;
        item.IsStackable = true;
        item.MaxStackSize = 99;
        _createdObjects.Add(item);
        return item;
    }

    private ShopDefinition Shop(params ShopEntry[] entries)
    {
        ShopDefinition shop = ScriptableObject.CreateInstance<ShopDefinition>();
        shop.Configure("shop.test", "Test Shop", entries);
        _createdObjects.Add(shop);
        return shop;
    }

    private static ShopEntry Entry(
        string entryId,
        ItemData item,
        int price,
        int quantity,
        int limit = 0)
    {
        return new ShopEntry(
            entryId,
            item,
            price,
            quantity,
            limit,
            $"shop.test.{entryId}.purchases");
    }

    private static void SetItemDatabase(ItemData item)
    {
        FieldInfo cacheField = typeof(ItemDatabase).GetField(
            "_cache",
            BindingFlags.Static | BindingFlags.NonPublic);
        cacheField.SetValue(
            null,
            new Dictionary<string, ItemData>(StringComparer.Ordinal)
            {
                [item.ItemID] = item
            });
    }

    private static void SetGlobalInstance(GlobalDataManager value)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        property.GetSetMethod(true).Invoke(null, new object[] { value });
    }

    private sealed class FakeShopTransactionStore : IShopTransactionStore
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _flags = new Dictionary<string, int>();
        private readonly ItemData _registeredItem;

        public FakeShopTransactionStore(ItemData registeredItem, int money)
        {
            _registeredItem = registeredItem;
            Money = money;
        }

        public int Money { get; private set; }
        public int MaxItemCount { get; set; } = 99;
        public bool FailItemAdd { get; set; }
        public bool FailItemRemove { get; set; }
        public bool FailMoneyRefund { get; set; }
        public bool FailFlagSet { get; set; }
        public bool ThrowOnFlagSet { get; set; }

        public bool IsItemRegistered(ItemData item)
        {
            return item != null && ReferenceEquals(item, _registeredItem);
        }

        public int GetItemCount(string itemId)
        {
            return _items.TryGetValue(itemId, out int count) ? count : 0;
        }

        public int GetAdditionalItemCapacity(ItemData item)
        {
            return IsItemRegistered(item)
                ? Math.Max(0, MaxItemCount - GetItemCount(item.ItemID))
                : 0;
        }

        public bool TrySpendMoneyExact(int amount)
        {
            if (amount < 0 || Money < amount)
                return false;
            Money -= amount;
            return true;
        }

        public bool TryRefundMoneyExact(int amount)
        {
            if (FailMoneyRefund || amount < 0 || (long)Money + amount > int.MaxValue)
                return false;
            Money += amount;
            return true;
        }

        public bool TryAddItemExact(string itemId, int amount)
        {
            if (FailItemAdd || amount <= 0 || GetItemCount(itemId) + amount > MaxItemCount)
                return false;
            _items[itemId] = GetItemCount(itemId) + amount;
            return true;
        }

        public bool TryRemoveItemExact(string itemId, int amount)
        {
            if (FailItemRemove || amount <= 0 || GetItemCount(itemId) < amount)
                return false;
            int remaining = GetItemCount(itemId) - amount;
            if (remaining == 0)
                _items.Remove(itemId);
            else
                _items[itemId] = remaining;
            return true;
        }

        public bool TryGetFlag(string key, out int value)
        {
            return _flags.TryGetValue(key, out value);
        }

        public bool TrySetFlag(string key, int value)
        {
            if (ThrowOnFlagSet)
                throw new InvalidOperationException("forced flag failure");
            if (FailFlagSet)
                return false;
            _flags[key] = value;
            return true;
        }

        public void SeedItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0)
                _items.Remove(itemId);
            else
                _items[itemId] = amount;
        }
        public bool HasFlag(string key)
        {
            return _flags.ContainsKey(key);
        }

        public int GetFlag(string key)
        {
            return _flags.TryGetValue(key, out int value) ? value : 0;
        }

        public void SetFlag(string key, int value)
        {
            _flags[key] = value;
        }
    }
}