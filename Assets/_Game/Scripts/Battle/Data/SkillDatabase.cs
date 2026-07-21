using System;
using System.Collections.Generic;
using UnityEngine;

public static class SkillDatabase
{
    private static Dictionary<string, SkillData> _cache;

    public static SkillData FindById(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return null;
        EnsureCache();
        _cache.TryGetValue(skillId.Trim(), out SkillData data);
        return data;
    }

    public static IReadOnlyCollection<SkillData> GetAll()
    {
        EnsureCache();
        return _cache.Values;
    }

    public static void InvalidateCache() => _cache = null;

    private static void EnsureCache()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, SkillData>(StringComparer.Ordinal);
        GameContentCatalog catalog = GameContentCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError($"[SkillDatabase] Resources/{GameContentCatalog.ResourcesPath}.asset is missing.");
            return;
        }

        for (int i = 0; i < catalog.Skills.Count; i++)
        {
            SkillData data = catalog.Skills[i];
            if (data == null || string.IsNullOrWhiteSpace(data.SkillID)) continue;
            string id = data.SkillID.Trim();
            if (!_cache.ContainsKey(id)) _cache.Add(id, data);
        }
    }
}
