using System;
using System.Collections.Generic;

public sealed class DialogueManagerRunner : IDialogueRunner, ICancellableDialogueRunner
{
    private readonly Dictionary<string, DialogueData> _dialogues = new Dictionary<string, DialogueData>();
    private readonly DialogueManager _manager;
    private bool _isBusy;
    private int _runGeneration;
    private int _activeRunGeneration;
    private int _activeDialogueGeneration;

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
            throw new InvalidOperationException("Dialogue runner is already busy.");

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
            throw new InvalidOperationException("DialogueManager is missing.");

        int runGeneration = ++_runGeneration;
        _activeRunGeneration = runGeneration;
        _activeDialogueGeneration = 0;
        _isBusy = true;

        bool started = manager.TryStartDialogue(
            dialogue,
            () => CompleteRun(runGeneration, onComplete),
            () => CancelRun(runGeneration),
            null,
            out int dialogueGeneration);
        if (!started)
        {
            CancelRun(runGeneration);
            throw new InvalidOperationException(
                "DialogueManager did not start dialogue: " + normalizedDialogueId);
        }

        if (!_isBusy || _activeRunGeneration != runGeneration)
            return;

        _activeDialogueGeneration = dialogueGeneration;
        if (!manager.IsPlaying)
        {
            CancelRun(runGeneration);
            throw new InvalidOperationException(
                "DialogueManager stopped dialogue during startup: " + normalizedDialogueId);
        }
    }

    public void Cancel()
    {
        if (!_isBusy)
            return;

        int runGeneration = _activeRunGeneration;
        int dialogueGeneration = _activeDialogueGeneration;
        DialogueManager manager = ResolveManager();
        CancelRun(runGeneration);

        if (manager != null && dialogueGeneration > 0)
            manager.CancelDialogue(dialogueGeneration);
    }

    private void CompleteRun(int runGeneration, Action onComplete)
    {
        if (!_isBusy || runGeneration != _activeRunGeneration)
            return;

        _isBusy = false;
        _activeDialogueGeneration = 0;
        onComplete?.Invoke();
    }

    private void CancelRun(int runGeneration)
    {
        if (runGeneration != _activeRunGeneration)
            return;

        _isBusy = false;
        _activeDialogueGeneration = 0;
    }

    private DialogueManager ResolveManager()
    {
        return _manager != null ? _manager : DialogueManager.Instance;
    }
}
