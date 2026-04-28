using UnityEngine;

/// <summary>
/// 대화 가능한 NPC. InteractableBase를 상속하며
/// Z키 입력 시 DialogueManager에 대화 시퀀스를 요청합니다.
/// </summary>
public class DialogueNPC : InteractableBase
{
    [Header("Dialogue")]
    [SerializeField] private string _dialogueSequenceID = "";

    [Header("Conditional Dialogue")]
    [Tooltip("특정 플래그 값에 따라 다른 대화를 출력할 때 사용")]
    [SerializeField] private string _altDialogueSequenceID = "";
    [SerializeField] private string _altFlagKey            = "";
    [SerializeField] private int    _altFlagValue          = 1;

    public override void Interact(PlayerController player)
    {
        if (DialogueManager.Instance == null) return;

        player.SetInteracting(true);

        // 조건부 대화 분기
        string sequenceID = _dialogueSequenceID;
        if (!string.IsNullOrEmpty(_altFlagKey) &&
            GlobalDataManager.Instance != null &&
            GlobalDataManager.Instance.GetFlag(_altFlagKey) >= _altFlagValue &&
            !string.IsNullOrEmpty(_altDialogueSequenceID))
        {
            sequenceID = _altDialogueSequenceID;
        }

        DialogueManager.Instance.OnDialogueEnded += OnDialogueFinished;
        DialogueManager.Instance.StartDialogue(sequenceID);
    }

    private void OnDialogueFinished()
    {
        DialogueManager.Instance.OnDialogueEnded -= OnDialogueFinished;

        // 플레이어 이동 잠금 해제
        var player = FindFirstObjectByType<PlayerController>();
        player?.SetInteracting(false);
    }
}
