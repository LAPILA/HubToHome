using System;

public enum ShopSellStatus
{
    Succeeded,
    InvalidStore,
    InvalidItem,
    InvalidSellCount,
    InvalidSellState,
    ItemNotRegistered,
    NotSellable,
    KeyItemProtected,
    InsufficientQuantity,
    PriceOverflow,
    ItemCommitFailed,
    MoneyCommitFailed,
    StoreException,
    RollbackFailed
}

public readonly struct ShopSellResult
{
    public ShopSellResult(
        ShopSellStatus status,
        ShopSellStatus failureCause,
        string itemId,
        int sellCount,
        int unitPrice,
        int totalPrice,
        bool rollbackAttempted,
        bool rollbackSucceeded,
        string message)
    {
        Status = status;
        FailureCause = failureCause;
        ItemId = itemId ?? string.Empty;
        SellCount = sellCount;
        UnitPrice = unitPrice;
        TotalPrice = totalPrice;
        RollbackAttempted = rollbackAttempted;
        RollbackSucceeded = rollbackSucceeded;
        Message = message ?? string.Empty;
    }

    public ShopSellStatus Status { get; }
    public ShopSellStatus FailureCause { get; }
    public string ItemId { get; }
    public int SellCount { get; }
    public int UnitPrice { get; }
    public int TotalPrice { get; }
    public bool RollbackAttempted { get; }
    public bool RollbackSucceeded { get; }
    public string Message { get; }
    public bool Succeeded => Status == ShopSellStatus.Succeeded;
}

public static class ShopSellTransactionService
{
    public static ShopSellResult TrySell(
        IShopTransactionStore store,
        ItemData item,
        int sellCount = 1)
    {
        string itemId = item != null && !string.IsNullOrWhiteSpace(item.ItemID)
            ? item.ItemID.Trim()
            : string.Empty;
        if (store == null)
            return Failure(ShopSellStatus.InvalidStore, itemId, sellCount, "Store가 없습니다.");
        if (item == null || string.IsNullOrEmpty(itemId))
            return Failure(ShopSellStatus.InvalidItem, itemId, sellCount, "ItemData 또는 Item ID가 없습니다.");
        if (sellCount <= 0)
            return Failure(ShopSellStatus.InvalidSellCount, itemId, sellCount, "판매 수량은 1 이상이어야 합니다.");
        if (!store.IsItemRegistered(item))
            return Failure(ShopSellStatus.ItemNotRegistered, itemId, sellCount, "등록되지 않은 ItemData입니다.");
        if (item.Type == ItemType.KeyItem)
            return Failure(ShopSellStatus.KeyItemProtected, itemId, sellCount, "키 아이템은 판매할 수 없습니다.");
        if (!item.IsSellable || item.Price < 0)
            return Failure(ShopSellStatus.NotSellable, itemId, sellCount, "판매할 수 없는 아이템입니다.");
        if (store.GetItemCount(itemId) < sellCount)
            return Failure(ShopSellStatus.InsufficientQuantity, itemId, sellCount, "보유 수량이 부족합니다.");

        int unitPrice = item.Price <= 0 ? 0 : Math.Max(1, item.Price / 2);
        long totalLong = (long)unitPrice * sellCount;
        if (totalLong > int.MaxValue)
            return Failure(ShopSellStatus.PriceOverflow, itemId, sellCount, "판매 금액이 int 범위를 벗어납니다.", unitPrice);

        int totalPrice = (int)totalLong;
        bool itemRemoved = false;
        try
        {
            if (!store.TryRemoveItemExact(itemId, sellCount))
            {
                return Failure(
                    ShopSellStatus.ItemCommitFailed,
                    itemId,
                    sellCount,
                    "아이템 차감에 실패했습니다.",
                    unitPrice,
                    totalPrice);
            }

            itemRemoved = true;
            if (store.TryRefundMoneyExact(totalPrice))
            {
                return new ShopSellResult(
                    ShopSellStatus.Succeeded,
                    ShopSellStatus.Succeeded,
                    itemId,
                    sellCount,
                    unitPrice,
                    totalPrice,
                    false,
                    true,
                    string.Empty);
            }

            return Rollback(
                store,
                itemId,
                sellCount,
                unitPrice,
                totalPrice,
                ShopSellStatus.MoneyCommitFailed,
                "소지금 증가에 실패했습니다.");
        }
        catch (Exception exception)
        {
            if (itemRemoved)
            {
                return Rollback(
                    store,
                    itemId,
                    sellCount,
                    unitPrice,
                    totalPrice,
                    ShopSellStatus.StoreException,
                    exception.Message);
            }

            return Failure(
                ShopSellStatus.StoreException,
                itemId,
                sellCount,
                exception.Message,
                unitPrice,
                totalPrice);
        }
    }

    private static ShopSellResult Rollback(
        IShopTransactionStore store,
        string itemId,
        int sellCount,
        int unitPrice,
        int totalPrice,
        ShopSellStatus cause,
        string message)
    {
        bool rollbackSucceeded;
        try
        {
            rollbackSucceeded = store.TryAddItemExact(itemId, sellCount);
        }
        catch
        {
            rollbackSucceeded = false;
        }

        return new ShopSellResult(
            rollbackSucceeded ? cause : ShopSellStatus.RollbackFailed,
            cause,
            itemId,
            sellCount,
            unitPrice,
            totalPrice,
            true,
            rollbackSucceeded,
            message);
    }

    private static ShopSellResult Failure(
        ShopSellStatus status,
        string itemId,
        int sellCount,
        string message,
        int unitPrice = 0,
        int totalPrice = 0)
    {
        return new ShopSellResult(
            status,
            status,
            itemId,
            sellCount,
            unitPrice,
            totalPrice,
            false,
            true,
            message);
    }
}