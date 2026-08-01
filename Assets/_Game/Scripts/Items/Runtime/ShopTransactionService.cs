using System;

public enum ShopPurchaseStatus
{
    Succeeded,
    InvalidStore,
    InvalidShop,
    InvalidEntryId,
    EntryNotFound,
    DuplicateEntryId,
    InvalidEntry,
    InvalidPurchaseCount,
    ItemNotRegistered,
    QuantityOverflow,
    PriceOverflow,
    InsufficientFunds,
    InventoryCapacityExceeded,
    PurchaseLimitReached,
    InvalidPurchaseState,
    MoneyCommitFailed,
    ItemCommitFailed,
    FlagCommitFailed,
    StoreException,
    RollbackFailed
}

public readonly struct ShopPurchaseResult
{
    public ShopPurchaseResult(
        ShopPurchaseStatus status,
        ShopPurchaseStatus failureCause,
        string shopId,
        string entryId,
        int purchaseCount,
        int itemAmount,
        int totalPrice,
        int previousPurchaseCount,
        int currentPurchaseCount,
        bool rollbackAttempted,
        bool rollbackSucceeded,
        string message)
    {
        Status = status;
        FailureCause = failureCause;
        ShopId = shopId ?? string.Empty;
        EntryId = entryId ?? string.Empty;
        PurchaseCount = purchaseCount;
        ItemAmount = itemAmount;
        TotalPrice = totalPrice;
        PreviousPurchaseCount = previousPurchaseCount;
        CurrentPurchaseCount = currentPurchaseCount;
        RollbackAttempted = rollbackAttempted;
        RollbackSucceeded = rollbackSucceeded;
        Message = message ?? string.Empty;
    }

    public ShopPurchaseStatus Status { get; }
    public ShopPurchaseStatus FailureCause { get; }
    public string ShopId { get; }
    public string EntryId { get; }
    public int PurchaseCount { get; }
    public int ItemAmount { get; }
    public int TotalPrice { get; }
    public int PreviousPurchaseCount { get; }
    public int CurrentPurchaseCount { get; }
    public bool RollbackAttempted { get; }
    public bool RollbackSucceeded { get; }
    public string Message { get; }
    public bool Succeeded => Status == ShopPurchaseStatus.Succeeded;
}

