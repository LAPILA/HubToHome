using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TrainTravelValidationIssue
{
    public TrainTravelValidationIssue(
        string code,
        RoomMapValidationSeverity severity,
        string message,
        UnityEngine.Object context = null)
    {
        Code = code ?? string.Empty;
        Severity = severity;
        Message = message ?? string.Empty;
        Context = context;
    }

    public string Code { get; }
    public RoomMapValidationSeverity Severity { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }
}

public sealed class TrainTravelValidationReport
{
    private readonly List<TrainTravelValidationIssue> _issues =
        new List<TrainTravelValidationIssue>();

    public IReadOnlyList<TrainTravelValidationIssue> Issues => _issues;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;

    public void Add(TrainTravelValidationIssue issue)
    {
        if (issue == null)
            return;
        _issues.Add(issue);
        if (issue.Severity == RoomMapValidationSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }
}

public static class TrainTravelContentValidator
{
    public static TrainTravelValidationReport Validate(TrainNetworkDefinition network)
    {
        var report = new TrainTravelValidationReport();
        if (network == null)
        {
            AddError(report, "TRAIN_NETWORK_MISSING", "TrainNetworkDefinition이 없습니다.");
            return report;
        }
        if (!network.TryValidateRuntime(out string networkError))
            AddError(report, "TRAIN_NETWORK_INVALID", networkError, network);

        ValidateRoom(
            network.TrainRoom,
            new[] { network.TrainEntrySpawnPointId, "exit" },
            report,
            "TRAIN_ROOM");
        ValidateScene(
            network.TrainSceneName,
            network.TrainRoom,
            report);

        var markerIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<TrainStopDefinition> stops = network.Stops;
        for (int i = 0; i < stops.Count; i++)
        {
            TrainStopDefinition stop = stops[i];
            if (stop == null)
                continue;
            ValidateRoom(
                stop.TargetRoom,
                new[] { stop.TargetSpawnPointId },
                report,
                "TRAIN_STOP_ROOM");
            ValidateScene(stop.TargetSceneName, stop.TargetRoom, report);
            string markerId = stop.TargetRoom.RoomId + ".train_boarding";
            if (!markerIds.Add(markerId))
                AddError(report, "TRAIN_BOARDING_ID_DUPLICATE", "승차 Marker ID가 중복됩니다: " + markerId, stop);
        }

        return report;
    }

    private static void ValidateRoom(
        RoomDefinition room,
        string[] requiredSpawns,
        TrainTravelValidationReport report,
        string codePrefix)
    {
        if (room == null || !room.IsValid)
        {
            AddError(report, codePrefix + "_INVALID", "RoomDefinition이 유효하지 않습니다.", room);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(
            AssetDatabase.GetAssetPath(room.RoomPrefab.gameObject));
        try
        {
            RoomMapValidationInput input = RoomMapValidationScopeCapture.CaptureRoots(
                new[] { root },
                room.RoomId,
                false);
            input.RequiredSpawnPointIds = requiredSpawns ?? Array.Empty<string>();
            RoomMapValidationReport roomReport = RoomMapValidationScanner.Scan(input);
            for (int i = 0; i < roomReport.Issues.Count; i++)
            {
                RoomMapValidationIssue issue = roomReport.Issues[i];
                report.Add(new TrainTravelValidationIssue(
                    issue.Code,
                    issue.Severity,
                    room.RoomId + ": " + issue.Message,
                    issue.Context));
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateScene(
        string sceneName,
        RoomDefinition room,
        TrainTravelValidationReport report)
    {
        EditorBuildSettingsScene[] matches = EditorBuildSettings.scenes
            .Where(scene => scene.enabled
                && string.Equals(
                    Path.GetFileNameWithoutExtension(scene.path),
                    sceneName,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            AddError(report, "TRAIN_SCENE_NOT_IN_BUILD", "Build Settings에 Scene이 없습니다: " + sceneName, room);
            return;
        }
        if (matches.Length > 1)
        {
            AddError(report, "TRAIN_SCENE_AMBIGUOUS", "동일한 Scene 이름이 여러 개 등록됐습니다: " + sceneName, room);
            return;
        }

        Scene scene = FindLoaded(matches[0].path);
        bool opened = !scene.IsValid();
        Scene previous = SceneManager.GetActiveScene();
        try
        {
            if (opened)
                scene = EditorSceneManager.OpenScene(matches[0].path, OpenSceneMode.Additive);
            RegionEntryCoordinator[] coordinators = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RegionEntryCoordinator>(true))
                .ToArray();
            if (coordinators.Length == 0)
            {
                AddError(report, "TRAIN_SCENE_COORDINATOR_MISSING", "RegionEntryCoordinator가 없습니다: " + sceneName, room);
                return;
            }
            if (coordinators.Length > 1)
            {
                AddError(report, "TRAIN_SCENE_COORDINATOR_DUPLICATE", "RegionEntryCoordinator가 중복됩니다: " + sceneName, room);
                return;
            }

            var serialized = new SerializedObject(coordinators[0]);
            bool registered = ReferenceEquals(
                serialized.FindProperty("_defaultRoom").objectReferenceValue,
                room);
            SerializedProperty rooms = serialized.FindProperty("_rooms");
            for (int i = 0; i < rooms.arraySize && !registered; i++)
            {
                registered = ReferenceEquals(
                    rooms.GetArrayElementAtIndex(i).objectReferenceValue,
                    room);
            }
            if (!registered)
            {
                AddError(report, "TRAIN_SCENE_ROOM_UNREGISTERED", "Scene에 RoomDefinition이 등록되지 않았습니다: " + room.RoomId, room);
            }
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Scene FindLoaded(string path)
    {
        string normalized = path.Replace('\\', '/');
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (string.Equals(scene.path.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                return scene;
        }
        return default;
    }

    private static void AddError(
        TrainTravelValidationReport report,
        string code,
        string message,
        UnityEngine.Object context = null)
    {
        report.Add(new TrainTravelValidationIssue(
            code,
            RoomMapValidationSeverity.Error,
            message,
            context));
    }
}