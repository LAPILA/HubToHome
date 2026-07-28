using System;

public sealed class GlobalDataShopTransactionStore : IShopTransactionStore
{
    private readonly GlobalDataManager _global;

    public GlobalDataShopTransactionStore(GlobalDataManager global)
    {
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public int Money => _global.Money;

    public bool IsItemRegistered(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ItemID))
            return false;

        ItemData registered = ItemDatabase.FindById(item.ItemID);
        return registered != null && ReferenceEquals(registered, item);
    }

    public int GetItemCount(string itemId)
    {
        return _global.GetItemCount(itemId);
    }

    public int GetAdditionalItemCapacity(ItemData item)
    {
        if (!IsItemRegistered(item))
            return 0;

        int maxCount = item.IsStackable ? Math.Max(1, item.MaxStackSize) : 1;
        return Math.Max(0, maxCount - _global.GetItemCount(item.ItemID));
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

        int added = _global.AddItemAndGetAddedAmount(itemId, amount);
        if (added == amount)
            return true;

        if (added > 0 && !_global.RemoveItem(itemId, added))
        {
            throw new InvalidOperationException(
                $"Partial item addition could not be reverted: item={itemId}, amount={added}");
        }

        return false;
    }

    public bool TryRemoveItemExact(string itemId, int amount)
    {
        return amount > 0 && _global.RemoveItem(itemId, amount);
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