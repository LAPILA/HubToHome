using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MapTransitionServiceTests
{
    private GlobalDataManager _previousGlobalData;
    private GameStateManager _previousGameState;
    private MapTransitionService _previousMapTransition;
    private readonly System.Collections.Generic.List<GameObject> _objects = new System.Collections.Generic.List<GameObject>();

    private GlobalDataManager _globalData;
    private GameStateManager _gameState;
    private MapTransitionServiceTestDouble _service;
    private PlayerController _player;

    [SetUp]
    public void SetUp()
    {
        _previousGlobalData = GlobalDataManager.Instance;
        _previousGameState = GameStateManager.Instance;
        _previousMapTransition = MapTransitionService.Instance;
        SetStaticInstance(typeof(GlobalDataManager), null);
        SetStaticInstance(typeof(GameStateManager), null);
        SetStaticInstance(typeof(MapTransitionService), null);

        _globalData = CreateComponent<GlobalDataManager>("GlobalDataManager");
        _gameState = CreateComponent<GameStateManager>("GameStateManager");
        _service = CreateComponent<MapTransitionServiceTestDouble>("MapTransitionService");
        SetStaticInstance(typeof(GlobalDataManager), _globalData);
        SetStaticInstance(typeof(GameStateManager), _gameState);
        SetStaticInstance(typeof(MapTransitionService), _service);

        _player = CreateComponent<PlayerController>("Player");
        _player.gameObject.SetActive(false);
        _player.transform.position = new Vector3(12f, 34f, 0f);
        _player.SetFacingDirection((int)FacingDirection.Left);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                UnityEngine.Object.DestroyImmediate(_objects[i]);
        }

        SetStaticInstance(typeof(GlobalDataManager), _previousGlobalData);
        SetStaticInstance(typeof(GameStateManager), _previousGameState);
        SetStaticInstance(typeof(MapTransitionService), _previousMapTransition);
    }

    [UnityTest]
    public IEnumerator SceneTransition_KeepsStateAndReentryLockedUntilLoadCompletes()
    {
        MapTransitionRequest request = CreateSceneRequest("ArrivalSpawn");

        Assert.That(_service.TryRequestTransition(request, _player), Is.True);
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Cutscene));
        Assert.That(_service.IsTransitioning, Is.True);
        Assert.That(_service.TryRequestTransition(request, _player), Is.False);

        yield return WaitForPendingLoad();

        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Cutscene));
        Assert.That(_service.IsTransitioning, Is.True);

        _service.Complete(SceneLoadResult.Succeeded);
        yield return WaitForTransitionEnd();

        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(_service.IsTransitioning, Is.False);
        Assert.That(_globalData.SpawnScene, Is.EqualTo("TargetScene"));
        Assert.That(_globalData.SpawnPointId, Is.EqualTo("ArrivalSpawn"));
    }

    [Test]
    public void SceneTransition_CompletionRestoresStateAfterSceneLocalServiceIsDestroyed()
    {
        MapTransitionRequest request = CreateSceneRequest("ArrivalSpawn");

        Assert.That(_service.TryRequestTransition(request, _player), Is.True);
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Cutscene));

        Action<SceneLoadResult> completion = _service.TakePendingCompletion();
        GameObject serviceObject = _service.gameObject;
        UnityEngine.Object.DestroyImmediate(serviceObject);

        completion?.Invoke(SceneLoadResult.Succeeded);

        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [UnityTest]
    public IEnumerator SceneTransition_LoadFailureRollsBackDepartureState()
    {
        SetOriginalSpawnState();
        MapTransitionRequest request = CreateSceneRequest("ArrivalSpawn");

        Assert.That(_service.TryRequestTransition(request, _player), Is.True);
        yield return WaitForPendingLoad();

        _service.Complete(SceneLoadResult.LoadFailed);
        yield return WaitForTransitionEnd();

        AssertOriginalSpawnState();
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(_service.IsTransitioning, Is.False);
    }

    [Test]
    public void PlayerArrival_ConsumesSpawnPointBeforeCoordinateFallback()
    {
        SpawnPoint spawnPoint = CreateComponent<SpawnPoint>("ArrivalSpawn");
        spawnPoint.transform.position = new Vector3(7f, 8f, 0f);
        SetPrivateField(spawnPoint, "_spawnPointId", "door.arrival");

        _globalData.SpawnPointId = "door.arrival";
        _globalData.SpawnX = 1f;
        _globalData.SpawnY = 2f;
        _globalData.LookingDir = (int)FacingDirection.Up;

        _player.LoadPositionFromGlobal();

        Assert.That(_player.transform.position, Is.EqualTo(spawnPoint.transform.position));
        Assert.That(_player.FacingDirection, Is.EqualTo((int)FacingDirection.Up));
        Assert.That(_globalData.SpawnPointId, Is.Empty);
    }

    [Test]
    public void PlayerArrival_MissingSpawnPointFallsBackToCoordinatesAndConsumesId()
    {
        _globalData.SpawnPointId = "missing.spawn";
        _globalData.SpawnX = 3f;
        _globalData.SpawnY = 4f;
        _globalData.LookingDir = (int)FacingDirection.Right;

        _player.LoadPositionFromGlobal();

        Assert.That(_player.transform.position, Is.EqualTo(new Vector3(3f, 4f, 0f)));
        Assert.That(_player.FacingDirection, Is.EqualTo((int)FacingDirection.Right));
        Assert.That(_globalData.SpawnPointId, Is.Empty);
    }

    [UnityTest]
    public IEnumerator AreaMarkerSublocation_UsesTransitionRollbackOnFailure()
    {
        SetOriginalSpawnState();

        bool accepted = AreaMarkerRuntimeService.RequestSublocation(
            null,
            "TargetScene",
            "TargetArea",
            "ArrivalSpawn",
            0f);

        Assert.That(accepted, Is.True);
        yield return WaitForPendingLoad();
        Assert.That(_globalData.CurrentRoomId, Is.EqualTo("TargetArea"));

        _service.Complete(SceneLoadResult.InvalidScene);
        yield return WaitForTransitionEnd();

        AssertOriginalSpawnState();
    }

    private IEnumerator WaitForPendingLoad()
    {
        const int maxFrames = 20;
        int frame = 0;
        while (!_service.HasPendingLoad && frame++ < maxFrames)
            yield return null;

        Assert.That(_service.HasPendingLoad, Is.True, "Map transition did not start its scene load.");
    }

    private IEnumerator WaitForTransitionEnd()
    {
        const int maxFrames = 20;
        int frame = 0;
        while (_service.IsTransitioning && frame++ < maxFrames)
            yield return null;

        Assert.That(_service.IsTransitioning, Is.False, "Map transition did not complete in time.");
    }

    private static MapTransitionRequest CreateSceneRequest(string spawnPointId)
    {
        return new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = "TargetScene",
            TargetSpawnPointId = spawnPointId,
            FadeDuration = 0f
        };
    }

    private void SetOriginalSpawnState()
    {
        _globalData.SpawnScene = "OriginScene";
        _globalData.CurrentRoomId = "OriginRoom";
        _globalData.SpawnPointId = "OriginSpawn";
        _globalData.SpawnX = 1f;
        _globalData.SpawnY = 2f;
        _globalData.LookingDir = (int)FacingDirection.Down;
    }

    private void AssertOriginalSpawnState()
    {
        Assert.That(_globalData.SpawnScene, Is.EqualTo("OriginScene"));
        Assert.That(_globalData.CurrentRoomId, Is.EqualTo("OriginRoom"));
        Assert.That(_globalData.SpawnPointId, Is.EqualTo("OriginSpawn"));
        Assert.That(_globalData.SpawnX, Is.EqualTo(1f));
        Assert.That(_globalData.SpawnY, Is.EqualTo(2f));
        Assert.That(_globalData.LookingDir, Is.EqualTo((int)FacingDirection.Down));
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void SetStaticInstance(Type type, object value)
    {
        PropertyInfo property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }
}

public sealed class MapTransitionServiceTestDouble : MapTransitionService
{
    private Action<SceneLoadResult> _pendingCompletion;

    public bool HasPendingLoad { get; private set; }

    public void Complete(SceneLoadResult result)
    {
        Action<SceneLoadResult> completion = TakePendingCompletion();
        completion?.Invoke(result);
    }

    public Action<SceneLoadResult> TakePendingCompletion()
    {
        Action<SceneLoadResult> completion = _pendingCompletion;
        _pendingCompletion = null;
        HasPendingLoad = false;
        return completion;
    }

    protected override void BeginSceneLoad(
        MapTransitionRequest request,
        Action<SceneLoadResult> onCompleted)
    {
        HasPendingLoad = true;
        _pendingCompletion = onCompleted;
    }
}
