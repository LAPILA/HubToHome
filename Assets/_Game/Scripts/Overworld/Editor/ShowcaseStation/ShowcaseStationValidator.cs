using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ShowcaseStationValidationReport
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();

    public bool IsValid => Errors.Count == 0;

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Errors.Add(message.Trim());
    }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Warnings.Add(message.Trim());
    }
}

public static class ShowcaseStationValidator
{
    [MenuItem("HubToHome/오버월드/본편 월드/Showcase Station 검증")]
    public static void ValidateAndLog()
    {
        ShowcaseStationValidationReport report = ValidateGeneratedAssets();
        for (int i = 0; i < report.Warnings.Count; i++)
            Debug.LogWarning("[ShowcaseStationValidator] " + report.Warnings[i]);

        if (!report.IsValid)
        {
            throw new InvalidOperationException(
                "[ShowcaseStationValidator] 검증 실패\n- "
                + string.Join("\n- ", report.Errors));
        }

        Debug.Log("[ShowcaseStationValidator] 검증 통과: 메인 5개 Room과 Scenario Source가 정상입니다.");
    }

    public static ShowcaseStationValidationReport ValidateGeneratedAssets()
    {
        var report = new ShowcaseStationValidationReport();
        var rooms = new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);
        var spawnIdsByRoom = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
            RoomDefinition room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
                GetRoomDefinitionPath(roomId));
            if (room == null)
            {
                report.AddError("RoomDefinition이 없습니다: " + roomId);
                continue;
            }

            if (!string.Equals(room.RoomId, roomId, StringComparison.Ordinal))
                report.AddError($"Room ID가 일치하지 않습니다: expected={roomId}, actual={room.RoomId}");
            if (!room.IsValid)
            {
                report.AddError("RoomDefinition이 유효하지 않습니다: " + roomId);
                continue;
            }
            if (!rooms.TryAdd(roomId, room))
                report.AddError("Room ID가 중복됩니다: " + roomId);

            ValidateRoomPrefab(room, report, out HashSet<string> spawnIds);
            spawnIdsByRoom[roomId] = spawnIds;
        }

        ValidateConnections(rooms, spawnIdsByRoom, report);
        ValidateDataAssets(report);
        ValidateSequence(
            ShowcaseStationPaths.IntroRuntime,
            ShowcaseStationPaths.IntroSource,
            report);
        ValidateSequence(
            ShowcaseStationPaths.FinaleRuntime,
            ShowcaseStationPaths.FinaleSource,
            report);
        return report;
    }

    internal static string GetRoomDefinitionPath(string roomId)
    {
        return ShowcaseStationPaths.RoomDataRoot + "/"
            + ShowcaseStationDataBuilder.RoomAssetStem(roomId)
            + "_Definition.asset";
    }

    private static void ValidateRoomPrefab(
        RoomDefinition room,
        ShowcaseStationValidationReport report,
        out HashSet<string> spawnIds)
    {
        spawnIds = new HashSet<string>(StringComparer.Ordinal);
        GameObject prefab = room.RoomPrefab.gameObject;
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            report.AddError("Room Prefab 경로가 없습니다: " + room.RoomId);
            return;
        }

        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
            report.AddError("Missing Script가 있습니다: " + prefabPath);
        if (room.RoomPrefab.CameraBounds == null)
            report.AddError("CameraBounds가 없습니다: " + room.RoomId);

        string[] requiredRoots =
        {
            "Background",
            "Floor",
            "Walls",
            "Props",
            "Gameplay/Markers",
            "Gameplay/Systems",
            "Gameplay/Spawns",
            "CameraBounds"
        };
        for (int i = 0; i < requiredRoots.Length; i++)
        {
            if (prefab.transform.Find(requiredRoots[i]) == null)
                report.AddError($"{room.RoomId}: 필수 계층이 없습니다: {requiredRoots[i]}");
        }

        SpawnPoint[] spawns = prefab.GetComponentsInChildren<SpawnPoint>(true);
        for (int i = 0; i < spawns.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(spawns[i].SpawnPointId))
                report.AddError(room.RoomId + ": 빈 SpawnPoint ID가 있습니다.");
            else if (!spawnIds.Add(spawns[i].SpawnPointId))
                report.AddError(room.RoomId + ": SpawnPoint ID가 중복됩니다: " + spawns[i].SpawnPointId);
        }
        if (spawnIds.Count == 0)
            report.AddError(room.RoomId + ": SpawnPoint가 없습니다.");

        var markerIds = new HashSet<string>(StringComparer.Ordinal);
        AreaMarkerBase[] markers = prefab.GetComponentsInChildren<AreaMarkerBase>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            AreaMarkerBase marker = markers[i];
            if (!markerIds.Add(marker.MarkerId))
                report.AddError(room.RoomId + ": Marker ID가 중복됩니다: " + marker.MarkerId);
            if (!string.Equals(marker.AreaId, room.RoomId, StringComparison.Ordinal))
                report.AddError(marker.MarkerId + ": Area ID가 Room ID와 다릅니다.");
            if (marker.GetComponent<SpriteRenderer>() != null)
                report.AddWarning(marker.MarkerId + ": Marker GameObject에 런타임 SpriteRenderer가 있습니다.");

            var issues = new List<string>();
            marker.CollectValidationIssues(issues);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                report.AddError(marker.MarkerId + ": " + issues[issueIndex]);
        }

        ValidateFeatureComposition(room.RoomId, prefab, report);
    }

    private static void ValidateFeatureComposition(
        string roomId,
        GameObject prefab,
        ShowcaseStationValidationReport report)
    {
        if (roomId == ShowcaseStationIds.Arrival)
        {
            RequireCount<PlotPointMarker>(prefab, 1, roomId, report);
            RequireCount<SavePointMarker>(prefab, 1, roomId, report);
            RequireCount<SignMarker>(prefab, 1, roomId, report);
            RequireCount<AreaConnectionMarker>(prefab, 1, roomId, report);
        }
        else if (roomId == ShowcaseStationIds.Square)
        {
            RequireCount<NPCMarker>(prefab, 1, roomId, report);
            RequireCount<ItemPickupMarker>(prefab, 1, roomId, report);
            RequireCount<AreaConnectionMarker>(prefab, 4, roomId, report);
            RequireCount<ShortcutDoorMarker>(prefab, 1, roomId, report);
        }
        else if (roomId == ShowcaseStationIds.Workshop)
        {
            RequireCount<VendorMarker>(prefab, 1, roomId, report);
            RequireCount<PuzzleMarker>(prefab, 1, roomId, report);
            RequireCount<PuzzleSwitch>(prefab, 3, roomId, report);
            RequireCount<SequencePuzzleController>(prefab, 1, roomId, report);
            RequireCount<FlagStateBinder>(prefab, 1, roomId, report);
        }
        else if (roomId == ShowcaseStationIds.Passage)
        {
            RequireCount<HazardMarker>(prefab, 1, roomId, report);
            RequireCount<PeriodicHazardController>(prefab, 1, roomId, report);
            RequireCount<OverworldEnemy>(prefab, 1, roomId, report);
            RequireCount<ShortcutDoorMarker>(prefab, 1, roomId, report);
            RequireCount<AreaConnectionMarker>(prefab, 3, roomId, report);
        }
        else if (roomId == ShowcaseStationIds.Train)
        {
            RequireCount<PowerConsoleInteractable>(prefab, 1, roomId, report);
            RequireCount<SceneActionSequencePlayer>(prefab, 1, roomId, report);
            RequireCount<TrainBoardingMarker>(prefab, 1, roomId, report);
            if (prefab.GetComponentsInChildren<SublocationMarker>(true).Length != 0)
                report.AddError(roomId + ": legacy SublocationMarker가 남아 있습니다.");
            RequireCount<AreaConnectionMarker>(prefab, 1, roomId, report);
            SpawnPoint[] trainSpawns = prefab.GetComponentsInChildren<SpawnPoint>(true);
            bool hasTrainReturn = false;
            for (int i = 0; i < trainSpawns.Length; i++)
            {
                if (string.Equals(
                    trainSpawns[i].SpawnPointId,
                    "from_travel_train",
                    StringComparison.Ordinal))
                {
                    hasTrainReturn = true;
                    break;
                }
            }
            if (!hasTrainReturn)
                report.AddError(roomId + ": from_travel_train SpawnPoint가 없습니다.");
        }
    }

    private static void ValidateConnections(
        IReadOnlyDictionary<string, RoomDefinition> rooms,
        IReadOnlyDictionary<string, HashSet<string>> spawnIdsByRoom,
        ShowcaseStationValidationReport report)
    {
        foreach (KeyValuePair<string, RoomDefinition> pair in rooms)
        {
            AreaConnectionMarker[] connections =
                pair.Value.RoomPrefab.GetComponentsInChildren<AreaConnectionMarker>(true);
            for (int i = 0; i < connections.Length; i++)
            {
                AreaConnectionMarker connection = connections[i];
                MapTransitionRequest request = connection.MapTransition;
                if (request == null || request.TargetRoom == null)
                {
                    report.AddError(connection.MarkerId + ": 대상 Room이 없습니다.");
                    continue;
                }

                string targetRoomId = request.TargetRoom.RoomId;
                if (!rooms.ContainsKey(targetRoomId))
                {
                    report.AddError(connection.MarkerId + ": Showcase 밖의 Room을 가리킵니다: " + targetRoomId);
                    continue;
                }

                if (!spawnIdsByRoom.TryGetValue(targetRoomId, out HashSet<string> spawnIds)
                    || !spawnIds.Contains(request.TargetSpawnPointId))
                {
                    report.AddError(
                        connection.MarkerId + ": 대상 SpawnPoint가 없습니다: "
                        + targetRoomId + "/" + request.TargetSpawnPointId);
                }
            }
        }
    }

    private static void ValidateDataAssets(ShowcaseStationValidationReport report)
    {
        ShopDefinition shop = AssetDatabase.LoadAssetAtPath<ShopDefinition>(
            ShowcaseStationPaths.ShopRoot + "/Shop_Workshop.asset");
        if (shop == null)
            report.AddError("Workshop Shop 오류: asset missing");
        else if (!shop.TryValidate(out string shopError))
            report.AddError("Workshop Shop 오류: " + shopError);

        SequencePuzzleDefinition puzzle =
            AssetDatabase.LoadAssetAtPath<SequencePuzzleDefinition>(
                ShowcaseStationPaths.PuzzleRoot + "/Puzzle_WorkshopPower.asset");
        if (puzzle == null)
            report.AddError("Workshop Puzzle 오류: asset missing");
        else if (!puzzle.TryValidate(out string puzzleError))
            report.AddError("Workshop Puzzle 오류: " + puzzleError);

        FlagDialogueSelector selector =
            AssetDatabase.LoadAssetAtPath<FlagDialogueSelector>(
                ShowcaseStationPaths.DialogueRoot + "/StationNpcDialogueSelector.asset");
        if (selector == null)
            report.AddError("Station NPC Dialogue 오류: asset missing");
        else if (!selector.TryValidate(out string selectorError))
            report.AddError("Station NPC Dialogue 오류: " + selectorError);
    }

    private static void ValidateSequence(
        string runtimePath,
        string sourcePath,
        ShowcaseStationValidationReport report)
    {
        ActionSequenceAsset sequence =
            AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(runtimePath);
        if (sequence == null || sequence.Source == null)
        {
            report.AddError("Runtime Sequence가 없습니다: " + runtimePath);
            return;
        }

        if (!File.Exists(Path.GetFullPath(sourcePath)))
        {
            report.AddError("Scenario Source가 없습니다: " + sourcePath);
            return;
        }

        string sourceText = File.ReadAllText(Path.GetFullPath(sourcePath));
        if (!string.Equals(
                sequence.Source.SourceHash,
                ScenarioSourceHash.Compute(sourceText),
                StringComparison.Ordinal))
        {
            report.AddError("Scenario Source Hash가 다릅니다: " + runtimePath);
        }

        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        if (catalog == null)
        {
            report.AddError("Production Action Catalog가 없습니다.");
            return;
        }

        if (ScenarioCatalogValidator.ValidateSequence(sequence, catalog).HasErrors)
            report.AddError("Runtime Sequence Catalog 검증 실패: " + runtimePath);
    }

    private static void RequireCount<T>(
        GameObject prefab,
        int minimum,
        string roomId,
        ShowcaseStationValidationReport report)
        where T : Component
    {
        int count = prefab.GetComponentsInChildren<T>(true).Length;
        if (count < minimum)
        {
            report.AddError(
                $"{roomId}: {typeof(T).Name}이 부족합니다. expected>={minimum}, actual={count}");
        }
    }
}
