using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 패널 참조")]
    [SerializeField] private DialogueUI _overworldPanel; 
    [SerializeField] private DialogueUI _cinematicPanel; 
    [SerializeField] private NameInputUI _nameInputUI; // 🚨 인스펙터에서 꼭 연결

    private DialogueUI _activeUI; 
    private DialogueData _currentDialogue;
    private int _currentNodeIndex;
    private bool _isPlaying = false;
    private bool _isNaming = false;
    private Action _onCompleteCallback;

    private void Awake() 
    { 
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; 
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueData data, Action onComplete = null)
    {
        if (_isPlaying || data == null || data.Nodes.Count == 0) return;

        _isPlaying = true;
        _currentDialogue = data;
        _currentNodeIndex = 0;
        _onCompleteCallback = onComplete;

        _activeUI = (data.Style == DialogueStyle.Cinematic) ? _cinematicPanel : _overworldPanel;

        if (_activeUI == null) { _isPlaying = false; return; }

        GameStateManager.Instance?.ChangeState(GameState.Dialogue); 
        _activeUI.OpenPanel();
        
        PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
    }

    private void Update()
    {
        // 🚨 이름 입력 중이거나 재생 중이 아니면 무시
        if (!_isPlaying || _isNaming || _activeUI == null) return;

        bool isConfirmPressed = GameInput.DialogueAdvancePressed;

        if (isConfirmPressed)
        {
            if (_activeUI.IsTyping) _activeUI.SkipTyping(); 
            else if (!_activeUI.IsWaitingForChoice) NextNode(); 
        }
    }

    private void PlayNode(DialogueNode node)
    {
        if (node == null) return;

        // 🚨 1. 이름 입력 이벤트 체크
        if (node.EventTriggerID == "RequestName")
        {
            StartNamingProcess();
            return;
        }

        if (!string.IsNullOrEmpty(node.EventTriggerID)) EventManager.Trigger(node.EventTriggerID);

        string rawText = (LocalizationManager.Instance != null) ? LocalizationManager.Instance.GetText(node.LocalizationKey, node.DefaultText) : node.DefaultText;
        string playerName = (GlobalDataManager.Instance != null) ? GlobalDataManager.Instance.PlayerName : "Rapley";
        
        string finalText = rawText;
        try { finalText = string.Format(rawText, playerName); } catch { finalText = rawText; }

        _activeUI.DisplayNode(node.Speaker, node.Emotion, finalText);

        if (node.IsChoiceNode && node.Choices != null && node.Choices.Count > 0)
        {
            //_activeUI.ShowChoices(node.Choices, OnChoiceSelected);
        }
    }

    private void StartNamingProcess()
    {
        if (_nameInputUI == null)
        {
            Debug.LogError("NameInputUI가 매니저에 없습니다!");
            NextNode();
            return;
        }

        _isNaming = true;
        _activeUI.ClosePanel();

        _nameInputUI.Open((newName) => {
            if (GlobalDataManager.Instance != null) GlobalDataManager.Instance.PlayerName = newName;
            _isNaming = false;
            _activeUI.OpenPanel();
            NextNode(); // 이름 입력 후 다음 대사로
        });
    }

    private void NextNode()
    {
        _currentNodeIndex++;
        if (_currentNodeIndex < _currentDialogue.Nodes.Count) PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
        else EndDialogue();
    }

    public void EndDialogue()
    {
        _isPlaying = false;
        if (_activeUI != null) _activeUI.ClosePanel(); 
        
        GameStateManager.Instance?.ChangeState(GameState.Exploration); 
        
        var cb = _onCompleteCallback;
        _onCompleteCallback = null;
        cb?.Invoke(); // 🚨 인트로 매니저의 OnNameConfirmed 등이 여기서 실행됨
    }
}