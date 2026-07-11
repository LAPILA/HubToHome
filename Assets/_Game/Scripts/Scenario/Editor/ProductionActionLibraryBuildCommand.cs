using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ProductionActionLibraryBuildResult
{
    public bool Success;
    public ActionCatalogAsset Asset;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
}

public static class ProductionActionLibraryBuildCommand
{
    public const string SourceRoot = "Assets/_Game/Content/Scenarios/ActionLibrary/Source";
    public const string GeneratedRoot = "Assets/_Game/Content/Scenarios/ActionLibrary/Generated";
    public const string GeneratedAssetPath = GeneratedRoot + "/ActionLibrary.asset";

    [MenuItem("HubToHome/시나리오/Action Library 다시 만들기")]
    public static void RebuildFromMenu()
    {
        ProductionActionLibraryBuildResult result = Rebuild();
        if (result.Success)
        {
            Selection.activeObject = result.Asset;
            EditorGUIUtility.PingObject(result.Asset);
            Debug.Log("[ActionLibrary] 공식 Action Library를 다시 만들었습니다: " + GeneratedAssetPath);
            return;
        }

        Debug.LogError("[ActionLibrary] 재빌드 실패\n" + Format(result.Validation));
    }

    public static ProductionActionLibraryBuildResult Rebuild()
    {
        var result = new ProductionActionLibraryBuildResult();
        var documents = new List<ActionLibrarySourceDocument>();
        string absoluteRoot = Path.GetFullPath(SourceRoot);
        if (!Directory.Exists(absoluteRoot))
        {
            result.Validation.AddError(
                "action_library.source_root.missing",
                "Action Library source folder is missing: " + SourceRoot,
                SourceRoot);
            return result;
        }

        string[] paths = Directory.GetFiles(absoluteRoot, "*.actions.yaml", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = ToAssetPath(paths[i]);
            ActionLibrarySourceParseResult parse = ActionLibrarySourceParser.Parse(
                File.ReadAllText(paths[i]),
                assetPath);
            result.Validation.Merge(parse.Validation);
            if (parse.Document != null)
            {
                documents.Add(parse.Document);
            }
        }

        ResolvedActionLibrary resolved = ResolvedActionLibrary.Build(documents);
        result.Validation.Merge(resolved.Validation);
        ActionCatalogAsset contractCatalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        for (int i = 0; i < resolved.Entries.Count; i++)
        {
            contractCatalog.Entries.Add(ActionCatalogContractCopy.Entry(resolved.Entries[i]));
        }

        result.Validation.Merge(ActionAdapterContractScanner.Validate(
            new[]
            {
                BattleScenarioActionRegistryFactory.CreateRegistry(),
                SceneActionSequenceContextFactory.CreateRegistry()
            },
            contractCatalog));
        UnityEngine.Object.DestroyImmediate(contractCatalog);
        if (result.Validation.HasErrors)
        {
            return result;
        }

        EnsureAssetFolder(GeneratedRoot);
        ActionCatalogAsset target = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(GeneratedAssetPath);
        bool created = false;
        if (target == null)
        {
            target = ScriptableObject.CreateInstance<ActionCatalogAsset>();
            AssetDatabase.CreateAsset(target, GeneratedAssetPath);
            created = true;
        }

        ActionLibraryAssetSyncResult sync = ActionLibrarySourceSync.ApplyToAsset(target, resolved);
        result.Validation.Merge(sync.Validation);
        if (!sync.Success)
        {
            if (created)
            {
                AssetDatabase.DeleteAsset(GeneratedAssetPath);
            }

            return result;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceUpdate);
        result.Asset = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(GeneratedAssetPath);
        result.Success = result.Asset != null;
        if (!result.Success)
        {
            result.Validation.AddError(
                "action_library.generated_asset.missing",
                "Generated Action Library asset could not be loaded after save.",
                GeneratedAssetPath);
        }

        return result;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
        int index = normalized.IndexOf("Assets/", StringComparison.Ordinal);
        return index >= 0 ? normalized.Substring(index) : normalized;
    }

    private static string Format(ScenarioValidationResult validation)
    {
        var lines = new List<string>();
        if (validation != null && validation.Messages != null)
        {
            for (int i = 0; i < validation.Messages.Count; i++)
            {
                ScenarioValidationMessage message = validation.Messages[i];
                lines.Add(message.Severity + " " + message.Code + ": " + message.Message);
            }
        }

        return string.Join("\n", lines);
    }
}
