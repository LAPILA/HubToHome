using System;
using System.Collections.Generic;

public sealed class DialogueManagerRunner : IDialogueRunner
{
    private readonly Dictionary<string, DialogueData> _dialogues = new Dictionary<string, DialogueData>();
    private readonly DialogueManager _manager;
    private bool _isBusy;

    public DialogueManagerRunner(DialogueManager manager = null)
    {
        _manager = manager;
    }

    public bool IsBusy
    {
        get
        {
            DialogueManager manager = ResolveManager();
            return _isBusy || (manager != null && manager.IsPlaying);
        }
    }

    public void Register(string dialogueId, DialogueData dialogue)
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            throw new ArgumentException("Dialogue id is required.", nameof(dialogueId));
        }

        _dialogues[dialogueId.Trim()] = dialogue;
    }

    public bool TryGetRegisteredDialogue(string dialogueId, out DialogueData dialogue)
    {
        dialogue = null;
        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }

        return _dialogues.TryGetValue(dialogueId.Trim(), out dialogue) && dialogue != null;
    }

    public void ShowAndWait(string dialogueId, Action onComplete)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Dialogue runner is already busy.");
        }

        string normalizedDialogueId = dialogueId?.Trim() ?? string.Empty;
        if (!_dialogues.TryGetValue(normalizedDialogueId, out DialogueData dialogue)
            || dialogue == null)
        {
            throw new InvalidOperationException("Dialogue id is not registered: " + dialogueId);
        }

        if (!DialoguePlaybackPolicy.TryValidate(dialogue, out string validationError))
            throw new InvalidOperationException(validationError + " DialogueId=" + normalizedDialogueId);

        DialogueManager manager = ResolveManager();
        if (manager == null)
        {
            throw new InvalidOperationException("DialogueManager is missing.");
        }

        _isBusy = true;
        bool startAccepted = false;
        manager.StartDialogue(dialogue, () =>
        {
            _isBusy = false;
            if (startAccepted)
                onComplete?.Invoke();
        });

        if (!manager.IsPlaying)
        {
            _isBusy = false;
            throw new InvalidOperationException(
                "DialogueManager did not start dialogue: " + normalizedDialogueId);
        }

        startAccepted = true;
    }

    private DialogueManager ResolveManager()
    {
        return _manager != null ? _manager : DialogueManager.Instance;
    }
}
