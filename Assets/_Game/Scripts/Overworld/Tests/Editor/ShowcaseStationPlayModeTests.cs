using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ShowcaseStationPlayModeTests
{
    private SceneSetup[] _previousSceneSetup;
    private bool _hadBackupScenes;

    [SetUp]
    public void SetUp()
    {
        DG.Tweening.DOTween.KillAll(false);
        _previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        _hadBackupScenes = Directory.Exists("Temp/__Backupscenes");
        ShowcaseStationSceneBuilder.BuildOrUpdate();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();

        if (_previousSceneSetup != null && _previousSceneSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(_previousSceneSetup);

        if (!_hadBackupScenes && Directory.Exists("Temp/__Backupscenes"))
            FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes");
    }

    [UnityTest]
    public IEnumerator SceneBootsArrivalAndTransitionsToSquare()
    {
        EditorSceneManager.OpenScene(
            ShowcaseStationScenePaths.MainScene,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();

        RegionEntryCoordinator entry =
            Object.FindFirstObjectByType<RegionEntryCoordinator>(
                FindObjectsInactive.Include);
        RoomContainer roomContainer =
            Object.FindFirstObjectByType<RoomContainer>(
                FindObjectsInactive.Include);
        PlayerController player =
            Object.FindFirstObjectByType<PlayerController>(
                FindObjectsInactive.Include);
        SeamlessBattleHost battleHost =
            Object.FindFirstObjectByType<SeamlessBattleHost>(
                FindObjectsInactive.Include);

        yield return WaitUntilOrFail(
            () => entry != null
                && entry.Status == RegionEntryStatus.Succeeded
                && roomContainer != null
                && roomContainer.CurrentRoom != null,
            8f,
            "Showcase Station Arrival Room이 준비되지 않았습니다.");

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Region_ShowcaseStation"));
        Assert.That(player, Is.Not.Null);
        Assert.That(battleHost, Is.Not.Null);
        Assert.That(
            battleHost.IsRuntimeReady(out string battleError),
            Is.True,
            battleError);
        Assert.That(
            roomContainer.CurrentDefinition.RoomId,
            Is.EqualTo(ShowcaseStationIds.Arrival));
        Assert.That(CameraController.Instance, Is.Not.Null);
        Assert.That(
            CameraController.Instance.DefaultTarget,
            Is.SameAs(player.transform));
        Assert.That(
            CameraController.Instance.VirtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));

        RoomDefinition square = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            ShowcaseStationPaths.RoomDataRoot
            + "/Room_LanternSquare_Definition.asset");
        Assert.That(square, Is.Not.Null);

        SceneLoadResult transitionResult = SceneLoadResult.None;
        bool accepted = MapTransitionService.Instance.TryRequestTransition(
            new MapTransitionRequest
            {
                TransitionType = MapTransitionType.Room,
                TargetRoom = square,
                TargetSpawnPointId = "from_arrival",
                FacingAfterEnter = FacingDirection.Left,
                FadeDuration = 0f
            },
            player,
            result => transitionResult = result);
        Assert.That(accepted, Is.True);

        yield return WaitUntilOrFail(
            () => transitionResult != SceneLoadResult.None,
            5f,
            "Arrival에서 Lantern Square로 Room 전환이 끝나지 않았습니다.");

        Assert.That(transitionResult, Is.EqualTo(SceneLoadResult.Succeeded));
        Assert.That(
            roomContainer.CurrentDefinition.RoomId,
            Is.EqualTo(ShowcaseStationIds.Square));
        Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(
            CameraController.Instance.DefaultTarget,
            Is.SameAs(player.transform));

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PoweredTrainConsolePlaysFinaleAndRestoresGameplay()
    {
        EditorSceneManager.OpenScene(
            ShowcaseStationScenePaths.MainScene,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();

        yield return WaitUntilOrFail(
            () => IsRoomReady(
                "Region_ShowcaseStation",
                ShowcaseStationIds.Arrival),
            8f,
            "Showcase Station Arrival Room이 준비되지 않았습니다.");

        GlobalDataManager global = GlobalDataManager.Instance;
        PlayerController player =
            Object.FindFirstObjectByType<PlayerController>(
                FindObjectsInactive.Include);
        RoomContainer roomContainer =
            Object.FindFirstObjectByType<RoomContainer>(
                FindObjectsInactive.Include);
        RoomDefinition train = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            ShowcaseStationPaths.RoomDataRoot
            + "/Room_AbandonedTrain_Definition.asset");
        Assert.That(global, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(roomContainer, Is.Not.Null);
        Assert.That(train, Is.Not.Null);
        global.SetFlag("showcase.station.power_restored", 1);

        SceneLoadResult trainResult = SceneLoadResult.None;
        bool accepted = MapTransitionService.Instance.TryRequestTransition(
            new MapTransitionRequest
            {
                TransitionType = MapTransitionType.Room,
                TargetRoom = train,
                TargetSpawnPointId = "from_passage",
                FacingAfterEnter = FacingDirection.Right,
                FadeDuration = 0f
            },
            player,
            result => trainResult = result);
        Assert.That(accepted, Is.True);

        yield return WaitUntilOrFail(
            () => trainResult != SceneLoadResult.None,
            5f,
            "폐열차 Room 전환이 끝나지 않았습니다.");
        Assert.That(trainResult, Is.EqualTo(SceneLoadResult.Succeeded));

        PowerConsoleInteractable console =
            roomContainer.CurrentRoom.GetComponentInChildren<PowerConsoleInteractable>(true);
        OverworldCinematicStage stage =
            roomContainer.CurrentRoom.GetComponentInChildren<OverworldCinematicStage>(true);
        Assert.That(console, Is.Not.Null);
        Assert.That(stage, Is.Not.Null);

        SceneActionSequencePlayer sequencePlayer =
            console.GetComponent<SceneActionSequencePlayer>();
        Unity.Cinemachine.CinemachineCamera cinematicCamera =
            stage.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
        Assert.That(sequencePlayer, Is.Not.Null);
        Assert.That(cinematicCamera, Is.Not.Null);
        Assert.That(
            console.InteractionState,
            Is.EqualTo(PowerConsoleInteractionState.Ready));

        player.transform.position = console.transform.position;
        Physics2D.SyncTransforms();
        console.Interact(player);
        Assert.That(sequencePlayer.IsPlaying, Is.True);
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Cutscene));

        yield return WaitUntilOrFail(
            () => DialogueManager.Instance != null
                && DialogueManager.Instance.IsPlaying,
            5f,
            "피날레 대화가 시작되지 않았습니다.");
        Assert.That(cinematicCamera.gameObject.activeSelf, Is.True);
        DialogueManager.Instance.EndDialogue();

        yield return WaitUntilOrFail(
            () => global.GetFlag("showcase.station.completed", 0) == 1
                && !sequencePlayer.IsPlaying,
            6f,
            "피날레가 완료 상태로 끝나지 않았습니다.");

        Assert.That(cinematicCamera.gameObject.activeSelf, Is.False);
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(
            CameraController.Instance.VirtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
        Assert.That(
            console.InteractionState,
            Is.EqualTo(PowerConsoleInteractionState.Completed));

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TrainRouteRoundTripsBetweenShowcaseAndWideField()
    {
        EditorSceneManager.OpenScene(
            ShowcaseStationScenePaths.MainScene,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();

        yield return WaitUntilOrFail(
            () => IsRoomReady(
                "Region_ShowcaseStation",
                ShowcaseStationIds.Arrival),
            8f,
            "Showcase Station Arrival Room이 준비되지 않았습니다.");

        GlobalDataManager global = GlobalDataManager.Instance;
        PlayerController player =
            Object.FindFirstObjectByType<PlayerController>(
                FindObjectsInactive.Include);
        RoomContainer roomContainer =
            Object.FindFirstObjectByType<RoomContainer>(
                FindObjectsInactive.Include);
        RoomDefinition stationTrain = AssetDatabase.LoadAssetAtPath<RoomDefinition>(
            ShowcaseStationPaths.RoomDataRoot
            + "/Room_AbandonedTrain_Definition.asset");
        Assert.That(global, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(roomContainer, Is.Not.Null);
        Assert.That(stationTrain, Is.Not.Null);
        global.SetFlag("showcase.station.power_restored", 1);
        global.SetFlag("showcase.station.completed", 1);

        SceneLoadResult trainRoomResult = SceneLoadResult.None;
        bool trainRoomAccepted = MapTransitionService.Instance.TryRequestTransition(
            new MapTransitionRequest
            {
                TransitionType = MapTransitionType.Room,
                TargetRoom = stationTrain,
                TargetSpawnPointId = "from_passage",
                FacingAfterEnter = FacingDirection.Right,
                FadeDuration = 0f
            },
            player,
            result => trainRoomResult = result);
        Assert.That(trainRoomAccepted, Is.True);
        yield return WaitUntilOrFail(
            () => trainRoomResult != SceneLoadResult.None,
            5f,
            "폐열차 Room 전환이 끝나지 않았습니다.");
        Assert.That(trainRoomResult, Is.EqualTo(SceneLoadResult.Succeeded));

        TrainBoardingMarker showcaseBoarding =
            roomContainer.CurrentRoom.GetComponentInChildren<TrainBoardingMarker>(true);
        Assert.That(showcaseBoarding, Is.Not.Null);
        player.transform.position = showcaseBoarding.transform.position;
        Physics2D.SyncTransforms();
        showcaseBoarding.Interact(player);

        yield return WaitUntilOrFail(
            () => IsRoomReady("Region_TravelTrain", TravelTrainIds.Room),
            12f,
            "Showcase 정류소에서 공용 열차로 승차하지 못했습니다.");

        global = GlobalDataManager.Instance;
        player = Object.FindFirstObjectByType<PlayerController>(
            FindObjectsInactive.Include);
        roomContainer = Object.FindFirstObjectByType<RoomContainer>(
            FindObjectsInactive.Include);
        Assert.That(global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.ShowcaseStop));
        Assert.That(global.GetFlag(TravelTrainIds.ShowcaseCurrentFlag), Is.EqualTo(1));
        Assert.That(global.GetFlag(TravelTrainIds.WideFieldCurrentFlag), Is.Zero);

        TrainDestinationInteractable wideFieldDestination =
            FindDestination(roomContainer.CurrentRoom, TravelTrainIds.WideFieldStop);
        Assert.That(wideFieldDestination, Is.Not.Null);
        player.transform.position = wideFieldDestination.transform.position;
        Physics2D.SyncTransforms();
        wideFieldDestination.Interact(player);

        yield return WaitUntilOrFail(
            () => IsRoomReady("Region_WideField", WideFieldIds.Station),
            15f,
            "공용 열차에서 WideField 정류소로 이동하지 못했습니다.");

        global = GlobalDataManager.Instance;
        player = Object.FindFirstObjectByType<PlayerController>(
            FindObjectsInactive.Include);
        roomContainer = Object.FindFirstObjectByType<RoomContainer>(
            FindObjectsInactive.Include);
        Assert.That(global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.WideFieldStop));
        Assert.That(global.GetFlag(TravelTrainIds.ShowcaseCurrentFlag), Is.Zero);
        Assert.That(global.GetFlag(TravelTrainIds.WideFieldCurrentFlag), Is.EqualTo(1));
        yield return WaitUntilOrFail(
            () => SceneLoader.Instance != null && !SceneLoader.Instance.IsLoading,
            5f,
            "WideField 도착 페이드가 종료되지 않았습니다.");

        TrainBoardingMarker wideFieldBoarding =
            roomContainer.CurrentRoom.GetComponentInChildren<TrainBoardingMarker>(true);
        Assert.That(wideFieldBoarding, Is.Not.Null);
        Assert.That(MapTransitionService.Instance, Is.Not.Null);
        Assert.That(MapTransitionService.Instance.IsTransitioning, Is.False);
        Assert.That(SceneLoader.Instance, Is.Not.Null);
        Assert.That(wideFieldBoarding.CanInteract(player), Is.True);
        player.transform.position = wideFieldBoarding.transform.position;
        Physics2D.SyncTransforms();
        wideFieldBoarding.Interact(player);

        yield return WaitUntilOrFail(
            () => IsRoomReady("Region_TravelTrain", TravelTrainIds.Room),
            12f,
            "WideField 정류소에서 공용 열차로 승차하지 못했습니다.");

        player = Object.FindFirstObjectByType<PlayerController>(
            FindObjectsInactive.Include);
        roomContainer = Object.FindFirstObjectByType<RoomContainer>(
            FindObjectsInactive.Include);
        TrainDestinationInteractable showcaseDestination =
            FindDestination(roomContainer.CurrentRoom, TravelTrainIds.ShowcaseStop);
        Assert.That(showcaseDestination, Is.Not.Null);
        player.transform.position = showcaseDestination.transform.position;
        Physics2D.SyncTransforms();
        showcaseDestination.Interact(player);

        yield return WaitUntilOrFail(
            () => IsRoomReady(
                "Region_ShowcaseStation",
                ShowcaseStationIds.Train),
            15f,
            "WideField에서 Showcase 정류소로 왕복하지 못했습니다.");

        global = GlobalDataManager.Instance;
        Assert.That(global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.ShowcaseStop));
        Assert.That(global.GetFlag(TravelTrainIds.ShowcaseCurrentFlag), Is.EqualTo(1));
        Assert.That(global.GetFlag(TravelTrainIds.WideFieldCurrentFlag), Is.Zero);

        yield return new ExitPlayMode();
    }

    private static TrainDestinationInteractable FindDestination(
        RoomInstance room,
        string stopId)
    {
        if (room == null)
            return null;

        TrainDestinationInteractable[] destinations =
            room.GetComponentsInChildren<TrainDestinationInteractable>(true);
        for (int i = 0; i < destinations.Length; i++)
        {
            TrainStopDefinition destination = destinations[i].Destination;
            if (destination != null
                && string.Equals(
                    destination.StopId,
                    stopId,
                    System.StringComparison.Ordinal))
            {
                return destinations[i];
            }
        }
        return null;
    }
    private static bool IsRoomReady(string sceneName, string roomId)
    {
        if (!string.Equals(
                SceneManager.GetActiveScene().name,
                sceneName,
                System.StringComparison.Ordinal))
        {
            return false;
        }

        RegionEntryCoordinator entry =
            Object.FindFirstObjectByType<RegionEntryCoordinator>(
                FindObjectsInactive.Include);
        RoomContainer roomContainer =
            Object.FindFirstObjectByType<RoomContainer>(
                FindObjectsInactive.Include);
        return entry != null
            && entry.Status == RegionEntryStatus.Succeeded
            && roomContainer != null
            && roomContainer.CurrentDefinition != null
            && string.Equals(
                roomContainer.CurrentDefinition.RoomId,
                roomId,
                System.StringComparison.Ordinal);
    }
    private static IEnumerator WaitUntilOrFail(
        System.Func<bool> predicate,
        float timeoutSeconds,
        string failureMessage)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(predicate(), Is.True, failureMessage);
    }
}
