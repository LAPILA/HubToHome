using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AssetDatabaseScenarioDialogueReferenceResolverTests
{
    private const string TempRoot = "Assets/_Game/Features/Scenario/Tests/Editor/TempDialogueResolverTests";

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(TempRoot);
        AssetDatabase.Refresh();
    }

    [Test]
    public void ResolvesDialogueDataByAssetName()
    {
        DialogueData dialogue = CreateDialogueAsset("dlg_zev_phase2");
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver(new[] { TempRoot });

        bool resolved = resolver.TryResolveDialogue(" dlg_zev_phase2 ", out DialogueData result);

        Assert.That(resolved, Is.True);
        Assert.That(result, Is.SameAs(dialogue));
    }

    [Test]
    public void ResolvesDialogueDataByAssetPath()
    {
        DialogueData dialogue = CreateDialogueAsset("dlg_zev_path");
        string path = AssetDatabase.GetAssetPath(dialogue);
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver(new[] { TempRoot });

        bool resolved = resolver.TryResolveDialogue(path, out DialogueData result);

        Assert.That(resolved, Is.True);
        Assert.That(result, Is.SameAs(dialogue));
    }

    [Test]
    public void DuplicateDialogueNamesDoNotResolveAmbiguously()
    {
        CreateDialogueAsset("SharedName", "A");
        CreateDialogueAsset("SharedName", "B");
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver(new[] { TempRoot });

        bool resolved = resolver.TryResolveDialogue("SharedName", out DialogueData result);

        Assert.That(resolved, Is.False);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void InvalidSearchFolderDoesNotFallBackToGlobalSearch()
    {
        CreateDialogueAsset("ScopedOnly");
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver(new[] { TempRoot + "_Missing" });

        bool resolved = resolver.TryResolveDialogue("ScopedOnly", out DialogueData result);

        Assert.That(resolved, Is.False);
        Assert.That(result, Is.Null);
    }

    private static DialogueData CreateDialogueAsset(string assetName, string subFolder = "")
    {
        EnsureFolder(TempRoot);
        string folder = TempRoot;
        if (!string.IsNullOrWhiteSpace(subFolder))
        {
            folder = $"{TempRoot}/{subFolder}";
            EnsureFolder(folder);
        }

        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        string path = $"{folder}/{assetName}.asset";
        AssetDatabase.CreateAsset(dialogue, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<DialogueData>(path);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
