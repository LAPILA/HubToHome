using UnityEngine;

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

public interface IScenarioAudioReferenceResolver
{
    bool TryResolveAudioClip(string audioClipId, out AudioClip clip);
}

public interface IScenarioAudioReferenceIdProvider
{
    bool TryGetAudioClipId(AudioClip clip, out string audioClipId);
}

public sealed class MissingScenarioAudioReferenceResolver : IScenarioAudioReferenceResolver
{
    public bool TryResolveAudioClip(string audioClipId, out AudioClip clip)
    {
        clip = null;
        return false;
    }
}

public sealed class MissingScenarioAudioReferenceIdProvider : IScenarioAudioReferenceIdProvider
{
    public bool TryGetAudioClipId(AudioClip clip, out string audioClipId)
    {
        audioClipId = string.Empty;
        return false;
    }
}
