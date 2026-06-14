using System;
using System.Collections.Generic;

public sealed class ScenarioDialogueRegistry
{
    private readonly Dictionary<string, DialogueData> _dialogues = new Dictionary<string, DialogueData>();

    public ScenarioDialogueRegistry(IEnumerable<ScenarioDialogueReferenceData> references)
    {
        if (references == null)
        {
            return;
        }

        foreach (ScenarioDialogueReferenceData reference in references)
        {
            if (reference == null || reference.Dialogue == null || string.IsNullOrWhiteSpace(reference.DialogueId))
            {
                continue;
            }

            _dialogues[reference.DialogueId.Trim()] = reference.Dialogue;
        }
    }

    public int Count
    {
        get { return _dialogues.Count; }
    }

    public bool TryResolve(string dialogueId, out DialogueData dialogue)
    {
        dialogue = null;
        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }

        return _dialogues.TryGetValue(dialogueId.Trim(), out dialogue) && dialogue != null;
    }

    public int RegisterInto(DialogueManagerRunner runner)
    {
        if (runner == null)
        {
            throw new ArgumentNullException(nameof(runner));
        }

        int registeredCount = 0;
        foreach (KeyValuePair<string, DialogueData> pair in _dialogues)
        {
            runner.Register(pair.Key, pair.Value);
            registeredCount++;
        }

        return registeredCount;
    }
}
