using System.Collections.Generic;
using UnityEngine;

public static class CharacterDatabase
{
    private static Dictionary<string, CharacterData> _cache;

    public static CharacterData FindById(string characterDataId)
    {
        if (string.IsNullOrWhiteSpace(characterDataId)) return null;
        EnsureCache();
        _cache.TryGetValue(characterDataId, out var data);
        return data;
    }

    private static void EnsureCache()
    {
        if (_cache != null) return;

        _cache = new Dictionary<string, CharacterData>();
        CharacterData[] all = Resources.LoadAll<CharacterData>(string.Empty);
        foreach (var data in all)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.CharacterID)) continue;
            if (!_cache.ContainsKey(data.CharacterID))
                _cache.Add(data.CharacterID, data);
        }
    }
}