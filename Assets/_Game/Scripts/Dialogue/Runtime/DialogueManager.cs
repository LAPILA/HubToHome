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

    private DialogueUI _activeUI;
    private DialogueData _currentDialogue;
    private int _currentNodeIndex;
    private bool _isPlaying = false;
    private Action _onCompleteCallback;
    private Action _onCancelledCallback;
    private DialogueEncounterContext _encounterContext;
    private GameState _stateBeforeDialogue = GameState.Exploration;
    private bool _ownsDialogueState;
    private int _playbackGeneration;
    private int _playbackStartFrame = -1;
    private List<ChoiceData> _promptChoices;
    private Action<int> _promptChoiceCallback;
    private bool _skipRequested;
    private int _skipGeneration;
    private const int MaxSkippedNodesPerFrame = 64;

    public bool IsPlaying
    {
        get { return _isPlaying; }
    }

    public int PlaybackGeneration => _playbackGeneration;
    public bool IsWaitingForChoice => _isPlaying && _activeUI != null && _activeUI.IsWaitingForChoice;

    // 논리적인 대화 완료 이후에도 닫기 연출이 끝날 때까지 표시 중입니다.
    public bool IsPresentationVisible => _isPlaying
        || (_overworldPanel != null && _overworldPanel.isActiveAndEnabled)
        || (_cinematicPanel != null && _cinematicPanel.isActiveAndEnabled);

    public void HideFinishedPresentation(int playbackGeneration)
    {
        if (_isPlaying || playbackGeneration != _playbackGeneration)
            return;
        _overworldPanel?.HideImmediate();
        _cinematicPanel?.HideImmediate();
    }

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

        HideFinishedPresentation(_playbackGeneration);
        _isPlaying = true;
        _currentDialogue = null;
        _currentNodeIndex = 0;
        _onCompleteCallback = null;
        _onCancelledCallback = onCancelled;
        _encounterContext = null;
        _activeUI = _overworldPanel;
        _promptChoiceCallback = onSelected;
        _skipRequested = false;
        _playbackGeneration++;
        _playbackStartFrame = Time.frameCount;

        AcquireDialogueState();
        _activeUI.ConfigureTypingStyle(DialogueStyle.Overworld);
        _activeUI.RebindCanvasCameraImmediate();
        _activeUI.OpenPanel();
        _activeUI.DisplayPrompt(prompt);
        _activeUI.ShowChoices(_promptChoices, OnPromptChoiceSelected, allowShortcuts: false);
        return true;
    }

    public bool TryStartDialogue(
        DialogueData data,
        Action onComplete,
        Action onCancelled,
        DialogueEncounterContext encounterContext,
        out int playbackGeneration)
        => TryStartDialogueInternal(data, null, onComplete, onCancelled, encounterContext, out playbackGeneration);

    /// <summary>삽화 연출 전용 복제 패널. 공용 전투/필드 패널의 배치는 변경하지 않습니다.</summary>
    public DialogueUI CreateNarrationPanel(Transform parent)
        => _cinematicPanel != null && parent != null ? _cinematicPanel.CreateNarrationCopy(parent) : null;

    public bool TryStartDialogueOnPanel(
        DialogueData data,
        DialogueUI panel,
        Action onComplete,
        Action onCancelled,
        out int playbackGeneration)
    {
        if (panel == null)
        {
            playbackGeneration = 0;
            return false;
        }
        return TryStartDialogueInternal(data, panel, onComplete, onCancelled, null, out playbackGeneration);
    }

    private bool TryStartDialogueInternal(
        DialogueData data,
        DialogueUI presentation,
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

        bool inBattleHud = BattleUIController.Instance != null && BattleUIController.Instance.CanHostMainDialogue;
        DialogueUI activeUI = presentation != null ? presentation
            : inBattleHud && _overworldPanel != null ? _overworldPanel
            : data.Style == DialogueStyle.Cinematic
            ? _cinematicPanel
            : _overworldPanel;
        if (activeUI == null)
        {
            LogDialogueConsole($"StartDialogue failed: active UI missing data={GetDialogueName(data)} style={data.Style}");
            return false;
        }

        HideFinishedPresentation(_playbackGeneration);
        playbackGeneration = ++_playbackGeneration;
        _playbackStartFrame = Time.frameCount;
        _isPlaying = true;
        _currentDialogue = data;
        _currentNodeIndex = 0;
        _onCompleteCallback = onComplete;
        _onCancelledCallback = onCancelled;
        _encounterContext = encounterContext;
        _activeUI = activeUI;
        _skipRequested = false;

        AcquireDialogueState();
        LogDialogueConsole($"StartDialogue started data={GetDialogueName(data)} style={data.Style} nodes={data.Nodes.Count}");

        _activeUI.ConfigureTypingStyle(data.Style, _stateBeforeDialogue == GameState.Cutscene);
        _activeUI.RebindCanvasCameraImmediate();
        _activeUI.OpenPanel();
        PlayNode(_currentDialogue.Nodes[_currentNodeIndex]);
        return true;
    }

    private void Update()
    {
        // 저장/불러오기 같은 단독 확인창만 X로 닫습니다. 스토리 선택지는 기존 규칙을 유지합니다.
        if (_isPlaying && _promptChoices != null && Time.frameCount > _playbackStartFrame
            && GameInput.UICancelPressed
            && (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Paused))
        {
            FinishDialogue(completed: false);
            return;
        }
        if (!_isPlaying || _activeUI == null || _playbackStartFrame == Time.frameCount
            || _activeUI.IsWaitingForChoice
            || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused)) return;

        if (_skipRequested)
        {
            AdvanceSkippedDialogue();
            return;
        }
        if (GameInput.UICancelPressed && TrySkipRemainingDialogue()) return;

        bool isConfirmPressed = GameInput.DialogueAdvancePressed;

        if (isConfirmPressed)
        {
            if (_activeUI.IsTyping) _activeUI.SkipTyping(); 
            else if (!_activeUI.TryAdvancePage()) NextNode();
        }
    }

    /// <summary>이미 확정된 선형 구간만 빠르게 진행합니다. 선택지는 반드시 사용자에게 남깁니다.</summary>
    public bool TrySkipRemainingDialogue()
    {
        if (!_isPlaying || _activeUI == null || _activeUI.IsWaitingForChoice
            || _promptChoices != null || _currentDialogue == null) return false;
        _skipRequested = true;
        _skipGeneration = _playbackGeneration;
        GameInput.SuppressMenuInputsForCurrentFrame();
        GameInput.ConsumeBattleUIInput();
        AdvanceSkippedDialogue();
        return true;
    }

    private void AdvanceSkippedDialogue()
    {
        int generation = _skipGeneration;
        DialogueData source = _currentDialogue;
        // 긴 대본도 한 프레임에 무제한 이벤트를 실행하지 않습니다. 다음 프레임에 같은 소유자가 이어갑니다.
        for (int processed = 0; processed < MaxSkippedNodesPerFrame; processed++)
        {
            if (!_skipRequested || !OwnsPlayback(generation, source)) return;
            int index = ++_currentNodeIndex;
            if (source.Nodes == null || index >= source.Nodes.Count)
            { _skipRequested = false; EndDialogue(); return; }
            DialogueNode node = source.Nodes[index];
            if (node == null)
            { _skipRequested = false; FinishDialogue(completed: false); return; }
            if (DialogueSkipPolicy.RequiresInput(node))
            {
                _skipRequested = false;
                PlayNode(node);
                return;
            }
            // 이름 입력을 요구하던 이전 대본은 이벤트도 발생시키지 않고 통과합니다.
            if (node.EventTriggerID == "RequestName") continue;
            if (!InvokeNodeEvent(node.EventTriggerID, generation, source, index))
            {
                // 콜백이 연 새 대화의 상태를 이전 skip 요청으로 덮지 않습니다.
                if (generation == _playbackGeneration) _skipRequested = false;
                return;
            }
        }
    }

    private bool OwnsPlayback(int generation, DialogueData source)
        => _isPlaying && _playbackGeneration == generation && _currentDialogue == source;

    private bool InvokeNodeEvent(string eventId, int generation, DialogueData source, int index)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            try { EventManager.Trigger(eventId); }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (OwnsPlayback(generation, source)) FinishDialogue(completed: false);
                return false;
            }
        }
        if (OwnsPlayback(generation, source) && _activeUI == null)
        {
            FinishDialogue(completed: false);
            return false;
        }
        return OwnsPlayback(generation, source) && _currentNodeIndex == index;
    }

    private void PlayNode(DialogueNode node)
    {
        if (node == null)
        {
            LogDialogueConsole($"null DialogueNode를 만나 대화를 종료합니다. Data={GetDialogueName(_currentDialogue)} Index={_currentNodeIndex}");
            EndDialogue();
            return;
        }

        // 선택지 확정 콜백이 Update 도중 다음 노드를 열어도 같은 Z로 타이핑을 건너뛰지 않습니다.
        _playbackStartFrame = Time.frameCount;
        LogDialogueConsole($"PlayNode data={GetDialogueName(_currentDialogue)} index={_currentNodeIndex} speaker={node.Speaker} key={node.LocalizationKey} event={node.EventTriggerID}");

        // 기존 대본과의 호환성만 유지합니다. 주인공 이름은 캐릭터 데이터의 고정 이름입니다.
        if (node.EventTriggerID == "RequestName")
        {
            NextNode();
            return;
        }

        if (!InvokeNodeEvent(node.EventTriggerID, _playbackGeneration, _currentDialogue, _currentNodeIndex)) return;

        string rawText = (LocalizationManager.Instance != null) ? LocalizationManager.Instance.GetText(node.LocalizationKey, node.DefaultText) : node.DefaultText;
        string playerName = (GlobalDataManager.Instance != null) ? GlobalDataManager.Instance.PlayerName : GlobalDataManager.DefaultPlayerName;
        
        string finalText = rawText;
        try { finalText = string.Format(rawText, playerName); } catch { finalText = rawText; }

        // 선택지가 다른 대본으로 분기해도 같은 패널에 이전 속도를 남기지 않습니다.
        _activeUI.ConfigureTypingStyle(_currentDialogue.Style, _stateBeforeDialogue == GameState.Cutscene);
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
            FinishDialogue(completed: false);
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
        bool allowEscape = encounterContext == null || encounterContext.AllowEscape;
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
            battleScenarioData,
            false,
            allowEscape,
            encounterContext == null || encounterContext.PlayEntryPresentation);
    }

    private void NextNode()
    {
        _currentNodeIndex++;
        // 연속된 레거시 이름 노드도 재귀 호출 없이 넘깁니다.
        while (_currentNodeIndex < _currentDialogue.Nodes.Count
            && _currentDialogue.Nodes[_currentNodeIndex]?.EventTriggerID == "RequestName")
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

    // 전투 UI가 사라졌을 때 그 패널을 사용하던 재생만 중단합니다.
    public void CancelPresentation(DialogueUI panel)
    {
        if (_isPlaying && _activeUI == panel) FinishDialogue(completed: false);
    }

    private void FinishDialogue(bool completed)
    {
        if (!_isPlaying)
            return;

        LogDialogueConsole($"FinishDialogue completed={completed} data={GetDialogueName(_currentDialogue)} nodeIndex={_currentNodeIndex}");

        Action callback = completed ? _onCompleteCallback : _onCancelledCallback;
        _isPlaying = false;
        _skipRequested = false;
        _onCompleteCallback = null;
        _onCancelledCallback = null;
        _encounterContext = null;
        _currentDialogue = null;
        _currentNodeIndex = 0;
        _promptChoices = null;
        _promptChoiceCallback = null;

        if (_activeUI != null)
        {
            if (completed)
                _activeUI.ClosePanel();
            else
                _activeUI.HideImmediate();
        }
        _activeUI = null;

        GameInput.SuppressPlayerConfirmForCurrentFrame();
        GameInput.SuppressMenuInputsForCurrentFrame();
        GameInput.ConsumeBattleUIInput();
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
