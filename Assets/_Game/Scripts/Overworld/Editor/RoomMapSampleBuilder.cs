using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Room 기반 맵 시스템을 바로 확인할 수 있는 샘플 씬/룸/데이터 생성기입니다.
///
/// 원칙:
/// - 런타임 코드는 Assets/_Game/Scripts/Overworld/Runtime/Map 아래에 둡니다.
/// - 실제 월드/씬/룸 데이터/룸 프리팹은 Assets/_Game/Content/Maps 아래에 생성합니다.
/// - 개발 중인 월드/템플릿은 Content/Maps/Development 안에서 확인합니다.
/// 메뉴:
/// - HubToHome > 오버월드 > 맵 생성 > 기본 Room 샘플 생성
/// - HubToHome > 오버월드 > 맵 생성 > 맵 필드 스타터팩 생성
/// - HubToHome > 오버월드 > 맵 생성 > 템플릿 팩 생성
/// - HubToHome > 오버월드 > 맵 정렬 규칙 적용
/// </summary>
public static class RoomMapSampleBuilder
{
    private const int BackgroundSortingLayerId = unchecked((int)3914913265u);
    private const string BackgroundSortingLayerName = "Background";

    private const string SceneWorldRoot = "Assets/_Game/Content/Maps";

    private const string DevelopmentRoot = SceneWorldRoot + "/Development";
    private const string SharedGeneratedFolder = SceneWorldRoot + "/Shared/Generated";
    private const string SharedSpritePath = SharedGeneratedFolder + "/RoomMap_WhiteSquare.png";

    private const string BasicRoot = DevelopmentRoot + "/Samples/BasicRoomMap";
    private const string BasicScenePath = BasicRoot + "/Scenes/Sample_RoomMap.unity";
    private const string BasicPrefabFolder = BasicRoot + "/Prefabs/Rooms";
    private const string BasicDataFolder = BasicPrefabFolder;

    private const string StarterPackRoot = DevelopmentContentPaths.MapFieldStarterRoot;
    private const string StarterPackScenePath = DevelopmentContentPaths.MapFieldStarterScene;
    private const string StarterPackPrefabFolder = StarterPackRoot + "/Prefabs/Rooms";
    private const string StarterPackDataFolder = StarterPackPrefabFolder;

    private const string TemplateRoot = DevelopmentContentPaths.TemplatesRoot;

