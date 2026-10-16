using System;

public sealed class GlobalDataShopTransactionStore : IShopRecoveryStore
{
    private readonly GlobalDataManager _global;

    public GlobalDataShopTransactionStore(GlobalDataManager global)
    {
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public int Money => _global.Money;

    public PartyVitalsRestoreEvaluation EvaluatePartyVitalsRestore(bool restoreHp, bool restoreAp)
    {
        return _global.EvaluatePartyVitalsRestore(restoreHp, restoreAp);
    }

    public int RestorePartyVitals(bool restoreHp, bool restoreAp)
    {
        return _global.RestorePartyVitals(restoreHp, restoreAp);
    }

    public bool IsItemRegistered(ItemData item)
    {
        return InventoryEntryService.IsRegistered(item);
    }

    public int GetItemCount(string itemId)
    {
        return InventoryEntryService.GetRemovableCount(_global, ItemDatabase.FindById(itemId));
    }

    public int GetAdditionalItemCapacity(ItemData item)
    {
        return InventoryEntryService.GetAdditionalCapacity(_global, item);
    }

    public bool TrySpendMoneyExact(int amount)
    {
        return amount >= 0 && _global.SpendMoney(amount);
    }

    public bool TryRefundMoneyExact(int amount)
    {
        if (amount < 0)
            return false;
        if (amount == 0)
            return true;

        int before = _global.Money;
        long expected = (long)before + amount;
        if (expected > int.MaxValue)
            return false;

        _global.AddMoney(amount);
        return _global.Money == (int)expected;
    }

    public bool TryAddItemExact(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        ItemData item = ItemDatabase.FindById(itemId);
        int added = InventoryEntryService.Add(_global, item, amount);
        if (added == amount)
            return true;

        if (added > 0 && !InventoryEntryService.Remove(_global, item, added))
        {
            throw new InvalidOperationException(
                $"Partial item addition could not be reverted: item={itemId}, amount={added}");
        }

        return false;
    }

    public bool TryRemoveItemExact(string itemId, int amount)
    {
        return InventoryEntryService.Remove(_global, ItemDatabase.FindById(itemId), amount);
    }

    public bool TryGetFlag(string key, out int value)
    {
        return _global.TryGetFlag(key, out value);
    }

    public bool TrySetFlag(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        _global.SetFlag(key, value);
        return _global.GetFlag(key, 0) == value;
    }
}
