using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DialogueManagerRunnerTests
{
    [Test]
    public void ShowAndWait_WhenDialogueContainsNullNode_RejectsWithoutBecomingBusy()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(null);
        var runner = new DialogueManagerRunner();
        runner.Register("invalid.dialogue", dialogue);

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => runner.ShowAndWait("invalid.dialogue", null));

            Assert.That(exception.Message, Does.Contain("null node at index 0"));
            Assert.That(runner.IsBusy, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void StartDialogue_WhenDialogueContainsNullNode_CompletesWithoutStarting()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(null);
        GameObject managerObject = new GameObject("DialogueManager_NullNode_Test");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        int completionCount = 0;

        try
        {
            manager.StartDialogue(dialogue, () => completionCount++);

            Assert.That(manager.IsPlaying, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void StartDialogue_WhenConfiguredPanelIsMissing_CompletesWithoutStarting()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "valid" });
        GameObject managerObject = new GameObject("DialogueManager_MissingPanel_Test");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        int completionCount = 0;

        try
        {
            manager.StartDialogue(dialogue, () => completionCount++);

            Assert.That(manager.IsPlaying, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void ShowAndWait_WhenConfiguredPanelIsMissing_FailsWithoutCompleting()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "valid" });
        GameObject managerObject = new GameObject("DialogueRunner_MissingPanel_Test");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        var runner = new DialogueManagerRunner(manager);
        runner.Register("test.missing_panel", dialogue);
        int completionCount = 0;

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => runner.ShowAndWait("  test.missing_panel\t", () => completionCount++));

            Assert.That(exception.Message, Does.Contain("did not start"));
            Assert.That(completionCount, Is.Zero);
            Assert.That(runner.IsBusy, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void ChoiceWithEmptyNextDialogue_EndsCurrentDialogueExactlyOnce()
    {
        DialogueData current = ScriptableObject.CreateInstance<DialogueData>();
        current.Nodes.Add(new DialogueNode { DefaultText = "current" });
        DialogueData emptyNext = ScriptableObject.CreateInstance<DialogueData>();
        GameObject managerObject = new GameObject("DialogueManager_EmptyBranch_Test");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        int completionCount = 0;

        try
        {
            SetPrivateField(manager, "_isPlaying", true);
            SetPrivateField(manager, "_currentDialogue", current);
            SetPrivateField(manager, "_currentNodeIndex", 0);
            SetPrivateField(manager, "_onCompleteCallback", (Action)(() => completionCount++));
            var choice = new ChoiceData { NextDialogue = emptyNext };

            Assert.DoesNotThrow(() => InvokePrivate(manager, "OnChoiceSelected", choice));

            Assert.That(manager.IsPlaying, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(current);
            UnityEngine.Object.DestroyImmediate(emptyNext);
        }
    }

    [Test]
    public void PlayNode_WhenNullNodeIsEncounteredDuringPlayback_TerminatesExactlyOnce()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "valid" });
        dialogue.Nodes.Add(null);
        GameObject managerObject = new GameObject("DialogueManager_DefensiveNullNode_Test");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        int completionCount = 0;

        try
        {
            SetPrivateField(manager, "_isPlaying", true);
            SetPrivateField(manager, "_currentDialogue", dialogue);
            SetPrivateField(manager, "_currentNodeIndex", 1);
            SetPrivateField(manager, "_onCompleteCallback", (Action)(() => completionCount++));

            InvokePrivate(manager, "PlayNode", null);
            InvokePrivate(manager, "PlayNode", null);

            Assert.That(manager.IsPlaying, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void PlaybackPolicyReportsNullChoiceCoordinates()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode
        {
            IsChoiceNode = true,
            Choices = new System.Collections.Generic.List<ChoiceData> { null }
        });

        try
        {
            bool valid = DialoguePlaybackPolicy.TryValidate(dialogue, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("node 0, choice 0"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    private static void SetPrivateField<T>(DialogueManager manager, string fieldName, T value)
    {
        FieldInfo field = typeof(DialogueManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        field.SetValue(manager, value);
    }

    private static void InvokePrivate(DialogueManager manager, string methodName, object argument)
    {
        MethodInfo method = typeof(DialogueManager).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing private method: " + methodName);
        method.Invoke(manager, new[] { argument });
    }
}
