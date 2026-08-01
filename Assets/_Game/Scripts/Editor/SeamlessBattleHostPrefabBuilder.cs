#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SeamlessBattleHostPrefabBuilder
{
    public const string BattleScenePath = "Assets/_Game/Content/Maps/Battle/BattleScene.unity";
    public const string TestMapScenePath = DevelopmentContentPaths.TestMapScene;
    public const string PrefabPath = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";

    [MenuItem("Hub To Home/Battle/Open Shared Battle Host Prefab")]
    public static void OpenSharedPrefab()
    {
        GameObject prefab = LoadSharedPrefab(out string error);
        if (prefab == null)
        {
            Debug.LogError(error);
            return;
        }

        AssetDatabase.OpenAsset(prefab);
    }

    [MenuItem("Hub To Home/Battle/Sync Shared Host To BattleScene")]
    public static void SyncBattleSceneMenu()
    {
        if (!SyncBattleScene(out string error))
        {
            Debug.LogError($"[SharedBattleHost] BattleScene 동기화 실패: {error}");
            return;
        }

        Debug.Log($"[SharedBattleHost] BattleScene 동기화 완료: {BattleScenePath}");
    }

    [MenuItem("Hub To Home/Battle/Place Shared Host In TestMap")]
    public static void PlaceInTestMap()
    {
        GameObject prefab = LoadSharedPrefab(out string error);
        if (prefab == null)
        {
            Debug.LogError(error);
            return;
        }

        string placementError = string.Empty;
        bool succeeded = EditScene(
            TestMapScenePath,
            scene => EnsureSharedHostInstance(scene, prefab, false, out placementError),
            out string sceneError);
        error = !string.IsNullOrWhiteSpace(placementError) ? placementError : sceneError;
        if (!succeeded || !string.IsNullOrWhiteSpace(error))
        {
            Debug.LogError($"[SharedBattleHost] TestMap 배치 실패: {error}");
        }
    }

    public static GameObject RebuildPrefab()
    {
        return LoadSharedPrefab(out _);
    }

    public static bool SyncBattleScene(out string error)
    {
        GameObject prefab = LoadSharedPrefab(out error);
        if (prefab == null)
            return false;

        string syncError = string.Empty;
        bool succeeded = EditScene(
            BattleScenePath,
            scene => SyncDedicatedHost(scene, prefab, out syncError),
            out string sceneError);
        error = !string.IsNullOrWhiteSpace(syncError) ? syncError : sceneError;
        return succeeded && string.IsNullOrWhiteSpace(error);
    }

    private static bool SyncDedicatedHost(Scene scene, GameObject prefab, out string error)
    {
        Camera sceneCamera = FindSceneCamera(scene);
        if (sceneCamera == null)
        {
            error = "BattleScene 카메라를 찾지 못했습니다.";
            return false;
        }

        SeamlessBattleHost prefabHost = prefab.GetComponent<SeamlessBattleHost>();
        if (!HasRequiredReferences(prefabHost))
        {
            error = "공용 Battle Host 프리팹의 필수 참조가 누락됐습니다.";
            return false;
        }

        SeamlessBattleHost host = FindInScene<SeamlessBattleHost>(scene);
        if (host != null && !IsSharedPrefabInstance(host.gameObject))
        {
            UnityEngine.Object.DestroyImmediate(host.gameObject);
            host = null;
        }

        if (host == null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                error = "공용 Battle Host 프리팹 인스턴스 생성에 실패했습니다.";
                return false;
            }

            instance.name = "[System] SharedBattleHost";
            host = instance.GetComponent<SeamlessBattleHost>();
        }

        if (!HasRequiredReferences(host))
        {
            error = "공용 Battle Host 필수 참조가 누락됐습니다.";
            return false;
        }

        RemoveLegacyBattleRoots(scene, host.gameObject);

        SerializedObject manager = new SerializedObject(host.BattleManager);
        SerializedProperty dedicated = manager.FindProperty("_isDedicatedBattleScene");
        if (dedicated == null)
        {
            error = "BattleManager._isDedicatedBattleScene 필드를 찾지 못했습니다.";
            return false;
        }

        dedicated.boolValue = true;
        manager.ApplyModifiedPropertiesWithoutUndo();

        host.BattleUiRoot.SetActive(true);
        RevertSharedUiOverrides(host.BattleUiRoot);

        SerializedObject ui = new SerializedObject(host.BattleUiController);
        SerializedProperty worldCamera = ui.FindProperty("_worldCamera");
        if (worldCamera != null)
        {
            worldCamera.objectReferenceValue = sceneCamera;
            ui.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(host.gameObject);
        EditorUtility.SetDirty(host.BattleManager);
        EditorUtility.SetDirty(host.BattleUiController);
        EditorSceneManager.MarkSceneDirty(scene);
        error = string.Empty;
        return true;
    }

    private static bool HasRequiredReferences(SeamlessBattleHost host)
    {
        return host != null
            && host.BattleManager != null
            && host.PositionManager != null
            && host.BattleUiRoot != null
            && host.BattleUiController != null;
    }

    private static void RevertSharedUiOverrides(GameObject battleUiRoot)
    {
        if (battleUiRoot == null)
            return;

        Transform uiTransform = battleUiRoot.transform;
        if (PrefabUtility.IsPartOfPrefabInstance(uiTransform))
            PrefabUtility.RevertObjectOverride(uiTransform, InteractionMode.AutomatedAction);

        TMP_Text[] texts = battleUiRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            SerializedObject text = new SerializedObject(texts[i]);
            SerializedProperty sharedMaterial = text.FindProperty("m_sharedMaterial");
            if (sharedMaterial == null || !sharedMaterial.prefabOverride)
                continue;

            PrefabUtility.RevertPropertyOverride(sharedMaterial, InteractionMode.AutomatedAction);
        }
    }

    private static bool EnsureSharedHostInstance(
        Scene scene,
        GameObject prefab,
        bool dedicated,
        out string error)
    {
        SeamlessBattleHost existing = FindInScene<SeamlessBattleHost>(scene);
        if (existing != null)
        {
            if (!IsSharedPrefabInstance(existing.gameObject))
            {
                error = "씬의 Battle Host가 공용 프리팹 인스턴스가 아닙니다.";
                return false;
            }

            error = string.Empty;
            return false;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            error = "공용 Battle Host 프리팹 인스턴스 생성에 실패했습니다.";
            return false;
        }

        instance.name = dedicated ? "[System] SharedBattleHost" : "[System] SeamlessBattleHost";
        error = string.Empty;
        return true;
    }

    private static void RemoveLegacyBattleRoots(Scene scene, GameObject sharedHostRoot)
    {
        var rootsToRemove = new HashSet<GameObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root == sharedHostRoot)
                continue;

            bool ownsLegacyRuntime = root.GetComponentInChildren<BattleManager>(true) != null
                || root.GetComponentInChildren<PositionManager>(true) != null
                || root.GetComponentInChildren<BattleUIController>(true) != null;
            bool isLegacyPositionRoot = string.Equals(root.name, "[BattlePositions]", StringComparison.Ordinal);
            if (ownsLegacyRuntime || isLegacyPositionRoot)
                rootsToRemove.Add(root);
        }

        foreach (GameObject root in rootsToRemove)
            UnityEngine.Object.DestroyImmediate(root);
    }

    private static bool EditScene(
        string scenePath,
        Func<Scene, bool> edit,
        out string error)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (openedForEdit)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = $"씬을 열지 못했습니다: {scenePath}";
                return false;
            }

            bool changed = edit(scene);
            if (changed && !EditorSceneManager.SaveScene(scene))
            {
                error = $"씬 저장에 실패했습니다: {scenePath}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (openedForEdit && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject LoadSharedPrefab(out string error)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            error = $"공용 Battle Host 프리팹이 없습니다: {PrefabPath}";
            return null;
        }

        error = string.Empty;
        return prefab;
    }

    private static bool IsSharedPrefabInstance(GameObject instance)
    {
        return instance != null
            && string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance),
                PrefabPath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        Camera fallback = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            {
                Camera camera = cameras[cameraIndex];
                fallback ??= camera;
                if (camera.CompareTag("MainCamera"))
                    return camera;
            }
        }

        return fallback;
    }
}
#endif
