using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ProductionTriggerLibraryBuildResult
{
    public bool Success;
    public TriggerLibraryAsset Asset;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
}

public static class ProductionTriggerLibraryBuildCommand
{
    public const string SourceRoot = "Assets/_Game/Content/Scenarios/TriggerLibrary/Source";
    public const string GeneratedRoot = "Assets/_Game/Content/Scenarios/TriggerLibrary/Generated";
    public const string GeneratedAssetPath = GeneratedRoot + "/TriggerLibrary.asset";

    public static ProductionTriggerLibraryBuildResult Rebuild()
    {
        var result = new ProductionTriggerLibraryBuildResult();
        List<TriggerLibrarySourceDocument> documents = LoadSourceDocuments(result.Validation);
        if (result.Validation.HasErrors)
        {
            return result;
        }

        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(documents);
        result.Validation.Merge(resolved.Validation);
        TriggerLibraryAsset contracts = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        for (int i = 0; i < resolved.Events.Count; i++)
        {
            contracts.Events.Add(TriggerLibraryContractCopy.Event(resolved.Events[i]));
        }

        for (int i = 0; i < resolved.Conditions.Count; i++)
        {
            contracts.Conditions.Add(TriggerLibraryContractCopy.Condition(resolved.Conditions[i]));
        }

        result.Validation.Merge(TriggerConditionContractScanner.Validate(
            TriggerConditionRegistry.CreateDefault(),
            contracts));
        UnityEngine.Object.DestroyImmediate(contracts);
        if (result.Validation.HasErrors)
        {
            return result;
        }

        EnsureAssetFolder(GeneratedRoot);
        TriggerLibraryAsset target = AssetDatabase.LoadAssetAtPath<TriggerLibraryAsset>(GeneratedAssetPath);
        bool created = false;
        if (target == null)
        {
            target = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
            AssetDatabase.CreateAsset(target, GeneratedAssetPath);
            created = true;
        }

        TriggerLibraryAssetSyncResult sync = TriggerLibrarySourceSync.ApplyToAsset(target, resolved);
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
        result.Asset = AssetDatabase.LoadAssetAtPath<TriggerLibraryAsset>(GeneratedAssetPath);
        result.Success = result.Asset != null;
        if (!result.Success)
        {
            result.Validation.AddError(
                "trigger_library.generated_asset.missing",
                "Generated Trigger Library asset could not be loaded after save.",
                GeneratedAssetPath);
        }

        return result;
    }

    private static List<TriggerLibrarySourceDocument> LoadSourceDocuments(
        ScenarioValidationResult validation)
    {
        var documents = new List<TriggerLibrarySourceDocument>();
        string absoluteRoot = Path.GetFullPath(SourceRoot);
        if (!Directory.Exists(absoluteRoot))
        {
            validation.AddError(
                "trigger_library.source_root.missing",
                "Trigger Library source folder is missing: " + SourceRoot,
                SourceRoot);
            return documents;
        }

        string[] paths = Directory.GetFiles(absoluteRoot, "*.yaml", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = ToAssetPath(paths[i]);
            TriggerLibrarySourceParseResult parse = TriggerLibrarySourceParser.Parse(
                File.ReadAllText(paths[i]),
                assetPath);
            validation.Merge(parse.Validation);
            if (parse.Document != null)
            {
                documents.Add(parse.Document);
            }
        }

        if (documents.Count == 0)
        {
            validation.AddError(
                "trigger_library.sources.required",
                "Trigger Library source folder contains no YAML documents.",
                SourceRoot);
        }

        return documents;
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

}
