using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class GeneratedRoomContext
{
    public string RoomId;
    public GameObject Root;
    public Transform Geometry;
    public Transform Props;
    public Transform Actors;
    public Transform Markers;
    public Transform EventAnchors;
    public Transform Cinematics;
    public Transform Systems;
    public Transform Spawns;
}

internal static class GeneratedRoomEditorUtility
{
    public static GeneratedRoomContext CreateRoom(
        string rootName,
        string roomId,
        Vector2 floorSize,
        Vector2 boundsSize,
        Color backgroundColor,
        Color floorColor,
        Color wallColor)
    {
        GameObject root = new GameObject(rootName);
        RoomInstance instance = root.AddComponent<RoomInstance>();
        TravelTrainEditorAssetUtility.Set(instance, "_roomId", p => p.stringValue = roomId);

        Transform geometry = CreateEmpty("Geometry", root.transform);
        Transform props = CreateEmpty("Props", root.transform);
        Transform actors = CreateEmpty("Actors", root.transform);
        Transform markers = CreateEmpty("Markers", root.transform);
        Transform anchors = CreateEmpty("Event Anchors", root.transform);
        Transform cinematics = CreateEmpty("Cinematics", root.transform);
        Transform systems = CreateEmpty("Systems", root.transform);
        Transform spawns = CreateEmpty("Spawns", markers);

        CreateBlock("Backdrop", geometry, Vector3.zero, boundsSize + Vector2.one, backgroundColor, -10);
        CreateBlock("Walkable Floor", geometry, Vector3.zero, floorSize, floorColor, -2);
        float halfX = boundsSize.x * 0.5f;
        float halfY = boundsSize.y * 0.5f;
        CreateWall("Wall Top", geometry, new Vector2(0f, halfY), new Vector2(boundsSize.x, 0.3f), wallColor);
        CreateWall("Wall Bottom", geometry, new Vector2(0f, -halfY), new Vector2(boundsSize.x, 0.3f), wallColor);
        CreateWall("Wall Left", geometry, new Vector2(-halfX, 0f), new Vector2(0.3f, boundsSize.y), wallColor);
        CreateWall("Wall Right", geometry, new Vector2(halfX, 0f), new Vector2(0.3f, boundsSize.y), wallColor);

        GameObject boundsObject = new GameObject("CameraBounds");
        boundsObject.transform.SetParent(root.transform, false);
        PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
        bounds.isTrigger = true;
        bounds.points = new[]
        {
            new Vector2(-halfX, -halfY),
            new Vector2(-halfX, halfY),
            new Vector2(halfX, halfY),
            new Vector2(halfX, -halfY)
        };
        TravelTrainEditorAssetUtility.Set(instance, "_cameraBounds", p => p.objectReferenceValue = bounds);

        return new GeneratedRoomContext
        {
            RoomId = roomId,
            Root = root,
            Geometry = geometry,
            Props = props,
            Actors = actors,
            Markers = markers,
            EventAnchors = anchors,
            Cinematics = cinematics,
            Systems = systems,
            Spawns = spawns
        };
    }

