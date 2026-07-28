using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

public static class ShowcaseStationRoomBuilder
{
    private const string BackgroundSortingLayer = "Background";
    private const string CharacterSortingLayer = "Characters";

    private sealed class RoomBuildContext
    {
        public string RoomId;
        public GameObject Root;
        public Transform Props;
        public Transform Markers;
        public Transform Systems;
        public Transform Spawns;
    }

    private sealed class PendingRoom
    {
        public string RoomId;
        public string PrefabPath;
        public GameObject Root;
    }

    public static void Build(ShowcaseStationDataBundle data)
    {
        TrainNetworkDefinition network = AssetDatabase.LoadAssetAtPath<TrainNetworkDefinition>(TravelTrainPaths.Network);
        TrainStopDefinition showcaseStop = AssetDatabase.LoadAssetAtPath<TrainStopDefinition>(TravelTrainPaths.ShowcaseStop);
        if (network == null || showcaseStop == null)
            throw new InvalidOperationException("본편 열차 데이터가 없습니다. TravelWorldBuilder를 먼저 실행하세요.");
        Build(data, new TravelTrainDataBundle
        {
            Network = network,
            ShowcaseStop = showcaseStop
        });
    }

    public static void Build(
        ShowcaseStationDataBundle data,
        TravelTrainDataBundle trainData)
    {
        ValidateData(data);
        if (trainData?.Network == null || trainData.ShowcaseStop == null)
            throw new ArgumentNullException(nameof(trainData));
        ShowcaseStationDataBuilder.EnsureFolder(ShowcaseStationPaths.PrefabRoot);

        var pending = new List<PendingRoom>(ShowcaseStationIds.GeneratedRoomIds.Length);
        try
        {
            for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
            {
                string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
                string path = GetPrefabPath(roomId);
                EnsurePrefabPathAvailable(path);
                pending.Add(new PendingRoom
                {
                    RoomId = roomId,
                    PrefabPath = path,
                    Root = BuildRoom(roomId, data, trainData)
                });
            }

            ValidatePendingRooms(pending);
            SaveRooms(pending, data);
        }
        finally
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].Root != null)
                    UnityEngine.Object.DestroyImmediate(pending[i].Root);
            }
        }

        AssetDatabase.SaveAssets();
        RefreshAreaSummaries(data);
        AssetDatabase.SaveAssets();
    }

    internal static string GetPrefabPath(string roomId)
    {
        return ShowcaseStationPaths.PrefabRoot + "/"
            + ShowcaseStationDataBuilder.RoomAssetStem(roomId)
            + ".prefab";
    }

    private static GameObject BuildRoom(
        string roomId,
        ShowcaseStationDataBundle data,
        TravelTrainDataBundle trainData)
    {
        switch (roomId)
        {
            case ShowcaseStationIds.Arrival:
                return BuildArrival(data);
            case ShowcaseStationIds.Square:
                return BuildSquare(data);
            case ShowcaseStationIds.Workshop:
                return BuildWorkshop(data);
            case ShowcaseStationIds.Passage:
                return BuildPassage(data);
            case ShowcaseStationIds.Train:
                return BuildTrain(data, trainData);

            default:
                throw new ArgumentOutOfRangeException(nameof(roomId), roomId, "Unknown Showcase room.");
        }
    }

    private static GameObject BuildArrival(ShowcaseStationDataBundle data)
    {
        RoomBuildContext room = CreateRoom(
            ShowcaseStationIds.Arrival,
            new Color(0.035f, 0.045f, 0.06f),
            new Color(0.13f, 0.17f, 0.21f),
            new Color(0.34f, 0.39f, 0.42f));

        CreateBlock("Platform Edge", room.Props, new Vector2(0f, -1.75f), new Vector2(7.5f, 0.3f), new Color(0.72f, 0.62f, 0.32f), 2);
        CreateBlock("Stopped Train", room.Props, new Vector2(0.5f, 1.25f), new Vector2(5.4f, 1.15f), new Color(0.19f, 0.24f, 0.27f), 3);
        CreateBlock("Train Window", room.Props, new Vector2(0.5f, 1.3f), new Vector2(3.8f, 0.35f), new Color(0.56f, 0.68f, 0.70f), 4);

        CreateSpawn(room, "entry", new Vector2(-3.6f, -0.45f), FacingDirection.Right);
        CreateSpawn(room, "from_square", new Vector2(3.5f, -0.45f), FacingDirection.Left);

        CreatePlot(
            room,
            "arrival_intro",
            new Vector2(-2.5f, -0.45f),
            data.IntroDialogue,
            true,
            "showcase.station.started");
        CreateSavePoint(room, "platform_save", new Vector2(-0.8f, -0.65f));
        CreateSign(
            room,
            "station_map",
            new Vector2(1.2f, -0.45f),
            "* 정비 공방은 등불 광장 북쪽에 있다.");
        CreateConnection(
            room,
            "to_square",
            new Vector2(4.65f, -0.45f),
            data.Rooms[ShowcaseStationIds.Square],
            "from_arrival",
            FacingDirection.Left);

        return room.Root;
    }

    private static GameObject BuildSquare(ShowcaseStationDataBundle data)
    {
        RoomBuildContext room = CreateRoom(
            ShowcaseStationIds.Square,
            new Color(0.04f, 0.055f, 0.07f),
            new Color(0.18f, 0.22f, 0.24f),
            new Color(0.36f, 0.31f, 0.25f));

        CreateBlock("Central Lantern", room.Props, new Vector2(0f, 0.65f), new Vector2(0.35f, 1.2f), new Color(0.92f, 0.66f, 0.25f), 4);
        CreateBlock("Square Bench Left", room.Props, new Vector2(-2.1f, -0.8f), new Vector2(1.4f, 0.35f), new Color(0.30f, 0.22f, 0.16f), 3);
        CreateBlock("Square Bench Right", room.Props, new Vector2(2.1f, -0.8f), new Vector2(1.4f, 0.35f), new Color(0.30f, 0.22f, 0.16f), 3);

        CreateSpawn(room, "from_arrival", new Vector2(-3.8f, -0.45f), FacingDirection.Right);
        CreateSpawn(room, "from_workshop", new Vector2(0f, 1.7f), FacingDirection.Down);
        CreateSpawn(room, "from_passage", new Vector2(3.8f, -0.45f), FacingDirection.Left);
        CreateSpawn(room, "from_shortcut", new Vector2(3.0f, 1.45f), FacingDirection.Left);

        CreateNpc(room, "station_attendant", new Vector2(-1.35f, 0f), data.StationNpcDialogue);
        CreateItem(room, "platform_supplies", new Vector2(1.35f, -0.15f), data.SmallPotion);
        CreateConnection(
            room,
            "to_arrival",
            new Vector2(-4.65f, -0.45f),
            data.Rooms[ShowcaseStationIds.Arrival],
            "from_square",
            FacingDirection.Right);
        CreateConnection(
            room,
            "to_workshop",
            new Vector2(0f, 2.25f),
            data.Rooms[ShowcaseStationIds.Workshop],
            "from_square",
            FacingDirection.Down);
        CreateConnection(
            room,
            "to_passage",
            new Vector2(4.65f, -0.45f),
            data.Rooms[ShowcaseStationIds.Passage],
            "from_square",
            FacingDirection.Left);
        CreateShortcut(
            room,
            "shortcut_to_passage",
            new Vector2(4.0f, 1.45f),
            "showcase.shortcut.square",
            "showcase.shortcut.passage",
            "showcase.station.power_restored",
            data.PowerLockedDialogue,
            data.Rooms[ShowcaseStationIds.Passage],
            "from_shortcut",
            FacingDirection.Left);

        return room.Root;
    }

    private static GameObject BuildWorkshop(ShowcaseStationDataBundle data)
    {
        RoomBuildContext room = CreateRoom(
            ShowcaseStationIds.Workshop,
            new Color(0.045f, 0.04f, 0.035f),
            new Color(0.24f, 0.20f, 0.16f),
            new Color(0.39f, 0.29f, 0.20f));

        GameObject lampOff = CreateBlock("Power Lamp Off", room.Props, new Vector2(0f, 1.6f), new Vector2(0.45f, 0.45f), new Color(0.22f, 0.19f, 0.16f), 4);
        GameObject lampOn = CreateBlock("Power Lamp On", room.Props, new Vector2(0f, 1.6f), new Vector2(0.45f, 0.45f), new Color(0.95f, 0.78f, 0.32f), 5);
        lampOn.SetActive(false);
        CreateBlock("Workbench", room.Props, new Vector2(-2.4f, 0.85f), new Vector2(2.2f, 0.55f), new Color(0.34f, 0.23f, 0.16f), 3);

        CreateSpawn(room, "from_square", new Vector2(0f, -1.75f), FacingDirection.Up);
        CreateVendor(room, "workshop_vendor", new Vector2(-2.4f, 0.2f), data.WorkshopShop);

        GameObject controllerObject = CreateEmpty("Power Sequence Controller", room.Systems);
        SequencePuzzleController controller = controllerObject.AddComponent<SequencePuzzleController>();
        controller.Configure(data.WorkshopPuzzle);

        CreatePuzzleGuide(
            room,
            "power_sequence",
            new Vector2(0f, 0.2f),
            controller,
            data.PuzzleGuideDialogue);
        CreatePuzzleSwitch(room, "terminal.a", new Vector2(-1.4f, -0.45f), controller, new Color(0.72f, 0.32f, 0.24f));
        CreatePuzzleSwitch(room, "terminal.b", new Vector2(0f, -0.45f), controller, new Color(0.38f, 0.65f, 0.38f));
        CreatePuzzleSwitch(room, "terminal.c", new Vector2(1.4f, -0.45f), controller, new Color(0.32f, 0.52f, 0.76f));
        CreateSign(room, "terminal_order", new Vector2(2.6f, 0.8f), "* 적색, 녹색, 청색 단자 순서로 연결한다.");

        GameObject binderObject = CreateEmpty("Power Lamp State", room.Systems);
        FlagStateBinder binder = binderObject.AddComponent<FlagStateBinder>();
        binder.Configure(
            "showcase.station.power_restored",
            FlagValueComparison.Equal,
            1,
            new[] { lampOn },
            new[] { lampOff });

        CreateConnection(
            room,
            "to_square",
            new Vector2(0f, -2.25f),
            data.Rooms[ShowcaseStationIds.Square],
            "from_workshop",
            FacingDirection.Up);

        return room.Root;
    }

    private static GameObject BuildPassage(ShowcaseStationDataBundle data)
    {
        RoomBuildContext room = CreateRoom(
            ShowcaseStationIds.Passage,
            new Color(0.025f, 0.04f, 0.045f),
            new Color(0.12f, 0.20f, 0.21f),
            new Color(0.24f, 0.31f, 0.31f));

        CreateBlock("Steam Pipe Top", room.Props, new Vector2(0f, 1.65f), new Vector2(7.2f, 0.35f), new Color(0.27f, 0.34f, 0.34f), 3);
        CreateBlock("Steam Pipe Bottom", room.Props, new Vector2(0f, -1.55f), new Vector2(7.2f, 0.35f), new Color(0.27f, 0.34f, 0.34f), 3);

        CreateSpawn(room, "from_square", new Vector2(-3.8f, 0f), FacingDirection.Right);
        CreateSpawn(room, "from_train", new Vector2(3.8f, 0f), FacingDirection.Left);
        CreateSpawn(room, "from_shortcut", new Vector2(-2.8f, 1.25f), FacingDirection.Right);

        CreatePeriodicHazard(room, "steam_vent", new Vector2(-0.4f, 0f));
        CreateOverworldEnemy(room, "steam_wisp", new Vector2(1.55f, 0f), data.SteamEnemy);
        CreateShortcut(
            room,
            "shortcut_to_square",
            new Vector2(-3.8f, 1.25f),
            "showcase.shortcut.passage",
            "showcase.shortcut.square",
            "showcase.station.power_restored",
            data.PowerLockedDialogue,
            data.Rooms[ShowcaseStationIds.Square],
            "from_shortcut",
            FacingDirection.Right);
        CreateConnection(
            room,
            "to_square",
            new Vector2(-4.65f, 0f),
            data.Rooms[ShowcaseStationIds.Square],
            "from_passage",
            FacingDirection.Right);
        CreateConnection(
            room,
            "to_train",
            new Vector2(4.65f, 0f),
            data.Rooms[ShowcaseStationIds.Train],
            "from_passage",
            FacingDirection.Left);

        return room.Root;
    }

    private static GameObject BuildTrain(
        ShowcaseStationDataBundle data,
        TravelTrainDataBundle trainData)
    {
        RoomBuildContext room = CreateRoom(
            ShowcaseStationIds.Train,
            new Color(0.03f, 0.035f, 0.045f),
            new Color(0.15f, 0.16f, 0.20f),
            new Color(0.32f, 0.25f, 0.22f));

        GameObject cabinDark = CreateBlock("Cabin Lights Off", room.Props, new Vector2(0f, 1.45f), new Vector2(5.8f, 0.25f), new Color(0.16f, 0.15f, 0.17f), 3);
        GameObject cabinLit = CreateBlock("Cabin Lights On", room.Props, new Vector2(0f, 1.45f), new Vector2(5.8f, 0.25f), new Color(0.91f, 0.72f, 0.34f), 4);
        cabinLit.SetActive(false);
        CreateBlock("Engine Housing", room.Props, new Vector2(1.2f, 0.45f), new Vector2(2.3f, 1.25f), new Color(0.24f, 0.20f, 0.20f), 3);
        GameObject powerPulse = CreateBlock(
            "Power Pulse",
            room.Props,
            new Vector2(0f, -8f),
            new Vector2(0.32f, 0.32f),
            new Color(1f, 0.86f, 0.36f, 0.95f),
            8);
        GameObject steamLeft = CreateBlock(
            "Steam Burst Left",
            room.Props,
            new Vector2(-8f, -8f),
            new Vector2(0.42f, 0.24f),
            new Color(0.68f, 0.72f, 0.72f, 0.9f),
            7);
        GameObject steamRight = CreateBlock(
            "Steam Burst Right",
            room.Props,
            new Vector2(8f, -8f),
            new Vector2(0.42f, 0.24f),
            new Color(0.68f, 0.72f, 0.72f, 0.9f),
            7);

        CreateSpawn(room, "from_passage", new Vector2(-3.8f, -0.65f), FacingDirection.Right);
        CreateSpawn(room, "from_travel_train", new Vector2(2.9f, -0.55f), FacingDirection.Left);

        CreatePlot(
            room,
            "engine_goal",
            new Vector2(-1.8f, -0.5f),
            data.PowerLockedDialogue,
            false,
            string.Empty,
            data.StationNpcDialogue);
        OverworldCinematicStage finaleStage = CreateFinaleStage(
            room,
            data,
            powerPulse.transform,
            steamLeft.transform,
            steamRight.transform);
        CreatePowerConsole(
            room,
            "power_console",
            new Vector2(1.2f, -0.4f),
            data,
            finaleStage);
        CreateTrainBoarding(
            room,
            new Vector2(3.8f, 0.65f),
            trainData.Network,
            trainData.ShowcaseStop);
        CreateConnection(
            room,
            "to_passage",
            new Vector2(-4.65f, -0.65f),
            data.Rooms[ShowcaseStationIds.Passage],
            "from_train",
            FacingDirection.Right);

        GameObject binderObject = CreateEmpty("Cabin Light State", room.Systems);
        FlagStateBinder binder = binderObject.AddComponent<FlagStateBinder>();
        binder.Configure(
            "showcase.station.completed",
            FlagValueComparison.Equal,
            1,
            new[] { cabinLit },
            new[] { cabinDark, powerPulse, steamLeft, steamRight });

        return room.Root;
    }

    private static RoomBuildContext CreateRoom(
        string roomId,
        Color backgroundColor,
        Color floorColor,
        Color wallColor)
    {
        string stem = ShowcaseStationDataBuilder.RoomAssetStem(roomId);
        GameObject root = new GameObject(stem);
        RoomInstance instance = root.AddComponent<RoomInstance>();
        SetString(instance, "_roomId", roomId);

        Transform background = CreateEmpty("Background", root.transform).transform;
        Transform floor = CreateEmpty("Floor", root.transform).transform;
        Transform walls = CreateEmpty("Walls", root.transform).transform;
        Transform props = CreateEmpty("Props", root.transform).transform;
        Transform gameplay = CreateEmpty("Gameplay", root.transform).transform;
        Transform markers = CreateEmpty("Markers", gameplay).transform;
        Transform systems = CreateEmpty("Systems", gameplay).transform;
        Transform spawns = CreateEmpty("Spawns", gameplay).transform;

        CreateBlock("Backdrop", background, Vector3.zero, new Vector2(11f, 6f), backgroundColor, -10);
        CreateBlock("Walkable Floor", floor, new Vector2(0f, -0.1f), new Vector2(9.5f, 4.6f), floorColor, -2);
        CreateWall("Wall Top", walls, new Vector2(0f, 2.5f), new Vector2(10f, 0.3f), wallColor);
        CreateWall("Wall Bottom", walls, new Vector2(0f, -2.5f), new Vector2(10f, 0.3f), wallColor);
        CreateWall("Wall Left", walls, new Vector2(-5f, 0f), new Vector2(0.3f, 5.3f), wallColor);
        CreateWall("Wall Right", walls, new Vector2(5f, 0f), new Vector2(0.3f, 5.3f), wallColor);

        GameObject boundsObject = CreateEmpty("CameraBounds", root.transform);
        PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
        bounds.isTrigger = true;
        bounds.points = new[]
        {
            new Vector2(-4.8f, -2.3f),
            new Vector2(-4.8f, 2.3f),
            new Vector2(4.8f, 2.3f),
            new Vector2(4.8f, -2.3f)
        };
        SetObjectReference(instance, "_cameraBounds", bounds);

        return new RoomBuildContext
        {
            RoomId = roomId,
            Root = root,
            Props = props,
            Markers = markers,
            Systems = systems,
            Spawns = spawns
        };
    }

    private static void CreatePlot(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        DialogueData dialogue,
        bool oneShot,
        string completionFlag,
        FlagDialogueSelector dialogueSelector = null)
    {
        PlotPointMarker marker = CreateAreaMarker<PlotPointMarker>(
            room,
            featureId,
            "Plot " + featureId,
            position,
            AreaMarkerType.PlotPoint,
            oneShot,
            completionFlag);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "plotId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "triggerMode").enumValueIndex = (int)AreaPlotTriggerMode.OnEnter;
        Property(serialized, "dialogueData").objectReferenceValue = dialogue;
        Property(serialized, "dialogueSelector").objectReferenceValue = dialogueSelector;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSavePoint(RoomBuildContext room, string featureId, Vector2 position)
    {
        SavePointMarker marker = CreateAreaMarker<SavePointMarker>(
            room,
            featureId,
            "SAVE Point",
            position,
            AreaMarkerType.SavePoint,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "savePointId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "quickSaveSlot").intValue = 0;
        Property(serialized, "autoSaveOnPass").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateBlock("SAVE Point Visual", room.Props, position, new Vector2(0.35f, 0.65f), new Color(0.35f, 0.9f, 0.9f), 5);
    }

    private static void CreateSign(RoomBuildContext room, string featureId, Vector2 position, string text)
    {
        SignMarker marker = CreateAreaMarker<SignMarker>(
            room,
            featureId,
            "Sign " + featureId,
            position,
            AreaMarkerType.Sign,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "signText").stringValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateBlock("Sign " + featureId, room.Props, position, new Vector2(0.5f, 0.65f), new Color(0.40f, 0.27f, 0.17f), 4);
    }

    private static void CreateNpc(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        FlagDialogueSelector selector)
    {
        NPCMarker marker = CreateAreaMarker<NPCMarker>(
            room,
            featureId,
            "Station Attendant",
            position,
            AreaMarkerType.NPC,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "npcId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "dialogueId").stringValue = "showcase.station.attendant";
        Property(serialized, "dialogueSelector").objectReferenceValue = selector;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShowcaseStationPaths.TestNpcSprite);
        GameObject visual = CreateSpriteObject("Station Attendant Visual", room.Props, position, sprite, Color.white, 6);
        FitSpriteHeight(visual, 1.15f);
    }

    private static void CreateItem(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        ItemData item)
    {
        ItemPickupMarker marker = CreateAreaMarker<ItemPickupMarker>(
            room,
            featureId,
            "Platform Supplies",
            position,
            AreaMarkerType.Item,
            true,
            "showcase.station.item.potion_taken");
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "itemId").stringValue = item.ItemID;
        Property(serialized, "amount").intValue = 1;
        Property(serialized, "pickupMessage").stringValue = "* 정비용 회복약을 하나 챙겼다.";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateBlock("Supply Crate", room.Props, position, new Vector2(0.5f, 0.5f), new Color(0.64f, 0.48f, 0.27f), 4);
    }

    private static void CreateVendor(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        ShopDefinition shop)
    {
        VendorMarker marker = CreateAreaMarker<VendorMarker>(
            room,
            featureId,
            "Workshop Vendor",
            position,
            AreaMarkerType.Vendor,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "vendorId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "shopId").stringValue = shop.ShopId;
        Property(serialized, "shopDefinition").objectReferenceValue = shop;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateBlock("Vendor Counter", room.Props, position, new Vector2(1.3f, 0.45f), new Color(0.55f, 0.40f, 0.22f), 4);
    }

    private static void CreatePuzzleGuide(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        SequencePuzzleController controller,
        DialogueData instruction)
    {
        PuzzleMarker marker = CreateAreaMarker<PuzzleMarker>(
            room,
            featureId,
            "Power Sequence",
            position,
            AreaMarkerType.Puzzle,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "puzzleId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "puzzleRuntimeSource").objectReferenceValue = controller;
        Property(serialized, "instructionDialogue").objectReferenceValue = instruction;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePuzzleSwitch(
        RoomBuildContext room,
        string nodeId,
        Vector2 position,
        SequencePuzzleController controller,
        Color color)
    {
        GameObject switchObject = CreateEmpty("Switch " + nodeId, room.Markers);
        switchObject.layer = ResolveInteractableLayer();
        switchObject.transform.localPosition = position;
        CircleCollider2D collider = switchObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.38f;
        PuzzleSwitch puzzleSwitch = switchObject.AddComponent<PuzzleSwitch>();
        puzzleSwitch.Configure(nodeId, controller);
        CreateBlock("Switch Visual " + nodeId, room.Props, position, new Vector2(0.55f, 0.55f), color, 5);
    }

    private static void CreatePeriodicHazard(RoomBuildContext room, string featureId, Vector2 position)
    {
        HazardMarker marker = CreateAreaMarker<HazardMarker>(
            room,
            featureId,
            "Steam Vent",
            position,
            AreaMarkerType.Hazard,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "damage").intValue = 12;
        Property(serialized, "rehitDelay").floatValue = 0.8f;
        Property(serialized, "knockback").floatValue = 0.45f;
        Property(serialized, "triggerOnEnter").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject visual = CreateBlock("Steam Vent Active", room.Props, position, new Vector2(0.9f, 0.9f), new Color(0.72f, 0.86f, 0.88f, 0.75f), 5);
        GameObject controllerObject = CreateEmpty("Steam Vent Cycle", room.Systems);
        PeriodicHazardController controller = controllerObject.AddComponent<PeriodicHazardController>();
        controller.Configure(
            0.6f,
            1.25f,
            1.1f,
            new Collider2D[] { marker.GetComponent<Collider2D>() },
            new[] { visual });
    }

    private static void CreateOverworldEnemy(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        EnemyData enemyData)
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShowcaseStationPaths.EnemyBasePrefab);
        GameObject enemy = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
        if (enemy == null)
            throw new InvalidOperationException("Enemy_Base prefab could not be instantiated.");

        enemy.name = "Enemy " + featureId;
        enemy.transform.SetParent(room.Markers, false);
        enemy.transform.localPosition = position;

        EnemyCharacter character = enemy.GetComponent<EnemyCharacter>();
        if (character == null)
            character = enemy.AddComponent<EnemyCharacter>();
        character.Data = enemyData;

        OverworldEnemy overworldEnemy = enemy.GetComponent<OverworldEnemy>();
        if (overworldEnemy == null)
            overworldEnemy = enemy.AddComponent<OverworldEnemy>();
        SetString(overworldEnemy, "_enemyId", "showcase.steam_wisp");
        SetBool(overworldEnemy, "_useDedicatedBattleScene", false);
        SetBool(overworldEnemy, "_destroyAfterTouch", false);
        SetBool(overworldEnemy, "_canInstantKillLater", true);
        SetEnum(overworldEnemy, "_victoryHandling", 0);
        SetEnum(overworldEnemy, "_instantVictoryHandling", 2);
        SetFloat(overworldEnemy, "_battleFadeDuration", 0.08f);
    }

    private static OverworldCinematicStage CreateFinaleStage(
        RoomBuildContext room,
        ShowcaseStationDataBundle data,
        Transform powerPulse,
        Transform steamLeft,
        Transform steamRight)
    {
        GameObject stageObject = CreateEmpty("Finale Cinematic Stage", room.Systems);
        OverworldCinematicStage stage = stageObject.AddComponent<OverworldCinematicStage>();

        GameObject railObject = CreateEmpty("Camera Rail", stageObject.transform);
        Transform cameraRail = railObject.transform;
        cameraRail.localPosition = new Vector3(1.2f, -0.1f, 0f);

        GameObject cameraObject = CreateEmpty("Cinematic Camera", stageObject.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -1f);
        CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
        CinemachineFollow follow = cameraObject.AddComponent<CinemachineFollow>();
        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = data.FinalePowerShot.CameraPositionDamping;
        follow.TrackerSettings = trackerSettings;
        camera.Priority = new PrioritySettings { Enabled = true, Value = 100 };
        camera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        camera.Lens.OrthographicSize = data.FinalePowerShot.StartOrthographicSize;

        SerializedObject serialized = new SerializedObject(stage);
        Property(serialized, "_stageId").stringValue = "showcase.station.finale";
        Property(serialized, "_cinematicCamera").objectReferenceValue = camera;

        SerializedProperty subjects = Property(serialized, "_subjects");
        subjects.arraySize = 4;
        ConfigureCinematicSubject(
            subjects.GetArrayElementAtIndex(0),
            "camera_rail",
            cameraRail);
        ConfigureCinematicSubject(
            subjects.GetArrayElementAtIndex(1),
            "power_pulse",
            powerPulse);
        ConfigureCinematicSubject(
            subjects.GetArrayElementAtIndex(2),
            "steam_left",
            steamLeft);
        ConfigureCinematicSubject(
            subjects.GetArrayElementAtIndex(3),
            "steam_right",
            steamRight);

        SerializedProperty shots = Property(serialized, "_shots");
        shots.arraySize = 2;
        shots.GetArrayElementAtIndex(0).objectReferenceValue = data.FinalePowerShot;
        shots.GetArrayElementAtIndex(1).objectReferenceValue = data.FinaleDepartureShot;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ScenarioValidationResult validation = stage.ValidateDefinition();
        if (validation.HasErrors)
            throw new InvalidOperationException("Showcase finale Cinematic Stage validation failed.");

        cameraObject.SetActive(false);
        return stage;
    }

    private static void ConfigureCinematicSubject(
        SerializedProperty subject,
        string subjectId,
        Transform target)
    {
        subject.FindPropertyRelative("SubjectId").stringValue = subjectId;
        subject.FindPropertyRelative("Target").objectReferenceValue = target;
    }
    private static void CreatePowerConsole(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        ShowcaseStationDataBundle data,
        OverworldCinematicStage cinematicStage)
    {
        GameObject console = CreateEmpty("Power Console", room.Markers);
        console.layer = ResolveInteractableLayer();
        console.transform.localPosition = position;
        CircleCollider2D collider = console.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.6f;

        SceneActionSequencePlayer sequencePlayer = console.AddComponent<SceneActionSequencePlayer>();
        sequencePlayer.Configure(
            data.FinaleSequence,
            cinematicStage,
            "showcase.station.finale.power",
            new[]
            {
                new ScenarioDialogueReferenceData
                {
                    DialogueId = "showcase.station.finale.power_restored",
                    DialogueDataId = "Dialogue_FinalePowerRestored",
                    Dialogue = data.FinaleDialogue
                }
            });

        PowerConsoleInteractable interactable = console.AddComponent<PowerConsoleInteractable>();
        interactable.Configure(
            "showcase.station.power_restored",
            1,
            data.PowerLockedDialogue,
            "* 전력이 부족하다.",
            sequencePlayer,
            true,
            "showcase.station.completed");

        CreateBlock("Power Console Visual", room.Props, position, new Vector2(0.85f, 0.75f), new Color(0.48f, 0.35f, 0.25f), 5);
    }

    private static void CreateTrainBoarding(
        RoomBuildContext room,
        Vector2 position,
        TrainNetworkDefinition network,
        TrainStopDefinition stop)
    {
        TrainBoardingMarker marker = CreateAreaMarker<TrainBoardingMarker>(
            room,
            "train_boarding",
            "공용 열차 타기",
            position,
            AreaMarkerType.Sublocation,
            false,
            string.Empty);
        marker.Configure(network, stop, 0.25f);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "requiredFlag").stringValue = "showcase.station.completed";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateBlock(
            "Travel Train Door",
            room.Props,
            position,
            new Vector2(0.8f, 1.15f),
            new Color(0.48f, 0.36f, 0.24f),
            5);
    }

    private static void CreateConnection(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        RoomDefinition targetRoom,
        string targetSpawnId,
        FacingDirection facing)
    {
        AreaConnectionMarker marker = CreateAreaMarker<AreaConnectionMarker>(
            room,
            featureId,
            "Connection " + featureId,
            position,
            AreaMarkerType.Connection,
            false,
            string.Empty);
        ConfigureRoomTransition(marker, targetRoom, targetSpawnId, facing, DoorActivationMode.OnTriggerEnter);
        CreateBlock("Door " + featureId, room.Props, position, new Vector2(0.55f, 1.15f), new Color(0.62f, 0.65f, 0.62f), 4);
    }

    private static void CreateShortcut(
        RoomBuildContext room,
        string featureId,
        Vector2 position,
        string doorId,
        string linkedDoorId,
        string unlockFlag,
        DialogueData lockedDialogue,
        RoomDefinition targetRoom,
        string targetSpawnId,
        FacingDirection facing)
    {
        ShortcutDoorMarker marker = CreateAreaMarker<ShortcutDoorMarker>(
            room,
            featureId,
            "Shortcut " + featureId,
            position,
            AreaMarkerType.ShortcutDoor,
            false,
            string.Empty);
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "doorId").stringValue = doorId;
        Property(serialized, "linkedDoorId").stringValue = linkedDoorId;
        Property(serialized, "isLocked").boolValue = true;
        Property(serialized, "unlockFlag").stringValue = unlockFlag;
        Property(serialized, "lockedDialogue").objectReferenceValue = lockedDialogue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ConfigureRoomTransition(marker, targetRoom, targetSpawnId, facing, DoorActivationMode.OnInteract);
        CreateBlock("Shortcut Door " + featureId, room.Props, position, new Vector2(0.65f, 1.15f), new Color(0.25f, 0.48f, 0.46f), 5);
    }

    private static void ConfigureRoomTransition(
        AreaConnectionMarker marker,
        RoomDefinition targetRoom,
        string targetSpawnId,
        FacingDirection facing,
        DoorActivationMode activationMode)
    {
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "interactToUse").boolValue = activationMode != DoorActivationMode.OnTriggerEnter;
        Property(serialized, "activationMode").enumValueIndex = (int)activationMode;
        Property(serialized, "oneShotUntilExit").boolValue = true;
        Property(serialized, "mapTransition.TransitionType").enumValueIndex = (int)MapTransitionType.Room;
        Property(serialized, "mapTransition.TargetRoom").objectReferenceValue = targetRoom;
        Property(serialized, "mapTransition.TargetRoomId").stringValue = targetRoom.RoomId;
        Property(serialized, "mapTransition.TargetAreaId").stringValue = targetRoom.RoomId;
        Property(serialized, "mapTransition.TargetSpawnPointId").stringValue = targetSpawnId;
        Property(serialized, "mapTransition.FacingAfterEnter").enumValueIndex = (int)facing;
        Property(serialized, "mapTransition.FadeDuration").floatValue = 0.18f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T CreateAreaMarker<T>(
        RoomBuildContext room,
        string featureId,
        string displayName,
        Vector2 position,
        AreaMarkerType markerType,
        bool oneShot,
        string completionFlag)
        where T : AreaMarkerBase
    {
        GameObject markerObject = CreateEmpty("Marker " + featureId, room.Markers);
        markerObject.layer = ResolveInteractableLayer();
        markerObject.transform.localPosition = position;
        CircleCollider2D collider = markerObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.48f;

        T marker = markerObject.AddComponent<T>();
        SerializedObject serialized = new SerializedObject(marker);
        Property(serialized, "markerId").stringValue = room.RoomId + "." + featureId;
        Property(serialized, "areaId").stringValue = room.RoomId;
        Property(serialized, "markerType").enumValueIndex = (int)markerType;
        Property(serialized, "displayName").stringValue = displayName;
        Property(serialized, "description").stringValue = "Showcase Station 기능 검증용 " + displayName;
        Property(serialized, "isOneShot").boolValue = oneShot;
        Property(serialized, "setFlagOnComplete").stringValue = completionFlag ?? string.Empty;
        Property(serialized, "interactionRange").floatValue = 1.35f;
        Property(serialized, "showLabelInSceneView").boolValue = true;
        Property(serialized, "gizmoColor").colorValue = AreaMarkerDefaults.GetColor(markerType);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return marker;
    }

    private static void CreateSpawn(
        RoomBuildContext room,
        string spawnId,
        Vector2 position,
        FacingDirection facing)
    {
        GameObject spawnObject = CreateEmpty("Spawn " + spawnId, room.Spawns);
        spawnObject.transform.localPosition = position;
        SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
        SetString(spawn, "_spawnPointId", spawnId);
        SetEnum(spawn, "_defaultFacing", (int)facing);
    }

    private static GameObject CreateBlock(
        string name,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Color color,
        int sortingOrder)
    {
        return CreateSpriteObject(
            name,
            parent,
            position,
            AssetDatabase.LoadAssetAtPath<Sprite>(ShowcaseStationPaths.SharedWhiteSprite),
            color,
            sortingOrder,
            size);
    }

    private static GameObject CreateSpriteObject(
        string name,
        Transform parent,
        Vector3 position,
        Sprite sprite,
        Color color,
        int sortingOrder,
        Vector2? size = null)
    {
        if (sprite == null)
            throw new InvalidOperationException("Showcase sprite dependency is missing: " + name);

        GameObject gameObject = CreateEmpty(name, parent);
        gameObject.transform.localPosition = position;
        if (size.HasValue)
            gameObject.transform.localScale = new Vector3(size.Value.x, size.Value.y, 1f);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = BackgroundSortingLayer;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static void FitSpriteHeight(GameObject gameObject, float targetHeight)
    {
        SpriteRenderer renderer = gameObject != null ? gameObject.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null || renderer.sprite.bounds.size.y <= 0f)
            return;

        float scale = targetHeight / renderer.sprite.bounds.size.y;
        gameObject.transform.localScale = new Vector3(scale, scale, 1f);
        renderer.sortingLayerName = CharacterSortingLayer;
    }

    private static void CreateWall(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject wall = CreateBlock(name, parent, position, size, color, 1);
        wall.AddComponent<BoxCollider2D>();
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static int ResolveInteractableLayer()
    {
        int layer = LayerMask.NameToLayer("Interactable");
        return layer >= 0 ? layer : 0;
    }

    private static void ValidateData(ShowcaseStationDataBundle data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Rooms.Count != ShowcaseStationIds.GeneratedRoomIds.Length)
            throw new InvalidOperationException("Showcase Station requires five RoomDefinitions.");
        if (data.SmallPotion == null
            || data.SteamEnemy == null
            || data.WorkshopShop == null
            || data.WorkshopPuzzle == null
            || data.StationNpcDialogue == null
            || data.IntroSequence == null
            || data.FinaleSequence == null
            || data.FinalePowerShot == null
            || data.FinaleDepartureShot == null)
        {
            throw new InvalidOperationException("Showcase Station data bundle is incomplete.");
        }

        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
            if (!data.Rooms.TryGetValue(roomId, out RoomDefinition definition)
                || definition == null
                || !string.Equals(definition.RoomId, roomId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Missing or mismatched RoomDefinition: " + roomId);
            }
        }
    }

    private static void EnsurePrefabPathAvailable(string path)
    {
        UnityEngine.Object occupied = AssetDatabase.LoadMainAssetAtPath(path);
        if (occupied != null && !(occupied is GameObject))
        {
            throw new InvalidOperationException(
                $"Showcase prefab path is occupied by {occupied.GetType().Name}: {path}");
        }
    }

    private static void ValidatePendingRooms(IReadOnlyList<PendingRoom> pending)
    {
        var rootsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var spawnIdsByRoom = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        for (int i = 0; i < pending.Count; i++)
        {
            PendingRoom room = pending[i];
            if (room.Root == null || !rootsById.TryAdd(room.RoomId, room.Root))
                throw new InvalidOperationException("Duplicate or missing Showcase room root: " + room.RoomId);

            RoomInstance instance = room.Root.GetComponent<RoomInstance>();
            if (instance == null || !string.Equals(instance.RoomId, room.RoomId, StringComparison.Ordinal))
                throw new InvalidOperationException("RoomInstance ID mismatch: " + room.RoomId);
            if (instance.CameraBounds == null)
                throw new InvalidOperationException("Room camera bounds missing: " + room.RoomId);

            var markerIds = new HashSet<string>(StringComparer.Ordinal);
            AreaMarkerBase[] markers = room.Root.GetComponentsInChildren<AreaMarkerBase>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                AreaMarkerBase marker = markers[markerIndex];
                if (!markerIds.Add(marker.MarkerId))
                    throw new InvalidOperationException("Duplicate marker ID: " + marker.MarkerId);

                var issues = new List<string>();
                marker.CollectValidationIssues(issues);
                if (issues.Count > 0)
                {
                    throw new InvalidOperationException(
                        marker.MarkerId + " validation failed: " + string.Join(" | ", issues));
                }
            }

            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            SpawnPoint[] spawns = room.Root.GetComponentsInChildren<SpawnPoint>(true);
            for (int spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex++)
            {
                string spawnId = spawns[spawnIndex].SpawnPointId;
                if (string.IsNullOrWhiteSpace(spawnId) || !spawnIds.Add(spawnId))
                    throw new InvalidOperationException("Duplicate or empty spawn ID in " + room.RoomId);
            }
            spawnIdsByRoom.Add(room.RoomId, spawnIds);
        }

        for (int i = 0; i < pending.Count; i++)
        {
            AreaConnectionMarker[] connections =
                pending[i].Root.GetComponentsInChildren<AreaConnectionMarker>(true);
            for (int connectionIndex = 0; connectionIndex < connections.Length; connectionIndex++)
            {
                MapTransitionRequest request = connections[connectionIndex].MapTransition;
                string targetRoomId = request != null && request.TargetRoom != null
                    ? request.TargetRoom.RoomId
                    : string.Empty;
                if (string.IsNullOrEmpty(targetRoomId)
                    || !spawnIdsByRoom.TryGetValue(targetRoomId, out HashSet<string> targetSpawns)
                    || !targetSpawns.Contains(request.TargetSpawnPointId))
                {
                    throw new InvalidOperationException(
                        $"Connection target is missing: {connections[connectionIndex].MarkerId} -> "
                        + targetRoomId + "/" + request?.TargetSpawnPointId);
                }
            }
        }
    }

    private static void SaveRooms(
        IReadOnlyList<PendingRoom> pending,
        ShowcaseStationDataBundle data)
    {
        for (int i = 0; i < pending.Count; i++)
        {
            PendingRoom room = pending[i];
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(room.Root, room.PrefabPath);
            if (saved == null)
                throw new InvalidOperationException("Failed to save Showcase room prefab: " + room.PrefabPath);

            RoomInstance prefabInstance = saved.GetComponent<RoomInstance>();
            if (prefabInstance == null)
                throw new InvalidOperationException("Saved prefab has no RoomInstance: " + room.PrefabPath);

            ShowcaseStationDataBuilder.SetObject(
                data.Rooms[room.RoomId],
                "_roomPrefab",
                prefabInstance);
            EditorUtility.SetDirty(data.Rooms[room.RoomId]);
        }
    }

    private static void RefreshAreaSummaries(ShowcaseStationDataBundle data)
    {
        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            RoomDefinition room = data.Rooms[ShowcaseStationIds.GeneratedRoomIds[i]];
            AreaDefinition area = room.AreaDefinition;
            if (area == null)
                throw new InvalidOperationException("AreaDefinition is missing: " + room.RoomId);

            area.RefreshMarkerSummary();
            if (area.InvalidMarkerCount > 0)
            {
                throw new InvalidOperationException(
                    $"Showcase area validation failed: {area.AreaId}, invalid={area.InvalidMarkerCount}");
            }
        }
    }

    private static SerializedProperty Property(SerializedObject serialized, string path)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property == null)
            throw new InvalidOperationException(
                $"Serialized property '{path}' was not found on {serialized.targetObject.GetType().Name}.");
        return property;
    }

    private static void SetString(UnityEngine.Object target, string path, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Property(serialized, path).stringValue = value ?? string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(UnityEngine.Object target, string path, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Property(serialized, path).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(UnityEngine.Object target, string path, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Property(serialized, path).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(UnityEngine.Object target, string path, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Property(serialized, path).enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string path,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Property(serialized, path).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
