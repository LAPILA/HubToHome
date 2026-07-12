using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ProductionActionLibraryTests
{
    private const string SourceRoot = "Assets/_Game/Content/Scenarios/ActionLibrary/Source";
    private const string GeneratedAssetPath = "Assets/_Game/Content/Scenarios/ActionLibrary/Generated/ActionLibrary.asset";

    [Test]
    public void ProductionSourcesResolveWithoutErrorsAndCoverRuntimeAdapters()
    {
        List<ActionLibrarySourceDocument> documents = LoadDocuments();
        ResolvedActionLibrary resolved = ResolvedActionLibrary.Build(documents);

        Assert.That(resolved.Validation.HasErrors, Is.False, Format(resolved.Validation));
        Assert.That(resolved.Entries, Has.Count.EqualTo(29));

        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        for (int i = 0; i < resolved.Entries.Count; i++)
        {
            catalog.Entries.Add(ActionCatalogContractCopy.Entry(resolved.Entries[i]));
        }

        ScenarioValidationResult contracts = ActionAdapterContractScanner.Validate(
            new[]
            {
                BattleScenarioActionRegistryFactory.CreateRegistry(),
                SceneActionSequenceContextFactory.CreateRegistry()
            },
            catalog);

        Assert.That(contracts.HasErrors, Is.False, Format(contracts));
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void GeneratedProductionAssetMatchesResolvedSources()
    {
        ActionCatalogAsset asset = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(GeneratedAssetPath);
        ResolvedActionLibrary resolved = ResolvedActionLibrary.Build(LoadDocuments());

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.SourceHash, Is.EqualTo(resolved.SourceHash));
        Assert.That(asset.Entries, Has.Count.EqualTo(resolved.Entries.Count));
    }

    private static List<ActionLibrarySourceDocument> LoadDocuments()
    {
        var documents = new List<ActionLibrarySourceDocument>();
        string absoluteRoot = Path.GetFullPath(SourceRoot);
        string[] paths = Directory.GetFiles(absoluteRoot, "*.actions.yaml", SearchOption.TopDirectoryOnly);
        System.Array.Sort(paths, System.StringComparer.Ordinal);
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = paths[i].Replace('\\', '/');
            int assetsIndex = assetPath.IndexOf("Assets/", System.StringComparison.Ordinal);
            if (assetsIndex >= 0)
            {
                assetPath = assetPath.Substring(assetsIndex);
            }

            ActionLibrarySourceParseResult parse = ActionLibrarySourceParser.Parse(
                File.ReadAllText(paths[i]),
                assetPath);
            Assert.That(parse.Success, Is.True, Format(parse.Validation));
            documents.Add(parse.Document);
        }

        Assert.That(documents, Is.Not.Empty);
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
