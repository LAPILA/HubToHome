using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Room 기반 맵 시스템을 바로 확인할 수 있는 샘플 씬/룸/데이터 생성기입니다.
/// 메뉴:
/// - HubToHome > Overworld > Create Room Map Sample
/// - HubToHome > Overworld > Create Map Field Starter Pack
/// - HubToHome > Overworld > Create Template Packs
/// - HubToHome > Overworld > Apply Sorting Rules
/// </summary>
public static class RoomMapSampleBuilder
{
    private const int BackgroundSortingLayerId = unchecked((int)3914913265u);
    private const string BackgroundSortingLayerName = "Background";

    private const string MapRoot = "Assets/_Game/Features/Overworld/Maps";
    private const string SharedGeneratedFolder = MapRoot + "/_Shared/Generated";
    private const string SharedSpritePath = SharedGeneratedFolder + "/RoomMap_WhiteSquare.png";

    private const string BasicRoot = MapRoot + "/Samples/BasicRoomMap";
    private const string BasicScenePath = BasicRoot + "/Scenes/Sample_RoomMap.unity";
    private const string BasicPrefabFolder = BasicRoot + "/Prefabs/Rooms";
    private const string BasicDataFolder = BasicRoot + "/Data/Rooms";

    private const string StarterPackRoot = MapRoot + "/MapFieldStarter";
    private const string StarterPackScenePath = StarterPackRoot + "/Scenes/Region_MapFieldStarter.unity";
    private const string StarterPackPrefabFolder = StarterPackRoot + "/Prefabs/Rooms";
    private const string StarterPackDataFolder = StarterPackRoot + "/Data/Rooms";

    private const string TemplateRoot = MapRoot + "/Templates";

    [MenuItem("HubToHome/오버월드/맵 생성/기본 Room 샘플 생성")]
    public static void CreateBasicSample()
    {
        EnsureFolder(BasicRoot + "/Scenes");
        EnsureSharedSpriteAsset();
        EnsureFolder(BasicPrefabFolder);
        EnsureFolder(BasicDataFolder);

        RoomDefinition roomA = CreateRoomDefinition(
            BasicPrefabFolder,
            BasicDataFolder,
            "sample.room_a",
            "Room_Sample_A",
            new Color(0.16f, 0.22f, 0.35f),
            new Color(0.25f, 0.45f, 0.75f),
            "to_room_b",
            "from_room_b",
            CreateBasicRoomLayout);

        RoomDefinition roomB = CreateRoomDefinition(
            BasicPrefabFolder,
            BasicDataFolder,
            "sample.room_b",
            "Room_Sample_B",
            new Color(0.28f, 0.18f, 0.22f),
            new Color(0.72f, 0.36f, 0.42f),
            "to_room_a",
            "from_room_a",
            CreateBasicRoomLayout);

        WireRoomDoor(roomA, roomB, "Door_To_Room_B", "from_room_a", FacingDirection.Right);
        WireRoomDoor(roomB, roomA, "Door_To_Room_A", "from_room_b", FacingDirection.Left);

        CreateScene(BasicScenePath, roomA, "Sample_RoomMap", new Color(0.04f, 0.04f, 0.06f));

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(BasicScenePath);
        Debug.Log($"[RoomMapSampleBuilder] 기본 샘플 생성 완료: {BasicScenePath}");
    }

    [MenuItem("HubToHome/오버월드/맵 생성/맵 필드 스타터팩 생성")]
    public static void CreateMapFieldStarterPack()
    {
        DeleteAssetIfExists(StarterPackRoot);
        EnsureSharedSpriteAsset();
        EnsureFolder(StarterPackRoot);
        EnsureFolder(StarterPackRoot + "/Scenes");
        EnsureFolder(StarterPackPrefabFolder);
        EnsureFolder(StarterPackDataFolder);
        EnsureFolder(StarterPackRoot + "/Materials");
        EnsureFolder(StarterPackRoot + "/Notes");

        RoomDefinition gate = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.gate",
            "Room_MapField_Gate",
            new Color(0.68f, 0.84f, 0.95f),
            new Color(0.44f, 0.58f, 0.70f),
            "to_village",
            "from_village",
            CreateMapFieldGateLayout);

