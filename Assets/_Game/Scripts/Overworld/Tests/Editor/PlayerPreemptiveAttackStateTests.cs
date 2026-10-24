using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPreemptiveAttackStateTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameStateManager _previousStateManager;
    private GameObject _stateObject;
    private GameStateManager _stateManager;

    [SetUp]
    public void SetUp()
    {
        _previousStateManager = GameStateManager.Instance;
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, null);
        _stateObject = new GameObject("Preemptive Attack State Owner");
        _stateManager = _stateObject.AddComponent<GameStateManager>();
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, _stateManager);
    }

    [TearDown]
    public void TearDown()
    {
        if (_stateObject != null) Object.DestroyImmediate(_stateObject);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, _previousStateManager);
    }

    [Test]
    public void TryStartPreemptiveAttack_WhenAttackIsInProgressRejectsReentry()
    {
        GameObject playerObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            SetPrivateField(player, "_preemptiveAttackInProgress", true);

            Assert.That(player.TryStartPreemptiveAttack(), Is.False);
        }
        finally
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void RestoreAfterFailedPreemptiveAttack_ClearsAttackState()
    {
        GameObject playerObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            SetPrivateField(player, "_preemptiveAttackInProgress", true);
            SetPrivateField(player, "_preemptiveAttackHitResolved", true);
            SetPrivateField(player, "_preemptiveAttackStartedEncounter", true);
            SetPrivateField<Animator>(player, "_anim", null);

            MethodInfo restore = typeof(PlayerController).GetMethod(
                "RestoreAfterFailedPreemptiveAttack",
                PrivateInstance);
            Assert.That(restore, Is.Not.Null);
            restore.Invoke(player, new object[] { GameState.Exploration });

            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackHitResolved"), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackStartedEncounter"), Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        }
        finally
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisableDuringPreparation_ReleasesOwnedStateAndAllowsFreshAttack(bool disableObject)
    {
        PlayerController player = CreatePlayer(out GameObject playerObject);
        try
        {
            IEnumerator old = BeginPreparation(player);
            if (disableObject) playerObject.SetActive(false);
            else player.enabled = false;

            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Exploration));
            Assert.That(old.MoveNext(), Is.False);

            if (disableObject) playerObject.SetActive(true);
            else player.enabled = true;
            IEnumerator fresh = BeginPreparation(player);
            Assert.That(old.MoveNext(), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.True);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Cutscene));
            player.CompletePreemptiveAttackWithoutBattle();
            Assert.That(fresh.MoveNext(), Is.False);
        }
        finally { Object.DestroyImmediate(playerObject); }
    }

    [TestCase(GameState.Dialogue)]
    [TestCase(GameState.Paused)]
    [TestCase(GameState.Battle)]
    public void ExternalStateChange_EndsOldPreparationWithoutRestoringOverNewOwner(GameState state)
    {
        PlayerController player = CreatePlayer(out GameObject playerObject);
        try
        {
            IEnumerator attack = BeginPreparation(player);
            _stateManager.ChangeState(state);
            Assert.That(attack.MoveNext(), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(state));
        }
        finally { Object.DestroyImmediate(playerObject); }
    }

    [Test]
    public void ExternalStateLeavesAndReturnsToCutscene_DoesNotRegainOldOwnership()
    {
        PlayerController player = CreatePlayer(out GameObject playerObject);
        try
        {
            IEnumerator attack = BeginPreparation(player);
            _stateManager.ChangeState(GameState.Dialogue);
            _stateManager.ChangeState(GameState.Cutscene);
            Assert.That(attack.MoveNext(), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Cutscene));
        }
        finally { Object.DestroyImmediate(playerObject); }
    }

    [Test]
    public void CompleteWithoutBattle_CancelsOldPreparationAndDoesNotReleaseNewOne()
    {
        PlayerController player = CreatePlayer(out GameObject playerObject);
        try
        {
            IEnumerator old = BeginPreparation(player);
            player.CompletePreemptiveAttackWithoutBattle();
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Exploration));
            IEnumerator fresh = BeginPreparation(player);
            Assert.That(old.MoveNext(), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.True);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Cutscene));
            player.CompletePreemptiveAttackWithoutBattle();
            Assert.That(fresh.MoveNext(), Is.False);
        }
        finally { Object.DestroyImmediate(playerObject); }
    }

    [Test]
    public void BattleEntryTransfer_CancelsOldPreparationWithoutRestoringExploration()
    {
        PlayerController player = CreatePlayer(out GameObject playerObject);
        try
        {
            IEnumerator attack = BeginPreparation(player);
            player.HoldForBattleEntry();
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(player.IsBattleEntryHeld, Is.True);
            Assert.That(attack.MoveNext(), Is.False);
            Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Cutscene));
        }
        finally { Object.DestroyImmediate(playerObject); }
    }

    private IEnumerator BeginPreparation(PlayerController player)
    {
        SetPrivateField<Animator>(player, "_anim", null);
        SetPrivateField(player, "_attackDelay", 60f);
        MethodInfo method = typeof(PlayerController).GetMethod("CoPreemptiveAttack", PrivateInstance);
        Assert.That(method, Is.Not.Null);
        var attack = (IEnumerator)method.Invoke(player, null);
        Assert.That(attack.MoveNext(), Is.True);
        Assert.That(_stateManager.CurrentState, Is.EqualTo(GameState.Cutscene));
        Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.True);
        return attack;
    }

    private static PlayerController CreatePlayer(out GameObject playerObject)
    {
        playerObject = new GameObject("Test Player", typeof(Rigidbody2D), typeof(Animator));
        PlayerController player = playerObject.AddComponent<PlayerController>();
        MethodInfo awake = typeof(PlayerController).GetMethod("Awake", PrivateInstance);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(player, null);
        return player;
    }

    private static void SetPrivateField<T>(PlayerController player, string fieldName, T value)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(player, value);
    }

    private static T GetPrivateField<T>(PlayerController player, string fieldName)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        return (T)field.GetValue(player);
    }
}