    public static Transform CreateEmpty(string name, Transform parent, Vector3? localPosition = null)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        if (localPosition.HasValue)
            item.transform.localPosition = localPosition.Value;
        return item.transform;
    }

    public static GameObject CreateBlock(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector2 size,
        Color color,
        int sortingOrder)
    {
        Sprite sprite = TravelTrainEditorAssetUtility.RequireAsset<Sprite>(
            ShowcaseStationPaths.SharedWhiteSprite);
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = localPosition;
        item.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = sortingOrder;
        return item;
    }

    public static SpawnPoint CreateSpawn(
        GeneratedRoomContext room,
        string spawnId,
        Vector2 position,
        FacingDirection facing)
    {
        Transform transform = CreateEmpty("Spawn " + spawnId, room.Spawns, position);
        SpawnPoint spawn = transform.gameObject.AddComponent<SpawnPoint>();
        TravelTrainEditorAssetUtility.Set(spawn, "_spawnPointId", p => p.stringValue = spawnId);
        TravelTrainEditorAssetUtility.Set(spawn, "_defaultFacing", p => p.enumValueIndex = (int)facing);
        return spawn;
    }

    public static T CreateMarker<T>(
        GeneratedRoomContext room,
        string markerId,
        string displayName,
        Vector2 position,
        AreaMarkerType markerType,
        float radius = 0.48f)
        where T : AreaMarkerBase
    {
        Transform transform = CreateEmpty("Marker " + markerId, room.Markers, position);
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            transform.gameObject.layer = interactableLayer;
        CircleCollider2D collider = transform.gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius;
        T marker = transform.gameObject.AddComponent<T>();
        var serialized = new SerializedObject(marker);
        serialized.FindProperty("markerId").stringValue = markerId;
        serialized.FindProperty("areaId").stringValue = room.RoomId;
        serialized.FindProperty("markerType").enumValueIndex = (int)markerType;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = displayName;
        serialized.FindProperty("isOneShot").boolValue = false;
        serialized.FindProperty("interactionRange").floatValue = 1.35f;
        serialized.FindProperty("showLabelInSceneView").boolValue = true;
        serialized.FindProperty("gizmoColor").colorValue = AreaMarkerDefaults.GetColor(markerType);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return marker;
    }

    public static AreaConnectionMarker CreateConnection(
        GeneratedRoomContext room,
        string markerId,
        Vector2 position,
        RoomDefinition targetRoom,
        string targetSpawnId,
        FacingDirection facing)
    {
        AreaConnectionMarker marker = CreateMarker<AreaConnectionMarker>(
            room,
            markerId,
            "다음 구역",
            position,
            AreaMarkerType.Connection);
        var serialized = new SerializedObject(marker);
        serialized.FindProperty("interactToUse").boolValue = false;
        serialized.FindProperty("activationMode").enumValueIndex = (int)DoorActivationMode.OnTriggerEnter;
        serialized.FindProperty("oneShotUntilExit").boolValue = true;
        serialized.FindProperty("mapTransition.TransitionType").enumValueIndex = (int)MapTransitionType.Room;
        serialized.FindProperty("mapTransition.TargetRoom").objectReferenceValue = targetRoom;
        serialized.FindProperty("mapTransition.TargetRoomId").stringValue = targetRoom.RoomId;
        serialized.FindProperty("mapTransition.TargetAreaId").stringValue = targetRoom.RoomId;
        serialized.FindProperty("mapTransition.TargetSpawnPointId").stringValue = targetSpawnId;
        serialized.FindProperty("mapTransition.FacingAfterEnter").enumValueIndex = (int)facing;
        serialized.FindProperty("mapTransition.FadeDuration").floatValue = 0.18f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return marker;
    }

    public static void SaveRoom(
        GeneratedRoomContext room,
        string prefabPath,
        RoomDefinition definition,
        AreaDefinition area)
    {
        TravelTrainEditorAssetUtility.EnsureFolder(
            Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(room.Root, prefabPath);
        RoomInstance instance = saved != null ? saved.GetComponent<RoomInstance>() : null;
        if (instance == null)
            throw new InvalidOperationException("Room Prefab 저장에 실패했습니다: " + prefabPath);

        TravelTrainEditorAssetUtility.Set(definition, "_roomPrefab", p => p.objectReferenceValue = instance);
        TravelTrainEditorAssetUtility.Set(definition, "_areaDefinition", p => p.objectReferenceValue = area);
        TravelTrainEditorAssetUtility.Set(area, "_roomDefinition", p => p.objectReferenceValue = definition);
        AssetDatabase.SaveAssets();
        area.RefreshMarkerSummary();
    }

    private static void CreateWall(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject wall = CreateBlock(name, parent, position, size, color, 0);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }
}

internal static class GeneratedRegionSceneBuilder
{
    public const string TemplateScene =
        "Assets/_Game/Content/Maps/Regions/MapFieldStarter/Scenes/Region_MapFieldStarter.unity";