        RoomDefinition village = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.village",
            "Room_MapField_Village",
            new Color(0.74f, 0.88f, 0.97f),
            new Color(0.50f, 0.65f, 0.78f),
            "to_inn",
            "from_gate",
            CreateMapFieldVillageLayout);

        RoomDefinition inn = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.inn",
            "Room_MapField_Inn",
            new Color(0.42f, 0.30f, 0.24f),
            new Color(0.26f, 0.18f, 0.15f),
            "to_village",
            "from_village",
            CreateMapFieldInnLayout);

        RoomDefinition shop = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.shop",
            "Room_MapField_Shop",
            new Color(0.34f, 0.27f, 0.20f),
            new Color(0.18f, 0.12f, 0.08f),
            "to_village",
            "from_village",
            CreateMapFieldShopLayout);

        RoomDefinition house = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.house",
            "Room_MapField_House",
            new Color(0.38f, 0.30f, 0.25f),
            new Color(0.20f, 0.15f, 0.12f),
            "to_village",
            "from_village",
            CreateMapFieldHouseLayout);

        RoomDefinition forestPath = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.forest_path",
            "Room_MapField_ForestPath",
            new Color(0.22f, 0.38f, 0.26f),
            new Color(0.10f, 0.22f, 0.12f),
            "to_dungeon",
            "from_village",
            CreateMapFieldForestPathLayout);

        RoomDefinition dungeonEntrance = CreateRoomDefinition(
            StarterPackPrefabFolder,
            StarterPackDataFolder,
            "mapfield.dungeon_entrance",
            "Room_MapField_DungeonEntrance",
            new Color(0.18f, 0.19f, 0.24f),
            new Color(0.08f, 0.08f, 0.11f),
            "to_forest",
            "from_forest",
            CreateMapFieldDungeonEntranceLayout);

        WireRoomDoor(gate, village, "Door_To_Village", "from_gate", FacingDirection.Right);
        WireRoomDoor(village, gate, "Door_To_Gate", "from_village", FacingDirection.Left);
        WireRoomDoor(village, inn, "Door_To_Inn", "from_village", FacingDirection.Up);
        WireRoomDoor(inn, village, "Door_To_Village", "to_inn", FacingDirection.Down);
        WireRoomDoor(village, shop, "Door_To_Shop", "from_village", FacingDirection.Up);
        WireRoomDoor(shop, village, "Door_To_Village", "to_shop", FacingDirection.Down);
        WireRoomDoor(village, house, "Door_To_House", "from_village", FacingDirection.Up);
        WireRoomDoor(house, village, "Door_To_Village", "to_house", FacingDirection.Down);
        WireRoomDoor(village, forestPath, "Door_To_ForestPath", "from_village", FacingDirection.Right);
        WireRoomDoor(forestPath, village, "Door_To_Village", "to_forest", FacingDirection.Left);
        WireRoomDoor(forestPath, dungeonEntrance, "Door_To_DungeonEntrance", "from_forest", FacingDirection.Right);
        WireRoomDoor(dungeonEntrance, forestPath, "Door_To_ForestPath", "to_dungeon", FacingDirection.Left);

        CreateScene(StarterPackScenePath, gate, "Region_MapFieldStarter", new Color(0.11f, 0.15f, 0.20f));
        CreateStarterPackReadme();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(StarterPackScenePath);
        Debug.Log($"[RoomMapSampleBuilder] Map Field Starter 맵팩 생성 완료: {StarterPackRoot}");
    }

    [MenuItem("HubToHome/오버월드/맵 정렬/MapFieldStarter 프리팹 정렬 적용")]
    public static void ApplySortingToMapFieldStarterPrefabs()
    {
        ApplySortingRulesToPrefabsInFolder(StarterPackPrefabFolder);
    }

    [MenuItem("HubToHome/오버월드/맵 생성/템플릿/필드 템플릿 생성")]
    public static void CreateFieldTemplate() => CreateSingleRoomTemplatePack(
        "FieldTemplate",
        "template.field",
        "Room_Template_Field",
        "Region_Template_Field",
        new Color(0.36f, 0.62f, 0.34f),
        new Color(0.18f, 0.36f, 0.18f));

    [MenuItem("HubToHome/오버월드/맵 생성/템플릿/마을 템플릿 생성")]
    public static void CreateTownTemplate() => CreateSingleRoomTemplatePack(
        "TownTemplate",
        "template.town",
        "Room_Template_Town",
        "Region_Template_Town",
        new Color(0.58f, 0.52f, 0.43f),
        new Color(0.30f, 0.25f, 0.20f));

    [MenuItem("HubToHome/오버월드/맵 생성/템플릿/실내 템플릿 생성")]
    public static void CreateInteriorTemplate() => CreateSingleRoomTemplatePack(
        "InteriorTemplate",
        "template.interior",
        "Room_Template_Interior",
        "Region_Template_Interior",
        new Color(0.44f, 0.32f, 0.24f),
        new Color(0.22f, 0.14f, 0.10f));

    [MenuItem("HubToHome/오버월드/맵 생성/템플릿/던전 템플릿 생성")]
    public static void CreateDungeonTemplate() => CreateSingleRoomTemplatePack(
        "DungeonTemplate",
        "template.dungeon",
        "Room_Template_Dungeon",
        "Region_Template_Dungeon",
        new Color(0.20f, 0.22f, 0.28f),
        new Color(0.10f, 0.11f, 0.15f));

    [MenuItem("HubToHome/오버월드/맵 생성/템플릿/전체 템플릿 생성")]
    public static void CreateAllTemplatePacks()
    {
        CreateFieldTemplate();
        CreateTownTemplate();
        CreateInteriorTemplate();
        CreateDungeonTemplate();
        Debug.Log("[RoomMapSampleBuilder] 모든 맵 제작 템플릿 생성 완료");
    }

    private delegate GameObject RoomLayoutFactory(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId);

    private static void CreateSingleRoomTemplatePack(string packName, string roomId, string roomPrefabName, string sceneName, Color floorColor, Color wallColor)
    {
        EnsureSharedSpriteAsset();

        string root = $"{TemplateRoot}/{packName}";
        string sceneFolder = root + "/Scenes";
        string prefabFolder = root + "/Prefabs/Rooms";
        string dataFolder = root + "/Data/Rooms";
        string notesFolder = root + "/Notes";

        EnsureFolder(sceneFolder);
        EnsureFolder(prefabFolder);
        EnsureFolder(dataFolder);
        EnsureFolder(notesFolder);

        RoomDefinition room = CreateRoomDefinition(
            prefabFolder,
            dataFolder,
            roomId,
            roomPrefabName,
            floorColor,
            wallColor,
            "center",
            "entry",
            CreateTemplateRoomLayout);

        CreateScene($"{sceneFolder}/{sceneName}.unity", room, sceneName, Color.black);
        File.WriteAllText($"{notesFolder}/{packName}_README.md", $"# {packName}\n\nRoom 기반 맵 제작용 단일 룸 템플릿입니다.\n", System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    private static RoomDefinition CreateRoomDefinition(
        string prefabFolder,
        string dataFolder,
        string roomId,
        string prefabName,
        Color floorColor,
        Color wallColor,
        string doorSpawnId,
        string returnSpawnId,
        RoomLayoutFactory layoutFactory)
    {
        string prefabPath = $"{prefabFolder}/{prefabName}.prefab";
        string definitionPath = $"{dataFolder}/{prefabName}_Definition.asset";

        GameObject roomRoot = layoutFactory(roomId, floorColor, wallColor, doorSpawnId, returnSpawnId);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(roomRoot, prefabPath);
        Object.DestroyImmediate(roomRoot);

        RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RoomDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        RoomInstance roomPrefab = prefab.GetComponent<RoomInstance>();
        SetPrivateString(definition, "_roomId", roomId);
        SetPrivateObject(definition, "_roomPrefab", roomPrefab);
        SetPrivateBool(definition, "_keepCurrentBgm", true);
        EditorUtility.SetDirty(definition);

        return definition;
    }

    private static void ApplySortingRulesToPrefabsInFolder(string prefabFolder)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        int updatedCount = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(prefabPath))
                continue;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
                continue;

            SpriteRenderer[] renderers = prefabRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int j = 0; j < renderers.Length; j++)
                ApplyDefaultSorting(renderers[j], renderers[j].gameObject.name);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            updatedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RoomMapSampleBuilder] Sorting 적용 완료. Folder={prefabFolder}, UpdatedPrefabs={updatedCount}");
    }

    private static GameObject CreateBasicRoomLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Floor", root.transform, new Vector3(0f, 0f, 1f), new Vector3(8f, 4.5f, 0.1f), floorColor);
        CreateWalls(root.transform, 8.5f, 4.8f, wallColor);
        SetRoomCameraBounds(root, 4.15f, 2.4f);
        CreateSpawnPoint("Spawn_From_OtherRoom", root.transform, returnSpawnId, new Vector3(-2.5f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_Near_Door", root.transform, doorSpawnId, new Vector3(2.5f, 0f, 0f), FacingDirection.Left);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(3.45f, 0f, 0f), new Vector3(0.55f, 1.15f, 0.1f), new Color(0.95f, 0.86f, 0.32f));
        return root;
    }

    private static GameObject CreateTemplateRoomLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Editable Floor", root.transform, Vector3.zero, new Vector3(9f, 5f, 0.1f), floorColor);
        CreateWalls(root.transform, 9.3f, 5.3f, wallColor);
        CreateSpawnPoint("Spawn_Entry", root.transform, returnSpawnId, new Vector3(-3f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_Center", root.transform, doorSpawnId, Vector3.zero, FacingDirection.Down);
        CreateBlock("Authoring Marker", root.transform, new Vector3(0f, 1.65f, -0.1f), new Vector3(1.2f, 0.4f, 0.1f), Color.white);
        SetRoomCameraBounds(root, 4.6f, 2.65f);
        return root;
    }

    private static GameObject CreateMapFieldGateLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Field Path", root.transform, Vector3.zero, new Vector3(10f, 4.2f, 0.1f), floorColor);
        CreateBlock("Dark Tree Line Top", root.transform, new Vector3(0f, 2.25f, 0f), new Vector3(10.5f, 0.35f, 0.1f), wallColor).AddComponent<BoxCollider2D>();
        CreateBlock("Dark Tree Line Bottom", root.transform, new Vector3(0f, -2.25f, 0f), new Vector3(10.5f, 0.35f, 0.1f), wallColor).AddComponent<BoxCollider2D>();
        CreateBlock("Entrance Sign", root.transform, new Vector3(-2.4f, 1.15f, -0.1f), new Vector3(0.9f, 0.5f, 0.1f), new Color(0.45f, 0.24f, 0.12f));
        CreateSpawnPoint("Spawn_From_Village", root.transform, returnSpawnId, new Vector3(-3.6f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_To_Village", root.transform, doorSpawnId, new Vector3(3.05f, 0f, 0f), FacingDirection.Left);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(4.25f, 0f, 0f), new Vector3(0.45f, 1.7f, 0.1f), new Color(0.88f, 0.95f, 1f));
        SetRoomCameraBounds(root, 5.1f, 2.35f);
        return root;
    }

    private static GameObject CreateMapFieldVillageLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Village Floor", root.transform, Vector3.zero, new Vector3(12f, 6f, 0.1f), floorColor);
        CreateWalls(root.transform, 12.4f, 6.3f, wallColor);
        CreateBlock("Inn Exterior", root.transform, new Vector3(1.9f, 1.15f, -0.1f), new Vector3(2.2f, 1.35f, 0.1f), new Color(0.42f, 0.22f, 0.16f)).AddComponent<BoxCollider2D>();
        CreateBlock("Warm Window", root.transform, new Vector3(1.35f, 1.25f, -0.2f), new Vector3(0.35f, 0.35f, 0.1f), new Color(1f, 0.76f, 0.28f));
        CreateBlock("Lantern", root.transform, new Vector3(-1.2f, 0.65f, -0.2f), new Vector3(0.25f, 0.7f, 0.1f), new Color(1f, 0.70f, 0.22f));
        CreateBlock("Frozen Pond", root.transform, new Vector3(-2.35f, -1.1f, -0.1f), new Vector3(1.9f, 0.8f, 0.1f), new Color(0.50f, 0.80f, 0.95f));
        CreateSpawnPoint("Spawn_From_Gate", root.transform, returnSpawnId, new Vector3(-4.7f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_From_Inn", root.transform, doorSpawnId, new Vector3(1.9f, 0.05f, 0f), FacingDirection.Down);
        CreateSpawnPoint("Spawn_From_Shop", root.transform, "to_shop", new Vector3(3.7f, 0.25f, 0f), FacingDirection.Down);
        CreateSpawnPoint("Spawn_From_House", root.transform, "to_house", new Vector3(-0.25f, 0.4f, 0f), FacingDirection.Down);
        CreateSpawnPoint("Spawn_From_Forest", root.transform, "to_forest", new Vector3(5.0f, -0.9f, 0f), FacingDirection.Left);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(-5.55f, 0f, 0f), new Vector3(0.45f, 1.7f, 0.1f), new Color(0.88f, 0.95f, 1f));
        CreateDoorPlaceholder("Door_To_Inn", root.transform, new Vector3(1.9f, 0.45f, -0.2f), new Vector3(0.65f, 0.35f, 0.1f), new Color(0.10f, 0.06f, 0.04f));
        CreateDoorPlaceholder("Door_To_Shop", root.transform, new Vector3(3.7f, 0.65f, -0.2f), new Vector3(0.65f, 0.35f, 0.1f), new Color(0.10f, 0.06f, 0.04f));
        CreateDoorPlaceholder("Door_To_House", root.transform, new Vector3(-0.25f, 0.8f, -0.2f), new Vector3(0.65f, 0.35f, 0.1f), new Color(0.10f, 0.06f, 0.04f));
        CreateDoorPlaceholder("Door_To_ForestPath", root.transform, new Vector3(5.65f, -0.9f, 0f), new Vector3(0.45f, 1.2f, 0.1f), new Color(0.18f, 0.38f, 0.20f));
        SetRoomCameraBounds(root, 6.1f, 3.05f);
        return root;
    }

    private static GameObject CreateMapFieldInnLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Wood Floor", root.transform, Vector3.zero, new Vector3(7.2f, 4.4f, 0.1f), floorColor);
        CreateWalls(root.transform, 7.6f, 4.8f, wallColor);
        CreateBlock("Counter", root.transform, new Vector3(0.8f, 1.1f, -0.1f), new Vector3(2.1f, 0.45f, 0.1f), new Color(0.30f, 0.14f, 0.08f)).AddComponent<BoxCollider2D>();
        CreateBlock("Fireplace", root.transform, new Vector3(-2.2f, 1.25f, -0.1f), new Vector3(0.9f, 0.7f, 0.1f), new Color(0.95f, 0.30f, 0.12f)).AddComponent<BoxCollider2D>();
        CreateBlock("Table", root.transform, new Vector3(-1.2f, -0.75f, -0.1f), new Vector3(1.1f, 0.65f, 0.1f), new Color(0.28f, 0.14f, 0.07f)).AddComponent<BoxCollider2D>();
        CreateSpawnPoint("Spawn_From_Village", root.transform, returnSpawnId, new Vector3(0f, -1.45f, 0f), FacingDirection.Up);
        CreateSpawnPoint("Spawn_To_Village", root.transform, doorSpawnId, new Vector3(0f, -1.95f, 0f), FacingDirection.Down);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(0f, -2.2f, 0f), new Vector3(0.9f, 0.35f, 0.1f), new Color(0.10f, 0.06f, 0.04f));
        SetRoomCameraBounds(root, 3.75f, 2.35f);
        return root;
    }

    private static GameObject CreateMapFieldShopLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Shop Floor", root.transform, Vector3.zero, new Vector3(6.5f, 4.2f, 0.1f), floorColor);
        CreateWalls(root.transform, 6.8f, 4.5f, wallColor);
        CreateBlock("Shop Counter", root.transform, new Vector3(0.3f, 1.0f, -0.1f), new Vector3(2.6f, 0.45f, 0.1f), new Color(0.20f, 0.10f, 0.05f)).AddComponent<BoxCollider2D>();
        CreateBlock("Display Left", root.transform, new Vector3(-2.2f, -0.3f, -0.1f), new Vector3(0.55f, 1.25f, 0.1f), new Color(0.55f, 0.38f, 0.20f)).AddComponent<BoxCollider2D>();
        CreateBlock("Display Right", root.transform, new Vector3(2.2f, -0.3f, -0.1f), new Vector3(0.55f, 1.25f, 0.1f), new Color(0.55f, 0.38f, 0.20f)).AddComponent<BoxCollider2D>();
        CreateSpawnPoint("Spawn_From_Village", root.transform, returnSpawnId, new Vector3(0f, -1.45f, 0f), FacingDirection.Up);
        CreateSpawnPoint("Spawn_To_Village", root.transform, doorSpawnId, new Vector3(0f, -1.95f, 0f), FacingDirection.Down);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(0f, -2.08f, 0f), new Vector3(0.9f, 0.35f, 0.1f), new Color(0.08f, 0.04f, 0.02f));
        SetRoomCameraBounds(root, 3.35f, 2.2f);
        return root;
    }

    private static GameObject CreateMapFieldHouseLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("House Floor", root.transform, Vector3.zero, new Vector3(6.2f, 4.0f, 0.1f), floorColor);
        CreateWalls(root.transform, 6.5f, 4.3f, wallColor);
        CreateBlock("Bed", root.transform, new Vector3(-1.9f, 0.85f, -0.1f), new Vector3(1.2f, 0.75f, 0.1f), new Color(0.25f, 0.32f, 0.55f)).AddComponent<BoxCollider2D>();
        CreateBlock("Table", root.transform, new Vector3(1.3f, -0.35f, -0.1f), new Vector3(1.0f, 0.65f, 0.1f), new Color(0.28f, 0.14f, 0.07f)).AddComponent<BoxCollider2D>();
        CreateBlock("Bookshelf", root.transform, new Vector3(2.35f, 1.1f, -0.1f), new Vector3(0.55f, 1.2f, 0.1f), new Color(0.20f, 0.10f, 0.04f)).AddComponent<BoxCollider2D>();
        CreateSpawnPoint("Spawn_From_Village", root.transform, returnSpawnId, new Vector3(0f, -1.35f, 0f), FacingDirection.Up);
        CreateSpawnPoint("Spawn_To_Village", root.transform, doorSpawnId, new Vector3(0f, -1.85f, 0f), FacingDirection.Down);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(0f, -1.95f, 0f), new Vector3(0.9f, 0.35f, 0.1f), new Color(0.08f, 0.04f, 0.02f));
        SetRoomCameraBounds(root, 3.2f, 2.1f);
        return root;
    }

    private static GameObject CreateMapFieldForestPathLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Forest Path", root.transform, Vector3.zero, new Vector3(11.0f, 4.2f, 0.1f), floorColor);
        CreateBlock("Tree Wall Top", root.transform, new Vector3(0f, 2.25f, 0f), new Vector3(11.5f, 0.35f, 0.1f), wallColor).AddComponent<BoxCollider2D>();
        CreateBlock("Tree Wall Bottom", root.transform, new Vector3(0f, -2.25f, 0f), new Vector3(11.5f, 0.35f, 0.1f), wallColor).AddComponent<BoxCollider2D>();
        CreateBlock("Rock", root.transform, new Vector3(-0.8f, -0.85f, -0.1f), new Vector3(0.75f, 0.55f, 0.1f), new Color(0.28f, 0.30f, 0.32f)).AddComponent<BoxCollider2D>();
        CreateSpawnPoint("Spawn_From_Village", root.transform, returnSpawnId, new Vector3(-4.2f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_From_Dungeon", root.transform, doorSpawnId, new Vector3(4.0f, 0f, 0f), FacingDirection.Left);
        CreateDoorPlaceholder("Door_To_Village", root.transform, new Vector3(-5.15f, 0f, 0f), new Vector3(0.45f, 1.55f, 0.1f), new Color(0.18f, 0.38f, 0.20f));
        CreateDoorPlaceholder("Door_To_DungeonEntrance", root.transform, new Vector3(5.15f, 0f, 0f), new Vector3(0.45f, 1.55f, 0.1f), new Color(0.16f, 0.16f, 0.20f));
        SetRoomCameraBounds(root, 5.55f, 2.35f);
        return root;
    }

    private static GameObject CreateMapFieldDungeonEntranceLayout(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId)
    {
        GameObject root = CreateRoomRoot(roomId);
        CreateBlock("Dungeon Approach", root.transform, Vector3.zero, new Vector3(8.6f, 4.6f, 0.1f), floorColor);
        CreateWalls(root.transform, 9.0f, 4.9f, wallColor);
        CreateBlock("Cave Mouth", root.transform, new Vector3(1.8f, 0.65f, -0.1f), new Vector3(1.7f, 1.3f, 0.1f), new Color(0.03f, 0.03f, 0.045f)).AddComponent<BoxCollider2D>();
        CreateBlock("Torch Left", root.transform, new Vector3(0.55f, 1.1f, -0.2f), new Vector3(0.22f, 0.7f, 0.1f), new Color(1.0f, 0.42f, 0.12f));
        CreateBlock("Torch Right", root.transform, new Vector3(3.05f, 1.1f, -0.2f), new Vector3(0.22f, 0.7f, 0.1f), new Color(1.0f, 0.42f, 0.12f));
        CreateSpawnPoint("Spawn_From_Forest", root.transform, returnSpawnId, new Vector3(-3.2f, 0f, 0f), FacingDirection.Right);
        CreateSpawnPoint("Spawn_To_Forest", root.transform, doorSpawnId, new Vector3(-3.85f, 0f, 0f), FacingDirection.Left);
        CreateDoorPlaceholder("Door", root.transform, new Vector3(-4.15f, 0f, 0f), new Vector3(0.45f, 1.5f, 0.1f), new Color(0.16f, 0.16f, 0.20f));
        SetRoomCameraBounds(root, 4.35f, 2.4f);
        return root;
    }

    private static GameObject CreateRoomRoot(string roomId)
    {
        GameObject root = new GameObject(roomId);
        RoomInstance roomInstance = root.AddComponent<RoomInstance>();
        SetPrivateString(roomInstance, "_roomId", roomId);
        return root;
    }

    private static void SetRoomCameraBounds(GameObject root, float halfWidth, float halfHeight)
    {
        PolygonCollider2D cameraBounds = CreateCameraBounds(root.transform, halfWidth, halfHeight);
        SetPrivateObject(root.GetComponent<RoomInstance>(), "_cameraBounds", cameraBounds);
    }

    private static void WireRoomDoor(RoomDefinition sourceRoom, RoomDefinition targetRoom, string doorName, string targetSpawnId, FacingDirection facing)
    {
        string prefabPath = AssetDatabase.GetAssetPath(sourceRoom.RoomPrefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError($"[RoomMapSampleBuilder] Room Prefab 경로를 찾지 못했습니다: {sourceRoom.RoomId}");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Transform door = prefabRoot.transform.Find(doorName);
        if (door == null) door = prefabRoot.transform.Find("Door");
        if (door == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError($"[RoomMapSampleBuilder] Door를 찾지 못했습니다: {prefabPath}");
            return;
        }

        door.name = doorName;
        DoorTransition transition = door.GetComponent<DoorTransition>();
        if (transition == null) transition = door.gameObject.AddComponent<DoorTransition>();

        SerializedObject serialized = new SerializedObject(transition);
        serialized.FindProperty("_activationMode").enumValueIndex = (int)DoorActivationMode.OnTriggerEnter;
        serialized.FindProperty("_oneShotUntilExit").boolValue = true;
        serialized.FindProperty("_request.TransitionType").enumValueIndex = (int)MapTransitionType.Room;
        serialized.FindProperty("_request.TargetRoom").objectReferenceValue = targetRoom;
        serialized.FindProperty("_request.TargetSpawnPointId").stringValue = targetSpawnId;
        serialized.FindProperty("_request.FacingAfterEnter").enumValueIndex = (int)facing;
        serialized.FindProperty("_request.FadeDuration").floatValue = 0.15f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static void CreateScene(string scenePath, RoomDefinition initialRoom, string sceneName, Color background)
    {
        EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = sceneName;

        CreateMainCamera(background);

        GameObject systems = new GameObject("Map Systems");
        RoomContainer roomContainer = systems.AddComponent<RoomContainer>();
        MapTransitionService transitionService = systems.AddComponent<MapTransitionService>();
        SetPrivateObject(roomContainer, "_initialRoom", initialRoom);
        SetPrivateBool(roomContainer, "_loadInitialRoomOnStart", true);
        SetPrivateObject(transitionService, "_roomContainer", roomContainer);

        GameObject player = CreateSamplePlayer();
        player.transform.position = new Vector3(-2.5f, 0f, 0f);

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Camera CreateMainCamera(Color background)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        return camera;
    }

    private static GameObject CreateSamplePlayer()
    {
        GameObject player = CreateBlock("Sample Player", null, Vector3.zero, new Vector3(0.45f, 0.65f, 0.1f), new Color(1f, 0.92f, 0.25f));
        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        player.AddComponent<BoxCollider2D>();
        player.AddComponent<Animator>();
        player.AddComponent<PlayerController>();
        return player;
    }

    private static GameObject CreateDoorPlaceholder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject door = CreateBlock(name, parent, localPosition, localScale, color);
        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        door.AddComponent<DoorTransition>();
        return door;
    }

    private static void CreateSpawnPoint(string name, Transform parent, string spawnPointId, Vector3 localPosition, FacingDirection facing)
    {
        GameObject spawn = new GameObject(name);
        spawn.transform.SetParent(parent);
        spawn.transform.localPosition = localPosition;
        SpawnPoint point = spawn.AddComponent<SpawnPoint>();
        SetPrivateString(point, "_spawnPointId", spawnPointId);
        SetPrivateEnum(point, "_defaultFacing", (int)facing);
    }

    private static PolygonCollider2D CreateCameraBounds(Transform parent, float halfWidth, float halfHeight)
    {
        GameObject bounds = new GameObject("CameraBounds");
        bounds.transform.SetParent(parent);
        bounds.transform.localPosition = Vector3.zero;
        PolygonCollider2D collider = bounds.AddComponent<PolygonCollider2D>();
        collider.isTrigger = true;
        collider.points = new[]
        {
            new Vector2(-halfWidth, -halfHeight),
            new Vector2(-halfWidth, halfHeight),
            new Vector2(halfWidth, halfHeight),
            new Vector2(halfWidth, -halfHeight)
        };
        return collider;
    }

    private static void CreateWalls(Transform parent, float width, float height, Color color)
    {
        CreateWall("Wall_Top", parent, new Vector3(0f, height * 0.5f, 0f), new Vector2(width, 0.25f), color);
        CreateWall("Wall_Bottom", parent, new Vector3(0f, -height * 0.5f, 0f), new Vector2(width, 0.25f), color);
        CreateWall("Wall_Left", parent, new Vector3(-width * 0.5f, 0f, 0f), new Vector2(0.25f, height), color);
        CreateWall("Wall_Right", parent, new Vector3(width * 0.5f, 0f, 0f), new Vector2(0.25f, height), color);
    }

    private static void CreateWall(string name, Transform parent, Vector3 localPosition, Vector2 size, Color color)
    {
        GameObject wall = CreateBlock(name, parent, localPosition, new Vector3(size.x, size.y, 0.1f), color);
        wall.AddComponent<BoxCollider2D>();
    }

    private static GameObject CreateBlock(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject block = new GameObject(name);
        block.name = name;
        block.transform.SetParent(parent);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;

        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSharedWhiteSprite();
        renderer.color = color;
        ApplyDefaultSorting(renderer, name);

        return block;
    }

    private static void ApplyDefaultSorting(SpriteRenderer renderer, string objectName)
    {
        if (renderer == null) return;

        renderer.sortingLayerID = BackgroundSortingLayerId;
        renderer.sortingLayerName = BackgroundSortingLayerName;
        renderer.sortingOrder = ResolveSortingOrder(objectName);
    }

    private static int ResolveSortingOrder(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return 2;

        string name = objectName.ToLowerInvariant();

        if (name.Contains("door")) return 5;

        if (name.Contains("torch")
            || name.Contains("lantern")
            || name.Contains("window")
            || name.Contains("fireplace"))
            return 4;

        if (name.Contains("wall")
            || name.Contains("counter")
            || name.Contains("display")
            || name.Contains("table")
            || name.Contains("bed")
            || name.Contains("bookshelf")
            || name.Contains("rock")
            || name.Contains("cave")
            || name.Contains("sign")
            || name.Contains("exterior"))
            return 3;

        return 2;
    }

    private static void EnsureSharedSpriteAsset()
    {
        EnsureFolder(SharedGeneratedFolder);
        if (!File.Exists(SharedSpritePath))
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(SharedSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SharedSpritePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(SharedSpritePath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite LoadSharedWhiteSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SharedSpritePath);
        if (sprite != null) return sprite;

        EnsureSharedSpriteAsset();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SharedSpritePath);
    }

    private static void CreateStarterPackReadme()
    {
        string path = StarterPackRoot + "/Notes/MapFieldStarter_README.md";
        string content = "# Map Field Starter Pack\n\n"
            + "Room 기반 맵 제작 흐름을 검증하기 위한 기본 맵팩입니다. 특정 상용 게임의 명칭/구조를 그대로 복제하지 않고, 필드/마을/실내 연결 구조를 빠르게 확인하는 예시입니다.\n\n"
            + "## 구성\n\n"
            + "- Region Scene: `Scenes/Region_MapFieldStarter.unity`\n"
            + "- Rooms: Gate, Village, Inn, Shop, House, ForestPath, DungeonEntrance\n"
            + "- Data: 각 RoomDefinition asset\n"
            + "- DoorTransition: Gate <-> Village <-> Inn / Shop / House / ForestPath <-> DungeonEntrance\n\n"
            + "## 기본 생성 룸 7개\n\n"
            + "1. `Room_MapField_Gate`\n"
            + "2. `Room_MapField_Village`\n"
            + "3. `Room_MapField_Inn`\n"
            + "4. `Room_MapField_Shop`\n"
            + "5. `Room_MapField_House`\n"
            + "6. `Room_MapField_ForestPath`\n"
            + "7. `Room_MapField_DungeonEntrance`\n\n"
            + "## 다음 제작 포인트\n\n"
            + "- NPC 배치\n"
            + "- 지역 분위기 파티클\n"
            + "- 이벤트 트리거\n"
            + "- 지역 BGM/실내 BGM override\n";

        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        AssetDatabase.ImportAsset(path);
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void DeleteAssetIfExists(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;
        if (!AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null) return;

        if (!AssetDatabase.DeleteAsset(assetPath))
            Debug.LogWarning($"[RoomMapSampleBuilder] 기존 에셋 삭제 실패: {assetPath}");
    }

    private static void SetPrivateString(Object target, string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateBool(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateEnum(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
