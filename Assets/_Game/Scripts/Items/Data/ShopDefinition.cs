using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class ShopEntry
{
    [HorizontalGroup("Identity", Width = 0.42f)]
    [SerializeField, LabelText("Entry ID")]
    private string _entryId;

    [HorizontalGroup("Identity", Width = 0.58f)]
    [SerializeField, Required, LabelText("Item")]
    private ItemData _item;

    [HorizontalGroup("Purchase", Width = 0.34f)]
    [SerializeField, Min(0), LabelText("단가")]
    private int _price;

    [HorizontalGroup("Purchase", Width = 0.33f)]
    [SerializeField, Min(1), LabelText("1회 수량")]
    private int _quantity = 1;

    [HorizontalGroup("Purchase", Width = 0.33f)]
    [SerializeField, Min(0), LabelText("구매 제한")]
    [Tooltip("0은 무제한입니다. 제한은 아이템 개수가 아니라 구매 횟수 기준입니다.")]
    private int _purchaseLimit;

    [SerializeField, LabelText("구매 카운터 Flag")]
    private string _purchaseCounterFlag;

    public string EntryId => Normalize(_entryId);
    public ItemData Item => _item;
    public int Price => _price;
    public int Quantity => _quantity;
    public int PurchaseLimit => _purchaseLimit;
    public string PurchaseCounterFlag => Normalize(_purchaseCounterFlag);

    public ShopEntry()
    {
    }

    public ShopEntry(
        string entryId,
        ItemData item,
        int price,
        int quantity,
        int purchaseLimit,
        string purchaseCounterFlag)
    {
        _entryId = Normalize(entryId);
        _item = item;
        _price = price;
        _quantity = quantity;
        _purchaseLimit = purchaseLimit;
        _purchaseCounterFlag = Normalize(purchaseCounterFlag);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(EntryId))
        {
            error = "Entry ID가 비어 있습니다.";
            return false;
        }

        if (_item == null)
        {
            error = $"{EntryId}: ItemData가 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_item.ItemID))
        {
            error = $"{EntryId}: ItemData의 Item ID가 비어 있습니다.";
            return false;
        }

        if (_price < 0)
        {
            error = $"{EntryId}: 단가는 0 이상이어야 합니다.";
            return false;
        }

        if (_quantity <= 0)
        {
            error = $"{EntryId}: 1회 구매 수량은 1 이상이어야 합니다.";
            return false;
        }

        if (_purchaseLimit < 0)
        {
            error = $"{EntryId}: 구매 제한은 0 이상이어야 합니다.";
            return false;
        }

        if (string.IsNullOrEmpty(PurchaseCounterFlag))
        {
            error = $"{EntryId}: 구매 카운터 Flag가 비어 있습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[CreateAssetMenu(fileName = "ShopDefinition", menuName = "Hub To Home/Items/Shop Definition")]
public sealed class ShopDefinition : ScriptableObject
{
    [TitleGroup("기본 정보")]
    [SerializeField, Required, LabelText("Shop ID")]
    private string _shopId;

    [TitleGroup("기본 정보")]
    [SerializeField, LabelText("표시 이름")]
    private string _displayName;

    [TitleGroup("판매 목록")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("Entries")]
    private List<ShopEntry> _entries = new List<ShopEntry>();

    public string ShopId => Normalize(_shopId);
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ShopId : _displayName.Trim();
    public IReadOnlyList<ShopEntry> Entries => _entries;

    public void Configure(string shopId, string displayName, IEnumerable<ShopEntry> entries)
    {
        _shopId = Normalize(shopId);
        _displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        _entries = entries != null ? new List<ShopEntry>(entries) : new List<ShopEntry>();
    }

    public bool TryFindUniqueEntry(
        string entryId,
        out ShopEntry entry,
        out int matchCount)
    {
        entry = null;
        matchCount = 0;
        string normalizedId = Normalize(entryId);
        if (string.IsNullOrEmpty(normalizedId) || _entries == null)
            return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            ShopEntry candidate = _entries[i];
            if (candidate == null
                || !string.Equals(candidate.EntryId, normalizedId, StringComparison.Ordinal))
            {
                continue;
            }

            matchCount++;
            if (entry == null)
                entry = candidate;
        }

        return matchCount == 1;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(ShopId))
        {
            error = "Shop ID가 비어 있습니다.";
            return false;
        }

        if (_entries == null || _entries.Count == 0)
        {
            error = $"{ShopId}: 판매 Entry가 하나 이상 필요합니다.";
            return false;
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _entries.Count; i++)
        {
            ShopEntry entry = _entries[i];
            if (entry == null)
            {
                error = $"Entry #{i + 1}이 비어 있습니다.";
                return false;
            }

            if (!entry.TryValidate(out string entryError))
            {
                error = $"Entry #{i + 1}: {entryError}";
                return false;
            }

            if (!entryIds.Add(entry.EntryId))
            {
                error = $"Entry ID가 중복됩니다: {entry.EntryId}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    [TitleGroup("검증")]
    [Button("Shop 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log($"[ShopDefinition] 검증 통과: {ShopId}", this);
        else
            Debug.LogError("[ShopDefinition] " + error, this);
    }

    private void OnValidate()
    {
        // Validation intentionally reports bad authoring data without rewriting it.
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}