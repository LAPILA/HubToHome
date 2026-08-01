using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SceneActionSequencePlayerTests
{
    private DialogueManager _previousDialogueManager;
    private GameObject _playerObject;
    private GameObject _dialogueManagerObject;
    private SceneActionSequencePlayer _player;
    private ActionSequenceAsset _sequence;
    private DialogueData _dialogue;

    [SetUp]
    public void SetUp()
    {
        _previousDialogueManager = DialogueManager.Instance;
        SetDialogueManagerInstance(null);

        _playerObject = new GameObject("SceneActionSequencePlayerTests_Player");
        _player = _playerObject.AddComponent<SceneActionSequencePlayer>();
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "test.overworld.sequence";
        _dialogue = ScriptableObject.CreateInstance<DialogueData>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_playerObject);
        UnityEngine.Object.DestroyImmediate(_dialogueManagerObject);
        UnityEngine.Object.DestroyImmediate(_sequence);
        UnityEngine.Object.DestroyImmediate(_dialogue);
        SetDialogueManagerInstance(_previousDialogueManager);
    }

    [Test]
    public void ContextFactory_RegistersDialogueWaitAdapter()
    {
        ActionAdapterRegistry registry = SceneActionSequenceContextFactory.CreateRegistry();

        Assert.That(registry.TryGet(DialogueWaitActionAdapter.Id, out IActionAdapter adapter), Is.True);
        Assert.That(adapter, Is.TypeOf<DialogueWaitActionAdapter>());
    }

    [Test]
    public void ValidateConfiguration_RejectsInvalidDialogueReferences()
    {
        AssertInvalid(new ScenarioDialogueReferenceData[] { null }, "index 0 is null");
        AssertInvalid(
            new[] { new ScenarioDialogueReferenceData { DialogueId = " ", Dialogue = _dialogue } },
            "has no ID");
        AssertInvalid(
            new[] { new ScenarioDialogueReferenceData { DialogueId = "missing.data" } },
            "missing DialogueData");
        AssertInvalid(
            new[]
            {
                new ScenarioDialogueReferenceData { DialogueId = "duplicate", Dialogue = _dialogue },
                new ScenarioDialogueReferenceData { DialogueId = " duplicate ", Dialogue = _dialogue }
            },
            "Duplicate dialogue ID");
    }

    [Test]
    public void TryCreateLiveContext_WithDialogueReferences_InjectsRegisteredRunner()
    {
        _dialogueManagerObject = new GameObject("SceneActionSequencePlayerTests_DialogueManager");
        DialogueManager manager = _dialogueManagerObject.AddComponent<DialogueManager>();
        SetDialogueManagerInstance(manager);
        _player.Configure(
            _sequence,
            null,
            string.Empty,
            new[]
            {
                new ScenarioDialogueReferenceData
                {
                    DialogueId = " test.dialogue ",
                    Dialogue = _dialogue
                }
            });

        bool created = _player.TryCreateLiveContext(
            _sequence,
            out ActionDirector director,
            out ActionExecutionContext context,
            out string error);

        Assert.That(created, Is.True, error);
        Assert.That(director, Is.Not.Null);
        Assert.That(context, Is.Not.Null);
        Assert.That(context.GetService<IDialogueRunner>(), Is.TypeOf<DialogueManagerRunner>());
    }

    [Test]
    public void TryCreateLiveContext_ForDifferentSequence_DoesNotExposeContext()
    {
        ActionSequenceAsset other = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        try
        {
            _player.Configure(_sequence, null, string.Empty);

            bool created = _player.TryCreateLiveContext(
                other,
                out ActionDirector director,
                out ActionExecutionContext context,
                out string error);

            Assert.That(created, Is.False);
            Assert.That(director, Is.Null);
            Assert.That(context, Is.Null);
            Assert.That(error, Does.Contain("not owned"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(other);
        }
    }

    private void AssertInvalid(
        IEnumerable<ScenarioDialogueReferenceData> references,
        string expectedError)
    {
        _player.Configure(_sequence, null, string.Empty, references);

        Assert.That(_player.TryValidateConfiguration(out string error), Is.False);
        Assert.That(error, Does.Contain(expectedError));
    }

    private static void SetDialogueManagerInstance(DialogueManager value)
    {
        PropertyInfo property = typeof(DialogueManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, value);
    }
}