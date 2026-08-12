using System;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TestMap을 오버월드 기능과 스프라이트 비율을 한곳에서 검증하는 QA 쇼케이스로 구성합니다.
/// 생성 산출물은 TestMap 폴더에만 두고, 재실행 시 __TEST_MAP_QA__ 루트만 교체합니다.
/// </summary>
public static class TestMapShowcaseBuilder
{
    private const string ScenePath = DevelopmentContentPaths.TestMapScene;
    private const string GeneratedRootName = "__TEST_MAP_QA__";
    private const string TestMapRoot = DevelopmentContentPaths.TestMapRoot;
    private const string MarkerPrefabRoot = TestMapRoot + "/Prefabs/Markers";
    private const string NpcPrefabRoot = TestMapRoot + "/Prefabs/NPC";
    private const string LabPrefabRoot = TestMapRoot + "/Prefabs/Labs";
    private const string DataRoot = TestMapRoot + "/Data";
    private const string PuzzleDefinitionPath = DataRoot + "/QA_Puzzle_Sequence.asset";
    private const string ShopDefinitionPath = DataRoot + "/QA_Shop_DebugInventory.asset";
    private const string DebugItemPath = DataRoot + "/QA_Item_DebugTonic.asset";
    private const string ContentCatalogPath = "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";
    private const string PlayerPrefabPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
    private const string BootstrapPrefabPath = "Assets/_Game/Core/Prefabs/[GameBootstrap].prefab";
    private const string MarkerEnemyDataPath = "Assets/_Game/Content/Characters/EnemyDB/DB_Slime.asset";
    private const string BunnySlimePrefabPath = "Assets/_Game/Content/Characters/Prefabs/Enemy/BunnySlime.prefab";
    private const string TestNpcSpritePath = DevelopmentContentPaths.TestNpcSprite;
    private const string WhiteSpritePath = "Assets/_Game/Content/Maps/Shared/Generated/RoomMap_WhiteSquare.png";
    private const string LabelFontPath = "Assets/_Game/Presentation/UI/Fonts/Silver SDF.asset";
    private const string AreaId = "testmap.qa";

    private const float HalfWidth = 32f;
    private const float MapBottom = -20f;
    private const float MapTop = 28f;
    private static float MapHeight => MapTop - MapBottom;
    private static float MapCenterY => (MapTop + MapBottom) * 0.5f;

