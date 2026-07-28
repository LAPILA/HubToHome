using System;
using System.Collections.Generic;
using UnityEngine;

public static class EquipmentDatabase
{
    private static Dictionary<string, EquipmentData> _cache;

    public static EquipmentData FindById(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId))
            return null;

        EnsureCache();
        _cache.TryGetValue(equipmentId.Trim(), out EquipmentData data);
        return data;
    }

    public static IReadOnlyCollection<EquipmentData> GetAll()
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
        if (_cache != null)
            return;

        _cache = new Dictionary<string, EquipmentData>(StringComparer.Ordinal);
        GameContentCatalog catalog = GameContentCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError($"[EquipmentDatabase] Resources/{GameContentCatalog.ResourcesPath}.asset is missing.");
            return;
        }

        if (catalog.Equipment == null)
            return;

        for (int i = 0; i < catalog.Equipment.Count; i++)
        {
            EquipmentData equipment = catalog.Equipment[i];
            if (equipment == null || string.IsNullOrWhiteSpace(equipment.ItemID))
                continue;

            string id = equipment.ItemID.Trim();
            if (!_cache.ContainsKey(id))
                _cache.Add(id, equipment);
        }
    }
}