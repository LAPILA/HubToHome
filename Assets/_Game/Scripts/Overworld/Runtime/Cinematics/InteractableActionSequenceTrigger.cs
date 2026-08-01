using UnityEngine;

/// <summary>
/// Starts a configured scene action sequence through the normal interaction path.
/// </summary>
public sealed class InteractableActionSequenceTrigger : InteractableBase
{
    [Header("Sequence")]
    [SerializeField] private SceneActionSequencePlayer _sequencePlayer;

    [Header("Completion")]
    [SerializeField] private bool _runOncePerSave;
    [SerializeField] private string _completionFlagId = string.Empty;

    private void Reset()
    {
        _sequencePlayer = GetComponent<SceneActionSequencePlayer>();
    }

    public override bool CanInteract(PlayerController player)
    {
        return base.CanInteract(player)
            && _sequencePlayer != null
            && !_sequencePlayer.IsPlaying
            && !HasCompletedForCurrentSave();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        _sequencePlayer.TryPlay(result =>
        {
            if (result != null && result.Status == ActionExecutionStatus.Succeeded)
                MarkCompletedForCurrentSave();
        });
    }

    private bool HasCompletedForCurrentSave()
    {
        return _runOncePerSave
            && !string.IsNullOrWhiteSpace(_completionFlagId)
            && GlobalDataManager.Instance != null
            && GlobalDataManager.Instance.GetFlag(_completionFlagId) != 0;
    }

    private void MarkCompletedForCurrentSave()
    {
        if (_runOncePerSave && !string.IsNullOrWhiteSpace(_completionFlagId))
            GlobalDataManager.Instance?.SetFlag(_completionFlagId, 1);
    }
}