using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShowcaseStationBuilderTests
{
    private sealed class BuildSnapshot
    {
        public readonly Dictionary<string, string> Guids =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> PrefabShapes =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> StableReferences =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private ShowcaseStationDataBundle _data;
    private BuildSnapshot _first;
    private BuildSnapshot _second;

    [OneTimeSetUp]
    public void BuildTwice()
    {
        _data = ShowcaseStationDataBuilder.Build();
        ShowcaseStationRoomBuilder.Build(_data);
        AssetDatabase.SaveAssets();
        _first = Capture();

        _data = ShowcaseStationDataBuilder.Build();
        ShowcaseStationRoomBuilder.Build(_data);
        AssetDatabase.SaveAssets();
        _second = Capture();
    }

    [Test]
    public void BuildCreatesFiveMainRoomsWithUniqueValidDefinitions()
    {
        Assert.That(_data.Rooms.Count, Is.EqualTo(ShowcaseStationIds.GeneratedRoomIds.Length));
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
            Assert.That(_data.Rooms.TryGetValue(roomId, out RoomDefinition room), Is.True);
            Assert.That(room, Is.Not.Null);
            Assert.That(room.IsValid, Is.True, roomId);
            Assert.That(room.RoomPrefab.RoomId, Is.EqualTo(roomId));
            Assert.That(room.AreaDefinition, Is.Not.Null);
            Assert.That(ids.Add(room.RoomId), Is.True, room.RoomId);
        }
    }

    [Test]
    public void RuntimeCatalogContainsGeneratedEnemyExactlyOnce()
    {
        GameContentCatalog catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            AssetDatabaseContentSource.DefaultCatalogAssetPath);
        Assert.That(catalog, Is.Not.Null);

        int matches = 0;
        for (int i = 0; i < catalog.Enemies.Count; i++)
        {
            if (catalog.Enemies[i] == _data.SteamEnemy)
                matches++;
        }
        Assert.That(matches, Is.EqualTo(1));
    }

    [Test]
    public void RebuildPreservesGuidsAssetSetAndPrefabShape()
    {
        Assert.That(_second.Guids, Is.EqualTo(_first.Guids));
        Assert.That(_second.PrefabShapes, Is.EqualTo(_first.PrefabShapes));
        Assert.That(_second.StableReferences, Is.EqualTo(_first.StableReferences));
    }

    [Test]
    public void EveryConnectionTargetsAnExistingSpawnPoint()
    {
        var spawnsByRoom = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            RoomDefinition room = _data.Rooms[ShowcaseStationIds.GeneratedRoomIds[i]];
            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            SpawnPoint[] spawns = room.RoomPrefab.GetComponentsInChildren<SpawnPoint>(true);
            for (int spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex++)
                Assert.That(spawnIds.Add(spawns[spawnIndex].SpawnPointId), Is.True);
            spawnsByRoom.Add(room.RoomId, spawnIds);
        }

        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            RoomDefinition source = _data.Rooms[ShowcaseStationIds.GeneratedRoomIds[i]];
            AreaConnectionMarker[] connections =
                source.RoomPrefab.GetComponentsInChildren<AreaConnectionMarker>(true);
            for (int connectionIndex = 0; connectionIndex < connections.Length; connectionIndex++)
            {
                MapTransitionRequest request = connections[connectionIndex].MapTransition;
                Assert.That(request, Is.Not.Null, connections[connectionIndex].MarkerId);
                Assert.That(request.TargetRoom, Is.Not.Null, connections[connectionIndex].MarkerId);
                Assert.That(
                    spawnsByRoom[request.TargetRoom.RoomId].Contains(request.TargetSpawnPointId),
                    Is.True,
                    connections[connectionIndex].MarkerId);
            }
        }
    }

    [Test]
    public void MarkerIdsAreUniqueInsideEachRoom()
    {
        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            RoomDefinition room = _data.Rooms[ShowcaseStationIds.GeneratedRoomIds[i]];
            var markerIds = new HashSet<string>(StringComparer.Ordinal);
            AreaMarkerBase[] markers =
                room.RoomPrefab.GetComponentsInChildren<AreaMarkerBase>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                Assert.That(
                    markerIds.Add(markers[markerIndex].MarkerId),
                    Is.True,
                    room.RoomId + ": " + markers[markerIndex].MarkerId);
            }
        }
    }

    [Test]
    public void TrainEntryPlotUsesPowerAwareDialogueSelector()
    {
        RoomDefinition train = _data.Rooms[ShowcaseStationIds.Train];
        PlotPointMarker[] plots =
            train.RoomPrefab.GetComponentsInChildren<PlotPointMarker>(true);
        PlotPointMarker engineGoal = null;
        for (int i = 0; i < plots.Length; i++)
        {
            if (plots[i].MarkerId == "showcase.abandoned_train.engine_goal")
            {
                engineGoal = plots[i];
                break;
            }
        }

        Assert.That(engineGoal, Is.Not.Null);

        var serialized = new SerializedObject(engineGoal);
        FlagDialogueSelector selector = serialized
            .FindProperty("dialogueSelector")
            .objectReferenceValue as FlagDialogueSelector;

        Assert.That(selector, Is.SameAs(_data.StationNpcDialogue));

        bool hasPowerRule = false;
        for (int i = 0; i < selector.Rules.Count; i++)
        {
            FlagDialogueRule rule = selector.Rules[i];
            if (rule != null
                && rule.FlagKey == "showcase.station.power_restored"
                && rule.ExpectedValue == 1)
            {
                hasPowerRule = true;
                break;
            }
        }

        Assert.That(hasPowerRule, Is.True);
    }

    [Test]
    public void TrainFinaleHasValidStageShotsAndDirectReference()
    {
        const string powerPath =
            "Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation/showcase_station_finale_power.asset";
        const string departurePath =
            "Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation/showcase_station_finale_departure.asset";

        CinematicShotAsset power =
            AssetDatabase.LoadAssetAtPath<CinematicShotAsset>(powerPath);
        CinematicShotAsset departure =
            AssetDatabase.LoadAssetAtPath<CinematicShotAsset>(departurePath);
        Assert.That(power, Is.Not.Null, powerPath);
        Assert.That(departure, Is.Not.Null, departurePath);
        Assert.That(power.StageId, Is.EqualTo("showcase.station.finale"));
        Assert.That(power.ShotId, Is.EqualTo("showcase.station.finale.power"));
        Assert.That(departure.StageId, Is.EqualTo("showcase.station.finale"));
        Assert.That(
            departure.ShotId,
            Is.EqualTo("showcase.station.finale.departure"));

        RoomDefinition train = _data.Rooms[ShowcaseStationIds.Train];
        OverworldCinematicStage stage =
            train.RoomPrefab.GetComponentInChildren<OverworldCinematicStage>(true);
        SceneActionSequencePlayer player =
            train.RoomPrefab.GetComponentInChildren<SceneActionSequencePlayer>(true);
        Assert.That(stage, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(stage.StageId, Is.EqualTo("showcase.station.finale"));
        Assert.That(stage.ValidateDefinition().HasErrors, Is.False);
        Assert.That(player.Sequence, Is.SameAs(_data.FinaleSequence));

        var serialized = new SerializedObject(player);
        Assert.That(
            serialized.FindProperty("_cinematicStage").objectReferenceValue,
            Is.SameAs(stage));
    }

    [Test]
    public void RuntimeSequencesRemainSynchronizedWithYaml()
    {
        AssertSequenceSource(_data.IntroSequence, ShowcaseStationPaths.IntroSource);
        AssertSequenceSource(_data.FinaleSequence, ShowcaseStationPaths.FinaleSource);
    }

    [Test]
    public void GeneratedWorldPassesReadOnlyValidator()
    {
        ShowcaseStationValidationReport report =
            ShowcaseStationValidator.ValidateGeneratedAssets();

        Assert.That(
            report.Errors,
            Is.Empty,
            string.Join("\n", report.Errors));
    }
    private static BuildSnapshot Capture()
    {
        var snapshot = new BuildSnapshot();
        CaptureAssetFolder(snapshot.Guids, ShowcaseStationPaths.Root);
        CaptureAssetFolder(snapshot.Guids, ShowcaseStationPaths.RuntimeSequenceRoot);

        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
            string path = ShowcaseStationRoomBuilder.GetPrefabPath(roomId);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            int transformCount = prefab.GetComponentsInChildren<Transform>(true).Length;
            int componentCount = prefab.GetComponentsInChildren<Component>(true).Length;
            int markerCount = prefab.GetComponentsInChildren<AreaMarkerBase>(true).Length;
            snapshot.PrefabShapes.Add(
                roomId,
                transformCount + ":" + componentCount + ":" + markerCount);

            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
                ShowcaseStationValidator.GetRoomDefinitionPath(roomId));
            snapshot.StableReferences[roomId + ":room-prefab"] =
                ObjectReferenceId(definition.RoomPrefab);
            snapshot.StableReferences[roomId + ":area"] =
                ObjectReferenceId(definition.AreaDefinition);

            var markerIds = new List<string>();
            AreaMarkerBase[] markers =
                prefab.GetComponentsInChildren<AreaMarkerBase>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                markerIds.Add(markers[markerIndex].MarkerId);
            markerIds.Sort(StringComparer.Ordinal);
            snapshot.StableReferences[roomId + ":markers"] =
                string.Join("|", markerIds);

            var spawnIds = new List<string>();
            SpawnPoint[] spawns = prefab.GetComponentsInChildren<SpawnPoint>(true);
            for (int spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex++)
                spawnIds.Add(spawns[spawnIndex].SpawnPointId);
            spawnIds.Sort(StringComparer.Ordinal);
            snapshot.StableReferences[roomId + ":spawns"] =
                string.Join("|", spawnIds);

            var connections = new List<string>();
            AreaConnectionMarker[] roomConnections =
                prefab.GetComponentsInChildren<AreaConnectionMarker>(true);
            for (int connectionIndex = 0; connectionIndex < roomConnections.Length; connectionIndex++)
            {
                AreaConnectionMarker connection = roomConnections[connectionIndex];
                MapTransitionRequest request = connection.MapTransition;
                connections.Add(
                    connection.MarkerId
                    + ">"
                    + ObjectReferenceId(request.TargetRoom)
                    + "/"
                    + request.TargetSpawnPointId
                    + "/"
                    + request.FacingAfterEnter);
            }
            connections.Sort(StringComparer.Ordinal);
            snapshot.StableReferences[roomId + ":connections"] =
                string.Join("|", connections);
        }

        ActionSequenceAsset intro = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(
            ShowcaseStationPaths.IntroRuntime);
        ActionSequenceAsset finale = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(
            ShowcaseStationPaths.FinaleRuntime);
        snapshot.StableReferences["sequence:intro"] =
            intro.Source.SourceHash + "|" + intro.Source.ImportedAtIso8601;
        snapshot.StableReferences["sequence:finale"] =
            finale.Source.SourceHash + "|" + finale.Source.ImportedAtIso8601;
        return snapshot;
    }

    private static string ObjectReferenceId(UnityEngine.Object target)
    {
        Assert.That(target, Is.Not.Null);
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target,
                out string guid,
                out long localId),
            Is.True);
        return guid + ":" + localId;
    }

    private static void CaptureAssetFolder(
        IDictionary<string, string> target,
        string folder)
    {
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                target[path] = guids[i];
        }
    }

    private static void AssertSequenceSource(
        ActionSequenceAsset sequence,
        string sourcePath)
    {
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.Source, Is.Not.Null);
        Assert.That(
            sequence.Source.SourcePath.Replace('\\', '/'),
            Is.EqualTo(sourcePath));
        string sourceText = File.ReadAllText(Path.GetFullPath(sourcePath));
        Assert.That(
            sequence.Source.SourceHash,
            Is.EqualTo(ScenarioSourceHash.Compute(sourceText)));
    }
}
