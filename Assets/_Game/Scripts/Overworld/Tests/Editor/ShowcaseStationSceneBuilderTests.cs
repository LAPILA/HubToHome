using System;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ShowcaseStationSceneBuilderTests
{
    [OneTimeSetUp]
    public void BuildScene()
    {
        ShowcaseStationSceneBuilder.BuildOrUpdate();
    }

    [Test]
    public void GeneratedSceneIsEnabledExactlyOnceInBuildSettings()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            ShowcaseStationScenePaths.MainScene);
        Assert.That(sceneAsset, Is.Not.Null);

        EditorBuildSettingsScene[] matches = EditorBuildSettings.scenes
            .Where(scene => string.Equals(
                scene.path,
                ShowcaseStationScenePaths.MainScene,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(matches.Length, Is.EqualTo(1));
        Assert.That(matches[0].enabled, Is.True);

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int showcaseIndex = Array.FindIndex(
            scenes,
            scene => string.Equals(
                scene.path,
                ShowcaseStationScenePaths.MainScene,
                StringComparison.OrdinalIgnoreCase));
        int trainIndex = Array.FindIndex(
            scenes,
            scene => string.Equals(
                scene.path,
                TravelTrainPaths.Scene,
                StringComparison.OrdinalIgnoreCase));
        int wideFieldIndex = Array.FindIndex(
            scenes,
            scene => string.Equals(
                scene.path,
                WideFieldPaths.Scene,
                StringComparison.OrdinalIgnoreCase));
        Assert.That(showcaseIndex, Is.LessThan(trainIndex));
        Assert.That(trainIndex, Is.LessThan(wideFieldIndex));
    }

    [Test]
    public void BuildSettingsRegistrationPreservesFirstPositionAndRemovesDuplicates()
    {
        EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
        try
        {
            var configured = original
                .Where(scene => !string.Equals(
                    scene.path,
                    ShowcaseStationScenePaths.MainScene,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            configured.Insert(
                0,
                new EditorBuildSettingsScene(
                    ShowcaseStationScenePaths.MainScene,
                    false));
            configured.Add(new EditorBuildSettingsScene(
                ShowcaseStationScenePaths.MainScene,
                true));
            EditorBuildSettings.scenes = configured.ToArray();

            TravelTrainEditorAssetUtility.EnsureBuildSettingsEntry(
                ShowcaseStationScenePaths.MainScene);

            EditorBuildSettingsScene[] updated = EditorBuildSettings.scenes;
            Assert.That(updated[0].path, Is.EqualTo(ShowcaseStationScenePaths.MainScene));
            Assert.That(updated[0].enabled, Is.True);
            Assert.That(
                updated.Count(scene => string.Equals(
                    scene.path,
                    ShowcaseStationScenePaths.MainScene,
                    StringComparison.OrdinalIgnoreCase)),
                Is.EqualTo(1));
        }
        finally
        {
            EditorBuildSettings.scenes = original;
        }
    }

    [Test]
    public void GeneratedSceneHasPlayableRegionWiring()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ShowcaseStationScenePaths.MainScene,
            OpenSceneMode.Additive);
        try
        {
            GameBootstrap bootstrap = Find<GameBootstrap>(scene);
            PlayerController player = Find<PlayerController>(scene);
            RoomContainer roomContainer = Find<RoomContainer>(scene);
            MapTransitionService transitions = Find<MapTransitionService>(scene);
            RegionEntryCoordinator entry = Find<RegionEntryCoordinator>(scene);
            CameraController cameraController = Find<CameraController>(scene);
            SeamlessBattleHost battleHost = Find<SeamlessBattleHost>(scene);

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(roomContainer, Is.Not.Null);
            Assert.That(transitions, Is.Not.Null);
            Assert.That(entry, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(cameraController.VirtualCamera, Is.Not.Null);
            Assert.That(battleHost, Is.Not.Null);

            Assert.That(roomContainer.InitialRoom, Is.Not.Null);
            Assert.That(
                roomContainer.InitialRoom.RoomId,
                Is.EqualTo(ShowcaseStationIds.Arrival));
            Assert.That(roomContainer.LoadInitialRoomOnStart, Is.False);
            Assert.That(
                cameraController.VirtualCamera.Lens.OrthographicSize,
                Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));

            SerializedObject entrySerialized = new SerializedObject(entry);
            Assert.That(
                entrySerialized.FindProperty("_defaultRoom").objectReferenceValue,
                Is.SameAs(roomContainer.InitialRoom));
            Assert.That(
                entrySerialized.FindProperty("_rooms").arraySize,
                Is.EqualTo(ShowcaseStationIds.RoomIds.Length));
            Assert.That(
                entrySerialized.FindProperty("_requireCameraBinding").boolValue,
                Is.True);

            Assert.That(CountMissingScripts(scene), Is.Zero);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GeneratedTravelTrainSceneHasEntryRouteAndExitContracts()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            TravelTrainPaths.Scene);
        Assert.That(sceneAsset, Is.Not.Null);

        EditorBuildSettingsScene[] matches = EditorBuildSettings.scenes
            .Where(scene => string.Equals(
                scene.path,
                TravelTrainPaths.Scene,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(matches.Length, Is.EqualTo(1));
        Assert.That(matches[0].enabled, Is.True);

        Scene scene = EditorSceneManager.OpenScene(
            TravelTrainPaths.Scene,
            OpenSceneMode.Additive);
        try
        {
            RoomContainer roomContainer = Find<RoomContainer>(scene);
            RegionEntryCoordinator entry = Find<RegionEntryCoordinator>(scene);
            PlayerController player = Find<PlayerController>(scene);
            CameraController cameraController = Find<CameraController>(scene);

            Assert.That(roomContainer, Is.Not.Null);
            Assert.That(entry, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(roomContainer.InitialRoom, Is.Not.Null);
            Assert.That(roomContainer.InitialRoom.RoomId, Is.EqualTo(TravelTrainIds.Room));

            RoomInstance train = roomContainer.InitialRoom.RoomPrefab;
            Assert.That(train, Is.Not.Null);
            Assert.That(
                train.GetComponentsInChildren<SpawnPoint>(true)
                    .Select(spawn => spawn.SpawnPointId),
                Does.Contain("entry"));
            Assert.That(
                train.GetComponentsInChildren<SpawnPoint>(true)
                    .Select(spawn => spawn.SpawnPointId),
                Does.Contain("exit"));

            TrainExitMarker[] exits = train.GetComponentsInChildren<TrainExitMarker>(true);
            TrainTravelController controller =
                train.GetComponentInChildren<TrainTravelController>(true);
            TrainDestinationInteractable[] destinations =
                train.GetComponentsInChildren<TrainDestinationInteractable>(true);
            Assert.That(exits, Has.Length.EqualTo(1));
            Assert.That(exits[0].Network, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(
                controller.TryValidateConfiguration(out string controllerError),
                Is.True,
                controllerError);
            Assert.That(destinations, Has.Length.EqualTo(2));
            Assert.That(
                destinations.Select(destination => destination.Destination.StopId),
                Is.EquivalentTo(new[]
                {
                    TravelTrainIds.ShowcaseStop,
                    TravelTrainIds.WideFieldStop
                }));
            Assert.That(
                train.GetComponentsInChildren<SublocationMarker>(true),
                Is.Empty);
            Assert.That(CountMissingScripts(scene), Is.Zero);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GeneratedWideFieldSceneHasStationExpanseAndBoardingContracts()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            WideFieldPaths.Scene);
        Assert.That(sceneAsset, Is.Not.Null);

        EditorBuildSettingsScene[] matches = EditorBuildSettings.scenes
            .Where(scene => string.Equals(
                scene.path,
                WideFieldPaths.Scene,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(matches.Length, Is.EqualTo(1));
        Assert.That(matches[0].enabled, Is.True);

        Scene scene = EditorSceneManager.OpenScene(
            WideFieldPaths.Scene,
            OpenSceneMode.Additive);
        try
        {
            RoomContainer roomContainer = Find<RoomContainer>(scene);
            RegionEntryCoordinator entry = Find<RegionEntryCoordinator>(scene);
            Assert.That(roomContainer, Is.Not.Null);
            Assert.That(entry, Is.Not.Null);
            Assert.That(roomContainer.InitialRoom.RoomId, Is.EqualTo(WideFieldIds.Station));

            SerializedObject entrySerialized = new SerializedObject(entry);
            Assert.That(entrySerialized.FindProperty("_rooms").arraySize, Is.EqualTo(2));

            RoomInstance station = roomContainer.InitialRoom.RoomPrefab;
            TrainBoardingMarker[] boarding =
                station.GetComponentsInChildren<TrainBoardingMarker>(true);
            Assert.That(boarding, Has.Length.EqualTo(1));
            Assert.That(boarding[0].Stop.StopId, Is.EqualTo(TravelTrainIds.WideFieldStop));

            RoomDefinition expanse = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
                WideFieldPaths.ExpanseDefinition);
            Assert.That(expanse, Is.Not.Null);
            Assert.That(expanse.IsValid, Is.True);
            Assert.That(
                expanse.RoomPrefab.GetComponentsInChildren<AreaConnectionMarker>(true),
                Is.Not.Empty);
            Assert.That(CountMissingScripts(scene), Is.Zero);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void LegacyCabinSceneIsRemovedAfterSuccessfulValidation()
    {
        const string legacyPath =
            "Assets/_Game/Content/Maps/Regions/ShowcaseStation/Scenes/Sublocation_ShowcaseCabin.unity";
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(legacyPath), Is.Null);
        Assert.That(
            EditorBuildSettings.scenes.Count(scene => string.Equals(
                scene.path,
                legacyPath,
                StringComparison.OrdinalIgnoreCase)),
            Is.Zero);

        TrainNetworkDefinition network =
            AssetDatabase.LoadAssetAtPath<TrainNetworkDefinition>(TravelTrainPaths.Network);
        TrainTravelValidationReport report = TrainTravelContentValidator.Validate(network);
        Assert.That(
            report.HasErrors,
            Is.False,
            string.Join("\n", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
    }
    [Test]
    public void RebuildPreservesSceneGuidAndRootShape()
    {
        string guidBefore = AssetDatabase.AssetPathToGUID(
            ShowcaseStationScenePaths.MainScene);
        string[] rootsBefore = LoadRootNames();

        ShowcaseStationSceneBuilder.BuildOrUpdate();

        Assert.That(
            AssetDatabase.AssetPathToGUID(ShowcaseStationScenePaths.MainScene),
            Is.EqualTo(guidBefore));
        Assert.That(LoadRootNames(), Is.EqualTo(rootsBefore));
    }

    private static string[] LoadRootNames()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ShowcaseStationScenePaths.MainScene,
            OpenSceneMode.Additive);
        try
        {
            return scene.GetRootGameObjects()
                .Select(root => root.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static int CountMissingScripts(Scene scene)
    {
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transforms[j].gameObject);
            }
        }

        return count;
    }
}
