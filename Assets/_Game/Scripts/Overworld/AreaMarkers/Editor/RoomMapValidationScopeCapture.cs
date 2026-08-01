using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomMapValidationScopeCapture
{
    public static RoomMapValidationInput CaptureCurrent()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.prefabContentsRoot != null)
        {
            return CaptureRoots(
                new[] { prefabStage.prefabContentsRoot },
                "Prefab: " + prefabStage.prefabContentsRoot.name,
                false);
        }

        var roots = new List<GameObject>();
        var sceneNames = new List<string>();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                continue;

            var sceneRoots = new List<GameObject>();
            scene.GetRootGameObjects(sceneRoots);
            roots.AddRange(sceneRoots);
            sceneNames.Add(scene.name);
        }

        string scopeName = sceneNames.Count > 0
            ? "Scenes: " + string.Join(", ", sceneNames)
            : "Loaded Scenes";
        return CaptureRoots(roots, scopeName, true);
    }

    public static RoomMapValidationInput CaptureRoots(
        IReadOnlyList<GameObject> roots,
        string scopeName,
        bool requiresSceneInfrastructure)
    {
        IReadOnlyList<GameObject> safeRoots = roots ?? Array.Empty<GameObject>();
        return new RoomMapValidationInput
        {
            ScopeName = scopeName,
            RequiresSceneInfrastructure = requiresSceneInfrastructure,
            Rooms = Collect<RoomInstance>(safeRoots),
            Markers = Collect<AreaMarkerBase>(safeRoots),
            OverworldEnemies = Collect<OverworldEnemy>(safeRoots),
            SpawnPoints = Collect<SpawnPoint>(safeRoots),
            Doors = Collect<DoorTransition>(safeRoots),
            MapTransitionServices = Collect<MapTransitionService>(safeRoots),
            RoomContainers = Collect<RoomContainer>(safeRoots)
        };
    }

    private static T[] Collect<T>(IReadOnlyList<GameObject> roots) where T : Component
    {
        var results = new List<T>();
        var instanceIds = new HashSet<int>();
        for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            if (root == null)
                continue;

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                T component = components[componentIndex];
                if (component == null || !instanceIds.Add(component.GetInstanceID()))
                    continue;

                results.Add(component);
            }
        }

        results.Sort(CompareComponents);
        return results.ToArray();
    }

    private static int CompareComponents<T>(T left, T right) where T : Component
    {
        int path = StringComparer.Ordinal.Compare(GetHierarchyPath(left), GetHierarchyPath(right));
        if (path != 0)
            return path;

        int leftId = left != null ? left.GetInstanceID() : 0;
        int rightId = right != null ? right.GetInstanceID() : 0;
        return leftId.CompareTo(rightId);
    }

    private static string GetHierarchyPath(Component component)
    {
        if (component == null)
            return string.Empty;

        var names = new List<string>();
        Transform current = component.transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        string scenePath = component.gameObject.scene.IsValid()
            ? component.gameObject.scene.path
            : string.Empty;
        return scenePath + "/" + string.Join("/", names);
    }
}
