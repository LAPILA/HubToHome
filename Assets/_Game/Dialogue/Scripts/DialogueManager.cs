using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 패널 참조")]
    [SerializeField] private DialogueUI _overworldPanel; 
    [SerializeField] private DialogueUI _cinematicPanel; 

    private DialogueUI _activeUI; // 현재 활성화된 패널
    private DialogueData _currentDialogue;
    private int _currentNodeIndex;
    private bool _isPlaying = false;
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

        // 🚨 데이터의 스타일(오버월드/시네마틱)에 따라 사용할 패널 결정
        _activeUI = (data.Style == DialogueStyle.Cinematic) ? _cinematicPanel : _overworldPanel;

        GameStateManager.Instance?.ChangeState(GameState.Dialogue); 
        _activeUI.OpenPanel();
        
        PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
    }

    private void Update()
    {
        if (!_isPlaying || _activeUI == null || Keyboard.current == null) return;

        bool isConfirmPressed = Keyboard.current.zKey.wasPressedThisFrame || 
                                Keyboard.current.spaceKey.wasPressedThisFrame || 
                                Keyboard.current.enterKey.wasPressedThisFrame;

        if (isConfirmPressed)
        {
            if (_activeUI.IsTyping) _activeUI.SkipTyping(); 
            else if (!_activeUI.IsWaitingForChoice) NextNode(); 
        }
    }

    private void PlayNode(DialogueNode node)
    {
        if (!string.IsNullOrEmpty(node.EventTriggerID)) EventManager.Trigger(node.EventTriggerID);

        string finalText = string.IsNullOrEmpty(node.LocalizationKey) ? node.DefaultText : node.DefaultText; 

        _activeUI.DisplayNode(node.Speaker, node.Emotion, finalText);

        if (node.IsChoiceNode && node.Choices.Count > 0)
        {
            _activeUI.ShowChoices(node.Choices, OnChoiceSelected);
        }
    }

    private void NextNode()
    {
        _currentNodeIndex++;
        if (_currentNodeIndex < _currentDialogue.Nodes.Count) PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
        else EndDialogue();
    }

    private void OnChoiceSelected(ChoiceData choice)
    {
        if (!string.IsNullOrEmpty(choice.SetFlagOnSelect)) GameFlagManager.Instance?.SetFlag(choice.SetFlagOnSelect);

        if (choice.NextDialogue != null) StartDialogue(choice.NextDialogue, _onCompleteCallback);
        else EndDialogue();
    }

    public void EndDialogue()
    {
        _isPlaying = false;
        if (_activeUI != null) _activeUI.ClosePanel(); // 사용했던 패널 닫기
        
        GameStateManager.Instance?.ChangeState(GameState.Exploration); 
        
        _onCompleteCallback?.Invoke();
        _onCompleteCallback = null;
        _activeUI = null;
    }
}