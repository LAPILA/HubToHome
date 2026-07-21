using System;
using System.Collections.Generic;
using UnityEngine;

public static class EnemyDatabase
{
    private static Dictionary<string, EnemyData> _cache;

    public static EnemyData FindById(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId)) return null;
        EnsureCache();
        _cache.TryGetValue(enemyId.Trim(), out EnemyData data);
        return data;
    }

    public static IReadOnlyCollection<EnemyData> GetAll()
    {
        EnsureCache();
        return _cache.Values;
    }

    public static void InvalidateCache() => _cache = null;

    private static void EnsureCache()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, EnemyData>(StringComparer.Ordinal);
        GameContentCatalog catalog = GameContentCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError($"[EnemyDatabase] Resources/{GameContentCatalog.ResourcesPath}.asset is missing.");
            return;
        }

        for (int i = 0; i < catalog.Enemies.Count; i++)
        {
            EnemyData data = catalog.Enemies[i];
            if (data == null || string.IsNullOrWhiteSpace(data.EnemyId)) continue;
            string id = data.EnemyId.Trim();
            if (!_cache.ContainsKey(id)) _cache.Add(id, data);
        }
    }
}
