using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ProductionTriggerLibraryTests
{
    [Test]
    public void ProductionSourcesResolveAndCoverDefaultConditionEvaluators()
    {
        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(LoadDocuments());

        Assert.That(resolved.Validation.HasErrors, Is.False, Format(resolved.Validation));
        Assert.That(resolved.Events, Has.Count.EqualTo(6));
        Assert.That(resolved.Conditions, Has.Count.EqualTo(7));

        TriggerLibraryAsset library = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        for (int i = 0; i < resolved.Events.Count; i++)
        {
            library.Events.Add(TriggerLibraryContractCopy.Event(resolved.Events[i]));
        }

        for (int i = 0; i < resolved.Conditions.Count; i++)
        {
            library.Conditions.Add(TriggerLibraryContractCopy.Condition(resolved.Conditions[i]));
        }

        ScenarioValidationResult contracts = TriggerConditionContractScanner.Validate(
            TriggerConditionRegistry.CreateDefault(),
            library);
        Assert.That(contracts.HasErrors, Is.False, Format(contracts));
        Object.DestroyImmediate(library);
    }

    [Test]
    public void GeneratedProductionAssetMatchesResolvedSources()
    {
        TriggerLibraryAsset asset = AssetDatabase.LoadAssetAtPath<TriggerLibraryAsset>(
            ProductionTriggerLibraryBuildCommand.GeneratedAssetPath);
        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(LoadDocuments());

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.SourceHash, Is.EqualTo(resolved.SourceHash));
        Assert.That(asset.Events, Has.Count.EqualTo(resolved.Events.Count));
        Assert.That(asset.Conditions, Has.Count.EqualTo(resolved.Conditions.Count));
    }

    private static List<TriggerLibrarySourceDocument> LoadDocuments()
    {
        var documents = new List<TriggerLibrarySourceDocument>();
        string absoluteRoot = Path.GetFullPath(ProductionTriggerLibraryBuildCommand.SourceRoot);
        string[] paths = Directory.GetFiles(absoluteRoot, "*.yaml", SearchOption.TopDirectoryOnly);
        System.Array.Sort(paths, System.StringComparer.Ordinal);
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = paths[i].Replace('\\', '/');
            int assetsIndex = assetPath.IndexOf("Assets/", System.StringComparison.Ordinal);
            if (assetsIndex >= 0)
            {
                assetPath = assetPath.Substring(assetsIndex);
            }

            TriggerLibrarySourceParseResult parse = TriggerLibrarySourceParser.Parse(
                File.ReadAllText(paths[i]),
                assetPath);
            Assert.That(parse.Success, Is.True, Format(parse.Validation));
            documents.Add(parse.Document);
        }

        Assert.That(documents, Has.Count.EqualTo(3));
        return documents;
    }

    private static string Format(ScenarioValidationResult validation)
    {
        var messages = new List<string>();
        if (validation != null && validation.Messages != null)
        {
            for (int i = 0; i < validation.Messages.Count; i++)
            {
                messages.Add(validation.Messages[i].Code + ": " + validation.Messages[i].Message);
            }
        }

        return string.Join("\n", messages);
    }
}
