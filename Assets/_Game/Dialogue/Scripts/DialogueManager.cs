using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;

/// <summary>
/// 대화 흐름을 총괄하는 싱글톤 매니저.
/// JSON 데이터 로드, 타이핑 프로세스, 선택지 처리, 이벤트 브릿지를 담당합니다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    // ── 이벤트 (UnityEvent — AddListener/RemoveListener 호환) ─
    public UnityEvent                          OnDialogueStarted = new UnityEvent();
    public UnityEvent                          OnDialogueEnded   = new UnityEvent();
    public UnityEvent<DialogueLine>            OnLineStarted     = new UnityEvent<DialogueLine>();
    public UnityEvent<List<DialogueChoice>>    OnChoicesShown    = new UnityEvent<List<DialogueChoice>>();

    // ── 상태 ──────────────────────────────────────────────────
    public bool IsActive { get; private set; } = false;

    // ── 데이터 캐시 ───────────────────────────────────────────
    private readonly Dictionary<string, DialogueSequence> _sequenceCache
        = new Dictionary<string, DialogueSequence>();

    // ── 현재 진행 중인 대화 ───────────────────────────────────
    private DialogueSequence _currentSequence;
    private int              _currentLineIndex;
    private Coroutine        _typingCoroutine;
    private bool             _isTypingComplete = false;

    // ── 캐싱 ──────────────────────────────────────────────────
    private WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── JSON 로드 ─────────────────────────────────────────────
    /// <summary>Resources 폴더의 JSON 파일에서 대화 시퀀스를 로드합니다.</summary>
    public void LoadDialogueFile(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogError($"[DialogueManager] Dialogue file not found: {resourcePath}");
            return;
        }
        LoadDialogueFromAsset(asset);
    }

    /// <summary>TextAsset을 직접 전달하여 대화 시퀀스를 로드합니다. (드래그&드롭 방식)</summary>
    public void LoadDialogueFromAsset(TextAsset asset)
    {
        if (asset == null) return;
        try
        {
            var sequences = JsonConvert.DeserializeObject<List<DialogueSequence>>(asset.text);
            foreach (var seq in sequences)
                _sequenceCache[seq.sequenceID] = seq;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] JSON parse error in '{asset.name}': {e.Message}");
        }
    }

    // ── 대화 시작 ─────────────────────────────────────────────
    public void StartDialogue(string sequenceID)
    {
        if (IsActive) return;

        if (!_sequenceCache.TryGetValue(sequenceID, out _currentSequence))
        {
            Debug.LogWarning($"[DialogueManager] Sequence not found: {sequenceID}");
            return;
        }

        IsActive = true;
        _currentLineIndex = 0;
        OnDialogueStarted?.Invoke();
        AudioManager.Instance?.DuckBGM();
        ShowNextLine();
    }

    // ── 다음 줄 표시 ──────────────────────────────────────────
    private void ShowNextLine()
    {
        if (_currentLineIndex >= _currentSequence.lines.Count)
        {
            EndDialogue();
            return;
        }

        var line = _currentSequence.lines[_currentLineIndex];
        OnLineStarted?.Invoke(line);

        // 명령 처리
        foreach (var cmd in line.commands)
            ProcessCommand(cmd);

        // 타이핑 시작 (DialogueController가 실제 타이핑 처리)
        _isTypingComplete = false;
        _typingCoroutine  = StartCoroutine(WaitForLineComplete(line));
    }

    private IEnumerator WaitForLineComplete(DialogueLine line)
    {
        // DialogueController가 타이핑 완료 시 CompleteTyping()을 호출
        yield return new WaitUntil(() => _isTypingComplete);

        if (line.choices != null && line.choices.Count > 0)
        {
            // 선택지 표시
            OnChoicesShown?.Invoke(line.choices);
        }
        else if (line.autoAdvance)
        {
            yield return new WaitForSeconds(line.autoDelay);
            AdvanceLine();
        }
        // 그 외: 플레이어 입력 대기 (AdvanceLine() 외부 호출)
    }

    /// <summary>타이핑 완료 시 DialogueController가 호출합니다.</summary>
    public void CompleteTyping() => _isTypingComplete = true;

    /// <summary>플레이어가 확인 버튼을 눌렀을 때 호출합니다.</summary>
    public void AdvanceLine()
    {
        _currentLineIndex++;
        ShowNextLine();
    }

    /// <summary>선택지를 선택했을 때 ChoiceHandler가 호출합니다.</summary>
    public void OnChoiceSelected(DialogueChoice choice)
    {
        // 이벤트 실행
        if (!string.IsNullOrEmpty(choice.eventID))
            DialogueEventBridge.Execute(choice.eventID);

        // 다음 대화 ID로 이동
        if (!string.IsNullOrEmpty(choice.nextDialogueID))
        {
            EndDialogue();
            StartDialogue(choice.nextDialogueID);
        }
        else
        {
            AdvanceLine();
        }
    }

    // ── 대화 종료 ─────────────────────────────────────────────
    private void EndDialogue()
    {
        IsActive = false;
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        AudioManager.Instance?.RestoreBGM();
        OnDialogueEnded?.Invoke();
        Debug.Log("[DialogueManager] Dialogue ended.");
    }

    // ── 명령 처리 ─────────────────────────────────────────────
    private void ProcessCommand(string command)
    {
        if (string.IsNullOrEmpty(command)) return;

        // 예: "[bgm:boss_theme]", "[shake]", "[flash]"
        string cmd = command.Trim('[', ']');
        string[] parts = cmd.Split(':');

        switch (parts[0].ToLower())
        {
            case "bgm":
                if (parts.Length > 1)
                    Debug.Log($"[DialogueManager] BGM command: {parts[1]}");
                break;
            case "shake":
                Debug.Log("[DialogueManager] Camera shake command.");
                break;
            case "flash":
                Debug.Log("[DialogueManager] Flash command.");
                break;
            default:
                DialogueEventBridge.Execute(cmd);
                break;
        }
    }
}