    public static void Build(
        string scenePath,
        RoomDefinition defaultRoom,
        IReadOnlyList<RoomDefinition> rooms,
        bool includeBattleHost)
    {
        EnsureSceneAsset(scenePath);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = FindLoadedScene(scenePath);
        bool opened = !scene.IsValid();
        if (opened)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        else if (scene.isDirty)
            throw new InvalidOperationException("자동 생성 Scene에 저장되지 않은 변경이 있습니다: " + scenePath);

        try
        {
            SceneManager.SetActiveScene(scene);
            Configure(scene, defaultRoom, rooms, includeBattleHost);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath, false))
                throw new InvalidOperationException("Scene 저장에 실패했습니다: " + scenePath);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        TravelTrainEditorAssetUtility.EnsureBuildSettingsEntry(scenePath);
    }

    private static void Configure(
        Scene scene,
        RoomDefinition defaultRoom,
        IReadOnlyList<RoomDefinition> rooms,
        bool includeBattleHost)
    {
        PlayerController player = FindComponent<PlayerController>(scene);
        CameraController camera = FindComponent<CameraController>(scene);
        RoomContainer container = FindComponent<RoomContainer>(scene);
        MapTransitionService transition = FindComponent<MapTransitionService>(scene);
        if (player == null || camera == null || camera.VirtualCamera == null
            || container == null || transition == null)
        {
            throw new InvalidOperationException(
                "Region 템플릿의 Player, Camera, RoomContainer 또는 MapTransitionService가 없습니다.");
        }

        var containerSo = new SerializedObject(container);
        containerSo.FindProperty("_initialRoom").objectReferenceValue = defaultRoom;
        containerSo.FindProperty("_loadInitialRoomOnStart").boolValue = false;
        containerSo.ApplyModifiedPropertiesWithoutUndo();

        var transitionSo = new SerializedObject(transition);
        transitionSo.FindProperty("_roomContainer").objectReferenceValue = container;
        transitionSo.FindProperty("_dontDestroyOnLoad").boolValue = false;
        transitionSo.ApplyModifiedPropertiesWithoutUndo();

        RegionEntryCoordinator entry = FindComponent<RegionEntryCoordinator>(scene);
        if (entry == null)
            entry = container.gameObject.AddComponent<RegionEntryCoordinator>();
        entry.Configure(container, player, defaultRoom, rooms, true);
        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_prepareOnAwake").boolValue = true;
        entrySo.FindProperty("_requireCameraBinding").boolValue = true;
        entrySo.ApplyModifiedPropertiesWithoutUndo();

        CinemachineCamera virtualCamera = camera.VirtualCamera;
        virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        virtualCamera.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;
        var cameraSo = new SerializedObject(camera);
        cameraSo.FindProperty("_vCam").objectReferenceValue = virtualCamera;
        cameraSo.FindProperty("_centerTarget").objectReferenceValue = player.transform;
        cameraSo.FindProperty("_defaultLensSize").floatValue = CameraLensDefaults.GameplayOrthographicSize;
        cameraSo.ApplyModifiedPropertiesWithoutUndo();

        Camera worldCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"))
            ?? FindComponent<Camera>(scene);
        if (worldCamera == null)
            throw new InvalidOperationException("Region 템플릿에 world Camera가 없습니다.");
        Vector3 cameraPosition = worldCamera.transform.position;
        worldCamera.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, -1f);
        worldCamera.orthographic = true;

        if (includeBattleHost && FindComponent<SeamlessBattleHost>(scene) == null)
        {
            GameObject prefab = TravelTrainEditorAssetUtility.RequireAsset<GameObject>(
                ShowcaseStationPaths.SeamlessBattleHostPrefab);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("SeamlessBattleHost 생성에 실패했습니다.");
            instance.name = "SeamlessBattleHost";
        }
    }

    private static void EnsureSceneAsset(string scenePath)
    {
        TravelTrainEditorAssetUtility.EnsureFolder(
            Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScene) == null
            || !AssetDatabase.CopyAsset(TemplateScene, scenePath))
        {
            throw new InvalidOperationException("Region Scene 템플릿 복제에 실패했습니다: " + scenePath);
        }
        AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Scene FindLoadedScene(string path)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (string.Equals(
                scene.path.Replace('\\', '/'),
                path.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }
        return default;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
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
}