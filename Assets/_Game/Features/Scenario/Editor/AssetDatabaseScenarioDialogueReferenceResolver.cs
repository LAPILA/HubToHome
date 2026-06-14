using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

public sealed class AssetDatabaseScenarioDialogueReferenceResolver : IScenarioDialogueReferenceResolver
{
    private readonly bool _hasExplicitSearchFolders;
    private readonly string[] _searchFolders;

    public AssetDatabaseScenarioDialogueReferenceResolver(IEnumerable<string> searchFolders = null)
    {
        _hasExplicitSearchFolders = searchFolders != null;
        _searchFolders = NormalizeSearchFolders(searchFolders);
    }

    public bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue)
    {
        dialogue = null;
        string normalizedId = NormalizeId(dialogueDataId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return false;
        }

        if (TryLoadDirectPath(normalizedId, out dialogue))
        {
            return true;
        }

        DialogueData match = null;
        string[] guids = FindDialogueAssetGuids();

        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
            DialogueData candidate = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
            if (candidate == null || !Matches(normalizedId, assetPath, candidate))
            {
                continue;
            }

            if (match != null && match != candidate)
            {
                dialogue = null;
                return false;
            }

            match = candidate;
        }

        dialogue = match;
        return dialogue != null;
    }

    private static bool TryLoadDirectPath(string dialogueDataId, out DialogueData dialogue)
    {
        dialogue = null;
        if (!dialogueDataId.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(dialogueDataId);
        if (dialogue != null)
        {
            return true;
        }

        if (Path.HasExtension(dialogueDataId))
        {
            return false;
        }

        dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(dialogueDataId + ".asset");
        return dialogue != null;
    }

    private string[] FindDialogueAssetGuids()
    {
        if (_searchFolders.Length > 0)
        {
            return AssetDatabase.FindAssets("t:DialogueData", _searchFolders);
        }

        return _hasExplicitSearchFolders ? Array.Empty<string>() : AssetDatabase.FindAssets("t:DialogueData");
    }

    private static bool Matches(string dialogueDataId, string assetPath, DialogueData candidate)
    {
        string assetName = NormalizeId(candidate.name);
        string assetPathWithoutExtension = StripAssetExtension(assetPath);

        return string.Equals(assetName, dialogueDataId, StringComparison.Ordinal)
            || string.Equals(Path.GetFileNameWithoutExtension(assetPath), dialogueDataId, StringComparison.Ordinal)
            || string.Equals(assetPath, dialogueDataId, StringComparison.Ordinal)
            || string.Equals(assetPathWithoutExtension, dialogueDataId, StringComparison.Ordinal)
            || assetPath.EndsWith("/" + dialogueDataId + ".asset", StringComparison.Ordinal)
            || assetPathWithoutExtension.EndsWith("/" + dialogueDataId, StringComparison.Ordinal);
    }

    private static string[] NormalizeSearchFolders(IEnumerable<string> searchFolders)
    {
        if (searchFolders == null)
        {
            return new string[0];
        }

        var folders = new List<string>();
        foreach (string folder in searchFolders)
        {
            string normalized = NormalizePath(folder);
            if (!string.IsNullOrEmpty(normalized) && AssetDatabase.IsValidFolder(normalized))
            {
                folders.Add(normalized);
            }
        }

        return folders.ToArray();
    }

    private static string StripAssetExtension(string assetPath)
    {
        return assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
            ? assetPath.Substring(0, assetPath.Length - ".asset".Length)
            : assetPath;
    }

    private static string NormalizeId(string value)
    {
        return NormalizePath(value).Trim();
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('\\', '/');
    }
}
