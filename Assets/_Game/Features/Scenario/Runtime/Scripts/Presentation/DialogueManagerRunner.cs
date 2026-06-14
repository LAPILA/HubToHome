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

    public void ShowAndWait(string dialogueId, Action onComplete)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Dialogue runner is already busy.");
        }

        DialogueData dialogue;
        if (!_dialogues.TryGetValue(dialogueId, out dialogue) || dialogue == null)
        {
            throw new InvalidOperationException("Dialogue id is not registered: " + dialogueId);
        }

        if (dialogue.Nodes == null || dialogue.Nodes.Count == 0)
        {
            throw new InvalidOperationException("Dialogue has no nodes: " + dialogueId);
        }

        DialogueManager manager = ResolveManager();
        if (manager == null)
        {
            throw new InvalidOperationException("DialogueManager is missing.");
        }

        _isBusy = true;
        manager.StartDialogue(dialogue, () =>
        {
            _isBusy = false;
            onComplete?.Invoke();
        });

        if (!manager.IsPlaying)
        {
            _isBusy = false;
            throw new InvalidOperationException("DialogueManager did not start dialogue: " + dialogueId);
        }
    }

    private DialogueManager ResolveManager()
    {
        return _manager != null ? _manager : DialogueManager.Instance;
    }
}
