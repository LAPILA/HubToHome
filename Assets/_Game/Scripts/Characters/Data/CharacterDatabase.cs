using System;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterDatabase
{
    private static Dictionary<string, CharacterData> _cache;

    public static CharacterData FindById(string characterDataId)
    {
        if (string.IsNullOrWhiteSpace(characterDataId)) return null;
        EnsureCache();
        _cache.TryGetValue(characterDataId.Trim(), out var data);
        return data;
    }

    public static IReadOnlyCollection<CharacterData> GetAll()
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

        _cache = new Dictionary<string, CharacterData>(StringComparer.Ordinal);
        GameContentCatalog catalog = GameContentCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError($"[CharacterDatabase] Resources/{GameContentCatalog.ResourcesPath}.asset is missing.");
            return;
        }

        for (int i = 0; i < catalog.Characters.Count; i++)
        {
            CharacterData data = catalog.Characters[i];
            if (data == null || string.IsNullOrWhiteSpace(data.CharacterID)) continue;
            string id = data.CharacterID.Trim();
            if (!_cache.ContainsKey(id))
                _cache.Add(id, data);
        }
    }
}