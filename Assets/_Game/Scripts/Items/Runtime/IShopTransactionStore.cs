public interface IShopTransactionStore
{
    int Money { get; }

    bool IsItemRegistered(ItemData item);
    int GetItemCount(string itemId);
    int GetAdditionalItemCapacity(ItemData item);

    bool TrySpendMoneyExact(int amount);
    bool TryRefundMoneyExact(int amount);
    bool TryAddItemExact(string itemId, int amount);
    bool TryRemoveItemExact(string itemId, int amount);

    bool TryGetFlag(string key, out int value);

    /// <summary>
    /// Applies the value atomically. False or an exception must leave the flag unchanged.
    /// </summary>
    bool TrySetFlag(string key, int value);
}