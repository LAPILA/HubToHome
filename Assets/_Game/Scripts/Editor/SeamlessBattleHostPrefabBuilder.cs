#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SeamlessBattleHostPrefabBuilder
{
    public const string BattleScenePath = "Assets/_Game/Content/Maps/Battle/BattleScene.unity";
    public const string TestMapScenePath = DevelopmentContentPaths.TestMapScene;
    public const string PrefabPath = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";

    [MenuItem("Hub To Home/Battle/Rebuild Seamless Battle Host")]
    public static void RebuildMenu()
    {
        GameObject prefab = RebuildPrefab();
        if (prefab != null)
            Debug.Log($"[SeamlessBattleHost] Rebuilt: {PrefabPath}", prefab);
    }

    [MenuItem("Hub To Home/Battle/Rebuild Host And Place In TestMap")]
    public static void RebuildAndPlaceInTestMap()
    {
        GameObject prefab = RebuildPrefab();
        if (prefab == null) return;

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            SeamlessBattleHost existing = Object.FindFirstObjectByType<SeamlessBattleHost>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null) instance.name = "[System] SeamlessBattleHost";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            RestoreOrCreateEmptyScene(setup);
        }
    }

    public static GameObject RebuildPrefab()
    {
        if (!File.Exists(BattleScenePath))
        {
            Debug.LogError($"Battle scene is missing: {BattleScenePath}");
            return null;
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        GameObject root = null;
        try
        {
            EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            BattleManager sourceManager = Object.FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
            PositionManager sourcePositions = Object.FindFirstObjectByType<PositionManager>(FindObjectsInactive.Include);
            if (sourceManager == null || sourcePositions == null)
            {
                Debug.LogError("BattleScene must contain BattleManager and PositionManager.");
                return null;
            }

            SerializedObject sourceSerialized = new SerializedObject(sourceManager);
            GameObject sourceUi = sourceSerialized.FindProperty("_battleUICanvas").objectReferenceValue as GameObject;
            if (sourceUi == null)
            {
                Debug.LogError("BattleManager._battleUICanvas is missing in BattleScene.");
                return null;
            }

            root = new GameObject("SeamlessBattleHost");
            BattleManager manager = Object.Instantiate(sourceManager.gameObject, root.transform).GetComponent<BattleManager>();
            manager.gameObject.name = "BattleManager";
            PositionManager embeddedPositions = manager.GetComponent<PositionManager>();
            if (embeddedPositions != null)
                Object.DestroyImmediate(embeddedPositions);

            PositionManager positions = CreatePositionManager(root.transform, sourcePositions);
            GameObject ui = Object.Instantiate(sourceUi, root.transform);
            ui.name = "BattleUI";
            ui.SetActive(false);
            BattleUIController uiController = ui.GetComponentInChildren<BattleUIController>(true);
            if (uiController == null)
            {
                Debug.LogError("Battle UI must contain BattleUIController.");
                return null;
            }

            SerializedObject managerSerialized = new SerializedObject(manager);
            managerSerialized.FindProperty("_isDedicatedBattleScene").boolValue = false;
            managerSerialized.FindProperty("_battleUICanvas").objectReferenceValue = ui;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SeamlessBattleHost host = root.AddComponent<SeamlessBattleHost>();
            SerializedObject hostSerialized = new SerializedObject(host);
            hostSerialized.FindProperty("_battleManager").objectReferenceValue = manager;
            hostSerialized.FindProperty("_positionManager").objectReferenceValue = positions;
            hostSerialized.FindProperty("_battleUiRoot").objectReferenceValue = ui;
            hostSerialized.FindProperty("_battleUiController").objectReferenceValue = uiController;
            hostSerialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) Debug.LogError($"Failed to save prefab: {PrefabPath}");
            AssetDatabase.SaveAssets();
            return success ? prefab : null;
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            RestoreOrCreateEmptyScene(setup);
        }
    }

    private static PositionManager CreatePositionManager(Transform parent, PositionManager source)
    {
        GameObject positionRoot = new GameObject("PositionManager");
        positionRoot.transform.SetParent(parent, false);
        PositionManager result = positionRoot.AddComponent<PositionManager>();

        SerializedObject sourceSerialized = new SerializedObject(source);
        SerializedObject resultSerialized = new SerializedObject(result);
        CopyTransformList(sourceSerialized, resultSerialized, "_playerDefaultPos", "Player");
        CopyTransformList(sourceSerialized, resultSerialized, "_enemyDefaultPos", "Enemy");
        CopyTransformList(sourceSerialized, resultSerialized, "_enemyAttackPos", "EnemyAttack");

        Transform sourceCenter = sourceSerialized.FindProperty("_centerPos").objectReferenceValue as Transform;
        Transform center = CreateMarker(positionRoot.transform, "Center", sourceCenter);
        resultSerialized.FindProperty("_centerPos").objectReferenceValue = center;
        resultSerialized.ApplyModifiedPropertiesWithoutUndo();
        return result;
    }

    private static void CopyTransformList(
        SerializedObject source,
        SerializedObject destination,
        string propertyName,
        string markerPrefix)
    {
        SerializedProperty sourceList = source.FindProperty(propertyName);
        SerializedProperty destinationList = destination.FindProperty(propertyName);
        destinationList.arraySize = sourceList != null ? sourceList.arraySize : 0;
        for (int i = 0; i < destinationList.arraySize; i++)
        {
            Transform sourceMarker = sourceList.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            Transform marker = CreateMarker(
                ((PositionManager)destination.targetObject).transform,
                $"{markerPrefix}_{i + 1}",
                sourceMarker);
            destinationList.GetArrayElementAtIndex(i).objectReferenceValue = marker;
        }
    }

    private static Transform CreateMarker(Transform parent, string name, Transform source)
    {
        GameObject markerObject = new GameObject(name);
        Transform marker = markerObject.transform;
        marker.SetParent(parent, false);
        if (source != null)
        {
            marker.position = source.position;
            marker.rotation = source.rotation;
            marker.localScale = source.lossyScale;
        }
        return marker;
    }

    private static void RestoreOrCreateEmptyScene(SceneSetup[] setup)
    {
        bool hasLoadedScene = false;
        if (setup != null)
        {
            for (int i = 0; i < setup.Length; i++)
                hasLoadedScene |= setup[i].isLoaded;
        }

        if (hasLoadedScene)
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
