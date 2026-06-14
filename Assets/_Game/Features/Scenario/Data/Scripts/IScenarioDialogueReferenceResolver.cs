public interface IScenarioDialogueReferenceResolver
{
    bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue);
}

public sealed class MissingScenarioDialogueReferenceResolver : IScenarioDialogueReferenceResolver
{
    public bool TryResolveDialogue(string dialogueDataId, out DialogueData dialogue)
    {
        dialogue = null;
        return false;
    }
}
