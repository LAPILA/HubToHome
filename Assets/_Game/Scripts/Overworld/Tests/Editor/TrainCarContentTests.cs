using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>Read-only asset contracts; no scene opening, generation or Play Mode.</summary>
public sealed class TrainCarContentTests
{
    private const string Root = "Assets/_Game/Content/Maps/Regions/ALL";
    private const string OriginalPrefabGuid = "468f5707b0af32e44a3532656974297f";
    private const string OriginalRoomGuid = "bbafe5a46e2c3a6439faa6340429ad2c";
    private const string OriginalAreaGuid = "c591d44e6ae22104bb420dd6d4b8002e";
    private const string CameraRigGuid = "c0515653d43070348953a475e02fc95f";
    private const string LitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string UnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    // Authored rooms may be removed or redesigned; do not enforce the old four-car blockout.
    private static IEnumerable<string> RoomDefinitionPaths()
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition", new[] { Root + "/Data/Rooms" }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void RoomAndAreaReferencesRemainConsistent(string definitionPath)
    {
        RoomDefinition definition = LoadDefinition(definitionPath);
        GameObject prefab = definition.RoomPrefab.gameObject;
        RoomInstance room = prefab.GetComponent<RoomInstance>();
        Assert.That(room, Is.EqualTo(definition.RoomPrefab));
        Assert.That(prefab.GetComponentsInChildren<RoomInstance>(true).Length, Is.EqualTo(1));
        Assert.That(room.RoomId, Is.EqualTo(definition.RoomId));
        Assert.That(definition.AreaDefinition, Is.Not.Null);
        Assert.That(definition.AreaDefinition.AreaId, Is.EqualTo(room.RoomId));
        Assert.That(definition.AreaDefinition.RoomDefinition, Is.EqualTo(definition));
        Assert.That(room.CameraBounds, Is.Not.Null);
        Assert.That(room.CameraBounds.isTrigger, Is.True);
        Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<PlayerController>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<MapTransitionService>(true), Is.Empty);

