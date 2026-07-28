using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WideFieldPaths
{
    public const string Root = "Assets/_Game/Content/Maps/Regions-TEST/WideField";
    public const string SceneRoot = Root + "/Scenes";
    public const string PrefabRoot = Root + "/Prefabs/Rooms";
    public const string RoomDataRoot = Root + "/Data/Rooms";
    public const string DialogueRoot = Root + "/Data/Dialogue";
    public const string Scene = SceneRoot + "/Region_WideField.unity";
    public const string StationPrefab = PrefabRoot + "/Room_WideFieldStation.prefab";
    public const string ExpansePrefab = PrefabRoot + "/Room_WideFieldExpanse.prefab";
    public const string StationDefinition = RoomDataRoot + "/Room_WideFieldStation_Definition.asset";
    public const string StationArea = RoomDataRoot + "/Room_WideFieldStation_Area.asset";
    public const string ExpanseDefinition = RoomDataRoot + "/Room_WideFieldExpanse_Definition.asset";
    public const string ExpanseArea = RoomDataRoot + "/Room_WideFieldExpanse_Area.asset";
}

public static class WideFieldIds
{
    public const string Station = "wide_field.station";
    public const string Expanse = "wide_field.expanse";
}

public static class WideFieldBuilder
{
    public static WideFieldDataBundle BuildData()
    {
        TravelTrainEditorAssetUtility.EnsureFolder(WideFieldPaths.PrefabRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(WideFieldPaths.RoomDataRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(WideFieldPaths.DialogueRoot);

        var data = new WideFieldDataBundle
        {
            StationRoom = TravelTrainEditorAssetUtility.LoadOrCreate<RoomDefinition>(
                WideFieldPaths.StationDefinition,
                out _),
            StationArea = TravelTrainEditorAssetUtility.LoadOrCreate<AreaDefinition>(
                WideFieldPaths.StationArea,
                out _),
            ExpanseRoom = TravelTrainEditorAssetUtility.LoadOrCreate<RoomDefinition>(
                WideFieldPaths.ExpanseDefinition,
                out _),
            ExpanseArea = TravelTrainEditorAssetUtility.LoadOrCreate<AreaDefinition>(
                WideFieldPaths.ExpanseArea,
                out _),
            RouteSignDialogue = TravelTrainEditorAssetUtility.BuildDialogue(
                WideFieldPaths.DialogueRoot + "/Dialogue_RouteSign.asset",
                "* 서쪽은 열차 정류소, 동쪽은 아직 조사되지 않은 들판이다.")
        };

        ConfigureData(
            data.StationRoom,
            data.StationArea,
            WideFieldIds.Station,
            "광역 지역의 열차 정류소입니다.");
        ConfigureData(
            data.ExpanseRoom,
            data.ExpanseArea,
            WideFieldIds.Expanse,
            "여러 화면에 걸쳐 이동하는 넓은 들판입니다.");

        EnsurePlaceholderPrefab(
            WideFieldPaths.StationPrefab,
            "Room_WideFieldStation",
            data.StationRoom,
            data.StationArea,
            WideFieldIds.Station);
        EnsurePlaceholderPrefab(
            WideFieldPaths.ExpansePrefab,
            "Room_WideFieldExpanse",
            data.ExpanseRoom,
            data.ExpanseArea,
            WideFieldIds.Expanse);
        AssetDatabase.SaveAssets();
        return data;
    }

    public static void BuildRooms(
        WideFieldDataBundle data,
        TravelTrainDataBundle train)
    {
        if (data == null || train?.Network == null || train.WideFieldStop == null)
            throw new ArgumentNullException(nameof(data));

        BuildStation(data, train);
        BuildExpanse(data);
        AssetDatabase.SaveAssets();
    }

    public static void BuildScene(WideFieldDataBundle data)
    {
        if (data?.StationRoom == null || data.ExpanseRoom == null)
            throw new ArgumentNullException(nameof(data));
        GeneratedRegionSceneBuilder.Build(
            WideFieldPaths.Scene,
            data.StationRoom,
            new[] { data.StationRoom, data.ExpanseRoom },
            includeBattleHost: true);
    }

    private static void BuildStation(
        WideFieldDataBundle data,
        TravelTrainDataBundle train)
    {
        GeneratedRoomContext room = GeneratedRoomEditorUtility.CreateRoom(
            "Room_WideFieldStation",
            WideFieldIds.Station,
            new Vector2(11f, 5.8f),
            new Vector2(11f, 6f),
            new Color(0.055f, 0.07f, 0.075f),
            new Color(0.18f, 0.22f, 0.20f),
            new Color(0.33f, 0.37f, 0.34f));
        try
        {
            GeneratedRoomEditorUtility.CreateBlock(
                "Station Platform",
                room.Props,
                new Vector2(-3.5f, -0.4f),
                new Vector2(3.2f, 1.8f),
                new Color(0.34f, 0.34f, 0.31f),
                2);
            GeneratedRoomEditorUtility.CreateBlock(
                "Waiting Shelter",
                room.Props,
                new Vector2(0.1f, 1.55f),
                new Vector2(3f, 0.8f),
                new Color(0.25f, 0.33f, 0.32f),
                3);
            GeneratedRoomEditorUtility.CreateSpawn(
                room, "from_train", new Vector2(-3.8f, -1f), FacingDirection.Right);
            GeneratedRoomEditorUtility.CreateSpawn(
                room, "from_expanse", new Vector2(4.5f, 0f), FacingDirection.Left);

            TrainBoardingMarker boarding = GeneratedRoomEditorUtility.CreateMarker<TrainBoardingMarker>(
                room,
                "wide_field.station.train_boarding",
                "공용 열차 타기",
                new Vector2(-4.75f, -0.2f),
                AreaMarkerType.Sublocation,
                0.6f);
            boarding.Configure(train.Network, train.WideFieldStop, 0.25f);

            GeneratedRoomEditorUtility.CreateConnection(
                room,
                "wide_field.station.to_expanse",
                new Vector2(5.35f, 0f),
                data.ExpanseRoom,
                "from_station",
                FacingDirection.Right);

            SavePointMarker save = GeneratedRoomEditorUtility.CreateMarker<SavePointMarker>(
                room,
                "wide_field.station.save",
                "SAVE Point",
                new Vector2(-0.8f, -0.75f),
                AreaMarkerType.SavePoint);
            TravelTrainEditorAssetUtility.Set(
                save,
                "savePointId",
                p => p.stringValue = "wide_field.station.save");

            SignMarker sign = GeneratedRoomEditorUtility.CreateMarker<SignMarker>(
                room,
                "wide_field.station.route_sign",
                "노선 안내판",
                new Vector2(1.35f, 0.2f),
                AreaMarkerType.Sign);
            TravelTrainEditorAssetUtility.Set(
                sign,
                "dialogueData",
                p => p.objectReferenceValue = data.RouteSignDialogue);

            GeneratedRoomEditorUtility.SaveRoom(
                room,
                WideFieldPaths.StationPrefab,
                data.StationRoom,
                data.StationArea);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(room.Root);
        }
    }

    private static void BuildExpanse(WideFieldDataBundle data)
    {
        GeneratedRoomContext room = GeneratedRoomEditorUtility.CreateRoom(
            "Room_WideFieldExpanse",
            WideFieldIds.Expanse,
            new Vector2(39.5f, 19.5f),
            new Vector2(40f, 20f),
            new Color(0.07f, 0.09f, 0.08f),
            new Color(0.21f, 0.29f, 0.20f),
            new Color(0.28f, 0.31f, 0.27f));
        try
        {
            GeneratedRoomEditorUtility.CreateSpawn(
                room, "from_station", new Vector2(-18f, 0f), FacingDirection.Right);
            GeneratedRoomEditorUtility.CreateConnection(
                room,
                "wide_field.expanse.to_station",
                new Vector2(-19.55f, 0f),
                data.StationRoom,
                "from_expanse",
                FacingDirection.Left);
            GeneratedRoomEditorUtility.CreateEmpty(
                "future_route_anchor",
                room.EventAnchors,
                new Vector3(18f, 0f, 0f));

            for (int i = 0; i < 7; i++)
            {
                float x = -14f + i * 4.7f;
                GeneratedRoomEditorUtility.CreateBlock(
                    "Distance Landmark " + (i + 1),
                    room.Props,
                    new Vector2(x, i % 2 == 0 ? 2.5f : -2.2f),
                    new Vector2(0.65f, 2.2f + (i % 3) * 0.45f),
                    new Color(0.38f, 0.42f, 0.31f),
                    2);
            }
            GeneratedRoomEditorUtility.CreateBlock(
                "Camera Scale Guide",
                room.Props,
                new Vector2(0f, -4.8f),
                new Vector2(31f, 0.35f),
                new Color(0.58f, 0.51f, 0.29f),
                1);

            GeneratedRoomEditorUtility.SaveRoom(
                room,
                WideFieldPaths.ExpansePrefab,
                data.ExpanseRoom,
                data.ExpanseArea);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(room.Root);
        }
    }

    private static void ConfigureData(
        RoomDefinition room,
        AreaDefinition area,
        string roomId,
        string description)
    {
        TravelTrainEditorAssetUtility.Set(room, "_roomId", p => p.stringValue = roomId);
        TravelTrainEditorAssetUtility.Set(room, "_areaDefinition", p => p.objectReferenceValue = area);
        TravelTrainEditorAssetUtility.Set(room, "_keepCurrentBgm", p => p.boolValue = true);
        TravelTrainEditorAssetUtility.Set(area, "_areaId", p => p.stringValue = roomId);
        TravelTrainEditorAssetUtility.Set(area, "_roomDefinition", p => p.objectReferenceValue = room);
        TravelTrainEditorAssetUtility.Set(area, "_description", p => p.stringValue = description);
    }

    private static void EnsurePlaceholderPrefab(
        string path,
        string rootName,
        RoomDefinition definition,
        AreaDefinition area,
        string roomId)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            RoomInstance existingRoom = existing.GetComponent<RoomInstance>();
            if (existingRoom != null)
            {
                TravelTrainEditorAssetUtility.Set(
                    definition,
                    "_roomPrefab",
                    p => p.objectReferenceValue = existingRoom);
                return;
            }
        }

        GeneratedRoomContext room = GeneratedRoomEditorUtility.CreateRoom(
            rootName,
            roomId,
            new Vector2(8f, 4f),
            new Vector2(9f, 5f),
            Color.black,
            new Color(0.15f, 0.18f, 0.16f),
            new Color(0.3f, 0.3f, 0.3f));
        try
        {
            GeneratedRoomEditorUtility.CreateSpawn(
                room,
                roomId == WideFieldIds.Station ? "from_train" : "from_station",
                Vector2.zero,
                FacingDirection.Right);
            GeneratedRoomEditorUtility.SaveRoom(room, path, definition, area);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(room.Root);
        }
    }
}