    private static readonly Color BackgroundColor = new Color(0.035f, 0.045f, 0.055f, 1f);
    private static readonly Color HubColor = new Color(0.16f, 0.19f, 0.22f, 1f);
    private static readonly Color NpcZoneColor = new Color(0.10f, 0.24f, 0.22f, 1f);
    private static readonly Color ScaleZoneColor = new Color(0.20f, 0.17f, 0.23f, 1f);
    private static readonly Color SystemZoneColor = new Color(0.20f, 0.22f, 0.16f, 1f);
    private static readonly Color CombatZoneColor = new Color(0.25f, 0.13f, 0.14f, 1f);
    private static readonly Color SpriteDropZoneColor = new Color(0.12f, 0.15f, 0.24f, 1f);
    private static readonly Color WallColor = new Color(0.48f, 0.52f, 0.57f, 1f);
    private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.92f, 1f);

    private static Sprite _whiteSprite;
    private static Sprite _testNpcSprite;
    private static Sprite _playerSprite;
    private static TMP_FontAsset _labelFont;

    private enum StationVisual
    {
        Npc,
        Enemy,
        Hazard,
        Puzzle,
        Vendor,
        Door,
        Item,
        Sign,
        Save,
        Plot,
        Sublocation,
        Connection
    }

    [MenuItem("Hub To Home/오버월드/맵 생성/TestMap QA 쇼케이스 재생성")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void Build()
    {
        EnsureFolder(MarkerPrefabRoot);
        EnsureFolder(NpcPrefabRoot);
        EnsureFolder(LabPrefabRoot);
        EnsureFolder(DataRoot);
        EnsureSceneInBuildSettings();
        LoadSourceAssets();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveGeneratedRoot(scene);

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        GameObject systemsRoot = CreateGroup("00_SYSTEMS_AND_SPAWNS", generatedRoot.transform);
        GameObject environmentRoot = CreateGroup("10_ENVIRONMENT", generatedRoot.transform);
        GameObject labelsRoot = CreateGroup("20_ZONE_LABELS", generatedRoot.transform);
        GameObject gameplayRoot = CreateGroup("30_GAMEPLAY_STATIONS", generatedRoot.transform);
        GameObject labsRoot = CreateGroup("40_VISUAL_LABS", generatedRoot.transform);
        GameObject hierarchyNotes = CreateGroup("99_README__START_AT_CENTER__TEST_CLOCKWISE", generatedRoot.transform);
        hierarchyNotes.SetActive(false);

        PlayerController player = EnsurePlayer(scene);
        EnsureBootstrap(scene);
        GameObject cameraBounds = BuildEnvironment(environmentRoot.transform, labelsRoot.transform);
        ConfigureSystems(systemsRoot.transform, cameraBounds, player);

        Dictionary<string, GameObject> stationPrefabs = BuildStationPrefabs();
        BuildNpcAndDialogueZone(gameplayRoot.transform, stationPrefabs);
        BuildSystemMarkerZone(gameplayRoot.transform, stationPrefabs);
        BuildCombatZone(gameplayRoot.transform, environmentRoot.transform, stationPrefabs);
        BuildVisualLabs(labsRoot.transform);
        CreateSpawnPoints(systemsRoot.transform);
        ConfigureCamera(player);

        ValidateGeneratedScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("[TestMapShowcaseBuilder] TestMap QA 쇼케이스 생성 완료: " + ScenePath);
    }

    private static void LoadSourceAssets()
    {
        _whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        _testNpcSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestNpcSpritePath);
        _labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        SpriteRenderer playerRenderer = playerPrefab != null ? playerPrefab.GetComponent<SpriteRenderer>() : null;
        _playerSprite = playerRenderer != null ? playerRenderer.sprite : null;


        if (_whiteSprite == null)
            throw new InvalidOperationException("TestMap 블록용 공용 스프라이트를 찾지 못했습니다: " + WhiteSpritePath);
        if (_testNpcSprite == null)
            throw new InvalidOperationException("TestNPC 스프라이트를 찾지 못했습니다: " + TestNpcSpritePath);
        if (_labelFont == null)
            throw new InvalidOperationException("TestMap TMP 폰트를 찾지 못했습니다: " + LabelFontPath);
    }

    private static GameObject BuildEnvironment(Transform environment, Transform labels)
    {
        CreateBlock("Map_Background", environment, new Vector3(0f, MapCenterY), new Vector2(HalfWidth * 2f, MapHeight), BackgroundColor, "Background", -1000);

        CreateBlock("Zone_A_NPC_Dialogue", environment, new Vector3(-17f, 10.25f, 0f), new Vector2(28f, 14.5f), NpcZoneColor, "Background", -900);
        CreateBlock("Zone_B_Sprite_Scale", environment, new Vector3(17f, 10.25f, 0f), new Vector2(28f, 14.5f), ScaleZoneColor, "Background", -900);
        CreateBlock("Zone_C_System_Markers", environment, new Vector3(-17f, -10.25f, 0f), new Vector2(28f, 14.5f), SystemZoneColor, "Background", -900);
        CreateBlock("Zone_D_Combat_Collision", environment, new Vector3(17f, -10.25f, 0f), new Vector2(28f, 14.5f), CombatZoneColor, "Background", -900);
        BuildSpriteDropYard(environment, labels);

        CreateBlock("Hub_Vertical_Path", environment, new Vector3(0f, MapCenterY), new Vector2(6f, MapHeight), HubColor, "Background", -850);
        CreateBlock("Hub_Horizontal_Path", environment, Vector3.zero, new Vector2(64f, 6f), HubColor, "Background", -850);
        CreateBlock("Hub_Center", environment, Vector3.zero, new Vector2(8f, 8f), new Color(0.24f, 0.28f, 0.31f), "Background", -800);

        CreateBlock("OuterWall_Top", environment, new Vector3(0f, MapTop - 0.35f), new Vector2(64f, 0.7f), WallColor, "Default", 0, true);
        CreateBlock("OuterWall_Bottom", environment, new Vector3(0f, MapBottom + 0.35f), new Vector2(64f, 0.7f), WallColor, "Default", 0, true);
        CreateBlock("OuterWall_Left", environment, new Vector3(-HalfWidth + 0.35f, MapCenterY), new Vector2(0.7f, MapHeight), WallColor, "Default", 0, true);
        CreateBlock("OuterWall_Right", environment, new Vector3(HalfWidth - 0.35f, MapCenterY), new Vector2(0.7f, MapHeight), WallColor, "Default", 0, true);

        CreateBlock("Divider_North_Left", environment, new Vector3(-17f, 2.75f), new Vector2(25f, 0.35f), new Color(0.38f, 0.76f, 0.66f), "Default", 0);
        CreateBlock("Divider_North_Right", environment, new Vector3(17f, 2.75f), new Vector2(25f, 0.35f), new Color(0.72f, 0.48f, 0.74f), "Default", 0);
        CreateBlock("Divider_South_Left", environment, new Vector3(-17f, -2.75f), new Vector2(25f, 0.35f), new Color(0.69f, 0.74f, 0.40f), "Default", 0);
        CreateBlock("Divider_South_Right", environment, new Vector3(17f, -2.75f), new Vector2(25f, 0.35f), new Color(0.88f, 0.40f, 0.38f), "Default", 0);

        PolygonCollider2D bounds = CreateCameraBounds(environment);

        CreateText("Map_Title", labels, new Vector3(0f, 2f), "OVERWORLD QA MAP", 0.13f, TextColor, 5200);
        CreateText("Controls", labels, new Vector3(0f, 0.85f), "WASD MOVE   Z INTERACT   F FIELD ATTACK   C MENU", 0.055f, new Color(0.78f, 0.84f, 0.88f), 5200);
        CreateText("Zone_A_Title", labels, new Vector3(-17f, 16.65f), "A  NPC + DIALOGUE", 0.085f, new Color(0.54f, 1f, 0.82f), 5200);
        CreateText("Zone_B_Title", labels, new Vector3(17f, 16.65f), "B  SPRITE SCALE + CAMERA", 0.085f, new Color(0.94f, 0.66f, 1f), 5200);
        CreateText("Zone_C_Title", labels, new Vector3(-17f, -3.75f), "C  SYSTEM MARKERS", 0.085f, new Color(0.89f, 0.95f, 0.49f), 5200);
        CreateText("Zone_D_Title", labels, new Vector3(17f, -3.75f), "D  COMBAT + COLLISION", 0.085f, new Color(1f, 0.57f, 0.52f), 5200);
        return bounds.gameObject;
    }

    private static void BuildSpriteDropYard(Transform environment, Transform labels)
    {
        const float yardY = 23f;
        CreateBlock("Zone_E_Sprite_Drop_Yard", environment, new Vector3(0f, yardY), new Vector2(60f, 8.4f), SpriteDropZoneColor, "Background", -900);
        CreateBlock("SpriteDropYard_Baseline", environment, new Vector3(0f, 20.05f), new Vector2(58f, 0.055f), new Color(0.93f, 0.94f, 0.80f, 0.65f), "Default", 35);

        for (int x = -28; x <= 28; x += 2)
        {
            float alpha = x == 0 ? 0.26f : 0.10f;
            CreateBlock("SpriteDropYard_Grid_V_" + x, environment, new Vector3(x, yardY), new Vector2(0.03f, 7.3f), new Color(0.78f, 0.84f, 0.96f, alpha), "Default", 25);
        }

        for (int i = 0; i <= 7; i++)
        {
            float y = 19.5f + i;
            float alpha = i == 0 ? 0.24f : 0.09f;
            CreateBlock("SpriteDropYard_Grid_H_" + i, environment, new Vector3(0f, y), new Vector2(58f, 0.03f), new Color(0.78f, 0.84f, 0.96f, alpha), "Default", 25);
        }

        CreateText("Zone_E_Title", labels, new Vector3(0f, 26.45f), "E  SPRITE DROP YARD", 0.085f, new Color(0.76f, 0.84f, 1f), 5200);
        CreateText("Zone_E_Usage", labels, new Vector3(0f, 25.55f), "DRAG TEMP SPRITES HERE   |   FEET ON BASELINE   |   GRID = 1 WORLD UNIT", 0.047f, new Color(0.86f, 0.89f, 0.98f), 5200);
        CreateText("Zone_E_Ruler", labels, new Vector3(0f, 19.25f), "Use this upper strip for rough sprite scale checks before making prefabs.", 0.04f, new Color(0.80f, 0.84f, 0.91f), 5200);

        CreateSpriteDropSlot(environment, "DropSlot_PlayerNpc", new Vector3(-23.5f, 23f), new Vector2(8f, 6f), "NPC / PLAYER\n1-2u tall", new Color(0.48f, 0.95f, 0.75f));
        CreateSpriteDropSlot(environment, "DropSlot_NormalEnemy", new Vector3(-8f, 23f), new Vector2(8f, 6f), "NORMAL ENEMY\n1-3u tall", new Color(1f, 0.58f, 0.55f));
        CreateSpriteDropSlot(environment, "DropSlot_BossWide", new Vector3(8f, 23f), new Vector2(10f, 6f), "BOSS / WIDE\n2-5u wide", new Color(1f, 0.78f, 0.38f));
        CreateSpriteDropSlot(environment, "DropSlot_PropBg", new Vector3(24f, 23f), new Vector2(8f, 6f), "PROP / BG\nno collider", new Color(0.72f, 0.64f, 1f));
    }

    private static void CreateSpriteDropSlot(Transform parent, string name, Vector3 center, Vector2 size, string label, Color accent)
    {
        Color fill = new Color(accent.r, accent.g, accent.b, 0.075f);
        Color line = new Color(accent.r, accent.g, accent.b, 0.58f);
        CreateBlock(name + "_Fill", parent, center, size, fill, "Default", 30);
        CreateBlock(name + "_Top", parent, center + new Vector3(0f, size.y * 0.5f), new Vector2(size.x, 0.06f), line, "Default", 36);
        CreateBlock(name + "_Bottom", parent, center - new Vector3(0f, size.y * 0.5f), new Vector2(size.x, 0.06f), line, "Default", 36);
        CreateBlock(name + "_Left", parent, center - new Vector3(size.x * 0.5f, 0f), new Vector2(0.06f, size.y), line, "Default", 36);
        CreateBlock(name + "_Right", parent, center + new Vector3(size.x * 0.5f, 0f), new Vector2(0.06f, size.y), line, "Default", 36);
        CreateBlock(name + "_FeetLine", parent, new Vector3(center.x, 20.05f), new Vector2(size.x - 0.6f, 0.08f), new Color(1f, 1f, 1f, 0.55f), "Default", 38);
        CreateText(name + "_Label", parent, center + new Vector3(0f, -2.25f), label, 0.04f, new Color(0.92f, 0.94f, 0.98f), 5200);
    }
    private static PolygonCollider2D CreateCameraBounds(Transform parent)
    {
        GameObject boundsObject = new GameObject("CameraBounds_QA_Map");
        boundsObject.transform.SetParent(parent, false);
        PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
        bounds.isTrigger = true;
        bounds.pathCount = 1;
        bounds.SetPath(0, new[]
        {
            new Vector2(-HalfWidth + 0.7f, MapBottom + 0.7f),
            new Vector2(-HalfWidth + 0.7f, MapTop - 0.7f),
            new Vector2(HalfWidth - 0.7f, MapTop - 0.7f),
            new Vector2(HalfWidth - 0.7f, MapBottom + 0.7f)
        });
        return bounds;
    }

    private static void ConfigureSystems(Transform parent, GameObject cameraBoundsObject, PlayerController player)
    {
        GameObject mapSystems = new GameObject("Map_Runtime_Systems");
        mapSystems.transform.SetParent(parent, false);

        MapTransitionService transitionService = UnityEngine.Object.FindFirstObjectByType<MapTransitionService>();
        if (transitionService == null)
            transitionService = mapSystems.AddComponent<MapTransitionService>();

        SerializedObject transitionSo = new SerializedObject(transitionService);
        SetBool(transitionSo, "_dontDestroyOnLoad", false);
        SetFloat(transitionSo, "_arrivalDoorSuppressSeconds", 0.35f);
        transitionSo.ApplyModifiedPropertiesWithoutUndo();

        MapSettings mapSettings = UnityEngine.Object.FindFirstObjectByType<MapSettings>();
        if (mapSettings == null)
            mapSettings = mapSystems.AddComponent<MapSettings>();

        SerializedObject mapSo = new SerializedObject(mapSettings);
        SetObject(mapSo, "_cameraBounds", cameraBoundsObject.GetComponent<PolygonCollider2D>());
        SetFloat(mapSo, "_bgmFadeDuration", 0.25f);
        mapSo.ApplyModifiedPropertiesWithoutUndo();

        if (player != null)
        {
            player.transform.position = Vector3.zero;
            player.SetFacingDirection((int)FacingDirection.Down);
        }
    }

    private static void ConfigureCamera(PlayerController player)
    {
        CinemachineCamera vcam = UnityEngine.Object.FindFirstObjectByType<CinemachineCamera>();
        if (vcam == null)
            throw new InvalidOperationException("TestMap의 CinemachineCamera를 찾지 못했습니다.");

        if (player != null)
            vcam.Follow = player.transform;

        vcam.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;
    }

    private static PlayerController EnsurePlayer(Scene scene)
    {
        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.gameObject.SetActive(true);
            return player;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Player prefab을 찾지 못했습니다: " + PlayerPrefabPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Player_Base";
        return instance.GetComponent<PlayerController>();
    }

    private static void EnsureBootstrap(Scene scene)
    {
        if (UnityEngine.Object.FindFirstObjectByType<GameBootstrap>() != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("GameBootstrap prefab을 찾지 못했습니다: " + BootstrapPrefabPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "[GameBootstrap]";
    }

    private static Dictionary<string, GameObject> BuildStationPrefabs()
    {
        Dictionary<string, GameObject> result = new Dictionary<string, GameObject>();
        EnemyData markerEnemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(MarkerEnemyDataPath);
        if (markerEnemyData == null)
            throw new InvalidOperationException("전투 마커용 Slime 데이터를 찾지 못했습니다: " + MarkerEnemyDataPath);
        SequencePuzzleDefinition qaPuzzle = CreateOrUpdatePuzzleDefinition();
        ShopDefinition qaShop = CreateOrUpdateDebugShop();

        result["npc_guide"] = CreateStationPrefab<NPCMarker>(
            NpcPrefabRoot + "/QA_NPC_TestGuide.prefab",
            AreaMarkerType.NPC,
            StationVisual.Npc,
            "NPC / REPEAT",
            "qa.npc.guide",
            "QA Guide",
            "반복 대화, 입력 잠금 해제, TestNPC 32px/32 PPU 표시를 검증합니다.",
            new Color(0.35f, 0.95f, 0.62f),
            so =>
            {
                SetString(so, "npcId", "qa_npc_test_guide");
                SetString(so, "dialogueId", "qa_dialogue_test_guide");
                SetString(so, "fallbackDialogueText", "* QA 테스트 맵에 온 걸 환영해.\n* Z로 대사를 넘긴 뒤 바로 이동 가능한지 확인해 줘.\n* 오른쪽 위에서는 32px 스프라이트 배율을 비교할 수 있어.");
            },
            false,
            string.Empty,
            _testNpcSprite);

        result["npc_one_shot"] = CreateStationPrefab<NPCMarker>(
            NpcPrefabRoot + "/QA_NPC_OneShot.prefab",
            AreaMarkerType.NPC,
            StationVisual.Npc,
            "NPC / ONE SHOT",
            "qa.npc.one_shot",
            "One-shot NPC",
            "대화 완료 플래그와 같은 세션 내 재상호작용 차단을 검증합니다.",
            new Color(0.36f, 0.72f, 1f),
            so =>
            {
                SetString(so, "npcId", "qa_npc_one_shot");
                SetString(so, "dialogueId", "qa_dialogue_one_shot");
                SetString(so, "fallbackDialogueText", "* 이 대화는 한 번만 재생돼.\n* 끝난 뒤 다시 Z를 눌러도 열리지 않아야 해.");
            },
            true,
            "qa.testmap.npc.one_shot.complete",
            _testNpcSprite);

        result["sign"] = CreateStationPrefab<SignMarker>(
            MarkerPrefabRoot + "/QA_Marker_Sign.prefab",
            AreaMarkerType.Sign,
            StationVisual.Sign,
            "SIGN / REPEAT",
            "qa.sign.controls",
            "Control Sign",
            "반복 표지판 대화와 줄바꿈을 검증합니다.",
            new Color(0.95f, 0.68f, 0.25f),
            so => SetString(so, "signText", "* 이동: WASD / 방향키\n* 상호작용: Z\n* 필드 선공: F\n* 메뉴: C"));

        result["plot"] = CreateStationPrefab<PlotPointMarker>(
            MarkerPrefabRoot + "/QA_Marker_PlotPoint.prefab",
            AreaMarkerType.PlotPoint,
            StationVisual.Plot,
            "PLOT / ENTER",
            "qa.plot.zone_entry",
            "Auto Plot Point",
            "접촉 자동 대화와 1회성 완료 플래그를 검증합니다.",
            new Color(1f, 0.32f, 0.82f),
            so =>
            {
                SetString(so, "plotId", "qa_plot_npc_zone_entry");
                SetEnum(so, "triggerMode", (int)AreaPlotTriggerMode.OnEnter);
                SetString(so, "fallbackDialogueText", "* 자동 플롯 포인트가 발동했다.\n* 이동이 멈추고, 대화 종료 후 다시 풀려야 한다.");
            },
            true,
            "qa.testmap.plot.entry.complete");

        result["item"] = CreateStationPrefab<ItemPickupMarker>(
            MarkerPrefabRoot + "/QA_Marker_Item.prefab",
            AreaMarkerType.Item,
            StationVisual.Item,
            "ITEM / ONE SHOT",
            "qa.item.debug_tonic",
            "Debug Tonic",
            "아이템 3개 지급, 획득 대화, 원샷 플래그를 검증합니다.",
            Color.white,
            so =>
            {
                SetString(so, "itemId", "qa_debug_tonic");
                SetInt(so, "amount", 3);
                SetString(so, "pickupMessage", "* qa_debug_tonic을 3개 얻었다.\n* 다시 조사하면 획득되지 않아야 한다.");
            },
            true,
            "qa.testmap.item.tonic.picked");

        result["save"] = CreateStationPrefab<SavePointMarker>(
            MarkerPrefabRoot + "/QA_Marker_SavePoint.prefab",
            AreaMarkerType.SavePoint,
            StationVisual.Save,
            "SAVE / SLOT 0",
            "qa.save.manual",
            "QA Save Point",
            "현재 위치 저장과 저장 슬롯 0 기록을 검증합니다.",
            new Color(0.24f, 1f, 1f),
            so =>
            {
                SetString(so, "savePointId", "qa_testmap_save_0");
                SetInt(so, "quickSaveSlot", 0);
                SetBool(so, "autoSaveOnPass", false);
            });

        result["puzzle"] = CreateStationPrefab<PuzzleMarker>(
            MarkerPrefabRoot + "/QA_Marker_Puzzle.prefab",
            AreaMarkerType.Puzzle,
            StationVisual.Puzzle,
            "PUZZLE / A-B-C",
            "qa.puzzle.sequence",
            "Sequence Console",
            "A, B, C 순서 입력·오답 초기화·저장 플래그·숏컷 해제를 검증합니다.",
            new Color(0.70f, 0.42f, 1f),
            so => SetString(so, "puzzleId", "qa_testmap_shortcut_switch"),
            configureRoot: (root, marker) => ConfigurePuzzleStation(root, marker, qaPuzzle));

        result["vendor"] = CreateStationPrefab<VendorMarker>(
            MarkerPrefabRoot + "/QA_Marker_Vendor.prefab",
            AreaMarkerType.Vendor,
            StationVisual.Vendor,
            "VENDOR / BUY-SELL",
            "qa.vendor.counter",
            "QA Vendor",
            "Vendor 진입, 구매·판매·취소·재진입과 저장 연동을 검증합니다.",
            new Color(1f, 0.84f, 0.25f),
            so =>
            {
                SetString(so, "vendorId", "qa_vendor_test_counter");
                SetString(so, "shopId", qaShop.ShopId);
                so.FindProperty("shopDefinition").objectReferenceValue = qaShop;
            });

        result["shortcut"] = CreateStationPrefab<ShortcutDoorMarker>(
            MarkerPrefabRoot + "/QA_Marker_ShortcutDoor.prefab",
            AreaMarkerType.ShortcutDoor,
            StationVisual.Door,
            "SHORTCUT / LOCKED",
            "qa.shortcut.locked",
            "Locked Shortcut",
            "Puzzle 플래그 전후 잠금과 TestMap 자기 전환을 검증합니다.",
            new Color(0.25f, 1f, 0.78f),
            so =>
            {
                SetString(so, "doorId", "qa_shortcut_a");
                SetString(so, "linkedDoorId", "qa_shortcut_b");
                SetBool(so, "isLocked", true);
                SetString(so, "unlockFlag", "qa.testmap.puzzle.solved");
                ConfigureSceneTransition(so, "qa.testmap.spawn.shortcut");
            });

        result["connection"] = CreateStationPrefab<AreaConnectionMarker>(
            MarkerPrefabRoot + "/QA_Marker_Connection.prefab",
            AreaMarkerType.Connection,
            StationVisual.Connection,
            "CONNECTION / LOOP",
            "qa.connection.loop",
            "Scene Loop Door",
            "SceneLoader 페이드, TestMap 재로드, SpawnPoint 도착을 검증합니다.",
            new Color(0.28f, 0.72f, 1f),
            so => ConfigureSceneTransition(so, "qa.testmap.spawn.connection"));

        result["sublocation"] = CreateStationPrefab<SublocationMarker>(
            MarkerPrefabRoot + "/QA_Marker_Sublocation.prefab",
            AreaMarkerType.Sublocation,
            StationVisual.Sublocation,
            "SUBLOCATION / LOOP",
            "qa.sublocation.loop",
            "Sublocation Loop",
            "Sublocation ID와 Scene/Area/Spawn 전달을 검증합니다.",
            new Color(0.60f, 0.78f, 1f),
            so =>
            {
                SetString(so, "sublocationId", "qa_testmap_subroom");
                SetString(so, "targetSceneName", "TestMap");
                SetString(so, "targetAreaId", AreaId);
                SetString(so, "targetSpawnId", "qa.testmap.spawn.sublocation");
                SetString(so, "returnSpawnPointId", "qa.testmap.spawn.sublocation");
                SetFloat(so, "fadeDuration", 0.2f);
            });

        result["hazard"] = CreateStationPrefab<HazardMarker>(
            MarkerPrefabRoot + "/QA_Marker_Hazard.prefab",
            AreaMarkerType.Hazard,
            StationVisual.Hazard,
            "HAZARD / TOUCH",
            "qa.hazard.knockback",
            "Knockback Hazard",
            "접촉 HP 피해·넉백·재피격 지연과 HP 0 GameOver를 검증합니다.",
            new Color(1f, 0.43f, 0.17f),
            so =>
            {
                SetInt(so, "damage", 10);
                SetFloat(so, "knockback", 1.25f);
                SetBool(so, "triggerOnEnter", true);
            });

        result["enemy"] = CreateStationPrefab<OverworldEnemyMarker>(
            MarkerPrefabRoot + "/QA_Marker_Enemy.prefab",
            AreaMarkerType.Enemy,
            StationVisual.Enemy,
            "ENEMY / Z BATTLE",
            "qa.enemy.slime.marker",
            "Slime Marker Encounter",
            "Area Enemy Marker의 Z 상호작용과 전용 BattleScene 왕복을 검증합니다.",
            new Color(1f, 0.25f, 0.28f),
            so =>
            {
                SetString(so, "enemyId", "qa_enemy_slime_marker");
                SetInt(so, "enemyLevel", 1);
                SetString(so, "battleEncounterId", "qa.testmap.encounter.slime_marker");
                SetObject(so, "enemyData", markerEnemyData);
                SetBool(so, "useDedicatedBattleScene", true);
                SetString(so, "battleSceneName", "BattleScene");
                SetFloat(so, "battleFadeDuration", 0.08f);
            },
            false,
            string.Empty,
            _testNpcSprite);

        return result;
    }

    private static GameObject CreateStationPrefab<T>(
        string path,
        AreaMarkerType markerType,
        StationVisual visual,
        string label,
        string markerId,
        string displayName,
        string description,
        Color accent,
        Action<SerializedObject> configure,
        bool oneShot = false,
        string completionFlag = "",
        Sprite characterSprite = null,
        Action<GameObject, T> configureRoot = null)
        where T : AreaMarkerBase
    {
        GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        root.layer = ResolveLayer("Interactable");
        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = markerType == AreaMarkerType.Hazard || markerType == AreaMarkerType.PlotPoint ? 0.85f : 0.58f;

        T marker = root.AddComponent<T>();
        SerializedObject so = new SerializedObject(marker);
        SetString(so, "markerId", markerId);
        SetString(so, "areaId", AreaId);
        SetEnum(so, "markerType", (int)markerType);
        SetString(so, "displayName", displayName);
        SetString(so, "description", description);
        SetBool(so, "isOneShot", oneShot);
        SetString(so, "setFlagOnComplete", completionFlag);
        SetFloat(so, "interactionRange", 1.5f);
        SetBool(so, "showLabelInSceneView", true);
        SetColor(so, "gizmoColor", accent);
        configure?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        configureRoot?.Invoke(root, marker);

        BuildStationVisual(root.transform, markerType, visual, label, accent, oneShot, characterSprite);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static ShopDefinition CreateOrUpdateDebugShop()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(DebugItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, DebugItemPath);
        }

        item.ItemID = "qa_debug_tonic";
        item.ItemName = "QA Debug Tonic";
        item.Description = "TestMap 구매·판매·저장 검증용 아이템";
        item.Type = ItemType.Consumable;
        item.IsStackable = true;
        item.MaxStackSize = 99;
        item.IsSellable = true;
        item.Price = 20;
        item.ActionType = EffectActionType.Heal;
        item.TargetStat = TargetStatType.HP;
        item.CalcType = ValueCalcType.Flat;
        item.EffectValue = 10;
        EditorUtility.SetDirty(item);

        ShopDefinition shop =
            AssetDatabase.LoadAssetAtPath<ShopDefinition>(ShopDefinitionPath);
        if (shop == null)
        {
            shop = ScriptableObject.CreateInstance<ShopDefinition>();
            AssetDatabase.CreateAsset(shop, ShopDefinitionPath);
        }

        shop.Configure(
            "qa_shop_debug_inventory",
            "QA Buy / Sell Counter",
            new[]
            {
                new ShopEntry(
                    "debug_tonic",
                    item,
                    10,
                    1,
                    0,
                    "qa.testmap.shop.debug_tonic.purchases")
            });
        EditorUtility.SetDirty(shop);
        RegisterCatalogItem(item);
        return shop;
    }

    private static void RegisterCatalogItem(ItemData item)
    {
        GameContentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ContentCatalogPath);
        if (catalog == null)
            throw new InvalidOperationException("GameContentCatalog을 찾지 못했습니다: " + ContentCatalogPath);

        catalog.Items ??= new List<ItemData>();
        if (!catalog.Items.Contains(item))
        {
            catalog.Items.Add(item);
            EditorUtility.SetDirty(catalog);
        }

        GameContentCatalog.InvalidateRuntimeCache();
    }
    private static SequencePuzzleDefinition CreateOrUpdatePuzzleDefinition()
    {
        SequencePuzzleDefinition definition =
            AssetDatabase.LoadAssetAtPath<SequencePuzzleDefinition>(PuzzleDefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SequencePuzzleDefinition>();
            AssetDatabase.CreateAsset(definition, PuzzleDefinitionPath);
        }

        definition.Configure(
            "qa_testmap_shortcut_switch",
            new[] { "terminal.a", "terminal.b", "terminal.c" },
            "qa.testmap.puzzle.solved",
            0.6f);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void ConfigurePuzzleStation(
        GameObject root,
        PuzzleMarker marker,
        SequencePuzzleDefinition definition)
    {
        GameObject controllerObject = new GameObject("Sequence Controller");
        controllerObject.transform.SetParent(root.transform, false);
        SequencePuzzleController controller = controllerObject.AddComponent<SequencePuzzleController>();
        controller.Configure(definition);

        SerializedObject markerObject = new SerializedObject(marker);
        SetString(markerObject, "solvedFlag", string.Empty);
        SetString(
            markerObject,
            "fallbackInstructionText",
            "* 아래 단자를 A, B, C 순서로 작동시킨다.\n* 틀리면 잠시 뒤 처음부터 다시 시작한다.");
        markerObject.FindProperty("puzzleRuntimeSource").objectReferenceValue = controller;
        markerObject.ApplyModifiedPropertiesWithoutUndo();

        CreatePuzzleSwitch(root.transform, "terminal.a", "A", new Vector3(-1.25f, -2.25f), controller, new Color(0.84f, 0.30f, 0.28f));
        CreatePuzzleSwitch(root.transform, "terminal.b", "B", new Vector3(0f, -2.25f), controller, new Color(0.36f, 0.78f, 0.42f));
        CreatePuzzleSwitch(root.transform, "terminal.c", "C", new Vector3(1.25f, -2.25f), controller, new Color(0.32f, 0.58f, 0.90f));
    }

    private static void CreatePuzzleSwitch(
        Transform parent,
        string nodeId,
        string label,
        Vector3 position,
        SequencePuzzleController controller,
        Color color)
    {
        GameObject switchObject = new GameObject("PuzzleSwitch_" + label);
        switchObject.layer = ResolveLayer("Interactable");
        switchObject.transform.SetParent(parent, false);
        switchObject.transform.localPosition = position;
        CircleCollider2D collider = switchObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.38f;
        PuzzleSwitch puzzleSwitch = switchObject.AddComponent<PuzzleSwitch>();
        puzzleSwitch.Configure(nodeId, controller);
        CreateBlock("Visual", switchObject.transform, Vector3.zero, new Vector2(0.68f, 0.68f), color, "Characters", 3);
        CreateText("Label", switchObject.transform, new Vector3(0f, 0f), label, 0.05f, Color.white, 5300);
    }
    private static string GetMarkerShortName(AreaMarkerType markerType)
    {
        switch (markerType)
        {
            case AreaMarkerType.Connection:
                return "LINK";
            case AreaMarkerType.Enemy:
                return "ENEMY";
            case AreaMarkerType.Hazard:
                return "HAZARD";
            case AreaMarkerType.Puzzle:
                return "PUZZLE";
            case AreaMarkerType.Vendor:
                return "VENDOR";
            case AreaMarkerType.ShortcutDoor:
                return "DOOR";
            case AreaMarkerType.NPC:
                return "NPC";
            case AreaMarkerType.Item:
                return "ITEM";
            case AreaMarkerType.Sign:
                return "SIGN";
            case AreaMarkerType.SavePoint:
                return "SAVE";
            case AreaMarkerType.PlotPoint:
                return "PLOT";
            case AreaMarkerType.Sublocation:
                return "SUB MAP";
            default:
                return markerType.ToString().ToUpperInvariant();
        }
    }

    private static string GetMarkerUsageText(AreaMarkerType markerType, bool oneShot)
    {
        switch (markerType)
        {
            case AreaMarkerType.NPC:
                return oneShot ? "Z TALK / ONCE" : "Z TALK / REPEAT";
            case AreaMarkerType.Sign:
                return "Z READ";
            case AreaMarkerType.PlotPoint:
                return oneShot ? "ENTER / ONCE" : "ENTER TRIGGER";
            case AreaMarkerType.Item:
                return "Z PICKUP / ONCE";
            case AreaMarkerType.SavePoint:
                return "Z SAVE SLOT 0";
            case AreaMarkerType.Puzzle:
                return "Z GUIDE / SWITCH A-B-C";
            case AreaMarkerType.ShortcutDoor:
                return "Z DOOR / LOCK";
            case AreaMarkerType.Vendor:
                return "Z SHOP HOOK";
            case AreaMarkerType.Connection:
                return "Z MAP LINK";
            case AreaMarkerType.Sublocation:
                return "Z SUB MAP";
            case AreaMarkerType.Hazard:
                return "TOUCH KNOCKBACK";
            case AreaMarkerType.Enemy:
                return "Z BATTLE MARKER";
            default:
                return "Z INTERACT";
        }
    }
    private static void BuildStationVisual(Transform root, AreaMarkerType markerType, StationVisual visual, string label, Color accent, bool oneShot, Sprite characterSprite)
    {
        CreateBlock("Base", root, new Vector3(0f, -0.52f), new Vector2(1.45f, 0.22f), new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f), "Characters", 0);

        switch (visual)
        {
            case StationVisual.Npc:
            case StationVisual.Enemy:
                CreateSpriteVisual("Character", root, characterSprite != null ? characterSprite : _testNpcSprite, new Vector3(0f, 0.18f), visual == StationVisual.Npc ? 1.15f : 1f, visual == StationVisual.Enemy ? new Color(1f, 0.62f, 0.62f) : Color.white);
                break;
            case StationVisual.Sign:
                CreateBlock("Post", root, new Vector3(0f, -0.05f), new Vector2(0.14f, 0.9f), new Color(0.40f, 0.20f, 0.08f), "Characters", 0);
                CreateBlock("Board", root, new Vector3(0f, 0.28f), new Vector2(1.05f, 0.55f), accent, "Characters", 2);
                break;
            case StationVisual.Item:
                CreateDiamond(root, "Pickup", new Vector3(0f, 0.16f), new Vector2(0.55f, 0.55f), accent, 0);
                CreateDiamond(root, "PickupGlow", new Vector3(0f, 0.16f), new Vector2(0.25f, 0.25f), new Color(0.42f, 0.85f, 1f), 2);
                break;
            case StationVisual.Save:
                CreateDiamond(root, "CrystalOuter", new Vector3(0f, 0.12f), new Vector2(0.72f, 1.05f), accent, 0);
                CreateDiamond(root, "CrystalCore", new Vector3(0f, 0.12f), new Vector2(0.32f, 0.64f), Color.white, 2);
                break;
            case StationVisual.Puzzle:
                CreateBlock("Switch", root, new Vector3(0f, 0.05f), new Vector2(0.82f, 0.82f), accent, "Characters", 0);
                CreateDiamond(root, "SwitchCore", new Vector3(0f, 0.05f), new Vector2(0.34f, 0.34f), Color.white, 2);
                break;
            case StationVisual.Vendor:
                CreateBlock("Counter", root, new Vector3(0f, -0.05f), new Vector2(1.35f, 0.62f), new Color(0.42f, 0.20f, 0.08f), "Characters", 0);
                CreateBlock("Awning", root, new Vector3(0f, 0.48f), new Vector2(1.45f, 0.28f), accent, "Characters", 2);
                break;
            case StationVisual.Door:
            case StationVisual.Connection:
            case StationVisual.Sublocation:
                CreateBlock("PillarLeft", root, new Vector3(-0.48f, 0.05f), new Vector2(0.2f, 1.35f), accent, "Characters", 0);
                CreateBlock("PillarRight", root, new Vector3(0.48f, 0.05f), new Vector2(0.2f, 1.35f), accent, "Characters", 0);
                CreateBlock("Header", root, new Vector3(0f, 0.65f), new Vector2(1.16f, 0.2f), accent, "Characters", 1);
                CreateBlock("Portal", root, new Vector3(0f, 0.05f), new Vector2(0.65f, 1f), new Color(accent.r, accent.g, accent.b, 0.35f), "Characters", -1);
                break;
            case StationVisual.Hazard:
                CreateDiamond(root, "SpikeLeft", new Vector3(-0.4f, -0.08f), new Vector2(0.42f, 0.8f), accent, 0);
                CreateDiamond(root, "SpikeCenter", new Vector3(0f, 0.05f), new Vector2(0.46f, 1.05f), new Color(1f, 0.72f, 0.20f), 1);
                CreateDiamond(root, "SpikeRight", new Vector3(0.4f, -0.08f), new Vector2(0.42f, 0.8f), accent, 0);
                break;
            case StationVisual.Plot:
                CreateDiamond(root, "PlotOuter", new Vector3(0f, 0.12f), new Vector2(0.85f, 0.85f), accent, 0);
                CreateBlock("PlotCore", root, new Vector3(0f, 0.12f), new Vector2(0.25f, 0.8f), Color.white, "Characters", 2);
                break;
        }

        CreateBlock("Station_TypePlate", root, new Vector3(0f, 1.08f), new Vector2(1.75f, 0.34f), new Color(0.015f, 0.018f, 0.024f, 0.78f), "Default", 5290);
        CreateText("Station_TypeBadge", root, new Vector3(0f, 1.08f), GetMarkerShortName(markerType), 0.032f, accent, 5300);
        CreateBlock("Station_LabelPlate", root, new Vector3(0f, -1.24f), new Vector2(2.85f, 0.78f), new Color(0.015f, 0.018f, 0.024f, 0.82f), "Default", 5290);
        CreateText("Station_Label", root, new Vector3(0f, -1.04f), label, 0.04f, TextColor, 5300);
        CreateText("Station_Use", root, new Vector3(0f, -1.39f), GetMarkerUsageText(markerType, oneShot), 0.03f, new Color(0.78f, 0.84f, 0.88f), 5300);
    }

    private static void CreateMarkerNote(Transform parent, string name, Vector3 position, string title, string body, Color accent, float width = 4.8f, float height = 1.35f)
    {
        CreateBlock(name + "_Plate", parent, position, new Vector2(width, height), new Color(0.015f, 0.018f, 0.024f, 0.78f), "Default", 5150);
        CreateBlock(name + "_Accent", parent, position - new Vector3(width * 0.5f - 0.08f, 0f), new Vector2(0.12f, height - 0.18f), accent, "Default", 5160);
        CreateText(name + "_Title", parent, position + new Vector3(0f, 0.30f), title, 0.04f, accent, 5200);
        CreateText(name + "_Body", parent, position + new Vector3(0f, -0.25f), body, 0.031f, new Color(0.86f, 0.89f, 0.91f), 5200);
    }
    private static void BuildNpcAndDialogueZone(Transform parent, Dictionary<string, GameObject> prefabs)
    {
        GameObject zone = CreateGroup("A_NPC_AND_DIALOGUE", parent);
        PlacePrefab(prefabs["npc_guide"], zone.transform, new Vector3(-24f, 10.5f));
        PlacePrefab(prefabs["npc_one_shot"], zone.transform, new Vector3(-17f, 10.5f));
        PlacePrefab(prefabs["sign"], zone.transform, new Vector3(-10f, 10.5f));
        PlacePrefab(prefabs["plot"], zone.transform, new Vector3(-5.2f, 6.1f));

        CreateMarkerNote(zone.transform, "Note_NPC_Repeat", new Vector3(-24f, 13.55f), "NPC / Repeat", "Z: dialogue repeats\ninput should return", new Color(0.35f, 0.95f, 0.62f));
        CreateMarkerNote(zone.transform, "Note_NPC_OneShot", new Vector3(-17f, 13.55f), "NPC / One-shot", "Z: talks once\nflag blocks repeat", new Color(0.36f, 0.72f, 1f));
        CreateMarkerNote(zone.transform, "Note_Sign", new Vector3(-10f, 13.55f), "Sign", "Z: read text\nrepeat is allowed", new Color(0.95f, 0.68f, 0.25f));
        CreateMarkerNote(zone.transform, "Note_Plot", new Vector3(-5.2f, 8.7f), "Plot Point", "enter trigger\nlocks after once", new Color(1f, 0.32f, 0.82f));

        CreateText("NPC_Test_Hint", zone.transform, new Vector3(-17f, 6.2f), "REPEAT   ONE-SHOT   SIGN   AUTO-PLOT", 0.05f, new Color(0.70f, 0.96f, 0.84f), 5200);
    }

    private static void BuildSystemMarkerZone(Transform parent, Dictionary<string, GameObject> prefabs)
    {
        GameObject zone = CreateGroup("C_SYSTEM_MARKERS", parent);
        PlacePrefab(prefabs["item"], zone.transform, new Vector3(-27f, -8.3f));
        PlacePrefab(prefabs["save"], zone.transform, new Vector3(-21f, -8.3f));
        PlacePrefab(prefabs["puzzle"], zone.transform, new Vector3(-15f, -8.3f));
        PlacePrefab(prefabs["shortcut"], zone.transform, new Vector3(-8f, -8.3f));
        PlacePrefab(prefabs["vendor"], zone.transform, new Vector3(-27f, -14.2f));
        PlacePrefab(prefabs["connection"], zone.transform, new Vector3(-19f, -14.2f));
        PlacePrefab(prefabs["sublocation"], zone.transform, new Vector3(-10f, -14.2f));

        CreateMarkerNote(zone.transform, "Note_Item", new Vector3(-27f, -5.75f), "Item", "Z: pickup x3\nthen one-shot flag", Color.white, 4.6f);
        CreateMarkerNote(zone.transform, "Note_Save", new Vector3(-21f, -5.75f), "SAVE", "Z: save slot 0\nreal save write", new Color(0.24f, 1f, 1f), 4.6f);
        CreateMarkerNote(zone.transform, "Note_Puzzle", new Vector3(-15f, -5.75f), "Puzzle", "Z: read guide\nthen A-B-C switches", new Color(0.70f, 0.42f, 1f), 4.6f);
        CreateMarkerNote(zone.transform, "Note_Shortcut", new Vector3(-8f, -5.75f), "Shortcut", "Z: locked first\ntry after puzzle", new Color(0.36f, 0.94f, 0.62f), 4.8f);
        CreateMarkerNote(zone.transform, "Note_Vendor", new Vector3(-27f, -11.65f), "Vendor", "Z: buy / sell\ncancel and reopen", new Color(1f, 0.84f, 0.25f), 4.6f);
        CreateMarkerNote(zone.transform, "Note_Connection", new Vector3(-19f, -11.65f), "Connection", "Z: map link\nreloads TestMap", new Color(0.48f, 0.82f, 1f), 4.8f);
        CreateMarkerNote(zone.transform, "Note_Sublocation", new Vector3(-10f, -11.65f), "Sublocation", "Z: sub map link\nreturns to spawn", new Color(0.70f, 0.68f, 1f), 4.8f);

        CreateText("System_Test_Order", zone.transform, new Vector3(-17f, -17.05f), "ITEM PICKUP -> VENDOR SELL/BUY   |   PUZZLE A-B-C -> SHORTCUT", 0.044f, new Color(0.90f, 0.94f, 0.59f), 5200);
    }

    private static void BuildCombatZone(Transform parent, Transform environment, Dictionary<string, GameObject> prefabs)
    {
        GameObject zone = CreateGroup("D_COMBAT_AND_COLLISION", parent);
        PlacePrefab(prefabs["enemy"], zone.transform, new Vector3(6f, -8.4f));
        PlacePrefab(prefabs["hazard"], zone.transform, new Vector3(21f, -8.4f));
        CreateMarkerNote(zone.transform, "Note_EnemyMarker", new Vector3(6f, -5.85f), "Enemy Marker", "Z: battle marker\ndata-driven test", new Color(1f, 0.45f, 0.45f), 4.8f);
        CreateMarkerNote(zone.transform, "Note_BunnySlime", new Vector3(13.5f, -5.85f), "Bunny Slime", "touch / F: seamless\nnever attacks", new Color(0.95f, 0.78f, 0.88f), 5.2f);
        CreateMarkerNote(zone.transform, "Note_Hazard", new Vector3(21f, -5.85f), "Hazard", "touch: HP damage\n0 HP: GameOver", new Color(1f, 0.42f, 0.18f), 4.8f);

        GameObject bunnyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BunnySlimePrefabPath);
        if (bunnyPrefab != null)
        {
            GameObject bunny = (GameObject)PrefabUtility.InstantiatePrefab(bunnyPrefab);
            bunny.name = "QA_BUNNY_SLIME_PASSIVE_ENCOUNTER";
            bunny.transform.SetParent(zone.transform, false);
            bunny.transform.position = new Vector3(13.5f, -8.4f);
            ApplyStaticYSort(bunny);
        }

        CreateText("Combat_Hint", zone.transform, new Vector3(17f, -4.55f), "MARKER / PASSIVE BUNNY / PREEMPTIVE / HAZARD", 0.04f, new Color(1f, 0.72f, 0.67f), 5200);

        GameObject collision = CreateGroup("Collision_And_YSort_Course", environment);
        CreateBlock("CollisionWall_Left", collision.transform, new Vector3(7.5f, -14.8f), new Vector2(0.7f, 5.4f), new Color(0.54f, 0.57f, 0.62f), "Characters", 0, true);
        CreateBlock("CollisionWall_Right", collision.transform, new Vector3(11.5f, -14.8f), new Vector2(0.7f, 5.4f), new Color(0.54f, 0.57f, 0.62f), "Characters", 0, true);
        CreateBlock("YSort_Pillar_A", collision.transform, new Vector3(20.5f, -14.2f), new Vector2(1.8f, 2.5f), new Color(0.28f, 0.50f, 0.62f), "Characters", 0, true);
        CreateBlock("YSort_Pillar_B", collision.transform, new Vector3(25f, -14.2f), new Vector2(1.8f, 2.5f), new Color(0.62f, 0.43f, 0.27f), "Characters", 0, true);
        ApplyStaticYSort(collision);
        CreateText("Collision_Hint", zone.transform, new Vector3(17f, -17.2f), "NARROW COLLIDER     WALK ABOVE/BELOW PILLARS TO CHECK Y-SORT", 0.041f, new Color(0.86f, 0.87f, 0.90f), 5200);
    }

    private static void BuildVisualLabs(Transform parent)
    {
        GameObject scalePrefab = BuildSpriteScaleLabPrefab();
        GameObject scaleInstance = (GameObject)PrefabUtility.InstantiatePrefab(scalePrefab);
        scaleInstance.name = "QA_SpriteScaleLab";
        scaleInstance.transform.SetParent(parent, false);
        scaleInstance.transform.position = new Vector3(17f, 10.1f);
    }

    private static GameObject BuildSpriteScaleLabPrefab()
    {
        GameObject root = new GameObject("QA_SpriteScaleLab");
        CreateBlock("Backdrop", root.transform, Vector3.zero, new Vector2(26f, 11.5f), new Color(0.055f, 0.06f, 0.075f, 0.94f), "Background", -100);

        for (int x = -12; x <= 12; x++)
        {
            float alpha = x == 0 ? 0.24f : 0.07f;
            CreateBlock("Grid_V_" + x, root.transform, new Vector3(x, 0f), new Vector2(0.025f, 10.7f), new Color(0.76f, 0.79f, 0.88f, alpha), "Default", 10);
        }
        for (int y = -5; y <= 5; y++)
        {
            float alpha = y == 0 ? 0.24f : 0.07f;
            CreateBlock("Grid_H_" + y, root.transform, new Vector3(0f, y), new Vector2(25f, 0.025f), new Color(0.76f, 0.79f, 0.88f, alpha), "Default", 10);
        }

        CreateText("Scale_Title", root.transform, new Vector3(0f, 4.8f), "1 GRID CELL = 1 WORLD UNIT", 0.052f, new Color(0.95f, 0.83f, 1f), 5400);
        CreateSpriteSample(root.transform, "Player_Current", _playerSprite, new Vector3(-5f, 2.1f), 1f, Color.white, "PLAYER CURRENT");
        CreateSpriteSample(root.transform, "TestNPC_Native", _testNpcSprite, new Vector3(5f, 2.1f), 1f, Color.white, "TESTNPC 32px / 32PPU / 1.00u");

        CreateSpriteSample(root.transform, "TestNPC_050", _testNpcSprite, new Vector3(-9f, -2.4f), 0.5f, Color.white, "0.50x / 0.50u");
        CreateSpriteSample(root.transform, "TestNPC_075", _testNpcSprite, new Vector3(-4.5f, -2.4f), 0.75f, Color.white, "0.75x / 0.75u");
        CreateSpriteSample(root.transform, "TestNPC_100", _testNpcSprite, new Vector3(0f, -2.4f), 1f, Color.white, "1.00x / 1.00u");
        CreateSpriteSample(root.transform, "TestNPC_150", _testNpcSprite, new Vector3(4.5f, -2.4f), 1.5f, Color.white, "1.50x / 1.50u");
        CreateSpriteSample(root.transform, "TestNPC_200", _testNpcSprite, new Vector3(9f, -2.4f), 2f, Color.white, "2.00x / 2.00u");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LabPrefabRoot + "/QA_SpriteScaleLab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateSpriteSample(Transform parent, string name, Sprite sprite, Vector3 position, float scale, Color color, string label)
    {
        if (sprite != null)
        {
            GameObject sample = new GameObject(name);
            sample.transform.SetParent(parent, false);
            sample.transform.localPosition = position;
            sample.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = sample.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 200;
        }

        CreateText(name + "_Label", parent, position + new Vector3(0f, -1.45f), label, 0.034f, new Color(0.88f, 0.90f, 0.95f), 5400);
    }

    private static void CreateSpawnPoints(Transform parent)
    {
        CreateSpawnPoint(parent, "Spawn_QA_Origin", "qa.testmap.spawn.origin", Vector3.zero, FacingDirection.Down);
        CreateSpawnPoint(parent, "Spawn_From_Connection", "qa.testmap.spawn.connection", new Vector3(0f, -1.5f), FacingDirection.Down);
        CreateSpawnPoint(parent, "Spawn_From_Shortcut", "qa.testmap.spawn.shortcut", new Vector3(-5.3f, -8.3f), FacingDirection.Left);
        CreateSpawnPoint(parent, "Spawn_From_Sublocation", "qa.testmap.spawn.sublocation", new Vector3(-5.3f, -14.2f), FacingDirection.Left);
    }

    private static void CreateSpawnPoint(Transform parent, string name, string id, Vector3 position, FacingDirection facing)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        SpawnPoint spawn = go.AddComponent<SpawnPoint>();
        SerializedObject so = new SerializedObject(spawn);
        SetString(so, "_spawnPointId", id);
        SetEnum(so, "_defaultFacing", (int)facing);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 position)
    {
        if (prefab == null)
            return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        ApplyStaticYSort(instance);
        return instance;
    }

    private static void ApplyStaticYSort(GameObject root)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.sortingLayerName != "Characters")
                continue;

            int localOffset = renderer.sortingOrder;
            renderer.sortingOrder = -Mathf.RoundToInt(renderer.transform.position.y * 100f) + localOffset;
        }
    }

    private static GameObject CreateBlock(
        string name,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Color color,
        string sortingLayer,
        int sortingOrder,
        bool collider = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.color = color;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;

        if (collider)
            go.AddComponent<BoxCollider2D>();

        return go;
    }

    private static void CreateDiamond(Transform parent, string name, Vector3 position, Vector2 size, Color color, int sortingOffset)
    {
        GameObject diamond = CreateBlock(name, parent, position, size, color, "Characters", sortingOffset);
        diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private static void CreateSpriteVisual(string name, Transform parent, Sprite sprite, Vector3 position, float scale, Color color)
    {
        if (sprite == null)
            return;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = "Characters";
        renderer.sortingOrder = 1;
    }

    private static TextMeshPro CreateText(string name, Transform parent, Vector3 position, string value, float characterSize, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = go.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.position = position;
        rectTransform.sizeDelta = new Vector2(1000f, 100f);
        rectTransform.localScale = Vector3.one * characterSize;

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.font = _labelFont;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 64;
        text.color = color;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.sortingLayerID = SortingLayer.NameToID("Default");
        text.sortingOrder = sortingOrder;

        return text;
    }

    private static GameObject CreateGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void ConfigureSceneTransition(SerializedObject so, string targetSpawnId)
    {
        SetString(so, "targetSceneName", "TestMap");
        SetString(so, "targetSpawnId", targetSpawnId);
        SetFloat(so, "fadeDuration", 0.2f);
        SetBool(so, "interactToUse", true);
        SetEnum(so, "activationMode", (int)DoorActivationMode.OnInteract);
        SetBool(so, "oneShotUntilExit", true);

        SerializedProperty request = so.FindProperty("mapTransition");
        if (request == null)
            return;

        SetEnum(request.FindPropertyRelative("TransitionType"), (int)MapTransitionType.Scene);
        SetString(request.FindPropertyRelative("TargetSceneName"), "TestMap");
        SetString(request.FindPropertyRelative("TargetSpawnPointId"), targetSpawnId);
        SetString(request.FindPropertyRelative("TargetAreaId"), AreaId);
        SetEnum(request.FindPropertyRelative("FacingAfterEnter"), (int)FacingDirection.Down);
        SetBool(request.FindPropertyRelative("UseFallbackPosition"), false);
        SetFloat(request.FindPropertyRelative("FadeDuration"), 0.2f);
    }

    private static void ValidateGeneratedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("생성 후 TestMap Scene이 로드되지 않았습니다.");

        if (GameObject.Find(GeneratedRootName) == null)
            throw new InvalidOperationException("생성 루트를 찾지 못했습니다: " + GeneratedRootName);
        if (UnityEngine.Object.FindFirstObjectByType<PlayerController>() == null)
            throw new InvalidOperationException("TestMap에 PlayerController가 없습니다.");
        if (UnityEngine.Object.FindFirstObjectByType<CinemachineCamera>() == null)
            throw new InvalidOperationException("TestMap에 CinemachineCamera가 없습니다.");
        if (UnityEngine.Object.FindFirstObjectByType<GameBootstrap>() == null)
            throw new InvalidOperationException("TestMap에 GameBootstrap이 없습니다.");
        if (UnityEngine.Object.FindFirstObjectByType<MapTransitionService>() == null)
            throw new InvalidOperationException("TestMap에 MapTransitionService가 없습니다.");

        AreaMarkerBase[] markers = UnityEngine.Object.FindObjectsByType<AreaMarkerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<AreaMarkerType> markerTypes = new HashSet<AreaMarkerType>();
        for (int i = 0; i < markers.Length; i++)
        {
            markerTypes.Add(markers[i].MarkerType);
            if (markers[i].GetComponent<Collider2D>() == null)
                throw new InvalidOperationException("Collider2D가 없는 QA Marker: " + markers[i].name);

            List<string> issues = new List<string>();
            markers[i].CollectValidationIssues(issues);
            if (issues.Count > 0)
                throw new InvalidOperationException($"QA Marker validation failed: {markers[i].name} / {string.Join(" | ", issues)}");
        }

        foreach (AreaMarkerType type in Enum.GetValues(typeof(AreaMarkerType)))
        {
            if (!markerTypes.Contains(type))
                throw new InvalidOperationException("TestMap에 Area Marker 타입이 누락되었습니다: " + type);
        }

        Debug.Log($"[TestMapShowcaseBuilder] Validation passed. Markers={markers.Length}, Types={markerTypes.Count}");
    }

    private static void RemoveGeneratedRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == GeneratedRootName)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
                break;
            }
        }
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == ScenePath)
            {
                if (!current[i].enabled)
                {
                    current[i].enabled = true;
                    EditorBuildSettings.scenes = current;
                }
                return;
            }
        }

        EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[current.Length + 1];
        Array.Copy(current, updated, current.Length);
        updated[updated.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = updated;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static int ResolveLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : 0;
    }

    private static void SetString(SerializedObject so, string name, string value) => SetString(so.FindProperty(name), value);
    private static void SetBool(SerializedObject so, string name, bool value) => SetBool(so.FindProperty(name), value);
    private static void SetInt(SerializedObject so, string name, int value) => SetInt(so.FindProperty(name), value);
    private static void SetFloat(SerializedObject so, string name, float value) => SetFloat(so.FindProperty(name), value);
    private static void SetEnum(SerializedObject so, string name, int value) => SetEnum(so.FindProperty(name), value);
    private static void SetObject(SerializedObject so, string name, UnityEngine.Object value) => SetObject(so.FindProperty(name), value);
    private static void SetColor(SerializedObject so, string name, Color value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.colorValue = value;
    }

    private static void SetString(SerializedProperty property, string value)
    {
        if (property != null) property.stringValue = value;
    }

    private static void SetBool(SerializedProperty property, bool value)
    {
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedProperty property, int value)
    {
        if (property != null) property.intValue = value;
    }

    private static void SetFloat(SerializedProperty property, float value)
    {
        if (property != null) property.floatValue = value;
    }

    private static void SetEnum(SerializedProperty property, int value)
    {
        if (property != null) property.enumValueIndex = value;
    }

    private static void SetObject(SerializedProperty property, UnityEngine.Object value)
    {
        if (property != null) property.objectReferenceValue = value;
    }
}
