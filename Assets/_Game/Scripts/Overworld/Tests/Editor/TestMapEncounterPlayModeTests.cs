using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TestMapEncounterPlayModeTests
{
    private const string SlimeDataPath = "Assets/_Game/Content/Characters/EnemyDB/DB_Slime.asset";

    private SceneSetup[] _previousSceneSetup;
    private bool _hadBackupScenes;

    [SetUp]
    public void SetUp()
    {
        DG.Tweening.DOTween.KillAll(false);
        _previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        _hadBackupScenes = Directory.Exists("Temp/__Backupscenes");
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
    public IEnumerator TestMapBootsWithConfiguredSeamlessBattleHost()
    {
        EditorSceneManager.OpenScene(
            SeamlessBattleHostPrefabBuilder.TestMapScenePath,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TestMap"));
        SeamlessBattleHost host = Object.FindFirstObjectByType<SeamlessBattleHost>(FindObjectsInactive.Include);
        Assert.That(host, Is.Not.Null, "TestMap requires SeamlessBattleHost.");
        Assert.That(host.IsRuntimeReady(out string error), Is.True, error);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InvalidBattleSceneRollsBackPlayerAndPendingEncounter()
    {
        EditorSceneManager.OpenScene(
            SeamlessBattleHostPrefabBuilder.TestMapScenePath,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        Assert.That(player, Is.Not.Null);

        if (GlobalDataManager.Instance == null)
            new GameObject("Test GlobalDataManager").AddComponent<GlobalDataManager>();
        if (GameStateManager.Instance == null)
            new GameObject("Test GameStateManager").AddComponent<GameStateManager>();
        if (SceneLoader.Instance == null)
            new GameObject("Test SceneLoader").AddComponent<SceneLoader>();

        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        bool started = BattleEncounterService.StartEncounter(
            player,
            new System.Collections.Generic.List<EnemyData> { enemy },
            useDedicatedBattleScene: true,
            battleSceneName: "__missing_battle_scene__",
            encounterId: "test.rollback",
            defeatsOnVictory: true,
            playerPreemptiveAttack: true);

        Assert.That(started, Is.False);
        Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(GlobalDataManager.Instance.PendingEnemies, Is.Empty);
        Assert.That(GlobalDataManager.Instance.CurrentEncounterEnemyId, Is.Null.Or.Empty);

        Object.Destroy(enemy);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SeamlessVictoryRestoresOverworldPositionAndPresentation()
    {
        EditorSceneManager.OpenScene(
            SeamlessBattleHostPrefabBuilder.TestMapScenePath,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        SeamlessBattleHost host = Object.FindFirstObjectByType<SeamlessBattleHost>(FindObjectsInactive.Include);
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(SlimeDataPath);
        var sourceObject = new GameObject("Victory Encounter Source");
        TestMapEncounterSourceProbe source = sourceObject.AddComponent<TestMapEncounterSourceProbe>();

        Assert.That(host, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);

        Vector3 originalPosition = player.transform.position;
        Transform originalCameraTarget = CameraController.Instance != null
            ? CameraController.Instance.DefaultTarget
            : null;
        bool started = BattleEncounterService.StartEncounter(
            player,
            new System.Collections.Generic.List<EnemyData> { enemy },
            useDedicatedBattleScene: false,
            encounterId: "test.seamless.victory",
            encounterSource: source);

        Assert.That(started, Is.True);
        yield return WaitUntilOrFail(
            () => host.BattleUiRoot.activeSelf
                && host.BattleManager._enemies.Count > 0
                && host.BattleManager.CurrentState == BattleState.PlayerActionSelect,
            12f,
            "Seamless battle did not reach player input.");

        host.BattleManager.EditorCheatWinBattle();
        yield return WaitUntilOrFail(
            () => !host.BattleUiRoot.activeSelf,
            15f,
            "Victory did not restore the overworld presentation.");
        yield return null;

        Assert.That(source.WasResolved, Is.True);
        Assert.That(source.Victory, Is.True);
        Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(Vector3.Distance(player.transform.position, originalPosition), Is.LessThan(0.01f));
        Assert.That(host.BattleManager._enemies, Is.Empty);
        Assert.That(GlobalDataManager.Instance.CurrentEncounterEnemyId, Is.Null.Or.Empty);
        if (originalCameraTarget != null && CameraController.Instance != null)
            Assert.That(CameraController.Instance.DefaultTarget, Is.SameAs(originalCameraTarget));

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SeamlessRunRestoresOverworldPositionAndPresentation()
    {
        EditorSceneManager.OpenScene(
            SeamlessBattleHostPrefabBuilder.TestMapScenePath,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        SeamlessBattleHost host = Object.FindFirstObjectByType<SeamlessBattleHost>(FindObjectsInactive.Include);
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(SlimeDataPath);
        var sourceObject = new GameObject("Run Encounter Source");
        TestMapEncounterSourceProbe source = sourceObject.AddComponent<TestMapEncounterSourceProbe>();

        Assert.That(host, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);

        Vector3 originalPosition = player.transform.position;
        bool started = BattleEncounterService.StartEncounter(
            player,
            new System.Collections.Generic.List<EnemyData> { enemy },
            useDedicatedBattleScene: false,
            encounterId: "test.seamless.run",
            encounterSource: source);

        Assert.That(started, Is.True);
        yield return WaitUntilOrFail(
            () => host.BattleUiRoot.activeSelf
                && host.BattleManager._playerParty.Count > 0
                && host.BattleManager.CurrentState == BattleState.PlayerActionSelect,
            12f,
            "Seamless battle did not reach player input.");

        SerializedObject managerObject = new SerializedObject(host.BattleManager);
        managerObject.FindProperty("_runSuccessChance").floatValue = 1f;
        managerObject.ApplyModifiedPropertiesWithoutUndo();
        host.BattleManager.OnPlayerActionSelected(
            host.BattleManager._playerParty[0],
            PlayerMenuAction.Run);

        yield return WaitUntilOrFail(
            () => !host.BattleUiRoot.activeSelf,
            12f,
            "Successful run did not restore the overworld presentation.");
        yield return null;

        Assert.That(source.WasResolved, Is.True);
        Assert.That(source.Victory, Is.False);
        Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(Vector3.Distance(player.transform.position, originalPosition), Is.LessThan(0.01f));
        Assert.That(host.BattleManager._enemies, Is.Empty);
        Assert.That(GlobalDataManager.Instance.CurrentEncounterEnemyId, Is.Null.Or.Empty);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SeamlessAbortRestoresStateAndIsIdempotent()
    {
        EditorSceneManager.OpenScene(
            SeamlessBattleHostPrefabBuilder.TestMapScenePath,
            OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        SeamlessBattleHost host = Object.FindFirstObjectByType<SeamlessBattleHost>(FindObjectsInactive.Include);
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(SlimeDataPath);
        var sourceObject = new GameObject("Abort Encounter Source");
        TestMapEncounterSourceProbe source = sourceObject.AddComponent<TestMapEncounterSourceProbe>();
        AudioManager audioManager = AudioManager.Instance;
        AudioClip mapClip = AudioClip.Create("Test_Map_BGM", 44100, 1, 44100, false);
        AudioClip battleClip = AudioClip.Create("Test_Battle_BGM", 44100, 1, 44100, false);

        Assert.That(host, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);
        Assert.That(audioManager, Is.Not.Null);
        audioManager.PlayBGM(mapClip);

        Vector3 originalPosition = player.transform.position;
        bool started = BattleEncounterService.StartEncounter(
            player,
            new System.Collections.Generic.List<EnemyData> { enemy },
            overrideBattleBgm: battleClip,
            useDedicatedBattleScene: false,
            encounterId: "test.seamless.abort",
            encounterSource: source);

        Assert.That(started, Is.True);
        yield return WaitUntilOrFail(
            () => host.BattleUiRoot.activeSelf && host.BattleManager._enemies.Count > 0,
            12f,
            "Seamless battle did not create its runtime actors.");
        Assert.That(audioManager.RequestedBgmClip, Is.SameAs(battleClip));

        BattleSpeechBubble speechBubble = player.GetComponentInChildren<BattleSpeechBubble>(true);
        Assert.That(speechBubble, Is.Not.Null);
        CanvasGroup speechCanvas = speechBubble.GetComponentInChildren<CanvasGroup>(true);
        Assert.That(speechCanvas, Is.Not.Null);
        speechBubble.Show("중단 정리 테스트", 10f);
        Assert.That(
            DG.Tweening.DOTween.TweensByTarget(speechCanvas, true),
            Is.Not.Null.And.Not.Empty,
            "The regression setup requires an active speech bubble fade.");

        Assert.That(host.BattleManager.AbortSeamlessBattle(), Is.True);
        Assert.That(host.BattleManager.AbortSeamlessBattle(), Is.False);
        yield return null;

        Assert.That(source.WasResolved, Is.False);
        Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));
        Assert.That(Vector3.Distance(player.transform.position, originalPosition), Is.LessThan(0.01f));
        Assert.That(host.BattleUiRoot.activeSelf, Is.False);
        Assert.That(host.BattleManager._enemies, Is.Empty);
        Assert.That(
            DG.Tweening.DOTween.TweensByTarget(speechCanvas, true),
            Is.Null.Or.Empty,
            "Speech bubble retained an active fade after abort.");
        Assert.That(audioManager.RequestedBgmClip, Is.SameAs(mapClip));
        CanvasGroup[] canvasGroups = host.BattleUiRoot.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = canvasGroups[i];
            Assert.That(DG.Tweening.DOTween.TweensByTarget(canvasGroup, true), Is.Null.Or.Empty,
                $"Battle UI CanvasGroup '{canvasGroup.name}' retained an active tween after abort.");
        }
        Assert.That(GlobalDataManager.Instance.CurrentEncounterEnemyId, Is.Null.Or.Empty);

        Object.Destroy(mapClip);
        Object.Destroy(battleClip);
        Object.Destroy(sourceObject);
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DuplicateSeamlessBattleHostLeavesOneCompleteRoot()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeamlessBattleHostPrefabBuilder.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Object.Instantiate(prefab);
        Object.Instantiate(prefab);
        yield return null;
        yield return null;

        SeamlessBattleHost[] hosts = Object.FindObjectsByType<SeamlessBattleHost>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Assert.That(hosts, Has.Length.EqualTo(1));
        Assert.That(hosts[0].IsConfigured(out string error), Is.True, error);

        yield return new ExitPlayMode();
    }

    private static IEnumerator WaitUntilOrFail(System.Func<bool> predicate, float timeout, string message)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < deadline)
        {
            BattleUIController.Instance?.ClearNarrationLog();
            yield return null;
        }

        Assert.That(predicate(), Is.True, message);
    }

}

public sealed class TestMapEncounterSourceProbe : MonoBehaviour, IEncounterSource
{
    public bool WasResolved { get; private set; }
    public bool Victory { get; private set; }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        WasResolved = true;
        Victory = victory;
    }
}
