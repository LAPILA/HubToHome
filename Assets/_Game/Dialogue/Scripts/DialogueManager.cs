using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueUI _dialogueUI; 

    private DialogueData _currentDialogue;
    private int _currentNodeIndex;
    private bool _isPlaying = false;
    private Action _onCompleteCallback;

    private void Awake() 
    { 
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; 
        
        // GameBootstrap에서 생성하더라도, 혹시 모를 씬 전환 시 파괴 방지
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

        GameStateManager.Instance?.ChangeState(GameState.Dialogue); // 플레이어 이동 잠금
        _dialogueUI.OpenPanel();
        
        PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
    }

    private void Update()
    {
        if (!_isPlaying) return;
        if (Keyboard.current == null) return;

        bool isConfirmPressed = Keyboard.current.zKey.wasPressedThisFrame ||
                                Keyboard.current.enterKey.wasPressedThisFrame;

        if (isConfirmPressed)
        {
            if (_dialogueUI.IsTyping)
            {
                _dialogueUI.SkipTyping(); // 타이핑 중이면 즉시 출력
            }
            else if (!_dialogueUI.IsWaitingForChoice)
            {
                NextNode(); // 타이핑이 끝나고 선택지 대기중이 아니면 다음 노드로
            }
        }
    }

    private void PlayNode(DialogueNode node)
    {
        if (!string.IsNullOrEmpty(node.EventTriggerID))
            EventManager.Trigger(node.EventTriggerID);

        string finalText = string.IsNullOrEmpty(node.LocalizationKey) ? node.DefaultText : node.DefaultText; // 추후 번역 시스템 연결

        _dialogueUI.DisplayNode(node.Speaker, node.Emotion, finalText);

        if (node.IsChoiceNode && node.Choices.Count > 0)
        {
            _dialogueUI.ShowChoices(node.Choices, OnChoiceSelected);
        }
    }

    private void NextNode()
    {
        _currentNodeIndex++;
        if (_currentNodeIndex < _currentDialogue.Nodes.Count)
        {
            PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    private void OnChoiceSelected(ChoiceData choice)
    {
        if (!string.IsNullOrEmpty(choice.SetFlagOnSelect))
            GameFlagManager.Instance?.SetFlag(choice.SetFlagOnSelect, 1);

        if (choice.NextDialogue != null)
        {
            StartDialogue(choice.NextDialogue, _onCompleteCallback);
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        _isPlaying = false;
        _dialogueUI.ClosePanel();
        GameStateManager.Instance?.ChangeState(GameState.Exploration); 
        
        _onCompleteCallback?.Invoke();
        _onCompleteCallback = null;
    }
}