        string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        Assert.That(prefabGuid, Is.Not.Empty);
        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(room, out string guid, out long localId), Is.True);
        Assert.That(guid, Is.EqualTo(prefabGuid));
        Assert.That(localId, Is.GreaterThan(0L));

        if (AssetDatabase.AssetPathToGUID(definitionPath) != OriginalRoomGuid) return;
        Assert.That(room.RoomId, Is.EqualTo("all.trainq0"));
        Assert.That(prefabGuid, Is.EqualTo(OriginalPrefabGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition.AreaDefinition)),
            Is.EqualTo(OriginalAreaGuid));
        Assert.That(localId, Is.EqualTo(5277721273395947060L));
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void FloorHasTilesAndValidSprites(string definitionPath)
    {
        Tilemap floor = LoadFloor(definitionPath);
        int occupied = 0;
        foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell)) continue;
            occupied++;
            Assert.That(floor.GetSprite(cell), Is.Not.Null, "Missing floor sprite at " + cell);
        }
        Assert.That(occupied, Is.GreaterThan(0), definitionPath);
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void ArrivalSpawnsHaveUniqueIdsAndStayOnTheFloor(string definitionPath)
    {
        GameObject prefab = LoadDefinition(definitionPath).RoomPrefab.gameObject;
        Tilemap floor = LoadFloor(definitionPath);
        SpawnPoint[] spawns = prefab.GetComponentsInChildren<SpawnPoint>(true);
        Assert.That(spawns, Is.Not.Empty);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpawnPoint spawn in spawns)
        {
            Assert.That(string.IsNullOrWhiteSpace(spawn.SpawnPointId), Is.False, spawn.name);
            Assert.That(ids.Add(spawn.SpawnPointId), Is.True, "Duplicate arrival ID: " + spawn.SpawnPointId);
            Assert.That(floor.HasTile(floor.WorldToCell(spawn.transform.position)), Is.True,
                "Spawn is outside the floor: " + spawn.SpawnPointId);
        }
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void DoorsReferenceExistingDestinationsWhenPresent(string definitionPath)
    {
        RoomDefinition definition = LoadDefinition(definitionPath);
        var markerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (AreaConnectionMarker door in definition.RoomPrefab.GetComponentsInChildren<AreaConnectionMarker>(true))
        {
            Assert.That(string.IsNullOrWhiteSpace(door.MarkerId), Is.False, door.name);
            Assert.That(markerIds.Add(door.MarkerId), Is.True, "Duplicate door marker ID");
            Assert.That(door.AreaId, Is.EqualTo(definition.AreaDefinition.AreaId));
            MapTransitionRequest request = door.MapTransition;
            Assert.That(request, Is.Not.Null);
            Assert.That(request.IsValid(out string error), Is.True, error);
            if (request.TransitionType == MapTransitionType.Scene)
            {
                bool sceneExists = false;
                foreach (string guid in AssetDatabase.FindAssets(request.TargetSceneName + " t:Scene"))
                    sceneExists |= Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) ==
                        request.TargetSceneName;
                Assert.That(sceneExists, Is.True, "Missing destination scene: " + request.TargetSceneName);
                continue;
            }

            RoomDefinition destination = LoadDefinition(AssetDatabase.GetAssetPath(request.TargetRoom));
            Assert.That(destination, Is.EqualTo(request.TargetRoom));
            if (request.UseFallbackPosition) continue;
            bool spawnExists = false;
            foreach (SpawnPoint spawn in destination.RoomPrefab.GetComponentsInChildren<SpawnPoint>(true))
                spawnExists |= spawn.SpawnPointId == request.TargetSpawnPointId;
            Assert.That(spawnExists, Is.True, "Missing destination spawn: " + request.TargetSpawnPointId);
        }
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void LightingStaysWithinNormalIntensityAndUses2DMaterials(string definitionPath)
    {
        GameObject prefab = LoadDefinition(definitionPath).RoomPrefab.gameObject;
        Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty, "Do not retain 3D room lights");
        foreach (Light2D light in prefab.GetComponentsInChildren<Light2D>(true))
            Assert.That(light.intensity, Is.InRange(0f, 1f), light.name + " must not overexpose the room");
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is TilemapRenderer) && !(renderer is SpriteRenderer)) continue;
            Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
            Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, renderer.name);
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(LitShader).Or.EqualTo(UnlitShader),
                renderer.name);
        }
    }

    [TestCaseSource(nameof(RoomDefinitionPaths))]
    public void NoWindowLightPoolBlockoutRemains(string definitionPath)
    {
        foreach (Transform child in LoadDefinition(definitionPath).RoomPrefab.GetComponentsInChildren<Transform>(true))
            Assert.That(child.name.StartsWith("Window Light Pool", StringComparison.Ordinal), Is.False,
                "Legacy translucent window decoration: " + child.name);
    }

    [Test]
    public void SceneRegistersExistingRoomsAndDisablesPostProcessingOnIts2DCamera()
    {
        // Read serialized text instead of opening the scene and invoking editor lifecycle callbacks.
        string scene = File.ReadAllText(Root + "/Scenes/Region_Train.unity");
        Match coordinator = Regex.Match(scene,
            @"m_EditorClassIdentifier:[^\r\n]*::RegionEntryCoordinator(?<body>.*?)(?=^---|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.That(coordinator.Success, Is.True);
        string body = coordinator.Groups["body"].Value;
        Match rooms = Regex.Match(body, @"_rooms:\r?\n(?<rooms>(?:[ \t]*-[^\r\n]*\r?\n)+)");
        Assert.That(rooms.Success, Is.True);
        var registered = new List<string>();
        foreach (Match guid in Regex.Matches(rooms.Groups["rooms"].Value, @"guid: ([0-9a-f]{32})"))
            registered.Add(guid.Groups[1].Value);
        var expected = new List<string>();
        var roomIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in RoomDefinitionPaths())
        {
            expected.Add(AssetDatabase.AssetPathToGUID(path));
            Assert.That(roomIds.Add(LoadDefinition(path).RoomId), Is.True, "Duplicate room ID: " + path);
        }
        Assert.That(expected, Is.Not.Empty);
        Assert.That(registered, Is.EquivalentTo(expected));
        Assert.That(body, Does.Match(@"_defaultRoom:.*guid: " + OriginalRoomGuid));
        Assert.That(scene, Does.Match(@"_initialRoom:.*guid: " + OriginalRoomGuid));
        Assert.That(scene, Does.Contain("_loadInitialRoomOnStart: 0"));
        AssertCameraOverride(scene, "m_RendererIndex", "1");
        AssertCameraOverride(scene, "m_RenderPostProcessing", "0");
    }

    private static void AssertCameraOverride(string scene, string property, string value)
    {
        Assert.That(Regex.Matches(scene, @"propertyPath: " + property + @"\r?\n").Count, Is.EqualTo(1));
        Assert.That(scene, Does.Match(@"target: \{fileID: 6798267794479740515, guid: " + CameraRigGuid +
            @", type: 3\}\r?\n\s+propertyPath: " + property + @"\r?\n\s+value: " + value + @"\r?\n"));
    }

    private static Tilemap LoadFloor(string definitionPath)
    {
        Transform ground = LoadDefinition(definitionPath).RoomPrefab.transform.Find("Grid/Ground");
        Assert.That(ground, Is.Not.Null, definitionPath);
        Tilemap floor = ground.GetComponent<Tilemap>();
        Assert.That(floor, Is.Not.Null, definitionPath);
        return floor;
    }

    private static RoomDefinition LoadDefinition(string path)
    {
        RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
        Assert.That(definition, Is.Not.Null, path);
        Assert.That(definition.IsValid, Is.True, path);
        return definition;
    }
}
