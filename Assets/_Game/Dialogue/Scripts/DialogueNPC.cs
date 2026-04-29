using UnityEngine;

/// <summary>
/// 대화 가능한 NPC. InteractableBase를 상속하며
/// Z키 입력 시 DialogueManager에 대화 시퀀스를 요청합니다.
/// 
/// 사용법:
/// 1. _dialogueFile에 JSON TextAsset을 드래그&드롭
/// 2. _dialogueSequenceID에 해당 파일 내 sequenceID 입력
/// </summary>
public class DialogueNPC : InteractableBase
{
    [Header("Dialogue")]
    [Tooltip("대화 JSON 파일을 여기에 드래그&드롭하세요 (Resources 폴더 불필요)")]
    [SerializeField] private TextAsset _dialogueFile = null;
    [SerializeField] private string _dialogueSequenceID = "";

    [Header("Conditional Dialogue")]
    [Tooltip("특정 플래그 값에 따라 다른 대화를 출력할 때 사용")]
    [SerializeField] private TextAsset _altDialogueFile        = null;
    [SerializeField] private string    _altDialogueSequenceID  = "";
    [SerializeField] private string    _altFlagKey             = "";
    [SerializeField] private int       _altFlagValue           = 1;

    private void Start()
    {
        // 씬 시작 시 연결된 JSON 파일을 자동으로 DialogueManager에 로드
        if (_dialogueFile != null)
            DialogueManager.Instance?.LoadDialogueFromAsset(_dialogueFile);

        if (_altDialogueFile != null && _altDialogueFile != _dialogueFile)
            DialogueManager.Instance?.LoadDialogueFromAsset(_altDialogueFile);
    }

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

        DialogueManager.Instance.OnDialogueEnded.AddListener(OnDialogueFinished);
        DialogueManager.Instance.StartDialogue(sequenceID);
    }

    private void OnDialogueFinished()
    {
        DialogueManager.Instance.OnDialogueEnded.RemoveListener(OnDialogueFinished);

        // 플레이어 이동 잠금 해제
        var player = FindFirstObjectByType<PlayerController>();
        player?.SetInteracting(false);
    }
}
