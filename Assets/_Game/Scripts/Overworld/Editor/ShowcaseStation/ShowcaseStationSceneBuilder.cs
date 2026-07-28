using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShowcaseStationScenePaths
{
    public const string TemplateScene =
        "Assets/_Game/Content/Maps/Regions/MapFieldStarter/Scenes/Region_MapFieldStarter.unity";
    public const string SceneRoot = ShowcaseStationPaths.Root + "/Scenes";
    public const string MainScene = SceneRoot + "/Region_ShowcaseStation.unity";
}

/// <summary>
/// Creates a runnable region scene while preserving the project's authored bootstrap and camera rig.
/// </summary>
public static class ShowcaseStationSceneBuilder
{
    public static void BuildOrUpdate()
    {
        TravelWorldBuilder.BuildOrUpdate();
    }

    public static void BuildOrUpdate(ShowcaseStationDataBundle data)
    {
        BuildMainScene(data);
        TravelTrainEditorAssetUtility.EnsureBuildSettingsEntry(
            ShowcaseStationScenePaths.MainScene);
        AssetDatabase.SaveAssets();
    }
    internal static void BuildMainScene(ShowcaseStationDataBundle data)
    {
        RoomDefinition arrivalRoom = RequireRoom(
            data,
            ShowcaseStationIds.Arrival);
        BuildScene(
            ShowcaseStationScenePaths.MainScene,
            arrivalRoom,
            ResolveRooms(data, ShowcaseStationIds.RoomIds),
            includeBattleHost: true);
    }

