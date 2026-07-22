#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectContentSnapshot
{
    private readonly Dictionary<UnityEngine.Object, string> _assetPaths =
        new Dictionary<UnityEngine.Object, string>();

    public List<CharacterData> Characters { get; } = new List<CharacterData>();
    public List<EnemyData> Enemies { get; } = new List<EnemyData>();
    public List<SkillData> Skills { get; } = new List<SkillData>();
    public List<ItemData> Items { get; } = new List<ItemData>();
    public List<BattleScenarioData> Scenarios { get; } = new List<BattleScenarioData>();
    public List<ActionCatalogAsset> ActionCatalogs { get; } = new List<ActionCatalogAsset>();
    public GameContentCatalog Catalog { get; set; }
    public string CatalogAssetPath { get; set; } = string.Empty;

    public void SetAssetPath(UnityEngine.Object asset, string path)
    {
        if (asset == null)
            return;

        _assetPaths[asset] = path?.Trim() ?? string.Empty;
    }

    public string GetAssetPath(UnityEngine.Object asset)
    {
        return asset != null && _assetPaths.TryGetValue(asset, out string path)
            ? path
            : string.Empty;
    }
}
#endif
