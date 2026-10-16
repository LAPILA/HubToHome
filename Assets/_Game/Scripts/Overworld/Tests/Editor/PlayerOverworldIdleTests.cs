using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PlayerOverworldIdleTests
{
    private const string ControllerPath = "Assets/_Game/Content/Art/Characters/Player/Wizzel/Player_Animator.controller";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> _objects = new List<GameObject>();
    private GlobalDataManager _previousGlobal;
    private GameStateManager _previousGameState;
    private PlayerController _player;
    private Animator _animator;
    private AnimatorController _controller;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        _previousGameState = GameStateManager.Instance;
        _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(_controller, Is.Not.Null, ControllerPath);

        GameObject playerObject = CreateObject("Overworld Idle Test Player");
        playerObject.AddComponent<Rigidbody2D>();
        _animator = playerObject.AddComponent<Animator>();
        _animator.runtimeAnimatorController = _controller;
        _player = playerObject.AddComponent<PlayerController>();
        playerObject.SetActive(true);
        InvokePrivate("Awake");
        _animator.Rebind();
        _animator.Update(0f);
    }

    [TearDown]
    public void TearDown()
    {
        // Restore before destruction so temporary managers cannot clear live save state.
        SetSingleton(_previousGlobal);
        SetSingleton(_previousGameState);
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        }
        _objects.Clear();
    }

    [TestCase(0, 0f, -1f)]
    [TestCase(1, 0f, 1f)]
    [TestCase(2, -1f, 0f)]
    [TestCase(3, 1f, 0f)]
    public void SetFacingDirection_UpdatesIdleImmediatelyAndSurvivesStop(int direction, float x, float y)
    {
        _animator.SetFloat("MoveX", 0.5f);
        _animator.SetFloat("MoveY", 0.5f);
        _animator.SetBool("IsMoving", true);

        _player.SetFacingDirection(direction);
        AssertPresentation(direction, x, y, false);

        _player.StopOverworldMovement();
        AssertPresentation(direction, x, y, false);
        Assert.That(_player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
    }

    [TestCase(-1)]
    [TestCase(4)]
    [TestCase(int.MaxValue)]
    public void SetFacingDirection_InvalidValueFallsBackToDown(int direction)
    {
        _player.SetFacingDirection(1);
        _player.SetFacingDirection(direction);

        AssertPresentation(0, 0f, -1f, false);
    }

    [TestCase(0, 0f, -1f)]
    [TestCase(1, 0f, 1f)]
    [TestCase(2, -1f, 0f)]
    [TestCase(3, 1f, 0f)]
    public void MovementThenInputRelease_PreservesLastCardinalDirection(int direction, float x, float y)
    {
        SetMovement(new Vector2(x, y));
        AssertPresentation(direction, x, y, true);

        SetMovement(Vector2.zero);
        AssertPresentation(direction, x, y, false);
    }

    [TestCase(1f, 1f, 3)]
    [TestCase(1f, -1f, 3)]
    [TestCase(-1f, 1f, 2)]
    [TestCase(-1f, -1f, 2)]
    public void DiagonalMovement_UsesHorizontalFacingForBothWalkAndIdle(float x, float y, int direction)
    {
        SetMovement(new Vector2(x, y).normalized);
        AssertPresentation(direction, x, 0f, true);

        SetMovement(Vector2.zero);
        AssertPresentation(direction, x, 0f, false);
    }

    [Test]
    public void Update_WhenMenuPausesExploration_StopsAnimationWithoutChangingFacing()
    {
        GameStateManager gameState = CreateObject("Idle Test Game State").AddComponent<GameStateManager>();
        SetSingleton(gameState);
        SetMovement(Vector2.up);
        gameState.ChangeState(GameState.Paused);

        InvokePrivate("Update");

        AssertPresentation(1, 0f, 1f, false);
        Assert.That(GetMoveInput(), Is.EqualTo(Vector2.zero));
    }

    [TestCase(0, 0f, -1f)]
    [TestCase(1, 0f, 1f)]
    [TestCase(2, -1f, 0f)]
    [TestCase(3, 1f, 0f)]
    public void LoadPositionFromGlobal_AppliesSavedFacingToIdleWithoutAnotherFrame(int direction, float x, float y)
    {
        GlobalDataManager global = CreateObject("Idle Test Global Data").AddComponent<GlobalDataManager>();
        SetSingleton(global);
        global.SpawnX = 12f;
        global.SpawnY = -7f;
        global.LookingDir = direction;
        _player.SetFacingDirection((direction + 1) % 4);

        _player.LoadPositionFromGlobal();

        AssertPresentation(direction, x, y, false);
        Assert.That(_player.transform.position, Is.EqualTo(new Vector3(12f, -7f, 0f)));
    }

    [TestCase(0, 0f, -1f)]
    [TestCase(1, 0f, 1f)]
    [TestCase(2, -1f, 0f)]
    [TestCase(3, 1f, 0f)]
    public void LeaveBattle_RestoresOverworldIdleAtCurrentFacing(int direction, float x, float y)
    {
        _player.SetFacingDirection(direction);
        _player.SetBattleMode(true);
        _animator.Update(0f);

        _player.SetBattleMode(false);
        _animator.Update(0f);

        AssertPresentation(direction, x, y, false);
        Assert.That(_player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("idle"), Is.True);
    }

    [Test]
    public void Controller_DefaultIdleUsesFourDirectionalIdleClips()
    {
        AnimatorState idle = FindState("idle");
        Assert.That(_controller.layers[0].stateMachine.defaultState, Is.SameAs(idle));
        BlendTree tree = idle.motion as BlendTree;
        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.SimpleDirectional2D));
        Assert.That(tree.blendParameter, Is.EqualTo("MoveX"));
        Assert.That(tree.blendParameterY, Is.EqualTo("MoveY"));

        var expected = new Dictionary<Vector2, string>
        {
            { Vector2.down, "idle" },
            { Vector2.up, "back_idle" },
            { Vector2.left, "left_idle" },
            { Vector2.right, "right_idle" }
        };
        Assert.That(tree.children, Has.Length.EqualTo(4));
        foreach (ChildMotion child in tree.children)
        {
            Assert.That(expected.TryGetValue(child.position, out string clipName), Is.True,
                $"Unexpected or duplicate idle direction: {child.position}");
            Assert.That(child.motion, Is.InstanceOf<AnimationClip>());
            Assert.That(child.motion.name, Is.EqualTo(clipName));
            expected.Remove(child.position);
        }
        Assert.That(expected, Is.Empty);
    }

    [Test]
    public void Controller_WalkAndIdleTransitionsDependOnlyOnIsMovingWithoutExitTime()
    {
        AnimatorState idle = FindState("idle");
        AnimatorState walk = FindState("Walk_BlendTree");
        AssertTransition(idle, walk, AnimatorConditionMode.If);
        AssertTransition(walk, idle, AnimatorConditionMode.IfNot);
    }

    private GameObject CreateObject(string name)
    {
        var created = new GameObject(name);
        created.SetActive(false);
        _objects.Add(created);
        return created;
    }

    private void SetMovement(Vector2 movement)
    {
        typeof(PlayerController).GetField("_moveInput", PrivateInstance).SetValue(_player, movement);
        InvokePrivate("UpdateFacingDirection");
        InvokePrivate("UpdateAnimator", movement.sqrMagnitude > 0.01f);
    }

    private Vector2 GetMoveInput()
    {
        return (Vector2)typeof(PlayerController).GetField("_moveInput", PrivateInstance).GetValue(_player);
    }

    private void AssertPresentation(int direction, float x, float y, bool isMoving)
    {
        Assert.That(_player.FacingDirection, Is.EqualTo(direction));
        Assert.That(_player.GetFacingVector2(), Is.EqualTo(new Vector2(x, y)));
        Assert.That(_animator.GetFloat("MoveX"), Is.EqualTo(x).Within(0.0001f));
        Assert.That(_animator.GetFloat("MoveY"), Is.EqualTo(y).Within(0.0001f));
        Assert.That(_animator.GetBool("IsMoving"), Is.EqualTo(isMoving));
    }

    private void InvokePrivate(string name, params object[] arguments)
    {
        MethodInfo method = typeof(PlayerController).GetMethod(name, PrivateInstance);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(_player, arguments);
    }

    private static void SetSingleton<T>(T instance)
    {
        PropertyInfo property = typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, instance);
    }

    private AnimatorState FindState(string name)
    {
        foreach (ChildAnimatorState child in _controller.layers[0].stateMachine.states)
        {
            if (child.state.name == name)
                return child.state;
        }
        Assert.Fail($"Missing player animation state: {name}");
        return null;
    }

    private static void AssertTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        AnimatorStateTransition found = null;
        foreach (AnimatorStateTransition transition in from.transitions)
        {
            if (transition.destinationState != to)
                continue;
            Assert.That(found, Is.Null, $"Duplicate transition: {from.name} -> {to.name}");
            found = transition;
        }
        Assert.That(found, Is.Not.Null, $"Missing transition: {from.name} -> {to.name}");
        Assert.That(found.hasExitTime, Is.False);
        Assert.That(found.duration, Is.Zero);
        Assert.That(found.conditions, Has.Length.EqualTo(1));
        Assert.That(found.conditions[0].parameter, Is.EqualTo("IsMoving"));
        Assert.That(found.conditions[0].mode, Is.EqualTo(mode));
    }
}
