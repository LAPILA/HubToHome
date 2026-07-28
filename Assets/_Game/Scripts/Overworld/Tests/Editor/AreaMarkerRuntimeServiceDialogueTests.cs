using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AreaMarkerRuntimeServiceDialogueTests
{
    private GameObject _managerObject;
    private GameObject _panelObject;
    private DialogueManager _manager;
    private GameConfigManager _configBeforeTest;

    [SetUp]
    public void SetUp()
    {
        _configBeforeTest = GameConfigManager.Instance;
        _managerObject = new GameObject("AreaMarkerDialogueManager_Test");
        _manager = _managerObject.AddComponent<DialogueManager>();
        SetDialogueManagerInstance(_manager);
        Assert.That(DialogueManager.Instance, Is.SameAs(_manager));
    }

    [TearDown]
    public void TearDown()
    {
        if (_managerObject != null)
            UnityEngine.Object.DestroyImmediate(_managerObject);
        if (DialogueManager.Instance == _manager)
            SetDialogueManagerInstance(null);
        if (_panelObject != null)
            UnityEngine.Object.DestroyImmediate(_panelObject);

        if (_configBeforeTest == null && GameConfigManager.Instance != null)
            UnityEngine.Object.DestroyImmediate(GameConfigManager.Instance.gameObject);

        foreach (DialogueData transient in FindTransientDialogues())
            UnityEngine.Object.DestroyImmediate(transient);
    }

    [Test]
    public void ConfiguredDialogueWithoutPanelReturnsFalseAndDoesNotComplete()
    {
        DialogueData dialogue = CreateDialogue("configured");
        int completionCount = 0;

        try
        {
            bool started = AreaMarkerRuntimeService.TryStartDialogue(
                null,
                dialogue,
                string.Empty,
                null,
                EmotionType.Normal,
                () => completionCount++);

            Assert.That(started, Is.False);
            Assert.That(completionCount, Is.Zero);
            Assert.That(_manager.IsPlaying, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void FallbackDialogueWithoutPanelReturnsFalseAndDoesNotLeak()
    {
        int transientCountBefore = FindTransientDialogues().Length;

        bool started = AreaMarkerRuntimeService.TryStartDialogue(
            null,
            null,
            "fallback",
            null,
            EmotionType.Normal);

        Assert.That(started, Is.False);
        Assert.That(FindTransientDialogues().Length, Is.EqualTo(transientCountBefore));
    }

    [Test]
    public void CancelingFallbackDialogueDestroysTransientWithoutCompleting()
    {
        _panelObject = new GameObject("AreaMarkerDialoguePanel_Test");
        DialogueUI panel = _panelObject.AddComponent<DialogueUI>();
        SetPrivateField(_manager, "_overworldPanel", panel);
        int transientCountBefore = FindTransientDialogues().Length;
        int completionCount = 0;

        bool started = AreaMarkerRuntimeService.TryStartDialogue(
            null,
            null,
            "fallback",
            null,
            EmotionType.Normal,
            () => completionCount++);

        Assert.That(started, Is.True);
        Assert.That(FindTransientDialogues().Length, Is.EqualTo(transientCountBefore + 1));

        _manager.CancelDialogue();

        Assert.That(completionCount, Is.Zero);
        Assert.That(FindTransientDialogues().Length, Is.EqualTo(transientCountBefore));
    }

    private static DialogueData CreateDialogue(string text)
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = text });
        return dialogue;
    }

    private static DialogueData[] FindTransientDialogues()
    {
        return Resources.FindObjectsOfTypeAll<DialogueData>()
            .Where(dialogue => dialogue != null && dialogue.name == "Runtime_AreaMarkerDialogue")
            .ToArray();
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void SetDialogueManagerInstance(DialogueManager manager)
    {
        PropertyInfo property = typeof(DialogueManager).GetProperty(
            nameof(DialogueManager.Instance),
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, manager);
    }
}
