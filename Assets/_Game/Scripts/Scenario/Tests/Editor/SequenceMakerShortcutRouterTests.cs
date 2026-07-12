using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceMakerShortcutRouterTests
{
    [TestCase(KeyCode.Z, false, SequenceMakerShortcutCommand.Undo)]
    [TestCase(KeyCode.Z, true, SequenceMakerShortcutCommand.Redo)]
    [TestCase(KeyCode.Y, false, SequenceMakerShortcutCommand.Redo)]
    [TestCase(KeyCode.S, false, SequenceMakerShortcutCommand.Save)]
    public void CanvasFocusRoutesDocumentShortcuts(
        KeyCode keyCode,
        bool shift,
        SequenceMakerShortcutCommand expected)
    {
        var canvas = new VisualElement();

        SequenceMakerShortcutCommand command = SequenceMakerShortcutRouter.Resolve(
            keyCode,
            true,
            false,
            shift,
            canvas);

        Assert.That(command, Is.EqualTo(expected));
    }

    [TestCase(KeyCode.Z, false)]
    [TestCase(KeyCode.Z, true)]
    [TestCase(KeyCode.Y, false)]
    public void TextFieldFocusKeepsNativeUndoRedo(KeyCode keyCode, bool shift)
    {
        var field = new TextField();
        VisualElement textInput = field.Q<VisualElement>(className: "unity-text-input") ?? field;

        SequenceMakerShortcutCommand command = SequenceMakerShortcutRouter.Resolve(
            keyCode,
            true,
            false,
            shift,
            textInput);

        Assert.That(command, Is.EqualTo(SequenceMakerShortcutCommand.None));
    }

    [Test]
    public void TextFieldFocusStillAllowsDocumentSave()
    {
        var field = new TextField();

        SequenceMakerShortcutCommand command = SequenceMakerShortcutRouter.Resolve(
            KeyCode.S,
            true,
            false,
            false,
            field);

        Assert.That(command, Is.EqualTo(SequenceMakerShortcutCommand.Save));
    }

    [Test]
    public void UnmodifiedKeysAreIgnored()
    {
        Assert.That(
            SequenceMakerShortcutRouter.Resolve(
                KeyCode.Z,
                false,
                false,
                false,
                new VisualElement()),
            Is.EqualTo(SequenceMakerShortcutCommand.None));
    }
}