    private static void BuildScene(
        string scenePath,
        RoomDefinition initialRoom,
        IReadOnlyList<RoomDefinition> rooms,
        bool includeBattleHost)
    {
        EnsureSceneAsset(scenePath);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene scene = FindLoadedScene(scenePath);
        bool openedForBuild = !scene.IsValid();
        if (openedForBuild)
        {
            scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
        }
        else if (scene.isDirty)
        {
            throw new InvalidOperationException(
                "Showcase Station Scene에 저장되지 않은 변경이 있어 자동 갱신을 중단했습니다: "
                + scenePath);
        }

        try
        {
            SceneManager.SetActiveScene(scene);
            ConfigureScene(
                scene,
                initialRoom,
                rooms,
                includeBattleHost);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath, false))
            {
                throw new InvalidOperationException(
                    "Showcase Station Scene 저장에 실패했습니다: " + scenePath);
            }
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);

            if (openedForBuild && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static RoomDefinition RequireRoom(
        ShowcaseStationDataBundle data,
        string roomId)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (!data.Rooms.TryGetValue(roomId, out RoomDefinition room)
            || room == null)
        {
            throw new InvalidOperationException(
                "Showcase Station RoomDefinition이 없습니다: " + roomId);
        }

        return room;
    }

    private static List<RoomDefinition> ResolveRooms(
        ShowcaseStationDataBundle data,
        IReadOnlyList<string> roomIds)
    {
        var rooms = new List<RoomDefinition>(roomIds.Count);
        for (int i = 0; i < roomIds.Count; i++)
            rooms.Add(RequireRoom(data, roomIds[i]));
        return rooms;
    }
    private static void ConfigureScene(
        Scene scene,
        RoomDefinition initialRoom,
        IReadOnlyList<RoomDefinition> rooms,
        bool includeBattleHost)
    {
        GameObject bootstrapRoot = FindRoot(scene, "[GameBootstrap]");
        if (bootstrapRoot == null || bootstrapRoot.GetComponentInChildren<GameBootstrap>(true) == null)
        {
            throw new InvalidOperationException(
                "Scene 템플릿에서 [GameBootstrap]을 찾지 못했습니다.");
        }

        PlayerController player = FindComponentInScene<PlayerController>(scene);
        CameraController cameraController = FindComponentInScene<CameraController>(scene);
        RoomContainer roomContainer = FindComponentInScene<RoomContainer>(scene);
        MapTransitionService transitionService = FindComponentInScene<MapTransitionService>(scene);
        if (player == null
            || cameraController == null
            || cameraController.VirtualCamera == null
            || roomContainer == null
            || transitionService == null)
        {
            throw new InvalidOperationException(
                "Scene 템플릿의 Player, CameraController, RoomContainer 또는 MapTransitionService가 누락되었습니다.");
        }

        ConfigureRoomContainer(roomContainer, initialRoom);
        ConfigureTransitionService(transitionService, roomContainer);
        ConfigureRegionEntry(
            scene,
            roomContainer,
            player,
            initialRoom,
            rooms);
        ConfigureCamera(cameraController, player);
        if (includeBattleHost)
            EnsureSeamlessBattleHost(scene);

        bootstrapRoot.name = "[GameBootstrap]";
        player.gameObject.name = "Player_Base";
        transitionService.gameObject.name = "Map Systems";
        EditorUtility.SetDirty(bootstrapRoot);
        EditorUtility.SetDirty(player.gameObject);
    }
    private static void ConfigureRoomContainer(
        RoomContainer roomContainer,
        RoomDefinition arrivalRoom)
    {
        SerializedObject serialized = new SerializedObject(roomContainer);
        serialized.FindProperty("_initialRoom").objectReferenceValue = arrivalRoom;
        serialized.FindProperty("_loadInitialRoomOnStart").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(roomContainer);
    }

    private static void ConfigureTransitionService(
        MapTransitionService transitionService,
        RoomContainer roomContainer)
    {
        SerializedObject serialized = new SerializedObject(transitionService);
        serialized.FindProperty("_roomContainer").objectReferenceValue = roomContainer;
        serialized.FindProperty("_dontDestroyOnLoad").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(transitionService);
    }

    private static void ConfigureRegionEntry(
        Scene scene,
        RoomContainer roomContainer,
        PlayerController player,
        RoomDefinition defaultRoom,
        IReadOnlyList<RoomDefinition> rooms)
    {
        RegionEntryCoordinator coordinator =
            FindComponentInScene<RegionEntryCoordinator>(scene);
        if (coordinator == null)
            coordinator = roomContainer.gameObject.AddComponent<RegionEntryCoordinator>();

        coordinator.Configure(
            roomContainer,
            player,
            defaultRoom,
            rooms,
            requireCameraBinding: true);

        SerializedObject serialized = new SerializedObject(coordinator);
        serialized.FindProperty("_prepareOnAwake").boolValue = true;
        serialized.FindProperty("_requireCameraBinding").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coordinator);
    }
    private static void ConfigureCamera(
        CameraController cameraController,
        PlayerController player)
    {
        CinemachineCamera virtualCamera = cameraController.VirtualCamera;
        virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        virtualCamera.Lens.OrthographicSize =
            CameraLensDefaults.GameplayOrthographicSize;

        SerializedObject serialized = new SerializedObject(cameraController);
        serialized.FindProperty("_vCam").objectReferenceValue = virtualCamera;
        serialized.FindProperty("_centerTarget").objectReferenceValue = player.transform;
        serialized.FindProperty("_defaultLensSize").floatValue =
            CameraLensDefaults.GameplayOrthographicSize;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Camera worldCamera = FindComponentInScene<Camera>(
            player.gameObject.scene,
            candidate => candidate.CompareTag("MainCamera"));
        if (worldCamera == null)
            worldCamera = FindComponentInScene<Camera>(player.gameObject.scene);
        if (worldCamera == null)
            throw new InvalidOperationException("메인 Camera가 없습니다.");

        Vector3 cameraPosition = worldCamera.transform.position;
        worldCamera.transform.position =
            new Vector3(cameraPosition.x, cameraPosition.y, -1f);
        worldCamera.orthographic = true;
        EditorUtility.SetDirty(worldCamera);
        EditorUtility.SetDirty(virtualCamera);
        EditorUtility.SetDirty(cameraController);
    }

    private static void EnsureSeamlessBattleHost(Scene scene)
    {
        if (FindComponentInScene<SeamlessBattleHost>(scene) != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShowcaseStationPaths.SeamlessBattleHostPrefab);
        if (prefab == null)
        {
            throw new InvalidOperationException(
                "SeamlessBattleHost Prefab이 없습니다: "
                + ShowcaseStationPaths.SeamlessBattleHostPrefab);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null || instance.GetComponent<SeamlessBattleHost>() == null)
            throw new InvalidOperationException("SeamlessBattleHost 생성에 실패했습니다.");
        instance.name = "SeamlessBattleHost";
    }

    private static void EnsureSceneAsset(string scenePath)
    {
        ShowcaseStationDataBuilder.EnsureFolder(ShowcaseStationScenePaths.SceneRoot);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                scenePath) != null)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                ShowcaseStationScenePaths.TemplateScene) == null)
        {
            throw new InvalidOperationException(
                "Region Scene 템플릿이 없습니다: "
                + ShowcaseStationScenePaths.TemplateScene);
        }

        if (!AssetDatabase.CopyAsset(
                ShowcaseStationScenePaths.TemplateScene,
                scenePath))
        {
            throw new InvalidOperationException(
                "Region Scene 템플릿 복제에 실패했습니다.");
        }

        AssetDatabase.ImportAsset(
            scenePath,
            ImportAssetOptions.ForceSynchronousImport);
    }

    private static Scene FindLoadedScene(string path)
    {
        string normalizedPath = path.Replace('\\', '/');
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (string.Equals(
                    candidate.path.Replace('\\', '/'),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return default;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(root => string.Equals(root.name, name, StringComparison.Ordinal));
    }

    private static T FindComponentInScene<T>(
        Scene scene,
        Func<T, bool> predicate = null)
        where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T[] candidates = roots[i].GetComponentsInChildren<T>(true);
            for (int j = 0; j < candidates.Length; j++)
            {
                T candidate = candidates[j];
                if (candidate != null && (predicate == null || predicate(candidate)))
                    return candidate;
            }
        }

        return null;
    }
}
