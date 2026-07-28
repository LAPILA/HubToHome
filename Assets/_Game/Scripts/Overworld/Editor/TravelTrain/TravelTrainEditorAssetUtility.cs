using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class TravelTrainEditorAssetUtility
{
    public static T LoadOrCreate<T>(string path, out bool created)
        where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && existing is not T)
        {
            throw new InvalidOperationException(
                $"Asset path is occupied by {existing.GetType().Name}, expected {typeof(T).Name}: {path}");
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            created = false;
            return asset;
        }

        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        created = true;
        return asset;
    }

    public static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Required asset is missing: " + path);
        return asset;
    }

    public static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static void Set(
        UnityEngine.Object target,
        string propertyPath,
        Action<SerializedProperty> assign)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property '{propertyPath}' was not found on {target.GetType().Name}.");
        }

        assign(property);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    public static DialogueData BuildDialogue(string path, string text)
    {
        DialogueData dialogue = LoadOrCreate<DialogueData>(path, out _);
        dialogue.Style = DialogueStyle.Overworld;
        dialogue.Nodes.Clear();
        dialogue.Nodes.Add(new DialogueNode
        {
            Emotion = EmotionType.Normal,
            DefaultText = text ?? string.Empty
        });
        EditorUtility.SetDirty(dialogue);
        return dialogue;
    }

    public static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim();
    }

    public static void EnsureBuildSettingsEntry(string scenePath)
    {
        EnsureBuildSettingsEntriesInOrder(scenePath);
    }

    public static void EnsureBuildSettingsEntriesInOrder(params string[] scenePaths)
    {
        if (scenePaths == null || scenePaths.Length == 0)
            throw new ArgumentException("At least one Scene path is required.", nameof(scenePaths));

        var orderedPaths = new List<string>(scenePaths.Length);
        var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < scenePaths.Length; i++)
        {
            string path = NormalizeAssetPath(scenePaths[i]);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Scene paths cannot be empty.", nameof(scenePaths));
            if (targetPaths.Add(path))
                orderedPaths.Add(path);
        }

        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        var next = new List<EditorBuildSettingsScene>(current.Length + orderedPaths.Count);
        int insertionIndex = -1;
        for (int i = 0; i < current.Length; i++)
        {
            string currentPath = NormalizeAssetPath(current[i].path);
            if (targetPaths.Contains(currentPath))
            {
                if (insertionIndex < 0)
                    insertionIndex = next.Count;
                continue;
            }
            next.Add(current[i]);
        }

        if (insertionIndex < 0)
            insertionIndex = next.Count;
        for (int i = 0; i < orderedPaths.Count; i++)
        {
            next.Insert(
                insertionIndex + i,
                new EditorBuildSettingsScene(orderedPaths[i], true));
        }

        if (!BuildSettingsMatch(current, next))
            EditorBuildSettings.scenes = next.ToArray();
    }

    private static bool BuildSettingsMatch(
        EditorBuildSettingsScene[] current,
        List<EditorBuildSettingsScene> next)
    {
        if (current.Length != next.Count)
            return false;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].enabled != next[i].enabled
                || !string.Equals(
                    NormalizeAssetPath(current[i].path),
                    NormalizeAssetPath(next[i].path),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }
}
