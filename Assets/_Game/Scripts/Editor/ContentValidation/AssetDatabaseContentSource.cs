#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AssetDatabaseContentSource
{
    public const string DefaultRootPath = "Assets/_Game";
    public const string DefaultCatalogAssetPath =
        "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";

    public static ProjectContentSnapshot Capture(
        string rootPath = DefaultRootPath,
        string catalogAssetPath = DefaultCatalogAssetPath)
    {
        var snapshot = new ProjectContentSnapshot
        {
            CatalogAssetPath = catalogAssetPath,
            Catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(catalogAssetPath)
        };

        if (snapshot.Catalog != null)
            snapshot.SetAssetPath(snapshot.Catalog, catalogAssetPath);

        LoadAll(snapshot.Characters, snapshot, rootPath);
        LoadAll(snapshot.Enemies, snapshot, rootPath);
        LoadAll(snapshot.Skills, snapshot, rootPath);
        LoadAll(snapshot.Items, snapshot, rootPath);
        LoadAll(snapshot.Scenarios, snapshot, rootPath);
        LoadAll(snapshot.ActionCatalogs, snapshot, rootPath);
        return snapshot;
    }

    private static void LoadAll<T>(
        List<T> destination,
        ProjectContentSnapshot snapshot,
        string rootPath) where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:" + typeof(T).Name,
            new[] { rootPath });
        var paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
            paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        paths.Sort(StringComparer.Ordinal);
        for (int i = 0; i < paths.Count; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            if (asset == null)
                continue;

            destination.Add(asset);
            snapshot.SetAssetPath(asset, paths[i]);
        }
    }
}
#endif
