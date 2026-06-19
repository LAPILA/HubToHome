using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class AssetDatabaseScenarioDialogueReferenceResolver :
    IScenarioDialogueReferenceResolver,
    IScenarioDialogueReferenceIdProvider,
    IScenarioAudioReferenceResolver,
    IScenarioAudioReferenceIdProvider
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

    public bool TryGetDialogueDataId(DialogueData dialogue, out string dialogueDataId)
    {
        dialogueDataId = string.Empty;
        if (dialogue == null)
        {
            return false;
        }

        string assetPath = NormalizePath(AssetDatabase.GetAssetPath(dialogue));
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        dialogueDataId = IsUniqueAssetName(assetName, dialogue) ? assetName : assetPath;
        return true;
    }

    public bool TryResolveAudioClip(string audioClipId, out AudioClip clip)
    {
        clip = null;
        string normalizedId = NormalizeId(audioClipId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return false;
        }

        if (TryLoadDirectAudioPath(normalizedId, out clip))
        {
            return true;
        }

        AudioClip match = null;
        string[] guids = FindAudioAssetGuids();

        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
            AudioClip candidate = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (candidate == null || !MatchesAudio(normalizedId, assetPath, candidate))
            {
                continue;
            }

            if (match != null && match != candidate)
            {
                clip = null;
                return false;
            }

            match = candidate;
        }

        clip = match;
        return clip != null;
    }

    public bool TryGetAudioClipId(AudioClip clip, out string audioClipId)
    {
        audioClipId = string.Empty;
        if (clip == null)
        {
            return false;
        }

        string assetPath = NormalizePath(AssetDatabase.GetAssetPath(clip));
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        audioClipId = IsUniqueAudioAssetName(assetName, clip) ? assetName : assetPath;
        return true;
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

    private static bool TryLoadDirectAudioPath(string audioClipId, out AudioClip clip)
    {
        clip = null;
        if (!audioClipId.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipId);
        return clip != null;
    }

    private string[] FindDialogueAssetGuids()
    {
        if (_searchFolders.Length > 0)
        {
            return AssetDatabase.FindAssets("t:DialogueData", _searchFolders);
        }

        return _hasExplicitSearchFolders ? Array.Empty<string>() : AssetDatabase.FindAssets("t:DialogueData");
    }

    private string[] FindAudioAssetGuids()
    {
        if (_searchFolders.Length > 0)
        {
            return AssetDatabase.FindAssets("t:AudioClip", _searchFolders);
        }

        return _hasExplicitSearchFolders ? Array.Empty<string>() : AssetDatabase.FindAssets("t:AudioClip");
    }

    private bool IsUniqueAssetName(string assetName, DialogueData dialogue)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            return false;
        }

        DialogueData match = null;
        string[] guids = FindDialogueAssetGuids();
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), assetName, StringComparison.Ordinal))
            {
                continue;
            }

            DialogueData candidate = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
            if (candidate == null)
            {
                continue;
            }

            if (match != null && match != candidate)
            {
                return false;
            }

            match = candidate;
        }

        return match == dialogue;
    }

    private bool IsUniqueAudioAssetName(string assetName, AudioClip clip)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            return false;
        }

        AudioClip match = null;
        string[] guids = FindAudioAssetGuids();
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), assetName, StringComparison.Ordinal))
            {
                continue;
            }

            AudioClip candidate = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (candidate == null)
            {
                continue;
            }

            if (match != null && match != candidate)
            {
                return false;
            }

            match = candidate;
        }

        return match == clip;
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

    private static bool MatchesAudio(string audioClipId, string assetPath, AudioClip candidate)
    {
        string assetName = NormalizeId(candidate.name);
        string assetPathWithoutExtension = StripKnownAudioExtension(assetPath);

        return string.Equals(assetName, audioClipId, StringComparison.Ordinal)
            || string.Equals(Path.GetFileNameWithoutExtension(assetPath), audioClipId, StringComparison.Ordinal)
            || string.Equals(assetPath, audioClipId, StringComparison.Ordinal)
            || string.Equals(assetPathWithoutExtension, audioClipId, StringComparison.Ordinal)
            || assetPathWithoutExtension.EndsWith("/" + audioClipId, StringComparison.Ordinal);
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

    private static string StripKnownAudioExtension(string assetPath)
    {
        string extension = Path.GetExtension(assetPath);
        return string.IsNullOrEmpty(extension)
            ? assetPath
            : assetPath.Substring(0, assetPath.Length - extension.Length);
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
