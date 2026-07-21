using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _cache;

    public static ItemData FindById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        EnsureCache();
        _cache.TryGetValue(itemId.Trim(), out ItemData data);
        return data;
    }

    public static IReadOnlyCollection<ItemData> GetAll()
    {
        EnsureCache();
        return _cache.Values;
    }

    public static void InvalidateCache()
    {
        _cache = null;
    }

    private static void EnsureCache()
    {
        if (_cache != null) return;

        _cache = new Dictionary<string, ItemData>(StringComparer.Ordinal);
        GameContentCatalog catalog = GameContentCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError($"[ItemDatabase] Resources/{GameContentCatalog.ResourcesPath}.asset is missing.");
            return;
        }

        for (int i = 0; i < catalog.Items.Count; i++)
        {
            ItemData item = catalog.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemID)) continue;
            string id = item.ItemID.Trim();
            if (!_cache.ContainsKey(id))
                _cache.Add(id, item);
        }
    }
}
