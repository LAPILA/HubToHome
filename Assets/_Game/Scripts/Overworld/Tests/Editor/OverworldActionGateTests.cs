using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public sealed class OverworldActionGateTests
{
    private static readonly MethodInfo SetInstance = typeof(GameStateManager)
        .GetProperty(nameof(GameStateManager.Instance), BindingFlags.Public | BindingFlags.Static)
        .GetSetMethod(true);

    private GameObject _stateObject;
    private GameStateManager _stateManager;
    private GameStateManager _previousStateManager;
    private GameState _previousState;

    [SetUp]
    public void SetUp()
    {
        _previousStateManager = GameStateManager.Instance;
        if (_previousStateManager != null)
        {
            _stateManager = _previousStateManager;
            _previousState = _previousStateManager.CurrentState;
        }
        else
        {
            _stateObject = new GameObject("GameStateManager_Test");
            _stateManager = _stateObject.AddComponent<GameStateManager>();
            SetInstance.Invoke(null, new object[] { _stateManager });
        }

        _stateManager.ChangeState(GameState.Exploration);
    }

    [TearDown]
    public void TearDown()
    {
        if (_previousStateManager != null)
        {
            _previousStateManager.ChangeState(_previousState);
        }
        else if (_stateObject != null)
        {
            SetInstance.Invoke(null, new object[] { null });
            Object.DestroyImmediate(_stateObject);
        }
    }

    [TestCase(GameState.Exploration, true)]
    [TestCase(GameState.Dialogue, false)]
    [TestCase(GameState.Battle, false)]
    [TestCase(GameState.Cutscene, false)]
    [TestCase(GameState.Paused, false)]
    public void AllowsWorldActions_OnlyDuringExploration(
        GameState state,
        bool expected)
    {
        _stateManager.ChangeState(state);

        Assert.That(OverworldActionGate.AllowsWorldActions, Is.EqualTo(expected));
    }

    [Test]
    public void InteractableAndAreaMarker_RejectBattleInteraction()
    {
        var interactableObject = new GameObject("Interactable_Test");
        var markerObject = new GameObject("Marker_Test");
        try
        {
            TestInteractable interactable = interactableObject.AddComponent<TestInteractable>();
            TestAreaMarker marker = markerObject.AddComponent<TestAreaMarker>();
            _stateManager.ChangeState(GameState.Battle);

            Assert.That(interactable.CanInteract(null), Is.False);
            Assert.That(marker.CanInteract(null), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(interactableObject);
            Object.DestroyImmediate(markerObject);
        }
    }

    private sealed class TestInteractable : InteractableBase
    {
        public override void Interact(PlayerController player)
        {
        }
    }

    private sealed class TestAreaMarker : AreaMarkerBase
    {
    }
}