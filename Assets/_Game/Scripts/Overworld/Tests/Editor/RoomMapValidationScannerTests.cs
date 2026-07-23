#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RoomMapValidationScannerTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] != null)
                Object.DestroyImmediate(_created[i]);
        }

        _created.Clear();
    }

    [Test]
    public void Scan_DuplicateMarkerIdsInSameRoom_AddsErrorForEachMarker()
    {
        RoomInstance room = CreateRoom("room.a");
        SignMarker first = CreateMarker<SignMarker>(room.transform, "shared");
        SignMarker second = CreateMarker<SignMarker>(room.transform, " shared ");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(new[] { room }, new AreaMarkerBase[] { first, second }));

        Assert.That(
            report.Issues.Count(issue => issue.Code == RoomMapValidationCodes.DuplicateMarkerId),
            Is.EqualTo(2));
    }

    [Test]
    public void Scan_SameMarkerIdInDifferentRooms_DoesNotAddDuplicateError()
    {
        RoomInstance firstRoom = CreateRoom("room.a");
        RoomInstance secondRoom = CreateRoom("room.b");
        SignMarker first = CreateMarker<SignMarker>(firstRoom.transform, "shared");
        SignMarker second = CreateMarker<SignMarker>(secondRoom.transform, "shared");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(
                new[] { firstRoom, secondRoom },
                new AreaMarkerBase[] { first, second }));

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.DuplicateMarkerId),
            Is.False);
    }

    [Test]
    public void Scan_UnboundMarker_AddsWarning()
    {
        SignMarker marker = CreateMarker<SignMarker>(null, "unbound");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(markers: new AreaMarkerBase[] { marker }));

        RoomMapValidationIssue issue = report.Issues.Single(
            candidate => candidate.Code == RoomMapValidationCodes.MarkerUnbound);
        Assert.That(issue.Severity, Is.EqualTo(RoomMapValidationSeverity.Warning));
        Assert.That(issue.Marker, Is.SameAs(marker));
    }

    [Test]
    public void Scan_MarkerOutsideRoomBounds_AddsWarning()
    {
        RoomInstance room = CreateRoomWithBounds("room.bounds", 2f, 2f);
        SignMarker marker = CreateMarker<SignMarker>(room.transform, "outside");
        marker.transform.localPosition = new Vector3(4f, 0f, 0f);

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(new[] { room }, new AreaMarkerBase[] { marker }));

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.MarkerOutsideBounds),
            Is.True);
    }

    [Test]
    public void Scan_MarkerInsideRoomBounds_DoesNotAddOutsideWarning()
    {
        RoomInstance room = CreateRoomWithBounds("room.bounds", 2f, 2f);
        SignMarker marker = CreateMarker<SignMarker>(room.transform, "inside");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(new[] { room }, new AreaMarkerBase[] { marker }));

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.MarkerOutsideBounds),
            Is.False);
    }

    [Test]
    public void Scan_RoomWithoutBounds_AddsOneRoomWarning()
    {
        RoomInstance room = CreateRoom("room.no_bounds");
        SignMarker first = CreateMarker<SignMarker>(room.transform, "first");
        SignMarker second = CreateMarker<SignMarker>(room.transform, "second");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(
                new[] { room },
                new AreaMarkerBase[] { first, second }));

        Assert.That(
            report.Issues.Count(issue => issue.Code == RoomMapValidationCodes.RoomBoundsMissing),
            Is.EqualTo(1));
    }

    [Test]
    public void Scan_MissingAndDuplicateSpawnPointIds_AreReported()
    {
        SpawnPoint missing = CreateSpawnPoint(string.Empty);
        SpawnPoint first = CreateSpawnPoint("entry");
        SpawnPoint second = CreateSpawnPoint("entry");

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(spawnPoints: new[] { missing, first, second }));

        Assert.That(
            report.Issues.Count(issue => issue.Code == RoomMapValidationCodes.SpawnPointIdMissing),
            Is.EqualTo(1));
        Assert.That(
            report.Issues.Count(issue => issue.Code == RoomMapValidationCodes.DuplicateSpawnPointId),
            Is.EqualTo(2));
    }

    [Test]
    public void Scan_RoomTransitionWithExistingTargetSpawn_DoesNotAddTargetError()
    {
        RoomDefinition target = CreateRoomDefinitionWithSpawn("room.target", "entry");
        DoorTransition door = CreateDoor(CreateRoomRequest(target, "entry"));

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(doors: new[] { door }));

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.TargetSpawnMissing),
            Is.False);
    }

    [Test]
    public void Scan_RoomTransitionMissingTargetSpawn_AddsError()
    {
        RoomDefinition target = CreateRoomDefinitionWithSpawn("room.target", "entry");
        DoorTransition door = CreateDoor(CreateRoomRequest(target, "missing"));

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            CreateInput(doors: new[] { door }));

        RoomMapValidationIssue issue = report.Issues.Single(
            candidate => candidate.Code == RoomMapValidationCodes.TargetSpawnMissing);
        Assert.That(issue.Severity, Is.EqualTo(RoomMapValidationSeverity.Error));
        Assert.That(issue.Context, Is.SameAs(door));
    }

    [Test]
    public void Scan_SceneScopeWithoutInfrastructure_AddsErrors()
    {
        RoomMapValidationInput input = CreateInput();
        input.RequiresSceneInfrastructure = true;

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(input);

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.MapTransitionServiceMissing),
            Is.True);
        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.RoomContainerMissing),
            Is.True);
    }

    [Test]
    public void Scan_PrefabScopeWithoutInfrastructure_DoesNotAddSceneErrors()
    {
        RoomMapValidationInput input = CreateInput();
        input.RequiresSceneInfrastructure = false;

        RoomMapValidationReport report = RoomMapValidationScanner.Scan(input);

        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.MapTransitionServiceMissing),
            Is.False);
        Assert.That(
            report.Issues.Any(issue => issue.Code == RoomMapValidationCodes.RoomContainerMissing),
            Is.False);
    }

    [Test]
    public void CaptureRoots_CollectsOnlyComponentsBelowProvidedRoots()
    {
        RoomInstance room = CreateRoom("room.capture");
        SignMarker included = CreateMarker<SignMarker>(room.transform, "included");
        SignMarker excluded = CreateMarker<SignMarker>(null, "excluded");

        RoomMapValidationInput input = RoomMapValidationScopeCapture.CaptureRoots(
            new[] { room.gameObject },
            "Test Roots",
            false);

        Assert.That(input.Rooms, Does.Contain(room));
        Assert.That(input.Markers, Does.Contain(included));
        Assert.That(input.Markers.Contains(excluded), Is.False);
        Assert.That(input.RequiresSceneInfrastructure, Is.False);
    }

    private RoomInstance CreateRoom(string roomId)
    {
        GameObject root = CreateGameObject("Room_" + roomId);
        RoomInstance room = root.AddComponent<RoomInstance>();
        SetSerializedValue(room, "_roomId", roomId);
        return room;
    }

    private RoomInstance CreateRoomWithBounds(string roomId, float halfWidth, float halfHeight)
    {
        RoomInstance room = CreateRoom(roomId);
        GameObject boundsObject = new GameObject("CameraBounds");
        boundsObject.transform.SetParent(room.transform, false);
        PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
        bounds.points = new[]
        {
            new Vector2(-halfWidth, -halfHeight),
            new Vector2(-halfWidth, halfHeight),
            new Vector2(halfWidth, halfHeight),
            new Vector2(halfWidth, -halfHeight)
        };
        SetSerializedObject(room, "_cameraBounds", bounds);
        return room;
    }

    private T CreateMarker<T>(Transform parent, string markerId) where T : AreaMarkerBase
    {
        GameObject markerObject = new GameObject(typeof(T).Name);
        if (parent != null)
            markerObject.transform.SetParent(parent, false);
        else
            _created.Add(markerObject);

        markerObject.AddComponent<CircleCollider2D>().isTrigger = true;
        T marker = markerObject.AddComponent<T>();
        SetSerializedValue(marker, "markerId", markerId);
        SetSerializedValue(marker, "areaId", parent != null ? "test.area" : "unbound.area");
        if (marker is SignMarker)
            SetSerializedValue(marker, "signText", "test");
        return marker;
    }

    private SpawnPoint CreateSpawnPoint(string spawnPointId)
    {
        GameObject spawnObject = CreateGameObject("Spawn_" + spawnPointId);
        SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
        SetSerializedValue(spawn, "_spawnPointId", spawnPointId);
        return spawn;
    }

    private RoomDefinition CreateRoomDefinitionWithSpawn(string roomId, string spawnPointId)
    {
        RoomInstance room = CreateRoom(roomId);
        GameObject spawnObject = new GameObject("TargetSpawn");
        spawnObject.transform.SetParent(room.transform, false);
        SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
        SetSerializedValue(spawn, "_spawnPointId", spawnPointId);

        RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
        _created.Add(definition);
        SetSerializedValue(definition, "_roomId", roomId);
        SetSerializedObject(definition, "_roomPrefab", room);
        return definition;
    }

    private DoorTransition CreateDoor(MapTransitionRequest request)
    {
        GameObject doorObject = CreateGameObject("Door");
        doorObject.AddComponent<BoxCollider2D>().isTrigger = true;
        DoorTransition door = doorObject.AddComponent<DoorTransition>();
        SerializedObject serializedObject = new SerializedObject(door);
        SerializedProperty property = serializedObject.FindProperty("_request");
        Assert.That(property, Is.Not.Null, "_request");
        property.FindPropertyRelative("TransitionType").enumValueIndex = (int)request.TransitionType;
        property.FindPropertyRelative("TargetSceneName").stringValue = request.TargetSceneName ?? string.Empty;
        property.FindPropertyRelative("TargetRoom").objectReferenceValue = request.TargetRoom;
        property.FindPropertyRelative("TargetSpawnPointId").stringValue =
            request.TargetSpawnPointId ?? string.Empty;
        property.FindPropertyRelative("UseFallbackPosition").boolValue = request.UseFallbackPosition;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    private static MapTransitionRequest CreateRoomRequest(
        RoomDefinition targetRoom,
        string targetSpawnPointId)
    {
        return new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Room,
            TargetRoom = targetRoom,
            TargetSpawnPointId = targetSpawnPointId
        };
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }

    private static RoomMapValidationInput CreateInput(
        RoomInstance[] rooms = null,
        AreaMarkerBase[] markers = null,
        SpawnPoint[] spawnPoints = null,
        DoorTransition[] doors = null)
    {
        return new RoomMapValidationInput
        {
            ScopeName = "Tests",
            Rooms = rooms ?? new RoomInstance[0],
            Markers = markers ?? new AreaMarkerBase[0],
            SpawnPoints = spawnPoints ?? new SpawnPoint[0],
            Doors = doors ?? new DoorTransition[0],
            MapTransitionServices = new MapTransitionService[0],
            RoomContainers = new RoomContainer[0]
        };
    }

    private static void SetSerializedValue(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
