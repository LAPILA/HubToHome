using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SequencePuzzleControllerTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private SequencePuzzleDefinition _definition;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("SequencePuzzleTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        _definition = ScriptableObject.CreateInstance<SequencePuzzleDefinition>();
        _definition.Configure(
            "workshop.power",
            new[] { "terminal.a", "terminal.b", "terminal.c" },
            "showcase.power.restored",
            0.5f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_definition);
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void CorrectSequenceAdvancesAndPersistsCompletionFlag()
    {
        GameObject root = new GameObject("SequencePuzzleTests_Controller");
        SequencePuzzleController controller = root.AddComponent<SequencePuzzleController>();
        try
        {
            controller.SetGlobalDataSource(_global);
            controller.Configure(_definition);

            Assert.That(controller.Submit("terminal.a").Status, Is.EqualTo(SequencePuzzleInputStatus.Advanced));
            Assert.That(controller.Submit("terminal.b").Status, Is.EqualTo(SequencePuzzleInputStatus.Advanced));
            Assert.That(controller.Submit("terminal.c").Status, Is.EqualTo(SequencePuzzleInputStatus.Completed));

            Assert.That(controller.IsCompleted, Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(3));
            Assert.That(_global.GetFlag("showcase.power.restored"), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void WrongInputRejectsFurtherInputUntilMatchingGenerationResets()
    {
        var progress = new SequencePuzzleProgress(_definition.OrderedNodeIds);
        Assert.That(progress.Submit("terminal.a").Status, Is.EqualTo(SequencePuzzleInputStatus.Advanced));

        SequencePuzzleInputResult wrong = progress.Submit("wrong");
        Assert.That(wrong.Status, Is.EqualTo(SequencePuzzleInputStatus.Incorrect));
        Assert.That(progress.Submit("terminal.b").Status, Is.EqualTo(SequencePuzzleInputStatus.ResetPending));
        Assert.That(progress.TryApplyScheduledReset(wrong.ResetGeneration - 1), Is.False);
        Assert.That(progress.CurrentStep, Is.EqualTo(1));

        Assert.That(progress.TryApplyScheduledReset(wrong.ResetGeneration), Is.True);
        Assert.That(progress.CurrentStep, Is.Zero);
        Assert.That(progress.IsResetPending, Is.False);
    }

    [Test]
    public void ExternalCompletionInvalidatesPendingReset()
    {
        GameObject root = new GameObject("SequencePuzzleTests_Controller");
        SequencePuzzleController controller = root.AddComponent<SequencePuzzleController>();
        try
        {
            controller.SetGlobalDataSource(_global);
            controller.Configure(_definition);
            controller.Submit("terminal.a");
            int pendingGeneration = controller.Submit("wrong").ResetGeneration;

            _global.SetFlag(_definition.CompletionFlag, 1);

            Assert.That(controller.IsCompleted, Is.True);
            Assert.That(controller.IsResetPending, Is.False);
            Assert.That(controller.CompleteScheduledReset(pendingGeneration), Is.False);
            Assert.That(controller.CurrentStep, Is.EqualTo(controller.TotalSteps));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DisableCancelsPendingResetAndRestartsIncompleteProgress()
    {
        GameObject root = new GameObject("SequencePuzzleTests_Controller");
        SequencePuzzleController controller = root.AddComponent<SequencePuzzleController>();
        try
        {
            controller.SetGlobalDataSource(_global);
            controller.Configure(_definition);
            controller.Submit("terminal.a");
            int pendingGeneration = controller.Submit("wrong").ResetGeneration;

            controller.StopRuntime();

            Assert.That(controller.IsResetPending, Is.False);
            Assert.That(controller.CurrentStep, Is.Zero);
            Assert.That(controller.CompleteScheduledReset(pendingGeneration), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CompletionFlagRestoresWithoutReplayingInput()
    {
        _global.SetFlag(_definition.CompletionFlag, 1);
        GameObject root = new GameObject("SequencePuzzleTests_Controller");
        SequencePuzzleController controller = root.AddComponent<SequencePuzzleController>();
        try
        {
            controller.SetGlobalDataSource(_global);
            controller.Configure(_definition);

            Assert.That(controller.IsCompleted, Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(controller.TotalSteps));
            Assert.That(controller.Submit("terminal.a").Status, Is.EqualTo(SequencePuzzleInputStatus.AlreadyCompleted));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DefinitionRejectsDuplicateNodeIds()
    {
        _definition.Configure(
            "workshop.power",
            new[] { "terminal.a", "terminal.a" },
            "showcase.power.restored",
            0.5f);

        Assert.That(_definition.TryValidate(out string error), Is.False);
        StringAssert.Contains("중복", error);
    }

    [Test]
    public void ControllerModePuzzleMarkerShowsGuideWithoutCompletingOrDisablingRoot()
    {
        GameObject controllerObject = new GameObject("SequencePuzzleTests_Controller");
        SequencePuzzleController controller = controllerObject.AddComponent<SequencePuzzleController>();
        controller.SetGlobalDataSource(_global);
        controller.Configure(_definition);
        GameObject markerObject = new GameObject("SequencePuzzleTests_Marker");
        markerObject.AddComponent<BoxCollider2D>();
        PuzzleMarkerProbe marker = markerObject.AddComponent<PuzzleMarkerProbe>();
        try
        {
            SetField(marker, "puzzleRuntimeSource", controller);
            SetField(marker, "isOneShot", true);

            marker.Interact((PlayerController)null);

            Assert.That(marker.GuideCount, Is.EqualTo(1));
            Assert.That(marker.IsCompleted, Is.False);
            Assert.That(markerObject.activeSelf, Is.True);
            Assert.That(_global.GetFlag(_definition.CompletionFlag), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(markerObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void PuzzleMarkerDelegatesToAnyPuzzleRuntimeImplementation()
    {
        GameObject runtimeObject = new GameObject("SequencePuzzleTests_RuntimeProbe");
        PuzzleRuntimeProbe runtime = runtimeObject.AddComponent<PuzzleRuntimeProbe>();
        GameObject markerObject = new GameObject("SequencePuzzleTests_Marker");
        markerObject.AddComponent<BoxCollider2D>();
        PuzzleMarkerProbe marker = markerObject.AddComponent<PuzzleMarkerProbe>();
        try
        {
            SetField(marker, "puzzleRuntimeSource", runtime);

            marker.Interact((PlayerController)null);

            Assert.That(runtime.InteractionCount, Is.EqualTo(1));
            Assert.That(marker.GuideCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(markerObject);
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void LockedShortcutExplainsLockThenPassesImmediatelyAfterFlagChange()
    {
        GameObject doorObject = new GameObject("SequencePuzzleTests_Shortcut");
        doorObject.AddComponent<BoxCollider2D>();
        ShortcutDoorMarkerProbe door = doorObject.AddComponent<ShortcutDoorMarkerProbe>();
        door.GlobalData = _global;
        try
        {
            SetField(door, "unlockFlag", "showcase.power.restored");

            Assert.That(door.CanInteract(null), Is.True);
            Assert.That(door.IsUnlocked, Is.False);
            door.Interact((PlayerController)null);
            Assert.That(door.LockedFeedbackCount, Is.EqualTo(1));
            Assert.That(door.UnlockedRequestCount, Is.Zero);

            _global.SetFlag("showcase.power.restored", 1);
            Assert.That(door.IsUnlocked, Is.True);
            door.Interact((PlayerController)null);
            Assert.That(door.UnlockedRequestCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(doorObject);
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                continue;

            field.SetValue(target, value);
            return;
        }

        Assert.Fail("Field not found: " + fieldName);
    }
}

public sealed class PuzzleMarkerProbe : PuzzleMarker
{
    public int GuideCount { get; private set; }

    protected override bool ShowInstruction()
    {
        GuideCount++;
        return true;
    }
}

public sealed class PuzzleRuntimeProbe : MonoBehaviour, IPuzzleRuntime
{
    public string PuzzleId => "tests.runtime-probe";
    public bool IsCompleted { get; private set; }
    public int InteractionCount { get; private set; }

    public bool CanInteract(PlayerController player)
    {
        return true;
    }

    public bool TryHandleMarkerInteraction(PlayerController player)
    {
        InteractionCount++;
        return true;
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        return true;
    }
}

public sealed class ShortcutDoorMarkerProbe : ShortcutDoorMarker
{
    public GlobalDataManager GlobalData { get; set; }
    public int LockedFeedbackCount { get; private set; }
    public int UnlockedRequestCount { get; private set; }

    protected override GlobalDataManager ResolveLockGlobalData()
    {
        return GlobalData;
    }

    protected override bool ShowLockedFeedback()
    {
        LockedFeedbackCount++;
        return true;
    }

    protected override void RequestUnlockedConnection(PlayerController player)
    {
        UnlockedRequestCount++;
    }
}