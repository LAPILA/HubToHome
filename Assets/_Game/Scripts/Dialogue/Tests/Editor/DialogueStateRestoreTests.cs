using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DialogueStateRestoreTests
{
    private GameStateManager _previousStateManager;
    private DialogueManager _previousDialogueManager;
    private GameObject _stateObject;
    private GameObject _managerObject;
    private GameObject _panelObject;
    private GameStateManager _state;
    private DialogueManager _manager;
    private DialogueUI _panel;
    private DialogueData _dialogue;

    [SetUp]
    public void SetUp()
    {
        _previousStateManager = GameStateManager.Instance;
        _previousDialogueManager = DialogueManager.Instance;
        SetStaticInstance(typeof(GameStateManager), null);
        SetStaticInstance(typeof(DialogueManager), null);

        _stateObject = new GameObject("GameStateManager_DialogueStateRestoreTests");
        _state = _stateObject.AddComponent<GameStateManager>();
        SetStaticInstance(typeof(GameStateManager), _state);

        _panelObject = new GameObject("DialoguePanel_DialogueStateRestoreTests");
        _panel = _panelObject.AddComponent<DialogueUI>();

        _managerObject = new GameObject("DialogueManager_DialogueStateRestoreTests");
        _manager = _managerObject.AddComponent<DialogueManager>();
        SetStaticInstance(typeof(DialogueManager), _manager);
        SetPrivateField(_manager, "_overworldPanel", _panel);
        SetPrivateField(_manager, "_cinematicPanel", _panel);

        _dialogue = ScriptableObject.CreateInstance<DialogueData>();
        _dialogue.Nodes.Add(new DialogueNode { DefaultText = "hello" });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_managerObject);
        UnityEngine.Object.DestroyImmediate(_panelObject);
        UnityEngine.Object.DestroyImmediate(_stateObject);
        UnityEngine.Object.DestroyImmediate(_dialogue);
        SetStaticInstance(typeof(DialogueManager), _previousDialogueManager);
        SetStaticInstance(typeof(GameStateManager), _previousStateManager);
    }

    [TestCase(GameState.Exploration)]
    [TestCase(GameState.Battle)]
    [TestCase(GameState.Cutscene)]
    public void EndDialogue_RestoresStateCapturedAtStart(GameState previous)
    {
        _state.ChangeState(previous);

        _manager.StartDialogue(_dialogue);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Dialogue));
        _manager.EndDialogue();

        Assert.That(_state.CurrentState, Is.EqualTo(previous));
    }

    [Test]
    public void EndDialogue_WhenExternalOwnerChangedState_DoesNotOverwriteIt()
    {
        _state.ChangeState(GameState.Cutscene);
        _manager.StartDialogue(_dialogue);

        _state.ChangeState(GameState.Battle);
        _manager.EndDialogue();

        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Battle));
    }

    [Test]
    public void CancelDialogue_ClosesPlaybackWithoutSuccessCallback()
    {
        int completed = 0;
        int cancelled = 0;
        bool started = _manager.TryStartDialogue(
            _dialogue,
            () => completed++,
            () => cancelled++,
            null,
            out int generation);

        bool result = _manager.CancelDialogue(generation);

        Assert.That(started, Is.True);
        Assert.That(result, Is.True);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(completed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void CancelDialogue_WithStaleGeneration_DoesNotCancelNewPlayback()
    {
        Assert.That(_manager.TryStartDialogue(_dialogue, null, null, null, out int first), Is.True);
        Assert.That(_manager.CancelDialogue(first), Is.True);
        Assert.That(_manager.TryStartDialogue(_dialogue, null, null, null, out int second), Is.True);

        bool staleResult = _manager.CancelDialogue(first);

        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(staleResult, Is.False);
        Assert.That(_manager.IsPlaying, Is.True);
        _manager.CancelDialogue(second);
    }

    [Test]
    public void DialogueRunnerCancel_ClearsBusyWithoutSuccessCallback()
    {
        var runner = new DialogueManagerRunner(_manager);
        runner.Register("test.dialogue", _dialogue);
        int completionCount = 0;

        runner.ShowAndWait("test.dialogue", () => completionCount++);
        runner.Cancel();

        Assert.That(runner.IsBusy, Is.False);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(completionCount, Is.Zero);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void NameInputCancelImmediate_DoesNotInvokeCompletion()
    {
        var inputObject = new GameObject("NameInputUI_DialogueStateRestoreTests");
        NameInputUI input = inputObject.AddComponent<NameInputUI>();
        int completionCount = 0;

        try
        {
            input.Open(_ => completionCount++);
            input.CancelImmediate();

            Assert.That(completionCount, Is.Zero);
            Assert.That(inputObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inputObject);
        }
    }

    private static void SetPrivateField<T>(DialogueManager target, string fieldName, T value)
    {
        FieldInfo field = typeof(DialogueManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void SetStaticInstance(Type type, object value)
    {
        PropertyInfo property = type.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null, "Missing singleton property on " + type.Name);
        property.SetValue(null, value);
    }
}