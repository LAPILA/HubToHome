using UnityEngine;

public enum PowerConsoleInteractionState
{
    Locked,
    Ready,
    Playing,
    Completed,
    Invalid
}

/// <summary>
/// 전력 조건에 따라 안내 대화 또는 Action Sequence 중 하나만 실행합니다.
/// </summary>
[DisallowMultipleComponent]
public class PowerConsoleInteractable : InteractableBase
{
    [Header("Power Gate")]
    [SerializeField] private string _powerReadyFlagId = string.Empty;
    [SerializeField] private int _powerReadyValue = 1;

    [Header("Locked Feedback")]
    [SerializeField] private DialogueData _lockedDialogue;
    [SerializeField, TextArea(2, 5)] private string _lockedFallbackText =
        "* 전력이 부족하다.";

    [Header("Sequence")]
    [SerializeField] private SceneActionSequencePlayer _sequencePlayer;

    [Header("Completion")]
    [SerializeField] private bool _runOncePerSave = true;
    [SerializeField] private string _completionFlagId = string.Empty;

    private GlobalDataManager _globalDataSource;

    public PowerConsoleInteractionState InteractionState => EvaluateState();

    public void Configure(
        string powerReadyFlagId,
        int powerReadyValue,
        DialogueData lockedDialogue,
        string lockedFallbackText,
        SceneActionSequencePlayer sequencePlayer,
        bool runOncePerSave,
        string completionFlagId)
    {
        _powerReadyFlagId = Normalize(powerReadyFlagId);
        _powerReadyValue = powerReadyValue;
        _lockedDialogue = lockedDialogue;
        _lockedFallbackText = lockedFallbackText ?? string.Empty;
        _sequencePlayer = sequencePlayer;
        _runOncePerSave = runOncePerSave;
        _completionFlagId = Normalize(completionFlagId);
    }

    public void SetGlobalDataSource(GlobalDataManager globalData)
    {
        _globalDataSource = globalData;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player))
            return false;

        PowerConsoleInteractionState state = EvaluateState();
        return state == PowerConsoleInteractionState.Locked
            || state == PowerConsoleInteractionState.Ready;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        if (EvaluateState() == PowerConsoleInteractionState.Locked)
        {
            TryStartLockedDialogue();
            return;
        }

        TryStartSequence();
    }

    protected virtual PowerConsoleInteractionState EvaluateState()
    {
        GlobalDataManager global = ResolveGlobalData();
        if (_runOncePerSave
            && !string.IsNullOrEmpty(_completionFlagId)
            && global != null
            && global.GetFlag(_completionFlagId, 0) != 0)
        {
            return PowerConsoleInteractionState.Completed;
        }

        bool powerReady = string.IsNullOrEmpty(_powerReadyFlagId)
            || (global != null && global.GetFlag(_powerReadyFlagId, 0) >= _powerReadyValue);
        if (!powerReady)
            return PowerConsoleInteractionState.Locked;
        if (_sequencePlayer == null)
            return PowerConsoleInteractionState.Invalid;
        return _sequencePlayer.IsPlaying
            ? PowerConsoleInteractionState.Playing
            : PowerConsoleInteractionState.Ready;
    }

    protected virtual bool TryStartLockedDialogue()
    {
        return AreaMarkerRuntimeService.TryStartDialogue(
            this,
            _lockedDialogue,
            _lockedFallbackText,
            null,
            EmotionType.Normal);
    }

    protected virtual bool TryStartSequence()
    {
        return _sequencePlayer != null && _sequencePlayer.TryPlay(result =>
        {
            if (result == null || result.Status != ActionExecutionStatus.Succeeded)
                return;

            if (_runOncePerSave && !string.IsNullOrEmpty(_completionFlagId))
                ResolveGlobalData()?.SetFlag(_completionFlagId, 1);
        });
    }

    private void Reset()
    {
        _sequencePlayer = GetComponent<SceneActionSequencePlayer>();
    }

    private void OnValidate()
    {
        _powerReadyFlagId = Normalize(_powerReadyFlagId);
        _completionFlagId = Normalize(_completionFlagId);
    }

    private GlobalDataManager ResolveGlobalData()
    {
        return _globalDataSource != null ? _globalDataSource : GlobalDataManager.Instance;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
