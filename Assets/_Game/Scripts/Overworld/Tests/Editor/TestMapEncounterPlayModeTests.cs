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
    private SceneSetup[] _previousSceneSetup;
    private bool _hadBackupScenes;

    [SetUp]
    public void SetUp()
    {
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
        Assert.That(host.IsConfigured(out string error), Is.True, error);

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
}
