using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoomMapValidationScanner
{
    public static RoomMapValidationReport Scan(RoomMapValidationInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        RoomInstance[] rooms = input.Rooms ?? Array.Empty<RoomInstance>();
        AreaMarkerBase[] markers = input.Markers ?? Array.Empty<AreaMarkerBase>();
        OverworldEnemy[] overworldEnemies = input.OverworldEnemies ?? Array.Empty<OverworldEnemy>();
        SpawnPoint[] spawnPoints = input.SpawnPoints ?? Array.Empty<SpawnPoint>();
        DoorTransition[] doors = input.Doors ?? Array.Empty<DoorTransition>();

        var report = new RoomMapValidationReport(input.ScopeName)
        {
            RoomCount = CountNonNull(rooms),
            SpawnPointCount = CountNonNull(spawnPoints),
            DoorCount = CountNonNull(doors)
        };

        var roomSet = new HashSet<RoomInstance>();
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null)
                roomSet.Add(rooms[i]);
        }

        ValidateSceneInfrastructure(input, report);
        ValidateMarkers(markers, roomSet, report);
        CollectOverworldEnemies(overworldEnemies, roomSet, report);
        ValidateRoomBounds(rooms, report);
        ValidateSpawnPoints(spawnPoints, input.RequiredSpawnPointIds, report);
        ValidateDoors(doors, spawnPoints, report);
        ValidateConnections(markers, spawnPoints, report);

        report.Sort();
        return report;
    }

    private static void ValidateSceneInfrastructure(
        RoomMapValidationInput input,
        RoomMapValidationReport report)
    {
        if (!input.RequiresSceneInfrastructure)
            return;

        if (CountNonNull(input.MapTransitionServices) == 0)
        {
            report.AddIssue(new RoomMapValidationIssue(
                RoomMapValidationCodes.MapTransitionServiceMissing,
                RoomMapValidationSeverity.Error,
                "현재 Scene 범위에 MapTransitionService가 없습니다."));
        }

        if (CountNonNull(input.RoomContainers) == 0)
        {
            report.AddIssue(new RoomMapValidationIssue(
                RoomMapValidationCodes.RoomContainerMissing,
                RoomMapValidationSeverity.Error,
                "현재 Scene 범위에 RoomContainer가 없습니다."));
        }
    }

    private static void ValidateMarkers(
        AreaMarkerBase[] markers,
        HashSet<RoomInstance> roomSet,
        RoomMapValidationReport report)
    {
        var markersByRoom = new Dictionary<RoomInstance, Dictionary<string, List<AreaMarkerBase>>>();

        for (int i = 0; i < markers.Length; i++)
        {
            AreaMarkerBase marker = markers[i];
            if (marker == null)
                continue;

            RoomInstance room = marker.GetComponentInParent<RoomInstance>(true);
            if (room == null || !roomSet.Contains(room))
                room = null;

            report.AddMarker(marker, room);
            if (room == null)
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.MarkerUnbound,
                    RoomMapValidationSeverity.Warning,
                    $"마커 '{marker.DisplayName}'가 RoomInstance 하위에 있지 않습니다.",
                    marker,
                    null,
                    marker));
            }

            var validationMessages = new List<string>();
            marker.CollectValidationIssues(validationMessages);
            for (int messageIndex = 0; messageIndex < validationMessages.Count; messageIndex++)
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.MarkerConfiguration,
                    RoomMapValidationSeverity.Error,
                    validationMessages[messageIndex],
                    marker,
                    room,
                    marker));
            }

            if (room == null || string.IsNullOrWhiteSpace(marker.MarkerId))
                continue;

            if (!markersByRoom.TryGetValue(
                    room,
                    out Dictionary<string, List<AreaMarkerBase>> markersById))
            {
                markersById = new Dictionary<string, List<AreaMarkerBase>>(StringComparer.Ordinal);
                markersByRoom.Add(room, markersById);
            }

            string normalizedId = marker.MarkerId.Trim();
            if (!markersById.TryGetValue(normalizedId, out List<AreaMarkerBase> duplicates))
            {
                duplicates = new List<AreaMarkerBase>();
                markersById.Add(normalizedId, duplicates);
            }

            duplicates.Add(marker);
        }

        foreach (KeyValuePair<RoomInstance, Dictionary<string, List<AreaMarkerBase>>> roomEntry
                 in markersByRoom)
        {
            foreach (KeyValuePair<string, List<AreaMarkerBase>> idEntry in roomEntry.Value)
            {
                if (idEntry.Value.Count <= 1)
                    continue;

                for (int i = 0; i < idEntry.Value.Count; i++)
                {
                    AreaMarkerBase marker = idEntry.Value[i];
                    report.AddIssue(new RoomMapValidationIssue(
                        RoomMapValidationCodes.DuplicateMarkerId,
                        RoomMapValidationSeverity.Error,
                        $"같은 Room 안에 Marker ID '{idEntry.Key}'가 {idEntry.Value.Count}개 있습니다.",
                        marker,
                        roomEntry.Key,
                        marker));
                }
            }
        }
    }

    private static void CollectOverworldEnemies(
        OverworldEnemy[] enemies,
        HashSet<RoomInstance> roomSet,
        RoomMapValidationReport report)
    {
        var instanceIds = new HashSet<int>();
        for (int i = 0; i < enemies.Length; i++)
        {
            OverworldEnemy enemy = enemies[i];
            if (enemy == null || !instanceIds.Add(enemy.GetInstanceID()))
                continue;

            RoomInstance room = enemy.GetComponentInParent<RoomInstance>(true);
            if (room == null || !roomSet.Contains(room))
                room = null;
            report.AddEnemy(enemy, room);
        }
    }

    private static void ValidateRoomBounds(
        RoomInstance[] rooms,
        RoomMapValidationReport report)
    {
        for (int i = 0; i < rooms.Length; i++)
        {
            RoomInstance room = rooms[i];
            if (room == null)
                continue;

            PolygonCollider2D bounds = room.CameraBounds;
            if (bounds == null)
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.RoomBoundsMissing,
                    RoomMapValidationSeverity.Warning,
                    $"Room '{GetRoomLabel(room)}'에 Camera Bounds가 없어 마커 이탈을 검사할 수 없습니다.",
                    room,
                    room));
                continue;
            }

            IReadOnlyList<RoomMapMarkerEntry> entries = report.Markers;
            for (int markerIndex = 0; markerIndex < entries.Count; markerIndex++)
            {
                RoomMapMarkerEntry entry = entries[markerIndex];
                if (entry.Room != room || entry.Marker == null)
                    continue;

                if (bounds.OverlapPoint(entry.Marker.transform.position))
                    continue;

                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.MarkerOutsideBounds,
                    RoomMapValidationSeverity.Warning,
                    $"마커 '{entry.Marker.DisplayName}'가 Room Camera Bounds 밖에 있습니다.",
                    entry.Marker,
                    room,
                    entry.Marker));
            }
        }
    }

    private static void ValidateSpawnPoints(
        SpawnPoint[] spawnPoints,
        string[] requiredSpawnPointIds,
        RoomMapValidationReport report)
    {
        var pointsById = new Dictionary<string, List<SpawnPoint>>(StringComparer.Ordinal);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnPoint spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
                continue;

            string spawnPointId = NormalizeId(spawnPoint.SpawnPointId);
            if (string.IsNullOrEmpty(spawnPointId))
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.SpawnPointIdMissing,
                    RoomMapValidationSeverity.Error,
                    $"SpawnPoint '{spawnPoint.name}'의 ID가 비어 있습니다.",
                    spawnPoint));
                continue;
            }

            if (!pointsById.TryGetValue(spawnPointId, out List<SpawnPoint> duplicates))
            {
                duplicates = new List<SpawnPoint>();
                pointsById.Add(spawnPointId, duplicates);
            }

            duplicates.Add(spawnPoint);
        }

        foreach (KeyValuePair<string, List<SpawnPoint>> entry in pointsById)
        {
            if (entry.Value.Count <= 1)
                continue;

            for (int i = 0; i < entry.Value.Count; i++)
            {
                SpawnPoint spawnPoint = entry.Value[i];
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.DuplicateSpawnPointId,
                    RoomMapValidationSeverity.Error,
                    $"현재 편집 범위에 SpawnPoint ID '{entry.Key}'가 {entry.Value.Count}개 있습니다.",
                    spawnPoint));
            }
        }

        var checkedRequiredIds = new HashSet<string>(StringComparer.Ordinal);
        string[] required = requiredSpawnPointIds ?? Array.Empty<string>();
        for (int i = 0; i < required.Length; i++)
        {
            string requiredId = NormalizeId(required[i]);
            if (string.IsNullOrEmpty(requiredId) || !checkedRequiredIds.Add(requiredId))
                continue;

            if (pointsById.ContainsKey(requiredId))
                continue;

            report.AddIssue(new RoomMapValidationIssue(
                RoomMapValidationCodes.RequiredSpawnPointMissing,
                RoomMapValidationSeverity.Error,
                $"필수 SpawnPoint ID '{requiredId}'를 찾을 수 없습니다."));
        }
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
    private static void ValidateDoors(
        DoorTransition[] doors,
        SpawnPoint[] currentScopeSpawnPoints,
        RoomMapValidationReport report)
    {
        for (int i = 0; i < doors.Length; i++)
        {
            DoorTransition door = doors[i];
            if (door == null)
                continue;

            MapTransitionRequest request = door.Request;
            if (request == null)
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.InvalidTransition,
                    RoomMapValidationSeverity.Error,
                    "DoorTransition 요청이 비어 있습니다.",
                    door));
                continue;
            }

            if (!request.IsValid(out string error))
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.InvalidTransition,
                    RoomMapValidationSeverity.Error,
                    error,
                    door));
                continue;
            }

            ValidateTransitionTarget(request, door, currentScopeSpawnPoints, report);
        }
    }

    private static void ValidateConnections(
        AreaMarkerBase[] markers,
        SpawnPoint[] currentScopeSpawnPoints,
        RoomMapValidationReport report)
    {
        for (int i = 0; i < markers.Length; i++)
        {
            if (!(markers[i] is AreaConnectionMarker connection))
                continue;

            MapTransitionRequest request = connection.MapTransition;
            if (request == null || !request.IsValid(out _))
                continue;

            ValidateTransitionTarget(request, connection, currentScopeSpawnPoints, report);
        }
    }

    private static void ValidateTransitionTarget(
        MapTransitionRequest request,
        UnityEngine.Object context,
        SpawnPoint[] currentScopeSpawnPoints,
        RoomMapValidationReport report)
    {
        if (request.TransitionType == MapTransitionType.Room)
        {
            RoomDefinition targetRoom = request.TargetRoom;
            if (targetRoom == null || !targetRoom.IsValid)
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.TargetRoomInvalid,
                    RoomMapValidationSeverity.Error,
                    "Room 전환 대상 RoomDefinition 또는 Room Prefab이 유효하지 않습니다.",
                    context));
                return;
            }

            if (string.IsNullOrWhiteSpace(request.TargetSpawnPointId))
                return;

            SpawnPoint[] targetPoints = targetRoom.RoomPrefab.GetComponentsInChildren<SpawnPoint>(true);
            if (!ContainsSpawnPoint(targetPoints, request.TargetSpawnPointId))
            {
                report.AddIssue(new RoomMapValidationIssue(
                    RoomMapValidationCodes.TargetSpawnMissing,
                    RoomMapValidationSeverity.Error,
                    $"대상 Room '{targetRoom.RoomId}'에 SpawnPoint ID '{request.TargetSpawnPointId}'가 없습니다.",
                    context));
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetSpawnPointId)
            && !ContainsSpawnPoint(currentScopeSpawnPoints, request.TargetSpawnPointId))
        {
            report.AddIssue(new RoomMapValidationIssue(
                RoomMapValidationCodes.TargetSpawnUnresolved,
                RoomMapValidationSeverity.Warning,
                $"현재 편집 범위에서 Scene 전환 대상 SpawnPoint ID '{request.TargetSpawnPointId}'를 확인할 수 없습니다.",
                context));
        }
    }

    private static bool ContainsSpawnPoint(SpawnPoint[] points, string spawnPointId)
    {
        if (points == null || string.IsNullOrWhiteSpace(spawnPointId))
            return false;

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null
                && string.Equals(points[i].SpawnPointId, spawnPointId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountNonNull<T>(T[] items) where T : UnityEngine.Object
    {
        if (items == null)
            return 0;

        int count = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                count++;
        }

        return count;
    }

    private static string GetRoomLabel(RoomInstance room)
    {
        return !string.IsNullOrWhiteSpace(room.RoomId) ? room.RoomId : room.name;
    }
}
