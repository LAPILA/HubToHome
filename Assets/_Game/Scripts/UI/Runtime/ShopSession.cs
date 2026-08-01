using System;

public enum ShopSessionEndReason
{
    Completed,
    Canceled,
    OpenFailed,
    ForcedClosed
}

public readonly struct ShopSessionResult
{
    public ShopSessionResult(
        ShopSessionEndReason reason,
        int successfulPurchaseCount,
        ShopPurchaseResult? lastPurchase)
        : this(reason, successfulPurchaseCount, lastPurchase, 0, null)
    {
    }

    public ShopSessionResult(
        ShopSessionEndReason reason,
        int successfulPurchaseCount,
        ShopPurchaseResult? lastPurchase,
        int successfulSaleCount,
        ShopSellResult? lastSale)
    {
        Reason = reason;
        SuccessfulPurchaseCount = Math.Max(0, successfulPurchaseCount);
        LastPurchase = lastPurchase;
        SuccessfulSaleCount = Math.Max(0, successfulSaleCount);
        LastSale = lastSale;
    }

    public ShopSessionEndReason Reason { get; }
    public int SuccessfulPurchaseCount { get; }
    public ShopPurchaseResult? LastPurchase { get; }
    public int SuccessfulSaleCount { get; }
    public ShopSellResult? LastSale { get; }
    public bool HasSuccessfulPurchase => SuccessfulPurchaseCount > 0;
    public bool HasSuccessfulTransaction => HasSuccessfulPurchase || SuccessfulSaleCount > 0;
}

/// <summary>
/// Shop UI와 분리된 선택, 구매, 종료 수명주기입니다.
/// </summary>
public sealed class ShopSession
{
    private readonly ShopDefinition _shop;
    private readonly IShopTransactionStore _store;
    private int _selectedIndex;
    private int _successfulPurchaseCount;
    private ShopPurchaseResult? _lastPurchase;
    private int _successfulSaleCount;
    private ShopSellResult? _lastSale;
    private bool _isClosed;

    public ShopSession(ShopDefinition shop, IShopTransactionStore store)
    {
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (!shop.TryValidate(out string error))
            throw new ArgumentException(error, nameof(shop));
    }

    public event Action Changed;
    public event Action<ShopSessionResult> Closed;

    public ShopDefinition Shop => _shop;
    public IShopTransactionStore Store => _store;
    public int SelectedIndex => _selectedIndex;
    public ShopEntry SelectedEntry =>
        _selectedIndex >= 0 && _selectedIndex < _shop.Entries.Count
            ? _shop.Entries[_selectedIndex]
            : null;
    public int SuccessfulPurchaseCount => _successfulPurchaseCount;
    public ShopPurchaseResult? LastPurchase => _lastPurchase;
    public int SuccessfulSaleCount => _successfulSaleCount;
    public ShopSellResult? LastSale => _lastSale;
    public bool IsClosed => _isClosed;

    public bool MoveSelection(int delta)
    {
        if (_isClosed || delta == 0 || _shop.Entries.Count <= 1)
            return false;

        int count = _shop.Entries.Count;
        int next = ((_selectedIndex + delta) % count + count) % count;
        if (next == _selectedIndex)
            return false;

        _selectedIndex = next;
        Changed?.Invoke();
        return true;
    }

    public ShopPurchaseResult PurchaseSelected(int purchaseCount = 1)
    {
        if (_isClosed || SelectedEntry == null)
        {
            return new ShopPurchaseResult(
                ShopPurchaseStatus.InvalidPurchaseState,
                ShopPurchaseStatus.InvalidPurchaseState,
                _shop.ShopId,
                SelectedEntry != null ? SelectedEntry.EntryId : string.Empty,
                purchaseCount,
                0,
                0,
                0,
                0,
                false,
                true,
                "종료되었거나 선택 항목이 없는 Shop Session입니다.");
        }

        ShopPurchaseResult result = ShopTransactionService.TryPurchase(
            _store,
            _shop,
            SelectedEntry.EntryId,
            purchaseCount);
        _lastPurchase = result;
        if (result.Succeeded)
            _successfulPurchaseCount++;
        Changed?.Invoke();
        return result;
    }

    public ShopSellResult Sell(ItemData item, int sellCount = 1)
    {
        if (_isClosed)
        {
            return new ShopSellResult(
                ShopSellStatus.InvalidSellState,
                ShopSellStatus.InvalidSellState,
                item != null ? item.ItemID : string.Empty,
                sellCount,
                0,
                0,
                false,
                true,
                "종료된 Shop Session입니다.");
        }

        ShopSellResult result = ShopSellTransactionService.TrySell(
            _store,
            item,
            sellCount);
        _lastSale = result;
        if (result.Succeeded)
            _successfulSaleCount++;
        Changed?.Invoke();
        return result;
    }
    public bool TryClose(ShopSessionEndReason reason, out ShopSessionResult result)
    {
        if (_isClosed)
        {
            result = default;
            return false;
        }

        _isClosed = true;
        result = new ShopSessionResult(
            reason,
            _successfulPurchaseCount,
            _lastPurchase,
            _successfulSaleCount,
            _lastSale);
        Closed?.Invoke(result);
        return true;
    }
}

public interface IShopSessionLauncher
{
    bool TryOpen(
        ShopDefinition shop,
        string vendorId,
        Action<ShopSessionResult> onClosed);
}
