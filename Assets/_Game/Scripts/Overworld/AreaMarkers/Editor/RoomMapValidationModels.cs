using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomMapValidationSeverity
{
    Error,
    Warning
}

public static class RoomMapValidationCodes
{
    public const string MarkerConfiguration = "AREA_MARKER_CONFIGURATION";
    public const string DuplicateMarkerId = "AREA_MARKER_DUPLICATE_ID";
    public const string MarkerUnbound = "AREA_MARKER_UNBOUND";
    public const string RoomBoundsMissing = "ROOM_BOUNDS_MISSING";
    public const string MarkerOutsideBounds = "AREA_MARKER_OUTSIDE_BOUNDS";
    public const string SpawnPointIdMissing = "SPAWN_POINT_ID_MISSING";
    public const string DuplicateSpawnPointId = "SPAWN_POINT_DUPLICATE_ID";
    public const string InvalidTransition = "MAP_TRANSITION_INVALID";
    public const string TargetRoomInvalid = "MAP_TRANSITION_TARGET_ROOM_INVALID";
    public const string TargetSpawnMissing = "MAP_TRANSITION_TARGET_SPAWN_MISSING";
    public const string TargetSpawnUnresolved = "MAP_TRANSITION_TARGET_SPAWN_UNRESOLVED";
    public const string MapTransitionServiceMissing = "MAP_TRANSITION_SERVICE_MISSING";
    public const string RoomContainerMissing = "ROOM_CONTAINER_MISSING";
}

public sealed class RoomMapValidationIssue
{
    public RoomMapValidationIssue(
        string code,
        RoomMapValidationSeverity severity,
        string message,
        UnityEngine.Object context = null,
        RoomInstance room = null,
        AreaMarkerBase marker = null)
    {
        Code = code ?? string.Empty;
        Severity = severity;
        Message = message ?? string.Empty;
        Context = context;
        Room = room;
        Marker = marker;
    }

    public string Code { get; }
    public RoomMapValidationSeverity Severity { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }
    public RoomInstance Room { get; }
    public AreaMarkerBase Marker { get; }
    public bool CanSelect => Context != null;
}

public sealed class RoomMapMarkerEntry
{
    public RoomMapMarkerEntry(AreaMarkerBase marker, RoomInstance room)
    {
        Marker = marker;
        Room = room;
    }

    public AreaMarkerBase Marker { get; }
    public RoomInstance Room { get; }
    public string RoomId => Room != null ? Room.RoomId : string.Empty;
}

public sealed class RoomMapValidationReport
{
    private readonly List<RoomMapMarkerEntry> _markers = new List<RoomMapMarkerEntry>();
    private readonly List<RoomMapValidationIssue> _issues = new List<RoomMapValidationIssue>();

    public RoomMapValidationReport(string scopeName)
    {
        ScopeName = string.IsNullOrWhiteSpace(scopeName) ? "Current Authoring Scope" : scopeName;
    }

    public string ScopeName { get; }
    public IReadOnlyList<RoomMapMarkerEntry> Markers => _markers;
    public IReadOnlyList<RoomMapValidationIssue> Issues => _issues;
    public int RoomCount { get; internal set; }
    public int SpawnPointCount { get; internal set; }
    public int DoorCount { get; internal set; }
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;

    public int GetIssueCount(AreaMarkerBase marker)
    {
        if (marker == null)
            return 0;

        int count = 0;
        for (int i = 0; i < _issues.Count; i++)
        {
            if (_issues[i].Marker == marker)
                count++;
        }

        return count;
    }

    public bool HasIssue(AreaMarkerBase marker)
    {
        return GetIssueCount(marker) > 0;
    }

    internal void AddMarker(AreaMarkerBase marker, RoomInstance room)
    {
        if (marker != null)
            _markers.Add(new RoomMapMarkerEntry(marker, room));
    }

    internal void AddIssue(RoomMapValidationIssue issue)
    {
        if (issue == null)
            return;

        _issues.Add(issue);
        if (issue.Severity == RoomMapValidationSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    internal void Sort()
    {
        _markers.Sort(CompareMarkers);
        _issues.Sort(CompareIssues);
    }

    private static int CompareMarkers(RoomMapMarkerEntry left, RoomMapMarkerEntry right)
    {
        int room = StringComparer.Ordinal.Compare(left?.RoomId, right?.RoomId);
        if (room != 0)
            return room;

        string leftId = left?.Marker != null ? left.Marker.MarkerId : string.Empty;
        string rightId = right?.Marker != null ? right.Marker.MarkerId : string.Empty;
        int marker = StringComparer.Ordinal.Compare(leftId, rightId);
        if (marker != 0)
            return marker;

        int leftInstance = left?.Marker != null ? left.Marker.GetInstanceID() : 0;
        int rightInstance = right?.Marker != null ? right.Marker.GetInstanceID() : 0;
        return leftInstance.CompareTo(rightInstance);
    }

    private static int CompareIssues(RoomMapValidationIssue left, RoomMapValidationIssue right)
    {
        int severity = left.Severity.CompareTo(right.Severity);
        if (severity != 0)
            return severity;

        string leftRoom = left.Room != null ? left.Room.RoomId : string.Empty;
        string rightRoom = right.Room != null ? right.Room.RoomId : string.Empty;
        int room = StringComparer.Ordinal.Compare(leftRoom, rightRoom);
        if (room != 0)
            return room;

        string leftMarker = left.Marker != null ? left.Marker.MarkerId : string.Empty;
        string rightMarker = right.Marker != null ? right.Marker.MarkerId : string.Empty;
        int marker = StringComparer.Ordinal.Compare(leftMarker, rightMarker);
        if (marker != 0)
            return marker;

        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        return code != 0 ? code : StringComparer.Ordinal.Compare(left.Message, right.Message);
    }
}

public sealed class RoomMapValidationInput
{
    public string ScopeName = "Current Authoring Scope";
    public bool RequiresSceneInfrastructure;
    public RoomInstance[] Rooms = Array.Empty<RoomInstance>();
    public AreaMarkerBase[] Markers = Array.Empty<AreaMarkerBase>();
    public SpawnPoint[] SpawnPoints = Array.Empty<SpawnPoint>();
    public DoorTransition[] Doors = Array.Empty<DoorTransition>();
    public MapTransitionService[] MapTransitionServices = Array.Empty<MapTransitionService>();
    public RoomContainer[] RoomContainers = Array.Empty<RoomContainer>();
}