public static class ShopTransactionService
{
    public static ShopPurchaseResult TryPurchase(
        IShopTransactionStore store,
        ShopDefinition shop,
        string entryId,
        int purchaseCount = 1)
    {
        string normalizedEntryId = Normalize(entryId);
        string shopId = shop != null ? shop.ShopId : string.Empty;
        if (store == null)
            return Failure(ShopPurchaseStatus.InvalidStore, shopId, normalizedEntryId, purchaseCount, "Store가 없습니다.");
        if (shop == null)
            return Failure(ShopPurchaseStatus.InvalidShop, shopId, normalizedEntryId, purchaseCount, "ShopDefinition이 없습니다.");
        if (string.IsNullOrEmpty(normalizedEntryId))
            return Failure(ShopPurchaseStatus.InvalidEntryId, shopId, normalizedEntryId, purchaseCount, "Entry ID가 비어 있습니다.");
        if (purchaseCount <= 0)
            return Failure(ShopPurchaseStatus.InvalidPurchaseCount, shopId, normalizedEntryId, purchaseCount, "구매 횟수는 1 이상이어야 합니다.");

        shop.TryFindUniqueEntry(normalizedEntryId, out ShopEntry entry, out int matchCount);
        if (matchCount == 0)
            return Failure(ShopPurchaseStatus.EntryNotFound, shopId, normalizedEntryId, purchaseCount, "Shop에 Entry가 없습니다.");
        if (matchCount > 1)
            return Failure(ShopPurchaseStatus.DuplicateEntryId, shopId, normalizedEntryId, purchaseCount, "Shop에 같은 Entry ID가 중복됩니다.");
        if (!shop.TryValidate(out string shopError))
            return Failure(ShopPurchaseStatus.InvalidShop, shopId, normalizedEntryId, purchaseCount, shopError);
        if (entry == null)
            return Failure(ShopPurchaseStatus.InvalidEntry, shopId, normalizedEntryId, purchaseCount, "Entry 데이터가 없습니다.");
        if (!entry.TryValidate(out string entryError))
            return Failure(ShopPurchaseStatus.InvalidEntry, shopId, normalizedEntryId, purchaseCount, entryError);

        try
        {
            if (!store.IsItemRegistered(entry.Item))
                return Failure(ShopPurchaseStatus.ItemNotRegistered, shopId, normalizedEntryId, purchaseCount, "등록되지 않은 ItemData입니다.");

            if (!TryMultiply(entry.Quantity, purchaseCount, out int itemAmount))
                return Failure(ShopPurchaseStatus.QuantityOverflow, shopId, normalizedEntryId, purchaseCount, "구매 수량이 int 범위를 벗어납니다.");
            if (!TryMultiply(entry.Price, itemAmount, out int totalPrice))
                return Failure(ShopPurchaseStatus.PriceOverflow, shopId, normalizedEntryId, purchaseCount, "총 가격이 int 범위를 벗어납니다.");

            int previousPurchaseCount = store.TryGetFlag(entry.PurchaseCounterFlag, out int storedCount)
                ? storedCount
                : 0;
            if (previousPurchaseCount < 0)
            {
                return Failure(
                    ShopPurchaseStatus.InvalidPurchaseState,
                    shopId,
                    normalizedEntryId,
                    purchaseCount,
                    "저장된 구매 횟수가 음수입니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }

            long nextPurchaseCountLong = (long)previousPurchaseCount + purchaseCount;
            if (nextPurchaseCountLong > int.MaxValue)
            {
                return Failure(
                    ShopPurchaseStatus.InvalidPurchaseState,
                    shopId,
                    normalizedEntryId,
                    purchaseCount,
                    "구매 카운터가 int 범위를 벗어납니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }

            int nextPurchaseCount = (int)nextPurchaseCountLong;
            if (entry.PurchaseLimit > 0 && nextPurchaseCount > entry.PurchaseLimit)
            {
                return Failure(
                    ShopPurchaseStatus.PurchaseLimitReached,
                    shopId,
                    normalizedEntryId,
                    purchaseCount,
                    "구매 제한을 초과합니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }

            if (store.Money < totalPrice)
            {
                return Failure(
                    ShopPurchaseStatus.InsufficientFunds,
                    shopId,
                    normalizedEntryId,
                    purchaseCount,
                    "소지금이 부족합니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }

            if (store.GetAdditionalItemCapacity(entry.Item) < itemAmount)
            {
                return Failure(
                    ShopPurchaseStatus.InventoryCapacityExceeded,
                    shopId,
                    normalizedEntryId,
                    purchaseCount,
                    "인벤토리 최대 보유 수량을 초과합니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }

            return Commit(
                store,
                shopId,
                normalizedEntryId,
                entry,
                purchaseCount,
                itemAmount,
                totalPrice,
                previousPurchaseCount,
                nextPurchaseCount);
        }
        catch (Exception exception)
        {
            return Failure(
                ShopPurchaseStatus.StoreException,
                shopId,
                normalizedEntryId,
                purchaseCount,
                exception.Message);
        }
    }

    private static ShopPurchaseResult Commit(
        IShopTransactionStore store,
        string shopId,
        string entryId,
        ShopEntry entry,
        int purchaseCount,
        int itemAmount,
        int totalPrice,
        int previousPurchaseCount,
        int nextPurchaseCount)
    {
        bool moneyCommitted = false;
        bool itemCommitted = false;
        ShopPurchaseStatus failureCause = ShopPurchaseStatus.StoreException;
        string failureMessage = string.Empty;

        try
        {
            if (!store.TrySpendMoneyExact(totalPrice))
            {
                return Failure(
                    ShopPurchaseStatus.MoneyCommitFailed,
                    shopId,
                    entryId,
                    purchaseCount,
                    "소지금 차감에 실패했습니다.",
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount);
            }
            moneyCommitted = true;

            if (!store.TryAddItemExact(entry.Item.ItemID, itemAmount))
            {
                failureCause = ShopPurchaseStatus.ItemCommitFailed;
                failureMessage = "아이템 추가에 실패했습니다.";
                return RollbackFailure(
                    store,
                    failureCause,
                    shopId,
                    entryId,
                    entry.Item.ItemID,
                    purchaseCount,
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount,
                    moneyCommitted,
                    itemCommitted,
                    failureMessage);
            }
            itemCommitted = true;

            if (!store.TrySetFlag(entry.PurchaseCounterFlag, nextPurchaseCount))
            {
                failureCause = ShopPurchaseStatus.FlagCommitFailed;
                failureMessage = "구매 카운터 기록에 실패했습니다.";
                return RollbackFailure(
                    store,
                    failureCause,
                    shopId,
                    entryId,
                    entry.Item.ItemID,
                    purchaseCount,
                    itemAmount,
                    totalPrice,
                    previousPurchaseCount,
                    moneyCommitted,
                    itemCommitted,
                    failureMessage);
            }

            return new ShopPurchaseResult(
                ShopPurchaseStatus.Succeeded,
                ShopPurchaseStatus.Succeeded,
                shopId,
                entryId,
                purchaseCount,
                itemAmount,
                totalPrice,
                previousPurchaseCount,
                nextPurchaseCount,
                false,
                true,
                string.Empty);
        }
        catch (Exception exception)
        {
            failureCause = failureCause == ShopPurchaseStatus.StoreException
                ? ShopPurchaseStatus.StoreException
                : failureCause;
            failureMessage = exception.Message;
            return RollbackFailure(
                store,
                failureCause,
                shopId,
                entryId,
                entry.Item.ItemID,
                purchaseCount,
                itemAmount,
                totalPrice,
                previousPurchaseCount,
                moneyCommitted,
                itemCommitted,
                failureMessage);
        }
    }

    private static ShopPurchaseResult RollbackFailure(
        IShopTransactionStore store,
        ShopPurchaseStatus failureCause,
        string shopId,
        string entryId,
        string itemId,
        int purchaseCount,
        int itemAmount,
        int totalPrice,
        int previousPurchaseCount,
        bool moneyCommitted,
        bool itemCommitted,
        string message)
    {
        bool attempted = moneyCommitted || itemCommitted;
        bool rollbackSucceeded = true;
        try
        {
            if (itemCommitted && !store.TryRemoveItemExact(itemId, itemAmount))
                rollbackSucceeded = false;
        }
        catch
        {
            rollbackSucceeded = false;
        }

        try
        {
            if (moneyCommitted && !store.TryRefundMoneyExact(totalPrice))
                rollbackSucceeded = false;
        }
        catch
        {
            rollbackSucceeded = false;
        }

        ShopPurchaseStatus status = rollbackSucceeded
            ? failureCause
            : ShopPurchaseStatus.RollbackFailed;
        return new ShopPurchaseResult(
            status,
            failureCause,
            shopId,
            entryId,
            purchaseCount,
            itemAmount,
            totalPrice,
            previousPurchaseCount,
            previousPurchaseCount,
            attempted,
            rollbackSucceeded,
            message);
    }

    private static ShopPurchaseResult Failure(
        ShopPurchaseStatus status,
        string shopId,
        string entryId,
        int purchaseCount,
        string message,
        int itemAmount = 0,
        int totalPrice = 0,
        int previousPurchaseCount = 0)
    {
        return new ShopPurchaseResult(
            status,
            status,
            shopId,
            entryId,
            purchaseCount,
            itemAmount,
            totalPrice,
            previousPurchaseCount,
            previousPurchaseCount,
            false,
            true,
            message);
    }

    private static bool TryMultiply(int left, int right, out int result)
    {
        long value = (long)left * right;
        if (value < 0 || value > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}