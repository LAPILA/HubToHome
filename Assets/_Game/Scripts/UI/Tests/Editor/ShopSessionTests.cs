using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShopSessionTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] != null)
                UnityEngine.Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
    }

    [Test]
    public void SelectionWrapsAndStopsAfterClose()
    {
        ItemData item = Item("item.session");
        ShopDefinition shop = Shop(
            Entry("a", item, 1),
            Entry("b", item, 1),
            Entry("c", item, 1));
        var session = new ShopSession(shop, new FakeStore(item, 100));
        int changed = 0;
        session.Changed += () => changed++;

        Assert.That(session.MoveSelection(-1), Is.True);
        Assert.That(session.SelectedEntry.EntryId, Is.EqualTo("c"));
        Assert.That(session.MoveSelection(1), Is.True);
        Assert.That(session.SelectedEntry.EntryId, Is.EqualTo("a"));
        Assert.That(changed, Is.EqualTo(2));

        Assert.That(session.TryClose(ShopSessionEndReason.Canceled, out _), Is.True);
        Assert.That(session.MoveSelection(1), Is.False);
        Assert.That(session.SelectedEntry.EntryId, Is.EqualTo("a"));
    }

    [Test]
    public void SuccessfulPurchaseIsPreservedInSessionResult()
    {
        ItemData item = Item("item.session");
        ShopDefinition shop = Shop(Entry("patch", item, 10, quantity: 2));
        var store = new FakeStore(item, 30);
        var session = new ShopSession(shop, store);

        ShopPurchaseResult purchase = session.PurchaseSelected();
        Assert.That(purchase.Succeeded, Is.True);
        Assert.That(store.Money, Is.EqualTo(10));
        Assert.That(session.SuccessfulPurchaseCount, Is.EqualTo(1));

        Assert.That(
            session.TryClose(ShopSessionEndReason.Canceled, out ShopSessionResult result),
            Is.True);
        Assert.That(result.Reason, Is.EqualTo(ShopSessionEndReason.Canceled));
        Assert.That(result.HasSuccessfulPurchase, Is.True);
        Assert.That(result.LastPurchase.HasValue, Is.True);
        Assert.That(result.LastPurchase.Value.Status, Is.EqualTo(ShopPurchaseStatus.Succeeded));
    }

    [Test]
    public void FailedPurchaseDoesNotCompleteSessionAndCloseIsIdempotent()
    {
        ItemData item = Item("item.session");
        ShopDefinition shop = Shop(Entry("patch", item, 10));
        var session = new ShopSession(shop, new FakeStore(item, 0));
        int closeCount = 0;
        session.Closed += _ => closeCount++;

        Assert.That(
            session.PurchaseSelected().Status,
            Is.EqualTo(ShopPurchaseStatus.InsufficientFunds));
        Assert.That(session.SuccessfulPurchaseCount, Is.Zero);
        Assert.That(
            session.TryClose(ShopSessionEndReason.Canceled, out ShopSessionResult result),
            Is.True);
        Assert.That(result.HasSuccessfulPurchase, Is.False);
        Assert.That(
            session.TryClose(ShopSessionEndReason.ForcedClosed, out _),
            Is.False);
        Assert.That(closeCount, Is.EqualTo(1));
    }

    [Test]
    public void SuccessfulSaleIsPreservedInSessionResult()
    {
        ItemData item = Item("item.sell_session");
        item.Price = 20;
        ShopDefinition shop = Shop(Entry("patch", item, 10));
        var store = new FakeStore(item, 5);
        store.SeedItem(item.ItemID, 2);
        var session = new ShopSession(shop, store);

        ShopSellResult sale = session.Sell(item);

        Assert.That(sale.Succeeded, Is.True);
        Assert.That(store.Money, Is.EqualTo(15));
        Assert.That(store.GetItemCount(item.ItemID), Is.EqualTo(1));
        Assert.That(session.SuccessfulSaleCount, Is.EqualTo(1));
        Assert.That(session.TryClose(ShopSessionEndReason.Canceled, out ShopSessionResult result), Is.True);
        Assert.That(result.HasSuccessfulTransaction, Is.True);
        Assert.That(result.LastSale.HasValue, Is.True);
        Assert.That(result.LastSale.Value.Status, Is.EqualTo(ShopSellStatus.Succeeded));
    }

    [Test]
    public void ClosedSessionRejectsFurtherSales()
    {
        ItemData item = Item("item.closed_sale");
        ShopDefinition shop = Shop(Entry("patch", item, 10));
        var session = new ShopSession(shop, new FakeStore(item, 0));
        session.TryClose(ShopSessionEndReason.Canceled, out _);

        Assert.That(session.Sell(item).Status, Is.EqualTo(ShopSellStatus.InvalidSellState));
    }

    [TestCase(PartyVitalsRestoreStatus.AlreadyFull, ShopServiceStatus.AlreadyFull)]
    [TestCase(PartyVitalsRestoreStatus.Blocked, ShopServiceStatus.Blocked)]
    [TestCase(PartyVitalsRestoreStatus.PartyMissing, ShopServiceStatus.PartyMissing)]
    public void UnavailableRecoveryDoesNotCharge(PartyVitalsRestoreStatus state, ShopServiceStatus expected)
    {
        var store = new FakeStore(null, 20) { RecoveryStatus = state };
        var service = new ShopServiceEntry("heal", "회복", "", 10, true, true);

        Assert.That(ShopServiceTransactionService.TryUse(store, service).Status, Is.EqualTo(expected));
        Assert.That(store.Money, Is.EqualTo(20));
        Assert.That(store.RecoveryCalls, Is.Zero);
    }

    [TestCase(false, ShopServiceStatus.RecoveryFailed, 20)]
    [TestCase(true, ShopServiceStatus.RefundFailed, 10)]
    public void UnappliedRecoveryRefundsOrReportsRefundFailure(bool refundFails, ShopServiceStatus expected, int money)
    {
        var store = new FakeStore(null, 20) { RecoveryCount = 0, RejectRefund = refundFails };
        var service = new ShopServiceEntry("heal", "회복", "", 10, true, true);

        Assert.That(ShopServiceTransactionService.TryUse(store, service).Status, Is.EqualTo(expected));
        Assert.That(store.Money, Is.EqualTo(money));
    }

    [Test]
    public void RecoveryPublishesOneRefreshAndIsRecordedInSessionResult()
    {
        ShopDefinition shop = Shop();
        var serialized = new SerializedObject(shop);
        SerializedProperty services = serialized.FindProperty("_services");
        services.arraySize = 1;
        SerializedProperty entry = services.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("_serviceId").stringValue = "heal";
        entry.FindPropertyRelative("_price").intValue = 10;
        entry.FindPropertyRelative("_restoreHp").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var store = new FakeStore(null, 20);
        var session = new ShopSession(shop, store);
        int changed = 0;
        session.Changed += () => changed++;

        Assert.That(session.UseService(0).Succeeded, Is.True);
        Assert.That(changed, Is.EqualTo(1));
        Assert.That(store.Money, Is.EqualTo(10));
        session.TryClose(ShopSessionEndReason.Completed, out ShopSessionResult result);
        Assert.That(result.SuccessfulServiceCount, Is.EqualTo(1));
        Assert.That(result.HasSuccessfulTransaction, Is.True);
        Assert.That(result.HasSuccessfulPurchase, Is.False);
        Assert.That(session.UseService(0).Status, Is.EqualTo(ShopServiceStatus.SessionClosed));
    }
    [Test]
    public void VendorLauncherAcceptsOneOwnerAndConsumesCloseCallbackOnce()
    {
        ItemData item = Item("item.session");
        ShopDefinition shop = Shop(Entry("patch", item, 10));
        var launcher = new FakeLauncher();
        var competingLauncher = new FakeLauncher();
        int callbackCount = 0;
        try
        {
            Assert.That(
                AreaMarkerRuntimeService.RegisterShopSessionLauncher(launcher),
                Is.True);
            Assert.That(
                AreaMarkerRuntimeService.RegisterShopSessionLauncher(competingLauncher),
                Is.False);
            Assert.That(
                AreaMarkerRuntimeService.RequestVendor(
                    null,
                    "vendor.test",
                    shop.ShopId,
                    shop,
                    _ => callbackCount++),
                Is.True);

            var result = new ShopSessionResult(
                ShopSessionEndReason.Canceled,
                1,
                null);
            launcher.Close(result);
            launcher.Close(result);

            Assert.That(callbackCount, Is.EqualTo(1));
        }
        finally
        {
            AreaMarkerRuntimeService.UnregisterShopSessionLauncher(launcher);
        }
    }

    [Test]
    public void OneShotVendorCompletesOnlyAfterSuccessfulPurchase()
    {
        ItemData item = Item("item.session");
        ShopDefinition shop = Shop(Entry("patch", item, 10));
        var launcher = new FakeLauncher();
        GameObject vendorObject = new GameObject("ShopSessionTests_Vendor");
        _created.Add(vendorObject);
        vendorObject.AddComponent<CircleCollider2D>().isTrigger = true;
        VendorMarker vendor = vendorObject.AddComponent<VendorMarker>();
        var serialized = new SerializedObject(vendor);
        serialized.FindProperty("markerId").stringValue = "vendor.test";
        serialized.FindProperty("areaId").stringValue = "area.test";
        serialized.FindProperty("isOneShot").boolValue = true;
        serialized.FindProperty("vendorId").stringValue = "vendor.test";
        serialized.FindProperty("shopId").stringValue = shop.ShopId;
        serialized.FindProperty("shopDefinition").objectReferenceValue = shop;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            Assert.That(
                AreaMarkerRuntimeService.RegisterShopSessionLauncher(launcher),
                Is.True);

            vendor.Interact((PlayerController)null);
            Assert.That(vendor.CanInteract(null), Is.False);
            launcher.Close(new ShopSessionResult(
                ShopSessionEndReason.Canceled,
                0,
                null));
            Assert.That(vendorObject.activeSelf, Is.True);
            Assert.That(vendor.CanInteract(null), Is.True);

            vendor.Interact((PlayerController)null);
            launcher.Close(new ShopSessionResult(
                ShopSessionEndReason.Canceled,
                1,
                null));
            Assert.That(vendorObject.activeSelf, Is.False);
        }
        finally
        {
            AreaMarkerRuntimeService.UnregisterShopSessionLauncher(launcher);
        }
    }
    private ItemData Item(string id)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.ItemID = id;
        item.IsStackable = true;
        item.MaxStackSize = 99;
        _created.Add(item);
        return item;
    }

    private ShopDefinition Shop(params ShopEntry[] entries)
    {
        ShopDefinition shop = ScriptableObject.CreateInstance<ShopDefinition>();
        shop.Configure("shop.session", "Session Shop", entries);
        _created.Add(shop);
        return shop;
    }

    private static ShopEntry Entry(
        string entryId,
        ItemData item,
        int price,
        int quantity = 1)
    {
        return new ShopEntry(
            entryId,
            item,
            price,
            quantity,
            0,
            "shop.session." + entryId + ".purchases");
    }

    private sealed class FakeLauncher : IShopSessionLauncher
    {
        private Action<ShopSessionResult> _onClosed;

        public bool TryOpen(
            ShopDefinition shop,
            string vendorId,
            Action<ShopSessionResult> onClosed)
        {
            _onClosed = onClosed;
            return true;
        }

        public void Close(ShopSessionResult result)
        {
            _onClosed?.Invoke(result);
        }
    }

    private sealed class FakeStore : IShopRecoveryStore
    {
        private readonly ItemData _item;
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _flags = new Dictionary<string, int>();

        public FakeStore(ItemData item, int money)
        {
            _item = item;
            Money = money;
        }

        public int Money { get; private set; }
        public PartyVitalsRestoreStatus RecoveryStatus = PartyVitalsRestoreStatus.Ready;
        public int RecoveryCount = 1;
        public int RecoveryCalls;
        public bool RejectRefund;

        public PartyVitalsRestoreEvaluation EvaluatePartyVitalsRestore(bool hp, bool ap) =>
            new PartyVitalsRestoreEvaluation(RecoveryStatus, RecoveryCount);

        public int RestorePartyVitals(bool hp, bool ap)
        {
            RecoveryCalls++;
            return RecoveryCount;
        }

        public void SeedItem(string itemId, int amount)
        {
            _items[itemId] = amount;
        }
        public bool IsItemRegistered(ItemData item) => ReferenceEquals(item, _item);
        public int GetItemCount(string itemId) =>
            _items.TryGetValue(itemId, out int count) ? count : 0;
        public int GetAdditionalItemCapacity(ItemData item) =>
            IsItemRegistered(item) ? 99 - GetItemCount(item.ItemID) : 0;

        public bool TrySpendMoneyExact(int amount)
        {
            if (amount < 0 || amount > Money)
                return false;
            Money -= amount;
            return true;
        }

        public bool TryRefundMoneyExact(int amount)
        {
            if (amount < 0 || RejectRefund)
                return false;
            Money += amount;
            return true;
        }

        public bool TryAddItemExact(string itemId, int amount)
        {
            if (amount <= 0)
                return false;
            _items[itemId] = GetItemCount(itemId) + amount;
            return true;
        }

        public bool TryRemoveItemExact(string itemId, int amount)
        {
            if (amount <= 0 || GetItemCount(itemId) < amount)
                return false;
            _items[itemId] = GetItemCount(itemId) - amount;
            return true;
        }

        public bool TryGetFlag(string key, out int value) =>
            _flags.TryGetValue(key, out value);

        public bool TrySetFlag(string key, int value)
        {
            _flags[key] = value;
            return true;
        }
    }
}
