public interface IScenarioDialogueReferenceResolver
{
    bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue);
}

public interface IScenarioDialogueReferenceIdProvider
{
    bool TryGetDialogueDataId(DialogueData dialogue, out string dialogueDataId);
}

public sealed class MissingScenarioDialogueReferenceResolver : IScenarioDialogueReferenceResolver
{
    public bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue)
    {
        dialogue = null;
        return false;
    }
}

public sealed class MissingScenarioDialogueReferenceIdProvider : IScenarioDialogueReferenceIdProvider
{
    public bool TryGetDialogueDataId(DialogueData dialogue, out string dialogueDataId)
    {
        dialogueDataId = string.Empty;
        return false;
    }
}
