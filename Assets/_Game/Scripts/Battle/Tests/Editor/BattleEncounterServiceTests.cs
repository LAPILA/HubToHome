using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BattleEncounterServiceTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    private GlobalDataManager _previousGlobalData;
    private GameStateManager _previousGameState;
    private SceneLoader _previousSceneLoader;
    private GlobalDataManager _globalData;
    private GameStateManager _gameState;
    private BattleEncounterSceneLoaderTestDouble _sceneLoader;
    private CanvasGroup _fadeCanvas;
    private PlayerController _player;

    [SetUp]
    public void SetUp()
    {
        _previousGlobalData = GlobalDataManager.Instance;
        _previousGameState = GameStateManager.Instance;
        _previousSceneLoader = SceneLoader.Instance;

        SetStaticInstance(typeof(GlobalDataManager), null);
        SetStaticInstance(typeof(GameStateManager), null);
        SetStaticInstance(typeof(SceneLoader), null);
        ResetEncounterRequestGateIfAvailable();

        _globalData = CreateComponent<GlobalDataManager>("BattleEncounterServiceTests.GlobalData");
        _gameState = CreateComponent<GameStateManager>("BattleEncounterServiceTests.GameState");
        _sceneLoader = CreateSceneLoader();
        _player = CreateComponent<PlayerController>("BattleEncounterServiceTests.Player");

        SetStaticInstance(typeof(GlobalDataManager), _globalData);
        SetStaticInstance(typeof(GameStateManager), _gameState);
        SetStaticInstance(typeof(SceneLoader), _sceneLoader);
    }

    [TearDown]
    public void TearDown()
    {
        _fadeCanvas?.DOKill(false);
        _fadeCanvas = null;

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }

        ResetEncounterRequestGateIfAvailable();
        SetStaticInstance(typeof(GlobalDataManager), _previousGlobalData);
        SetStaticInstance(typeof(GameStateManager), _previousGameState);
        SetStaticInstance(typeof(SceneLoader), _previousSceneLoader);
        Time.timeScale = 1f;
    }

    [Test]
    public void StartEncounter_WhileDedicatedRequestIsPending_PreservesFirstRequestContext()
    {
        _sceneLoader.SceneIsLoadable = true;
        EnemyData firstEnemy = CreateScriptableObject<EnemyData>();
        EnemyData secondEnemy = CreateScriptableObject<EnemyData>();

        bool firstStarted = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { firstEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "PendingBattleScene",
            battleSceneFadeDuration: 30f,
            encounterId: "encounter.first",
            allowEscape: false);

        bool secondStarted = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { secondEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "OtherBattleScene",
            encounterId: "encounter.second");

        Assert.That(firstStarted, Is.True);
        Assert.That(secondStarted, Is.False);
        Assert.That(_globalData.PendingEnemies, Has.Count.EqualTo(1));
        Assert.That(_globalData.PendingEnemies[0], Is.SameAs(firstEnemy));
        Assert.That(_globalData.CurrentEncounterEnemyId, Is.EqualTo("encounter.first"));
        Assert.That(_globalData.CurrentEncounterAllowsEscape, Is.False);
        Assert.That(_player.State, Is.EqualTo(PlayerController.PlayerState.InBattle));
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Battle));
    }

    [Test]
    public void StartEncounter_WhenSceneLoadIsRejected_RestoresPreviousRuntimeState()
    {
        EnemyData previousEnemy = CreateScriptableObject<EnemyData>();
        EnemyData requestedEnemy = CreateScriptableObject<EnemyData>();
        AudioClip previousBgm = AudioClip.Create("Previous Battle BGM", 1, 1, 44100, false);
        _createdObjects.Add(previousBgm);
        BattleScenarioData previousScenario = CreateScriptableObject<BattleScenarioData>();

        _globalData.PendingEnemies = new List<EnemyData> { previousEnemy };
        _globalData.PendingBattleBGM = previousBgm;
        _globalData.PendingBattleScenario = previousScenario;
        _globalData.LastOverworldScene = "PreviousWorld";
        _globalData.SpawnX = 12.5f;
        _globalData.SpawnY = -4.25f;
        _globalData.LookingDir = (int)FacingDirection.Left;
        _globalData.BeginOverworldEnemyEncounter(
            "encounter.previous",
            "PreviousWorld",
            true,
            true,
            false);
        _gameState.ChangeState(GameState.Cutscene);
        Time.timeScale = 0.35f;
        _sceneLoader.SceneIsLoadable = false;

        bool started = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { requestedEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "MissingBattleScene",
            encounterId: "encounter.requested");

        Assert.That(started, Is.False);
        Assert.That(_globalData.PendingEnemies, Has.Count.EqualTo(1));
        Assert.That(_globalData.PendingEnemies[0], Is.SameAs(previousEnemy));
        Assert.That(_globalData.PendingBattleBGM, Is.SameAs(previousBgm));
        Assert.That(_globalData.PendingBattleScenario, Is.SameAs(previousScenario));
        Assert.That(_globalData.LastOverworldScene, Is.EqualTo("PreviousWorld"));
        Assert.That(_globalData.SpawnX, Is.EqualTo(12.5f));
        Assert.That(_globalData.SpawnY, Is.EqualTo(-4.25f));
        Assert.That(_globalData.LookingDir, Is.EqualTo((int)FacingDirection.Left));
        Assert.That(_globalData.CurrentEncounterEnemyId, Is.EqualTo("encounter.previous"));
        Assert.That(_globalData.CurrentEncounterDefeatsOnVictory, Is.True);
        Assert.That(_globalData.CurrentEncounterPlayerPreemptiveAttack, Is.True);
        Assert.That(_globalData.CurrentEncounterAllowsEscape, Is.False);
        Assert.That(_player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Cutscene));
        Assert.That(Time.timeScale, Is.EqualTo(0.35f));
    }

    [Test]
    public void StartEncounter_WhenPreparationThrows_RollsBackAndAcceptsNextRequest()
    {
        _sceneLoader.SceneIsLoadable = true;
        EnemyData failedEnemy = CreateScriptableObject<EnemyData>();
        EnemyData nextEnemy = CreateScriptableObject<EnemyData>();
        Action<GameState> throwingObserver = state =>
        {
            if (state == GameState.Battle)
                throw new InvalidOperationException("prepare observer failure");
        };
        _gameState.OnStateChanged += throwingObserver;
        LogAssert.Expect(LogType.Exception, new Regex("prepare observer failure"));

        bool failedStart = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { failedEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "PendingBattleScene",
            battleSceneFadeDuration: 30f,
            encounterId: "encounter.failed");
        _gameState.OnStateChanged -= throwingObserver;

        Assert.That(failedStart, Is.False);
        Assert.That(_globalData.PendingEnemies, Is.Empty);
        Assert.That(_globalData.CurrentEncounterEnemyId, Is.Null.Or.Empty);
        Assert.That(_globalData.CurrentEncounterAllowsEscape, Is.True);
        Assert.That(_player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(_gameState.CurrentState, Is.EqualTo(GameState.Exploration));

        bool nextStarted = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { nextEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "NextBattleScene",
            battleSceneFadeDuration: 30f,
            encounterId: "encounter.next");

        Assert.That(nextStarted, Is.True);
        Assert.That(_globalData.CurrentEncounterEnemyId, Is.EqualTo("encounter.next"));
        Assert.That(_globalData.CurrentEncounterAllowsEscape, Is.True);
    }

    [Test]
    public void StartEncounter_WhenRollbackObserverThrows_ReleasesRequestGate()
    {
        _sceneLoader.SceneIsLoadable = true;
        EnemyData firstEnemy = CreateScriptableObject<EnemyData>();
        EnemyData nextEnemy = CreateScriptableObject<EnemyData>();

        bool firstStarted = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { firstEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "PendingBattleScene",
            battleSceneFadeDuration: 30f,
            encounterId: "encounter.failure");
        Assert.That(firstStarted, Is.True);

        Action<GameState> throwingObserver = state =>
        {
            if (state == GameState.Exploration)
                throw new InvalidOperationException("rollback observer failure");
        };
        _gameState.OnStateChanged += throwingObserver;
        LogAssert.Expect(LogType.Error, new Regex("Failed to restore game state"));
        LogAssert.Expect(LogType.Exception, new Regex("rollback observer failure"));

        CompleteActiveSceneLoad(SceneLoadResult.LoadFailed);
        _gameState.OnStateChanged -= throwingObserver;

        _sceneLoader.SceneIsLoadable = true;
        bool nextStarted = BattleEncounterService.StartEncounter(
            _player,
            new List<EnemyData> { nextEnemy },
            useDedicatedBattleScene: true,
            battleSceneName: "NextBattleScene",
            battleSceneFadeDuration: 30f,
            encounterId: "encounter.next");

        Assert.That(nextStarted, Is.True);
        Assert.That(_globalData.CurrentEncounterEnemyId, Is.EqualTo("encounter.next"));
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private T CreateScriptableObject<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        _createdObjects.Add(instance);
        return instance;
    }

    private BattleEncounterSceneLoaderTestDouble CreateSceneLoader()
    {
        var sceneLoaderObject = new GameObject("BattleEncounterServiceTests.SceneLoader");
        _createdObjects.Add(sceneLoaderObject);
        _fadeCanvas = sceneLoaderObject.AddComponent<CanvasGroup>();
        var loader = sceneLoaderObject.AddComponent<BattleEncounterSceneLoaderTestDouble>();
        SetPrivateField(loader, "_fadeCanvas", _fadeCanvas);
        return loader;
    }

    private static void SetStaticInstance(Type type, object value)
    {
        PropertyInfo property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(SceneLoader).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }

    private void CompleteActiveSceneLoad(SceneLoadResult result)
    {
        FieldInfo operationField = typeof(SceneLoader).GetField(
            "_activeOperation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        SceneLoadOperation operation = (SceneLoadOperation)operationField.GetValue(_sceneLoader);
        MethodInfo finish = typeof(SceneLoader).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic);
        finish.Invoke(_sceneLoader, new object[] { operation, result });
    }

    private static void ResetEncounterRequestGateIfAvailable()
    {
        MethodInfo reset = typeof(BattleEncounterService).GetMethod(
            "ResetRequestGate",
            BindingFlags.Static | BindingFlags.NonPublic);
        reset?.Invoke(null, null);
    }
}

public sealed class BattleEncounterSceneLoaderTestDouble : SceneLoader
{
    public bool SceneIsLoadable { get; set; }

    protected override bool IsSceneLoadable(string sceneName)
    {
        return SceneIsLoadable;
    }
}
