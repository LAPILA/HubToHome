using System;
using System.Collections;
using System.Collections.Generic;
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
    private Action _onCancelledCallback;
    private DialogueEncounterContext _encounterContext;
    private GameState _stateBeforeDialogue = GameState.Exploration;
    private bool _ownsDialogueState;
    private int _playbackGeneration;
    private List<ChoiceData> _promptChoices;
    private Action<int> _promptChoiceCallback;

    public bool IsPlaying
    {
        get { return _isPlaying; }
    }

    public int PlaybackGeneration => _playbackGeneration;

    private void Awake() 
    { 
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; 
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueData data, Action onComplete = null, DialogueEncounterContext encounterContext = null)
    {
        if (_isPlaying)
        {
            LogDialogueConsole($"StartDialogue ignored: already playing current={GetDialogueName(_currentDialogue)} requested={GetDialogueName(data)}");
            return;
        }

        if (!TryStartDialogue(data, onComplete, null, encounterContext, out _))
            onComplete?.Invoke();
    }

    public bool TryStartChoicePrompt(
        string prompt,
        IReadOnlyList<string> choiceLabels,
        Action<int> onSelected,
        Action onCancelled = null)
    {
        if (_isPlaying || _overworldPanel == null || choiceLabels == null || choiceLabels.Count == 0)
            return false;

        _promptChoices = new List<ChoiceData>(choiceLabels.Count);
        for (int i = 0; i < choiceLabels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(choiceLabels[i]))
            {
                _promptChoices = null;
                return false;
            }

            _promptChoices.Add(new ChoiceData { ChoiceText = choiceLabels[i].Trim() });
        }

        _isPlaying = true;
        _currentDialogue = null;
        _currentNodeIndex = 0;
        _onCompleteCallback = null;
        _onCancelledCallback = onCancelled;
        _encounterContext = null;
        _activeUI = _overworldPanel;
        _promptChoiceCallback = onSelected;
        _playbackGeneration++;

        AcquireDialogueState();
        _activeUI.RebindCanvasCameraImmediate();
        _activeUI.OpenPanel();
        _activeUI.DisplayPrompt(prompt);
        _activeUI.ShowChoices(_promptChoices, OnPromptChoiceSelected);
        return true;
    }

    public bool TryStartDialogue(
        DialogueData data,
        Action onComplete,
        Action onCancelled,
        DialogueEncounterContext encounterContext,
        out int playbackGeneration)
    {
        playbackGeneration = 0;
        LogDialogueConsole($"StartDialogue requested data={GetDialogueName(data)}");

        if (_isPlaying)
        {
            LogDialogueConsole($"StartDialogue rejected: already playing current={GetDialogueName(_currentDialogue)} requested={GetDialogueName(data)}");
            return false;
        }

        if (!DialoguePlaybackPolicy.TryValidate(data, out string validationError))
        {
            LogDialogueConsole($"StartDialogue rejected: {validationError} data={GetDialogueName(data)}");
            return false;
        }

        DialogueUI activeUI = data.Style == DialogueStyle.Cinematic
            ? _cinematicPanel
            : _overworldPanel;
        if (activeUI == null)
        {
            LogDialogueConsole($"StartDialogue failed: active UI missing data={GetDialogueName(data)} style={data.Style}");
            return false;
        }

        playbackGeneration = ++_playbackGeneration;
        _isPlaying = true;
        _currentDialogue = data;
        _currentNodeIndex = 0;
        _onCompleteCallback = onComplete;
        _onCancelledCallback = onCancelled;
        _encounterContext = encounterContext;
        _activeUI = activeUI;

        AcquireDialogueState();
        LogDialogueConsole($"StartDialogue started data={GetDialogueName(data)} style={data.Style} nodes={data.Nodes.Count}");

        _activeUI.RebindCanvasCameraImmediate();
        _activeUI.OpenPanel();
        PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
        return true;
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
        if (node == null)
        {
            LogDialogueConsole($"null DialogueNode를 만나 대화를 종료합니다. Data={GetDialogueName(_currentDialogue)} Index={_currentNodeIndex}");
            EndDialogue();
            return;
        }

        LogDialogueConsole($"PlayNode data={GetDialogueName(_currentDialogue)} index={_currentNodeIndex} speaker={node.Speaker} key={node.LocalizationKey} event={node.EventTriggerID}");

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
            LogDialogueConsole($"ShowChoices data={GetDialogueName(_currentDialogue)} index={_currentNodeIndex} count={node.Choices.Count}");
            _activeUI.ShowChoices(node.Choices, OnChoiceSelected);
        }
    }

    private void OnChoiceSelected(ChoiceData choice)
    {
        if (choice == null)
        {
            EndDialogue();
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice.SetFlagOnSelect))
            GlobalDataManager.Instance?.SetFlag(choice.SetFlagOnSelect, 1);

        if (choice.StartBattleEncounter)
        {
            LogDialogueConsole($"Choice selected: start battle flag={choice.SetFlagOnSelect}");
            StartCoroutine(CoStartBattleFromChoice(choice));
            return;
        }

        if (choice.NextDialogue != null)
        {
            if (!DialoguePlaybackPolicy.TryValidate(choice.NextDialogue, out string validationError))
            {
                LogDialogueConsole(
                    $"Choice next dialogue rejected: {validationError} data={GetDialogueName(choice.NextDialogue)}");
                EndDialogue();
                return;
            }

            LogDialogueConsole($"Choice selected: next dialogue={GetDialogueName(choice.NextDialogue)} flag={choice.SetFlagOnSelect}");
            _currentDialogue = choice.NextDialogue;
            _currentNodeIndex = 0;
            PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
            return;
        }

        EndDialogue();
    }

    private void OnPromptChoiceSelected(ChoiceData choice)
    {
        int selectedIndex = _promptChoices != null
            ? _promptChoices.IndexOf(choice)
            : -1;
        Action<int> callback = _promptChoiceCallback;

        _promptChoiceCallback = null;
        _promptChoices = null;
        if (selectedIndex < 0)
        {
            FinishDialogue(completed: false);
            return;
        }

        FinishDialogue(completed: true);
        callback?.Invoke(selectedIndex);
    }

    private IEnumerator CoStartBattleFromChoice(ChoiceData choice)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        DialogueEncounterContext encounterContext = _encounterContext;
        EndDialogue();
        yield return null;

        if (player == null)
        {
            Debug.LogWarning("[DialogueManager] PlayerController를 찾지 못해 대화 선택 전투를 시작할 수 없습니다.");
            yield break;
        }

        List<EnemyData> enemies = encounterContext != null && encounterContext.EncounterEnemies != null
            ? new List<EnemyData>(encounterContext.EncounterEnemies)
            : new List<EnemyData>();

        AudioClip battleBgm = encounterContext != null ? encounterContext.OverrideBattleBGM : null;
        bool useDedicatedBattleScene = encounterContext != null && encounterContext.UseDedicatedBattleScene;
        string battleSceneName = encounterContext != null && !string.IsNullOrWhiteSpace(encounterContext.BattleSceneName)
            ? encounterContext.BattleSceneName
            : SceneName.Battle;
        float battleFadeDuration = encounterContext != null && encounterContext.BattleSceneFadeDuration > 0f
            ? encounterContext.BattleSceneFadeDuration
            : 0.08f;
        string encounterId = encounterContext != null ? encounterContext.EncounterIdOverride : null;
        bool defeatsOnVictory = encounterContext != null && encounterContext.DefeatEnemyOnVictory;
        BattleScenarioData battleScenarioData = encounterContext != null ? encounterContext.BattleScenarioData : null;

        BattleEncounterService.StartEncounter(
            player,
            enemies,
            battleBgm,
            useDedicatedBattleScene,
            battleSceneName,
            battleFadeDuration,
            encounterId,
            defeatsOnVictory,
            null,
            battleScenarioData);
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
        int generation = _playbackGeneration;

        _nameInputUI.Open(newName =>
        {
            if (!_isPlaying || generation != _playbackGeneration)
                return;

            if (GlobalDataManager.Instance != null)
                GlobalDataManager.Instance.PlayerName = newName;
            _isNaming = false;
            _activeUI.RebindCanvasCameraImmediate();
            _activeUI.OpenPanel();
            NextNode();
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
        FinishDialogue(completed: true);
    }

    public bool CancelDialogue(int playbackGeneration)
    {
        if (!_isPlaying || playbackGeneration != _playbackGeneration)
            return false;

        FinishDialogue(completed: false);
        return true;
    }

    public void CancelDialogue()
    {
        if (_isPlaying)
            FinishDialogue(completed: false);
    }

    private void FinishDialogue(bool completed)
    {
        if (!_isPlaying)
            return;

        LogDialogueConsole($"FinishDialogue completed={completed} data={GetDialogueName(_currentDialogue)} nodeIndex={_currentNodeIndex}");

        Action callback = completed ? _onCompleteCallback : _onCancelledCallback;
        _isPlaying = false;
        _isNaming = false;
        _onCompleteCallback = null;
        _onCancelledCallback = null;
        _encounterContext = null;
        _currentDialogue = null;
        _currentNodeIndex = 0;
        _promptChoices = null;
        _promptChoiceCallback = null;

        _nameInputUI?.CancelImmediate();
        if (_activeUI != null)
        {
            if (completed)
                _activeUI.ClosePanel();
            else
                _activeUI.HideImmediate();
        }
        _activeUI = null;

        GameInput.SuppressPlayerConfirmForCurrentFrame();
        RestoreDialogueStateIfOwned();
        callback?.Invoke();
    }

    private void AcquireDialogueState()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager == null)
        {
            _ownsDialogueState = false;
            return;
        }

        _stateBeforeDialogue = stateManager.CurrentState;
        _ownsDialogueState = _stateBeforeDialogue != GameState.Dialogue;
        if (_ownsDialogueState)
            stateManager.ChangeState(GameState.Dialogue);
    }

    private void RestoreDialogueStateIfOwned()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (_ownsDialogueState
            && stateManager != null
            && stateManager.CurrentState == GameState.Dialogue)
        {
            stateManager.ChangeState(_stateBeforeDialogue);
        }

        _ownsDialogueState = false;
    }

    private void OnDisable()
    {
        CancelDialogue();
    }

    private void OnDestroy()
    {
        CancelDialogue();
        if (Instance == this)
            Instance = null;
    }

    private static string GetDialogueName(DialogueData data)
    {
        return data != null ? data.name : "null";
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void LogDialogueConsole(string message)
    {
#if UNITY_EDITOR
        FlyingWormConsole3.ConsoleProDebug.LogToFilter($"[DialogueManager] {message}", "Dialogue");
#endif
    }
}