    private const string DesignerGuidePath = SceneWorldRoot + "/README_MapAuthoring.md";
    [MenuItem("Hub To Home/오버월드/맵 생성/기본 Room 샘플 생성")]
    public static void CreateBasicSample()
    {
        EnsureDesignerGuide();
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

    [MenuItem("Hub To Home/오버월드/맵 생성/맵 필드 스타터팩 생성")]
    public static void CreateMapFieldStarterPack()
    {
        EnsureDesignerGuide();
        DeleteAssetIfExists(StarterPackRoot);
        EnsureSharedSpriteAsset();
        EnsureFolder(StarterPackRoot);
        EnsureFolder(StarterPackRoot + "/Scenes");
        EnsureFolder(StarterPackPrefabFolder);
        EnsureFolder(StarterPackDataFolder);
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
        WireShortcutDoor(house, forestPath, "Marker_house.shortcut_locked", "from_house_shortcut", FacingDirection.Right);
        WireShortcutDoor(forestPath, house, "Marker_forest.shortcut_locked", "from_forest_shortcut", FacingDirection.Left);

        CreateScene(StarterPackScenePath, gate, "Region_MapFieldStarter", new Color(0.11f, 0.15f, 0.20f));
        CreateStarterPackReadme();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(StarterPackScenePath);
        Debug.Log($"[RoomMapSampleBuilder] Map Field Starter 맵팩 생성 완료: {StarterPackRoot}");
    }

    [MenuItem("Hub To Home/오버월드/맵 정렬/MapFieldStarter 프리팹 정렬 적용")]
    public static void ApplySortingToMapFieldStarterPrefabs()
    {
        ApplySortingRulesToPrefabsInFolder(StarterPackPrefabFolder);
    }

    [MenuItem("Hub To Home/오버월드/맵 생성/템플릿/필드 템플릿 생성")]
    public static void CreateFieldTemplate() => CreateSingleRoomTemplatePack(
        "FieldTemplate",
        "template.field",
        "Room_Template_Field",
        "Region_Template_Field",
        new Color(0.36f, 0.62f, 0.34f),
        new Color(0.18f, 0.36f, 0.18f));

    [MenuItem("Hub To Home/오버월드/맵 생성/템플릿/마을 템플릿 생성")]
    public static void CreateTownTemplate() => CreateSingleRoomTemplatePack(
        "TownTemplate",
        "template.town",
        "Room_Template_Town",
        "Region_Template_Town",
        new Color(0.58f, 0.52f, 0.43f),
        new Color(0.30f, 0.25f, 0.20f));

    [MenuItem("Hub To Home/오버월드/맵 생성/템플릿/실내 템플릿 생성")]
    public static void CreateInteriorTemplate() => CreateSingleRoomTemplatePack(
        "InteriorTemplate",
        "template.interior",
        "Room_Template_Interior",
        "Region_Template_Interior",
        new Color(0.44f, 0.32f, 0.24f),
        new Color(0.22f, 0.14f, 0.10f));

    [MenuItem("Hub To Home/오버월드/맵 생성/템플릿/던전 템플릿 생성")]
    public static void CreateDungeonTemplate() => CreateSingleRoomTemplatePack(
        "DungeonTemplate",
        "template.dungeon",
        "Room_Template_Dungeon",
        "Region_Template_Dungeon",
        new Color(0.20f, 0.22f, 0.28f),
        new Color(0.10f, 0.11f, 0.15f));

    [MenuItem("Hub To Home/오버월드/맵 생성/템플릿/전체 템플릿 생성")]
    public static void CreateAllTemplatePacks()
    {
        EnsureDesignerGuide();
        CreateFieldTemplate();
        CreateTownTemplate();
        CreateInteriorTemplate();
        CreateDungeonTemplate();
        Debug.Log("[RoomMapSampleBuilder] 모든 맵 제작 템플릿 생성 완료");
    }

    private delegate GameObject RoomLayoutFactory(string roomId, Color floorColor, Color wallColor, string doorSpawnId, string returnSpawnId);

    private static void CreateSingleRoomTemplatePack(string packName, string roomId, string roomPrefabName, string sceneName, Color floorColor, Color wallColor)
    {
        EnsureDesignerGuide();
        EnsureSharedSpriteAsset();

        string root = $"{TemplateRoot}/{packName}";
        string sceneFolder = root + "/Scenes";
        string prefabFolder = root + "/Prefabs/Rooms";
        string dataFolder = prefabFolder;
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
        File.WriteAllText($"{notesFolder}/{packName}_README.md", $"# {packName}\n\nRoom 기반 맵 제작용 단일 룸 템플릿입니다.\n\n- Scene: `{sceneName}.unity`\n- RoomDefinition: `Prefabs/Rooms/{roomPrefabName}_Definition.asset`\n- Room Prefab: `Prefabs/Rooms/{roomPrefabName}.prefab`\n\n작업 순서: 룸 프리팹 편집 → SpawnPoint/AreaConnectionMarker 확인 → 현재 열린 룸 맵 검사 실행.\n", System.Text.Encoding.UTF8);
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
        string areaDefinitionPath = $"{dataFolder}/{prefabName}_Area.asset";

        GameObject roomRoot = layoutFactory(roomId, floorColor, wallColor, doorSpawnId, returnSpawnId);
        ConfigureSequencePuzzles(roomRoot, dataFolder, prefabName);
        PrefabUtility.SaveAsPrefabAsset(roomRoot, prefabPath);
        Object.DestroyImmediate(roomRoot);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        RoomInstance roomPrefab = prefab != null ? prefab.GetComponent<RoomInstance>() : null;
        if (roomPrefab == null)
            throw new System.InvalidOperationException($"Room Prefab 저장에 실패했습니다: {prefabPath}");

        RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RoomDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        AreaDefinition areaDefinition = AssetDatabase.LoadAssetAtPath<AreaDefinition>(areaDefinitionPath);
        if (areaDefinition == null)
        {
            areaDefinition = ScriptableObject.CreateInstance<AreaDefinition>();
            AssetDatabase.CreateAsset(areaDefinition, areaDefinitionPath);
        }

        SerializedObject definitionSerialized = new SerializedObject(definition);
        definitionSerialized.Update();
        definitionSerialized.FindProperty("_roomId").stringValue = roomId;
        definitionSerialized.FindProperty("_roomPrefab").objectReferenceValue = roomPrefab;
        definitionSerialized.FindProperty("_areaDefinition").objectReferenceValue = areaDefinition;
        definitionSerialized.FindProperty("_keepCurrentBgm").boolValue = true;
        definitionSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject areaSerialized = new SerializedObject(areaDefinition);
        areaSerialized.Update();
        areaSerialized.FindProperty("_areaId").stringValue = roomId;
        areaSerialized.FindProperty("_roomDefinition").objectReferenceValue = definition;
        areaSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(definition);
        EditorUtility.SetDirty(areaDefinition);
        AssetDatabase.SaveAssetIfDirty(definition);
        AssetDatabase.SaveAssetIfDirty(areaDefinition);

        areaDefinition.RefreshMarkerSummary();
        EditorUtility.SetDirty(areaDefinition);
        AssetDatabase.SaveAssetIfDirty(areaDefinition);

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
        CreateSignMarker(root.transform, roomId, "gate.welcome_sign", new Vector3(-2.4f, 0.72f, 0f), "* 북쪽 숲과 오래된 구조체 실험장으로 이어지는 초입이다.\n* Z로 표지판 반복/원샷 정책을 확인한다.", true, "mapfield.gate.sign.read");
        CreatePlotPointMarker(root.transform, roomId, "gate.first_entry_cutscene", new Vector3(-1.1f, 0f, 0f), AreaPlotTriggerMode.OnEnter, "* 짧은 컷씬 테스트: 카메라가 잠깐 멈춘 것처럼 입구의 공기가 무거워진다.\n* 실제 컷씬은 Action Sequence로 옮길 수 있다.", true, "mapfield.gate.plot.entered");
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
        CreateNPCMarker(root.transform, roomId, "village.guide", new Vector3(-1.2f, 0.1f, 0f), "* 숲 쪽은 아직 테스트 중이야.\n* 표지판, 아이템, 상점, 퍼즐, 지름길을 전부 만져 보고 이상한 입력 락을 찾아줘.", false, string.Empty);
        CreateItemMarker(root.transform, roomId, "village.test_item", new Vector3(-2.35f, -1.75f, 0f), "debug_healing_leaf", 2, "* 차가운 연못가에서 debug_healing_leaf 2개를 주웠다.\n* 아이템 획득 대화/원샷/저장 플래그 테스트.", "mapfield.village.item.pond_leaf");
        CreatePuzzleMarker(root.transform, roomId, "village.lantern_puzzle", new Vector3(-1.2f, 1.15f, 0f), "mapfield.village.lantern_puzzle.solved");
        CreateVendorMarker(root.transform, roomId, "village.street_vendor", new Vector3(3.7f, -0.55f, 0f), "vendor.mapfield.street", "shop.debug.village");
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
        CreateNPCMarker(root.transform, roomId, "inn.keeper", new Vector3(0.8f, 0.55f, 0f), "* 여관 카운터 테스트야.\n* 대화가 끝난 같은 프레임에 다시 열리지 않는지 확인해 줘.", false, string.Empty);
        CreateSavePointMarker(root.transform, roomId, "inn.save_crystal", new Vector3(-2.2f, 0.35f, 0f));
        CreateItemMarker(root.transform, roomId, "inn.table_item", new Vector3(-1.2f, -1.25f, 0f), "debug_room_key", 1, "* 낡은 debug_room_key를 얻었다.\n* 집 안의 잠긴 문 테스트에 쓰는 척하는 아이템이다.", "mapfield.inn.item.room_key");
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
        CreateVendorMarker(root.transform, roomId, "shop.main_counter", new Vector3(0.3f, 0.35f, 0f), "vendor.mapfield.shopkeeper", "shop.debug.general_store");
        CreateSignMarker(root.transform, roomId, "shop.price_sign", new Vector3(-2.2f, 1.1f, 0f), "* 모든 물건 0G.\n* 아직 Shop UI는 Debug.Log fallback이라 콘솔을 확인하자.", false, string.Empty);
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
        CreateSignMarker(root.transform, roomId, "house.bookshelf_note", new Vector3(2.0f, 0.35f, 0f), "* 책장 뒤에 지름길 문 도면이 끼어 있다.\n* 잠긴 ShortcutDoor와 플래그 조건 테스트용 문서다.", false, string.Empty);
        CreateItemMarker(root.transform, roomId, "house.bed_item", new Vector3(-1.9f, 0.2f, 0f), "debug_sleep_token", 1, "* debug_sleep_token을 얻었다.\n* 집 안 아이템 원샷 테스트.", "mapfield.house.item.sleep_token");
        CreateShortcutDoorMarker(root.transform, roomId, "house.shortcut_locked", new Vector3(2.75f, -0.9f, 0f), "shortcut.house.forest", "shortcut.forest.house", "mapfield.village.lantern_puzzle.solved");
        CreateSpawnPoint("Spawn_From_ForestShortcut", root.transform, "from_forest_shortcut", new Vector3(2.2f, -0.9f, 0f), FacingDirection.Left);
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
        CreateHazardMarker(root.transform, roomId, "forest.thorn_patch", new Vector3(-2.0f, -0.9f, 0f), 7, 0.8f, true);
        CreatePuzzleMarker(root.transform, roomId, "forest.rock_switch", new Vector3(-0.8f, -0.25f, 0f), "mapfield.forest.rock_switch.solved");
        CreateShortcutDoorMarker(root.transform, roomId, "forest.shortcut_locked", new Vector3(-3.1f, 1.0f, 0f), "shortcut.forest.house", "shortcut.house.forest", "mapfield.village.lantern_puzzle.solved");
        CreateSpawnPoint("Spawn_From_HouseShortcut", root.transform, "from_house_shortcut", new Vector3(-2.5f, 1.0f, 0f), FacingDirection.Right);
        CreatePlotPointMarker(root.transform, roomId, "forest.ambush_warning", new Vector3(2.2f, 0.95f, 0f), AreaPlotTriggerMode.OnInteract, "* 숲 안쪽에서 낯선 구조체 소리가 들린다.\n* 앞쪽의 위험 구역을 확인하자.", false, string.Empty);
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
        CreateSignMarker(root.transform, roomId, "dungeon.warning_sign", new Vector3(-1.2f, 1.55f, 0f), "* 앞은 아직 정비되지 않은 던전 구역이다.", false, string.Empty);
        CreateHazardMarker(root.transform, roomId, "dungeon.ember_floor", new Vector3(0.55f, 0.45f, 0f), 12, 0.65f, false);
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
        DoorTransition legacyTransition = door.GetComponent<DoorTransition>();
        if (legacyTransition != null) Object.DestroyImmediate(legacyTransition, true);

        AreaConnectionMarker transition = door.GetComponent<AreaConnectionMarker>();
        if (transition == null) transition = door.gameObject.AddComponent<AreaConnectionMarker>();

        SerializedObject serialized = new SerializedObject(transition);
        serialized.Update();
        serialized.FindProperty("markerId").stringValue = $"{sourceRoom.RoomId}.{doorName}";
        serialized.FindProperty("areaId").stringValue = sourceRoom.RoomId;
        serialized.FindProperty("markerType").enumValueIndex = (int)AreaMarkerType.Connection;
        serialized.FindProperty("displayName").stringValue = doorName;
        serialized.FindProperty("description").stringValue = $"{sourceRoom.RoomId}에서 {targetRoom.RoomId}로 이동";
        serialized.FindProperty("isOneShot").boolValue = false;
        serialized.FindProperty("interactionRange").floatValue = 1.5f;
        serialized.FindProperty("activationMode").enumValueIndex = (int)DoorActivationMode.OnTriggerEnter;
        serialized.FindProperty("interactToUse").boolValue = false;
        serialized.FindProperty("oneShotUntilExit").boolValue = true;
        ConfigureRoomTransition(serialized, targetRoom, targetSpawnId, facing);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static void WireShortcutDoor(RoomDefinition sourceRoom, RoomDefinition targetRoom, string markerName, string targetSpawnId, FacingDirection facing)
    {
        string prefabPath = AssetDatabase.GetAssetPath(sourceRoom.RoomPrefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError($"[RoomMapSampleBuilder] Shortcut Room Prefab 경로를 찾지 못했습니다: {sourceRoom.RoomId}");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Transform markerTransform = prefabRoot.transform.Find(markerName);
        ShortcutDoorMarker shortcut = markerTransform != null
            ? markerTransform.GetComponent<ShortcutDoorMarker>()
            : null;
        if (shortcut == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError($"[RoomMapSampleBuilder] ShortcutDoorMarker를 찾지 못했습니다: {prefabPath}/{markerName}");
            return;
        }

        SerializedObject serialized = new SerializedObject(shortcut);
        serialized.Update();
        ConfigureRoomTransition(serialized, targetRoom, targetSpawnId, facing);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static void ConfigureRoomTransition(SerializedObject serialized, RoomDefinition targetRoom, string targetSpawnId, FacingDirection facing)
    {
        serialized.FindProperty("mapTransition.TransitionType").enumValueIndex = (int)MapTransitionType.Room;
        serialized.FindProperty("mapTransition.TargetRoom").objectReferenceValue = targetRoom;
        serialized.FindProperty("mapTransition.TargetSpawnPointId").stringValue = targetSpawnId;
        serialized.FindProperty("mapTransition.FacingAfterEnter").enumValueIndex = (int)facing;
        serialized.FindProperty("mapTransition.FadeDuration").floatValue = 0.15f;
    }
    private static void CreateScene(string scenePath, RoomDefinition initialRoom, string sceneName, Color background)
    {
        EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = sceneName;

        CameraController cameraController = CreateGameplayCameraRig(scene, background);

        GameObject systems = new GameObject("Map Systems");
        RoomContainer roomContainer = systems.AddComponent<RoomContainer>();
        MapTransitionService transitionService = systems.AddComponent<MapTransitionService>();
        SetPrivateObject(roomContainer, "_initialRoom", initialRoom);
        SetPrivateBool(roomContainer, "_loadInitialRoomOnStart", true);
        SetPrivateObject(transitionService, "_roomContainer", roomContainer);

        GameObject player = CreateSamplePlayer();
        player.transform.position = new Vector3(-2.5f, 0f, 0f);

        BindGameplayCamera(cameraController, player.transform);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static CameraController CreateGameplayCameraRig(Scene scene, Color background)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DevelopmentContentPaths.GameplayCameraRigPrefab);
        if (prefab == null)
            throw new InvalidDataException("GameplayCameraRig 프리팹을 찾지 못했습니다.");

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
            throw new InvalidDataException("GameplayCameraRig 프리팹 생성에 실패했습니다.");

        instance.name = "[Camera]";
        Camera worldCamera = instance.GetComponentInChildren<Camera>(true);
        CameraController controller = instance.GetComponentInChildren<CameraController>(true);
        if (worldCamera == null || controller == null || controller.VirtualCamera == null)
            throw new InvalidDataException("GameplayCameraRig 구성 요소가 누락되었습니다.");

        worldCamera.clearFlags = CameraClearFlags.SolidColor;
        worldCamera.backgroundColor = background;
        worldCamera.orthographic = true;
        worldCamera.transform.position = new Vector3(0f, 0f, -1f);
        return controller;
    }

    private static void BindGameplayCamera(CameraController controller, Transform target)
    {
        if (controller == null || controller.VirtualCamera == null || target == null)
            return;

        controller.VirtualCamera.Follow = target;
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("_centerTarget").objectReferenceValue = target;
        serialized.FindProperty("_defaultLensSize").floatValue =
            CameraLensDefaults.GameplayOrthographicSize;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
        door.AddComponent<AreaConnectionMarker>();
        return door;
    }

    private static T CreateAreaMarker<T>(Transform parent, string roomId, string localId, Vector3 localPosition, string displayName, string description, bool oneShot, string completeFlag)
        where T : AreaMarkerBase
    {
        GameObject markerObject = new GameObject($"Marker_{displayName}");
        markerObject.layer = 6;
        markerObject.transform.SetParent(parent);
        markerObject.transform.localPosition = localPosition;

        CircleCollider2D trigger = markerObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.35f;

        T marker = markerObject.AddComponent<T>();
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("markerId").stringValue = $"{roomId}.{localId}";
        serialized.FindProperty("areaId").stringValue = roomId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("isOneShot").boolValue = oneShot;
        serialized.FindProperty("setFlagOnComplete").stringValue = completeFlag ?? string.Empty;
        serialized.FindProperty("interactionRange").floatValue = 1.35f;
        serialized.FindProperty("showLabelInSceneView").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return marker;
    }

    private static void CreateNPCMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string fallbackText, bool oneShot, string completeFlag)
    {
        NPCMarker marker = CreateAreaMarker<NPCMarker>(parent, roomId, localId, localPosition, localId, "NPC dialogue test marker", oneShot, completeFlag);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("npcId").stringValue = localId;
        serialized.FindProperty("dialogueId").stringValue = $"{roomId}.{localId}.dialogue";
        serialized.FindProperty("fallbackDialogueText").stringValue = fallbackText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSignMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string text, bool oneShot, string completeFlag)
    {
        SignMarker marker = CreateAreaMarker<SignMarker>(parent, roomId, localId, localPosition, localId, "Sign dialogue test marker", oneShot, completeFlag);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("signText").stringValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateItemMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string itemId, int amount, string pickupMessage, string completeFlag)
    {
        ItemPickupMarker marker = CreateAreaMarker<ItemPickupMarker>(parent, roomId, localId, localPosition, localId, "Item pickup dialogue and one-shot test marker", true, completeFlag);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("amount").intValue = amount;
        serialized.FindProperty("pickupMessage").stringValue = pickupMessage;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePuzzleMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string solvedFlag)
    {
        PuzzleMarker marker = CreateAreaMarker<PuzzleMarker>(parent, roomId, localId, localPosition, localId, "Puzzle flag test marker", false, string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("puzzleId").stringValue = localId;
        serialized.FindProperty("solvedFlag").stringValue = solvedFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSequencePuzzles(
        GameObject roomRoot,
        string dataFolder,
        string prefabName)
    {
        PuzzleMarker[] markers = roomRoot.GetComponentsInChildren<PuzzleMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            PuzzleMarker marker = markers[i];
            SerializedObject markerObject = new SerializedObject(marker);
            string puzzleId = markerObject.FindProperty("puzzleId").stringValue;
            string completionFlag = markerObject.FindProperty("solvedFlag").stringValue;
            if (string.IsNullOrWhiteSpace(puzzleId))
                puzzleId = prefabName + ".puzzle." + (i + 1);
            if (string.IsNullOrWhiteSpace(completionFlag))
                completionFlag = puzzleId + ".solved";

            string definitionPath =
                $"{dataFolder}/{prefabName}_Puzzle_{i + 1}.asset";
            SequencePuzzleDefinition definition =
                AssetDatabase.LoadAssetAtPath<SequencePuzzleDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SequencePuzzleDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            string nodeA = puzzleId + ".a";
            string nodeB = puzzleId + ".b";
            string nodeC = puzzleId + ".c";
            definition.Configure(
                puzzleId,
                new[] { nodeA, nodeB, nodeC },
                completionFlag,
                0.6f);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);

            GameObject controllerObject = new GameObject("Sequence Controller");
            controllerObject.transform.SetParent(marker.transform.parent, false);
            SequencePuzzleController controller =
                controllerObject.AddComponent<SequencePuzzleController>();
            controller.Configure(definition);

            markerObject.FindProperty("puzzleRuntimeSource").objectReferenceValue = controller;
            markerObject.FindProperty("solvedFlag").stringValue = string.Empty;
            markerObject.FindProperty("fallbackInstructionText").stringValue =
                "* 아래 스위치를 A, B, C 순서로 작동시킨다.\n* 틀리면 잠시 뒤 처음부터 다시 시작한다.";
            markerObject.ApplyModifiedPropertiesWithoutUndo();

            CreateSequenceSwitch(marker.transform, "A", nodeA, new Vector3(-0.8f, -0.9f, 0f), controller, new Color(0.82f, 0.28f, 0.24f));
            CreateSequenceSwitch(marker.transform, "B", nodeB, new Vector3(0f, -0.9f, 0f), controller, new Color(0.34f, 0.74f, 0.40f));
            CreateSequenceSwitch(marker.transform, "C", nodeC, new Vector3(0.8f, -0.9f, 0f), controller, new Color(0.30f, 0.52f, 0.82f));
        }
    }

    private static void CreateSequenceSwitch(
        Transform parent,
        string label,
        string nodeId,
        Vector3 localPosition,
        SequencePuzzleController controller,
        Color color)
    {
        GameObject switchObject = new GameObject("PuzzleSwitch_" + label);
        switchObject.layer = 6;
        switchObject.transform.SetParent(parent, false);
        switchObject.transform.localPosition = localPosition;
        CircleCollider2D collider = switchObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;
        PuzzleSwitch puzzleSwitch = switchObject.AddComponent<PuzzleSwitch>();
        puzzleSwitch.Configure(nodeId, controller);
        CreateBlock("SwitchVisual_" + label, switchObject.transform, Vector3.zero, new Vector3(0.45f, 0.45f, 1f), color);
    }
    private static void CreateVendorMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string vendorId, string shopId)
    {
        VendorMarker marker = CreateAreaMarker<VendorMarker>(parent, roomId, localId, localPosition, localId, "Vendor fallback test marker", false, string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("vendorId").stringValue = vendorId;
        serialized.FindProperty("shopId").stringValue = shopId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSavePointMarker(Transform parent, string roomId, string localId, Vector3 localPosition)
    {
        SavePointMarker marker = CreateAreaMarker<SavePointMarker>(parent, roomId, localId, localPosition, localId, "Save point fallback test marker", false, string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("savePointId").stringValue = $"{roomId}.{localId}";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateHazardMarker(Transform parent, string roomId, string localId, Vector3 localPosition, int damage, float knockback, bool triggerOnEnter)
    {
        HazardMarker marker = CreateAreaMarker<HazardMarker>(parent, roomId, localId, localPosition, localId, "Hazard trigger/interact test marker", false, string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("damage").intValue = damage;
        serialized.FindProperty("knockback").floatValue = knockback;
        serialized.FindProperty("triggerOnEnter").boolValue = triggerOnEnter;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePlotPointMarker(Transform parent, string roomId, string localId, Vector3 localPosition, AreaPlotTriggerMode triggerMode, string fallbackText, bool oneShot, string completeFlag)
    {
        PlotPointMarker marker = CreateAreaMarker<PlotPointMarker>(parent, roomId, localId, localPosition, localId, "Plot/cutscene fallback test marker", oneShot, completeFlag);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("plotId").stringValue = localId;
        serialized.FindProperty("triggerMode").enumValueIndex = (int)triggerMode;
        serialized.FindProperty("fallbackDialogueText").stringValue = fallbackText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateShortcutDoorMarker(Transform parent, string roomId, string localId, Vector3 localPosition, string doorId, string linkedDoorId, string unlockFlag)
    {
        ShortcutDoorMarker marker = CreateAreaMarker<ShortcutDoorMarker>(parent, roomId, localId, localPosition, localId, "Locked shortcut test marker", false, string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        serialized.FindProperty("doorId").stringValue = doorId;
        serialized.FindProperty("linkedDoorId").stringValue = linkedDoorId;
        serialized.FindProperty("isLocked").boolValue = true;
        serialized.FindProperty("unlockFlag").stringValue = unlockFlag;
        serialized.FindProperty("activationMode").enumValueIndex = (int)DoorActivationMode.OnInteract;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
            + "기획자가 필드/마을/실내/던전 입구 연결 흐름을 빠르게 확인할 수 있는 기본 맵팩입니다. 특정 상용 게임의 명칭/구조를 그대로 복제하지 않고, 작은 룸을 연결하는 RPG식 흐름을 검증하는 예시입니다.\n\n"
            + "## 구성\n\n"
            + $"- 루트: `{StarterPackRoot}`\n"
            + "- Region Scene: `Scenes/Region_MapFieldStarter.unity`\n"
            + "- RoomDefinition: `Prefabs/Rooms` (각 Room Prefab 옆 `_Definition.asset`)\n"
            + "- Room Prefab: `Prefabs/Rooms`\n"
            + "- AreaConnectionMarker: Gate <-> Village <-> Inn / Shop / House / ForestPath <-> DungeonEntrance\n"
            + "- 테스트용 Area Marker: NPC, Sign, Item, SavePoint, Vendor, Puzzle, Hazard, ShortcutDoor, PlotPoint\n"
            + "## 기본 생성 룸 7개\n\n"
            + "1. `Room_MapField_Gate`\n"
            + "2. `Room_MapField_Village`\n"
            + "3. `Room_MapField_Inn`\n"
            + "4. `Room_MapField_Shop`\n"
            + "5. `Room_MapField_House`\n"
            + "6. `Room_MapField_ForestPath`\n"
            + "7. `Room_MapField_DungeonEntrance`\n\n"
            + "## 버그 탐색 루트\n\n"
            + "1. Gate: 입장 PlotPoint 자동 발동과 welcome Sign one-shot을 확인합니다.\n"
            + "2. Village: 반복 NPC, 아이템 one-shot, 퍼즐 플래그, 상점 fallback을 확인합니다.\n"
            + "3. Inn/Shop/House: 실내 이동 후 대화 종료 입력 재소비, SavePoint fallback, ShortcutDoor 잠금 조건을 확인합니다.\n"
            + "4. ForestPath: 접촉 Hazard와 Z 상호작용 PlotPoint/ShortcutDoor를 확인합니다.\n"
            + "5. DungeonEntrance: 출입 연결, 충돌 영역, 비반복 Hazard 동작을 확인합니다.\n\n"
            + "## 기획자 체크 방법\n\n"
            + "1. `Region_MapFieldStarter.unity`를 엽니다.\n"
            + "2. Hierarchy의 `Map Systems`에서 초기 RoomDefinition을 확인합니다.\n"
            + "3. `Prefabs/Rooms`의 RoomDefinition을 열어 룸 ID, 프리팹, BGM 설정을 확인합니다.\n"
            + "4. 문 이동은 각 룸 프리팹 안의 `AreaConnectionMarker` 컴포넌트에서 MapTransition.TargetRoom/TargetSpawnPointId로 확인합니다.\n"
            + "5. 각 Room Prefab의 `Marker_*` 오브젝트가 Interactable 레이어와 Trigger Collider를 갖는지 확인합니다.\n"
            + "6. 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 연결 누락을 확인합니다.\n";

        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        AssetDatabase.ImportAsset(path);
    }

    [MenuItem("Hub To Home/오버월드/맵 문서/기획자용 맵 가이드 생성")]
    public static void CreateDesignerGuide()
    {
        WriteDesignerGuide();
    }

    private static void EnsureDesignerGuide()
    {
        if (File.Exists(DesignerGuidePath))
            return;

        WriteDesignerGuide();
    }

    private static void WriteDesignerGuide()
    {
        EnsureFolder(SceneWorldRoot);

        EnsureFolder(DevelopmentRoot);

        string content = "# Overworld Map Guide\n\n"
            + "오버월드 맵은 **큰 지역 Scene** 안에서 **작은 Room Prefab**을 갈아 끼우는 방식으로 관리합니다. 델타룬처럼 한 화면 단위의 방/통로/실내를 연결하는 구조를 목표로 합니다.\n\n"
            + "## 폴더 기준\n\n"
            + "- `Assets/_Game/Scripts/Overworld/Runtime/Map`: 개발자가 관리하는 맵 전환 런타임 코드\n"
            + "- `Assets/_Game/Scripts/Overworld/Editor`: 샘플/템플릿 생성기와 검사 도구\n"
            + "- `Assets/_Game/Content/Maps/Development/Regions/Title`: 현재 개발용 타이틀과 인트로 씬 위치\n"
            + "- `Assets/_Game/Content/Maps/Battle`: 전투 전용 씬 위치\n"
            + "- `Assets/_Game/Content/Maps/Regions`: 실제 지역 Scene, RoomDefinition, Room Prefab 생성 위치\n"
            + "- `Assets/_Game/Content/Maps/Development`: QA와 기능 검증용 맵 위치\n"
            + "- `Assets/_Game/Content/Maps/Shared`: 여러 맵이 함께 쓰는 마커, 타일, 생성 리소스\n\n"
            + "## 핵심 용어\n\n"
            + "- **Region Scene**: 하나의 큰 지역 씬입니다. 예: 마을 지역, 숲 지역, 던전 입구 지역.\n"
            + "- **RoomDefinition**: 룸 ID, 룸 프리팹, BGM 설정을 담는 데이터입니다. 기획자가 가장 먼저 확인할 데이터입니다.\n"
            + "- **Room Prefab**: 실제 바닥, 벽, 문, 스폰 지점, NPC가 들어가는 한 화면 단위 맵입니다.\n"
            + "- **AreaConnectionMarker**: 문/통로/계단 Area Marker입니다. 어느 Room/Scene으로 이동할지와 도착 SpawnPoint를 지정합니다.\n"
            + "- **SpawnPoint**: 이동 후 플레이어가 서는 위치와 바라볼 방향입니다.\n\n"
            + "## 제작 흐름\n\n"
            + "1. Unity 메뉴 `HubToHome > 오버월드 > 맵 생성 > 맵 필드 스타터팩 생성`을 실행합니다.\n"
            + "2. `Assets/_Game/Content/Maps/Development/Templates/MapFieldStarter/Scenes/Region_MapFieldStarter.unity`를 엽니다.\n"
            + "3. `Prefabs/Rooms`에서 Room Prefab 옆의 RoomDefinition으로 룸 목록과 BGM을 확인합니다.\n"
            + "4. `Prefabs/Rooms`의 Room Prefab을 열어 바닥/벽/문/NPC/이벤트를 배치합니다.\n"
            + "5. 문을 추가하면 `AreaConnectionMarker.MapTransition.TargetRoom`과 `TargetSpawnPointId`를 맞춥니다.\n"
            + "6. 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 누락된 연결을 확인합니다.\n\n"
            + "## 이름 규칙\n\n"
            + "- Room ID: `지역.장소` 형식. 예: `mapfield.village`, `forest.entrance`\n"
            + "- SpawnPoint ID: `from_출발지` 또는 `to_목적지` 형식. 예: `from_gate`, `to_inn`\n"
            + "- Room Prefab: `Room_지역_장소` 형식. 예: `Room_MapField_Village`\n"
            + "- Region Scene: `Region_지역명` 형식. 예: `Region_MapFieldStarter`\n\n"
            + "## 판단 기준\n\n"
            + "- 같은 큰 지역 안의 방/실내/통로 이동은 `Room` 전환을 사용합니다.\n"
            + "- 완전히 다른 지역, 전투 전용 씬, 타이틀 등으로 넘어갈 때는 `Scene` 전환을 사용합니다.\n"
            + "- 기획 문서에는 RoomDefinition 기준으로 룸 목록과 연결표를 적으면 됩니다.\n";

        File.WriteAllText(DesignerGuidePath, content, System.Text.Encoding.UTF8);
        AssetDatabase.ImportAsset(DesignerGuidePath);
        AssetDatabase.Refresh();
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
