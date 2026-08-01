using NUnit.Framework;
using UnityEngine;

public sealed class PowerConsoleInteractableTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private GameObject _consoleObject;
    private TestPowerConsoleInteractable _console;
    private SceneActionSequencePlayer _sequencePlayer;
    private DialogueData _lockedDialogue;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("PowerConsoleInteractableTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        _consoleObject = new GameObject("PowerConsoleInteractableTests_Console");
        _sequencePlayer = _consoleObject.AddComponent<SceneActionSequencePlayer>();
        _console = _consoleObject.AddComponent<TestPowerConsoleInteractable>();
        _lockedDialogue = ScriptableObject.CreateInstance<DialogueData>();
        _console.Configure(
            "station.power_ready",
            1,
            _lockedDialogue,
            "locked",
            _sequencePlayer,
            true,
            "station.completed");
        _console.SetGlobalDataSource(_global);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_lockedDialogue);
        Object.DestroyImmediate(_consoleObject);
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void LockedConsoleRunsOnlyDialogueBranch()
    {
        Assert.That(_console.InteractionState, Is.EqualTo(PowerConsoleInteractionState.Locked));

        _console.Interact(null);

        Assert.That(_console.DialogueStarts, Is.EqualTo(1));
        Assert.That(_console.SequenceStarts, Is.Zero);
    }

    [Test]
    public void PoweredConsoleRunsOnlySequenceBranch()
    {
        _global.SetFlag("station.power_ready", 1);
        Assert.That(_console.InteractionState, Is.EqualTo(PowerConsoleInteractionState.Ready));

        _console.Interact(null);

        Assert.That(_console.DialogueStarts, Is.Zero);
        Assert.That(_console.SequenceStarts, Is.EqualTo(1));
    }

    [Test]
    public void CompletedConsoleCannotRunAgain()
    {
        _global.SetFlag("station.power_ready", 1);
        _global.SetFlag("station.completed", 1);

        Assert.That(_console.InteractionState, Is.EqualTo(PowerConsoleInteractionState.Completed));
        Assert.That(_console.CanInteract(null), Is.False);

        _console.Interact(null);
        Assert.That(_console.DialogueStarts, Is.Zero);
        Assert.That(_console.SequenceStarts, Is.Zero);
    }

    private sealed class TestPowerConsoleInteractable : PowerConsoleInteractable
    {
        public int DialogueStarts { get; private set; }
        public int SequenceStarts { get; private set; }

        protected override bool TryStartLockedDialogue()
        {
            DialogueStarts++;
            return true;
        }

        protected override bool TryStartSequence()
        {
            SequenceStarts++;
            return true;
        }
    }
}
