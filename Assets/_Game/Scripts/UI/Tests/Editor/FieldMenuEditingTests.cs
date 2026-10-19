using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FieldMenuEditingTests
{
    private readonly List<FieldMenuCommand> _commands = new List<FieldMenuCommand>();
    private GameObject _root, _eventRoot;
    private EventSystem _events, _previousEvents;
    private FieldMenuWidgets _widgets;
    private FieldMenuGrowthView _growth;

    [SetUp]
    public void SetUp()
    {
        _commands.Clear();
        _previousEvents = EventSystem.current;
        _eventRoot = new GameObject("FieldMenuEditingEvents", typeof(EventSystem));
        _events = _eventRoot.GetComponent<EventSystem>();
        EventSystem.current = _events;
        _root = new GameObject("FieldMenuEditingRoot", typeof(RectTransform), typeof(Canvas));
        _widgets = new FieldMenuWidgets(null, command => _commands.Add(command));
        _growth = FieldMenuGrowthView.Create(_root.GetComponent<RectTransform>(), _widgets);
        SetField(_growth, "_mutable", true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_events != null) _events.SetSelectedGameObject(null);
        if (_root != null) Object.DestroyImmediate(_root);
        if (_eventRoot != null) Object.DestroyImmediate(_eventRoot);
        EventSystem.current = _previousEvents;
    }

    [Test]
    public void EndEditingClearsSelectionSoLaterSubmitCannotReactivateClosedInput()
    {
        TMP_InputField input = Input(0);
        Focus(input);
        Assert.That(_growth.IsTextEditing, Is.True);

        _growth.EndTextEditing();
        // The shared UI module sends Submit to the current selection. Retaining a
        // closed TMP field here would schedule its activation on the next update.
        if (_events.currentSelectedGameObject != null)
            ExecuteEvents.Execute(_events.currentSelectedGameObject, new BaseEventData(_events), ExecuteEvents.submitHandler);

        Assert.That(_events.currentSelectedGameObject, Is.Null);
        Assert.That(input.isFocused, Is.False);
        Assert.That(_growth.IsTextEditing, Is.False);
        Assert.That(GetField<bool>(input, "m_ShouldActivateNextUpdate"), Is.False);
    }

    [Test]
    public void EndEditingPreservesSelectionOwnedByAnotherPanel()
    {
        TMP_InputField input = Input(0);
        Focus(input);
        var outside = new GameObject("AnotherPanelSelection", typeof(RectTransform));
        outside.transform.SetParent(_root.transform, false);
        _events.SetSelectedGameObject(outside);

        _growth.EndTextEditing();

        Assert.That(_events.currentSelectedGameObject, Is.SameAs(outside));
        Assert.That(input.isFocused, Is.False);
    }

    [Test]
    public void PendingActivationCannotReclaimSelectionAfterEditingIsClosed()
    {
        TMP_InputField input = Input(0);
        Assert.That(input, Is.InstanceOf<FieldMenuNumberInput>());
        input.shouldActivateOnSelect = false;
        _events.SetSelectedGameObject(input.gameObject);
        // TMP can defer activation between a pointer selection and LateUpdate.
        SetField(input, "m_AllowInput", false);
        SetField(input, "m_ShouldActivateNextUpdate", true);

        _growth.EndTextEditing();
        _commands.Clear();
        Assert.That(GetField<bool>(input, "m_ShouldActivateNextUpdate"), Is.True);
        MethodInfo lateUpdate = typeof(FieldMenuNumberInput).GetMethod("LateUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.That(lateUpdate, Is.Not.Null);
        lateUpdate.Invoke(input, null);
        lateUpdate.Invoke(input, null);

        Assert.That(input.isFocused, Is.False);
        Assert.That(_growth.IsTextEditing, Is.False);
        Assert.That(_events.currentSelectedGameObject, Is.Null);
        Assert.That(_commands, Is.Empty);
    }

    [TestCase(0, "7", 7)]
    [TestCase(2, "19", 19)]
    [TestCase(4, "0", 0)]
    public void NumericEndEditDispatchesDraftAmountForExactStat(int index, string value, int expected)
    {
        TMP_InputField input = Input(index);
        input.SetTextWithoutNotify(value);

        input.onEndEdit.Invoke(value);

        Assert.That(_commands.Count, Is.EqualTo(1));
        Assert.That(_commands[0].Kind, Is.EqualTo(FieldMenuCommandKind.StatSet));
        Assert.That(_commands[0].Index, Is.EqualTo(index));
        Assert.That(_commands[0].Amount, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("-1")]
    [TestCase("not-a-number")]
    [TestCase("2147483648")]
    public void InvalidEndEditCannotDispatchDraftMutation(string value)
    {
        TMP_InputField input = Input(0);
        input.onEndEdit.Invoke(value);

        Assert.That(_commands, Is.Empty);
        Assert.That(input.text, Is.EqualTo("0"));
    }

    [Test]
    public void FocusedEndEditingDispatchesNumberOnceAndReleasesSelection()
    {
        TMP_InputField input = Input(1);
        Focus(input);
        input.SetTextWithoutNotify("8");
        _commands.Clear();

        _growth.EndTextEditing();
        _growth.EndTextEditing();

        Assert.That(_commands.Count, Is.EqualTo(1));
        Assert.That(_commands[0].Kind, Is.EqualTo(FieldMenuCommandKind.StatSet));
        Assert.That(_commands[0].Index, Is.EqualTo(1));
        Assert.That(_commands[0].Amount, Is.EqualTo(8));
        Assert.That(_events.currentSelectedGameObject, Is.Null);
    }

    [Test]
    public void MutationLockRejectsEndEditEvenWhenWidgetStillHasOldText()
    {
        SetField(_growth, "_mutable", false);

        Input(0).onEndEdit.Invoke("9");

        Assert.That(_commands, Is.Empty);
    }

    [Test]
    public void ModalReleasesEditingAndBlocksLateDraftCommandsUntilClosed()
    {
        FieldMenuView owner = CreateModalOwner();
        TMP_InputField input = Input(0);
        Focus(input);
        input.SetTextWithoutNotify("11");
        _commands.Clear();

        owner.Modal(true, "Confirm", "Body", "Apply");
        input.onEndEdit.Invoke("11");

        CanvasGroup group = _growth.GetComponent<CanvasGroup>();
        Assert.That(_commands, Is.Empty, "Closing a field for a modal must not apply its pending text.");
        Assert.That(group.interactable, Is.False);
        Assert.That(group.blocksRaycasts, Is.False);
        Assert.That(input.isFocused, Is.False);
        Assert.That(_events.currentSelectedGameObject, Is.Null);

        owner.Modal(false);
        input.onEndEdit.Invoke("3");

        Assert.That(group.interactable, Is.True);
        Assert.That(group.blocksRaycasts, Is.True);
        Assert.That(_commands.Count, Is.EqualTo(1));
        Assert.That(_commands[0].Kind, Is.EqualTo(FieldMenuCommandKind.StatSet));
        Assert.That(_commands[0].Amount, Is.EqualTo(3));
    }

    [Test]
    public void PlayerSaveBindingDistinguishesDifferentSaveInstancesWithSameDatabaseId()
    {
        var actorObject = new GameObject("InactiveSaveBindingActor");
        actorObject.transform.SetParent(_root.transform, false);
        actorObject.SetActive(false); // No Awake/Start or scene/global initialization.
        PlayerCharacter actor = actorObject.AddComponent<PlayerCharacter>();
        var first = new CharacterSaveData { CharacterDataID = "same-db", CharacterID = "same-character" };
        var second = new CharacterSaveData { CharacterDataID = "same-db", CharacterID = "same-character" };
        SetField(actor, "_mySaveDataRef", first);

        Assert.That(actor.IsBoundToSaveData(first), Is.True);
        Assert.That(actor.IsBoundToSaveData(second), Is.False);
        Assert.That(actor.IsBoundToSaveData(null), Is.False);
    }

    [Test]
    public void CancelledPointerHoldDoesNotConsumeNextFreshKeyboardSubmit()
    {
        Button button = _widgets.Button(_root.transform, "Repeat", "+", 0, 0, 32, 32,
            new FieldMenuCommand(FieldMenuCommandKind.StatStep, 0, 1), true);
        FieldMenuButtonRelay relay = button.GetComponent<FieldMenuButtonRelay>();
        var pointer = new PointerEventData(_events);
        relay.OnPointerDown(pointer);
        relay.OnPointerExit(pointer); relay.OnPointerUp(pointer);
        Assert.That(_commands.Count, Is.EqualTo(1));

        relay.SubmitFromKeyboard();

        Assert.That(_commands.Count, Is.EqualTo(2));
        Assert.That(_commands[1].Kind, Is.EqualTo(FieldMenuCommandKind.StatStep));
        Assert.That(GetField<bool>(relay, "_pointerGesture"), Is.False);
    }

    [Test]
    public void PointerTapDoesNotApplyRepeatStepTwice()
    {
        Button button = _widgets.Button(_root.transform, "RepeatTap", "+", 0, 0, 32, 32,
            new FieldMenuCommand(FieldMenuCommandKind.StatStep, 0, 1), true);
        FieldMenuButtonRelay relay = button.GetComponent<FieldMenuButtonRelay>();
        var pointer = new PointerEventData(_events);
        relay.OnPointerDown(pointer); relay.OnPointerUp(pointer); relay.Submit();
        Assert.That(_commands.Count, Is.EqualTo(1));
    }

    private TMP_InputField Input(int index)
    {
        Transform target = _growth.transform.Find("Attributes/AttributeRows/Viewport/Rows/Stat" + index + "/Amount" + index);
        Assert.That(target, Is.Not.Null);
        return target.GetComponent<TMP_InputField>();
    }

    private void Focus(TMP_InputField input)
    {
        // Seed only TMP's edit state: EditMode must not open a device keyboard or
        // depend on the project's default font, InputModule or frame scheduling.
        input.shouldActivateOnSelect = false;
        _events.SetSelectedGameObject(input.gameObject);
        SetField(input, "m_ShouldActivateNextUpdate", false);
        SetField(input, "m_AllowInput", true);
    }

    private FieldMenuView CreateModalOwner()
    {
        RectTransform ownerRoot = _widgets.Rect(_root.transform, "ModalOwner", 0, 0, 604, 178);
        FieldMenuView owner = ownerRoot.gameObject.AddComponent<FieldMenuView>();
        RectTransform modal = _widgets.Rect(ownerRoot, "Modal", 0, 0, 250, 150);
        SetField(owner, "_growthView", _growth);
        SetField(owner, "_modal", modal);
        SetField(owner, "_modalTitle", _widgets.Text(modal, "Title", string.Empty, 10, 0, 0, 240, 20));
        SetField(owner, "_modalBody", _widgets.Text(modal, "Body", string.Empty, 10, 0, 20, 240, 60));
        Button[] buttons = GetField<Button[]>(owner, "_modalButtons");
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = _widgets.Button(modal, "Choice" + i, string.Empty, i * 120, 90, 110, 32,
                new FieldMenuCommand(FieldMenuCommandKind.ModalAccept, i));
        modal.gameObject.SetActive(false);
        return owner;
    }

    private static void SetField(object target, string name, object value)
    {
        FindField(target, name).SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        return (T)FindField(target, name).GetValue(target);
    }

    private static FieldInfo FindField(object target, string name)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        Assert.Fail("Missing field: " + target.GetType().Name + "." + name);
        return null;
    }
}
