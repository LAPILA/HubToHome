using UnityEngine;
using UnityEngine.UIElements;

public enum SequenceMakerShortcutCommand
{
    None,
    Save,
    Undo,
    Redo
}

public static class SequenceMakerShortcutRouter
{
    public static SequenceMakerShortcutCommand Resolve(
        KeyCode keyCode,
        bool ctrlKey,
        bool commandKey,
        bool shiftKey,
        VisualElement focusedElement)
    {
        if (!ctrlKey && !commandKey)
        {
            return SequenceMakerShortcutCommand.None;
        }

        if (keyCode == KeyCode.S)
        {
            return SequenceMakerShortcutCommand.Save;
        }

        if (IsTextEditing(focusedElement))
        {
            return SequenceMakerShortcutCommand.None;
        }

        if (keyCode == KeyCode.Z)
        {
            return shiftKey
                ? SequenceMakerShortcutCommand.Redo
                : SequenceMakerShortcutCommand.Undo;
        }

        return keyCode == KeyCode.Y
            ? SequenceMakerShortcutCommand.Redo
            : SequenceMakerShortcutCommand.None;
    }

    public static bool IsTextEditing(VisualElement focusedElement)
    {
        for (VisualElement current = focusedElement; current != null; current = current.parent)
        {
            if (current is TextField
                || current.ClassListContains("unity-base-text-field")
                || current.ClassListContains("unity-text-input"))
            {
                return true;
            }
        }
        return false;
    }
}
