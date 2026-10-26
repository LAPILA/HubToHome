using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>한 열차 Scene의 진행 연결부입니다. 대화·전투·저장 구현을 중복 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed partial class PrologueTutorialController : MonoBehaviour
{
    [SerializeField, Required, LabelText("프롤로그 작성 데이터")]
    private PrologueTutorialDefinition _definition;
    [SerializeField, LabelText("필드 플레이어")]
    private PlayerController _player;
    [SerializeField, LabelText("객차 컨테이너")]
    private RoomContainer _roomContainer;

    private GlobalDataManager _global;
    private GameStateManager _stateOwner;
    private SceneLoader _loader;
    private MapTransitionService _transitions;
    private Coroutine _routine;
    private PrologueTutorialRoutine _flow;
    private int _generation;
    private bool _started;
    private bool _initialized;
    private bool _busy;
    private bool _stepSucceeded;
    private bool _ownsCutscene;
    private GameState _stateBeforeFlow;
    private DialogueManager _dialogueOwner;
    private int _dialogueGeneration;
    private SceneActionSequencePlayer _sequencePlayer;
    private IllustratedStoryPlayer _storyPlayer;
    private PrologueTrainAudioSession _audioSession;
    private MapTransitionRequest _ownedTransition;
    private bool _needsRetryAfterDisable;
    private readonly List<ScenarioDialogueReferenceData> _dialogues = new List<ScenarioDialogueReferenceData>();

    public PrologueTutorialDefinition Definition => _definition;
    public PlayerController FieldPlayer => _player;
    public PrologueTutorialStage Stage => PrologueTutorialProgress.ResolveStage(ReadFlag);
    public bool IsBusy => _busy || _battleRunning;
    public string LastError { get; private set; } = string.Empty;

    public void Configure(PrologueTutorialDefinition definition)
    {
        if (IsBusy) throw new InvalidOperationException("Cannot configure an active prologue tutorial.");
        _definition = definition;
        if (_started && isActiveAndEnabled) Initialize();
    }

    private void Start()
    {
        _started = true;
        Initialize();
    }

    private void OnEnable()
    {
        if (_started) Initialize();
    }

    private void Initialize()
    {
        if (_initialized || _busy || _definition == null) return;
        PrologueTutorialController[] peers = FindObjectsByType<PrologueTutorialController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < peers.Length; i++)
            if (peers[i] != this && peers[i].gameObject.scene == gameObject.scene)
            { Fail("prologue.controller.duplicate"); return; }

        _global = GlobalDataManager.Instance;
        _stateOwner = GameStateManager.Instance;
        _loader = SceneLoader.Instance;
        _transitions = MapTransitionService.Instance;
        if (_global == null || _stateOwner == null || _transitions == null
            || _transitions.gameObject.scene != gameObject.scene)
        { Fail("prologue.dependencies.missing"); return; }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        bool findPlayer = _player == null;
        bool findContainer = _roomContainer == null;
        for (int r = 0; r < roots.Length; r++)
        {
            if (findPlayer)
            {
                PlayerController[] players = roots[r].GetComponentsInChildren<PlayerController>(true);
                for (int i = 0; i < players.Length; i++)
                {
                    if (!players[i].gameObject.activeInHierarchy) continue;
                    if (_player != null) { Fail("prologue.player.ambiguous"); return; }
                    _player = players[i];
                }
            }
            if (findContainer)
            {
                RoomContainer[] containers = roots[r].GetComponentsInChildren<RoomContainer>(true);
                for (int i = 0; i < containers.Length; i++)
                {
                    if (_roomContainer != null) { Fail("prologue.room_container.ambiguous"); return; }
                    _roomContainer = containers[i];
                }
            }
        }
        if (_player == null || _player.gameObject.scene != gameObject.scene || _roomContainer == null
            || _roomContainer.gameObject.scene != gameObject.scene)
        { Fail("prologue.scene.bindings.missing"); return; }
        foreach (PrologueTutorialRoom room in Enum.GetValues(typeof(PrologueTutorialRoom)))
            if (_definition.GetRoom(room) == null || !_definition.GetRoom(room).IsValid)
            { Fail("prologue.room.missing: " + room); return; }

        _dialogues.Clear();
        for (int i = 0; _definition.Dialogues != null && i < _definition.Dialogues.Count; i++)
        {
            PrologueTutorialDialogueBinding binding = _definition.Dialogues[i];
            if (binding != null && binding.Dialogue != null)
                _dialogues.Add(new ScenarioDialogueReferenceData { DialogueId = binding.Id, Dialogue = binding.Dialogue });
        }
        _initialized = true;
        StartFlow(Resume());
    }

    public bool CanInteract(PrologueTutorialInteraction kind)
        => IsAvailable && PrologueTutorialProgress.CanInteract(kind, ReadFlag);

    public bool TryInteract(PrologueTutorialInteraction kind, PlayerController player, string dialogueId = null)
    {
        if (player != _player || !CanInteract(kind)) return false;
        if (kind == PrologueTutorialInteraction.OptionalDialogue && _definition.GetDialogue(dialogueId) == null) return false;
        return StartFlow(Interact(kind, dialogueId));
    }

    public bool CanTravelToRoom(PrologueTutorialRoom room)
    {
        if (!IsAvailable || _definition.GetRoom(room) == null || _roomContainer == null
            || _roomContainer.CurrentDefinition == null || _definition.Rooms == null) return false;
        for (int i = 0; i < _definition.Rooms.Count; i++)
        {
            PrologueTutorialRoomBinding binding = _definition.Rooms[i];
            if (binding != null && binding.Definition == _roomContainer.CurrentDefinition)
                return PrologueTutorialProgress.CanTravelBetween(binding.Room, room, ReadFlag);
        }
        return false;
    }

    public bool TryTravelToRoom(PrologueTutorialRoom room, string spawnPointId, PlayerController player)
    {
        if (player != _player || !CanTravelToRoom(room)) return false;
        return StartFlow(TravelAndContinue(room, spawnPointId));
    }

    private bool IsAvailable => _initialized && isActiveAndEnabled && !IsBusy
        && _global != null && ReferenceEquals(_global, GlobalDataManager.Instance)
        && _stateOwner != null && _stateOwner.CurrentState == GameState.Exploration
        && !GameInput.IsConfigModalActive && !GameLoadCoordinator.IsLoading
        && (_loader == null || !_loader.IsLoading)
        && (_transitions == null || !_transitions.IsTransitioning)
        && (DialogueManager.Instance == null || !DialogueManager.Instance.IsPresentationVisible)
        && (BattleManager.Instance == null || !BattleManager.Instance.IsBattleSessionActive);

    private int ReadFlag(string key) => _global != null ? _global.GetFlag(key, 0) : 0;
    private void SetFact(string key)
    {
        if (ReferenceEquals(_global, GlobalDataManager.Instance) && ReadFlag(key) == 0) _global.SetFlag(key, 1);
    }

    private bool StartFlow(IEnumerator flow)
    {
        if (_busy || !isActiveAndEnabled || flow == null) return false;
        _busy = true;
        LastError = string.Empty;
        int generation = ++_generation;
        _flow = new PrologueTutorialRoutine(flow, () => IsCurrent(generation));
        Coroutine routine = StartCoroutine(DriveFlow(_flow, generation));
        if (_busy && _generation == generation) _routine = routine;
        return true;
    }

    // Unity's nested IEnumerator exception handling does not unwind every parent reliably.
    // This owner drives/disposes its own stack so cancellation and faults share one cleanup path.
    private IEnumerator DriveFlow(PrologueTutorialRoutine flow, int generation)
    {
        try
        {
            while (IsCurrent(generation))
            {
                bool moved = false;
                object next = null;
                Exception failure = null;
                try { moved = flow.MoveNext(); if (moved) next = flow.Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Fail("prologue.flow.failed: " + failure.Message); Debug.LogException(failure, this); break; }
                if (!moved) break;
                yield return next;
            }
        }
        finally
        {
            try { flow.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (generation == _generation)
            {
                CleanupPresentation();
                _flow = null;
                _routine = null;
                _busy = false;
            }
        }
    }

    private bool IsCurrent(int generation) => this != null && isActiveAndEnabled && generation == _generation
        && ReferenceEquals(_global, GlobalDataManager.Instance);

    private void AcquireCutscene()
    {
        if (_ownsCutscene || _stateOwner == null || _stateOwner.CurrentState == GameState.Cutscene) return;
        _stateBeforeFlow = _stateOwner.CurrentState;
        _ownsCutscene = true;
        _stateOwner.ChangeState(GameState.Cutscene);
    }

    private void ReleaseCutscene()
    {
        if (_ownsCutscene && _stateOwner != null && ReferenceEquals(_stateOwner, GameStateManager.Instance)
            && _stateOwner.CurrentState == GameState.Cutscene
            && UnityEngine.SceneManagement.SceneManager.GetActiveScene() == gameObject.scene
            && (_loader == null || !_loader.IsLoading))
            _stateOwner.ChangeState(_stateBeforeFlow);
        _ownsCutscene = false;
    }

    private void CleanupPresentation()
    {
        if (_awaitingFinalSave && SaveManager.HasPendingAutoSave && SaveManager.AutoSaveRequestVersion == _autoSaveVersion)
            SaveManager.CancelPendingAutoSave();
        UnbindAutoSave();
        if (_dialogueOwner != null && _dialogueGeneration != 0)
        {
            _dialogueOwner.CancelDialogue(_dialogueGeneration);
            _dialogueOwner.HideFinishedPresentation(_dialogueGeneration);
        }
        _dialogueOwner = null;
        _dialogueGeneration = 0;
        if (_sequencePlayer != null) _sequencePlayer.Stop("Prologue owner released playback.");
        if (_storyPlayer != null) { _storyPlayer.PageStarted -= OnRearPage; _storyPlayer.Cancel(); }
        _audioSession?.StopEffects();
        ReleaseIntroduction(false, _introRunGeneration);
        ReleaseRoleMotion();
        RestoreUnfinishedRescueRole();
        ReleaseCutscene();
    }

    private void Fail(string reason)
    {
        LastError = reason ?? "prologue.failed";
        _stepSucceeded = false;
        Debug.LogError("[PrologueTutorialController] " + LastError, this);
    }

    private void OnDisable()
    {
        MarkOwnedIntroductionCancellation();
        ++_generation;
        if (_routine != null) StopCoroutine(_routine);
        try { _flow?.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        _routine = null;
        _flow = null;
        _busy = false;
        _needsRetryAfterDisable |= _battleRunning && _battleRetrySnapshot != null;
        UnbindBattle();
        if (_battle != null) _battle.TryAbortSeamlessBattle(this);
        if (_ownedTransition != null && _transitions != null) _transitions.TryCancelRoomTransition(_ownedTransition);
        _ownedTransition = null;
        if (_awaitingFinalSave && SaveManager.HasPendingAutoSave && SaveManager.AutoSaveRequestVersion == _autoSaveVersion)
            SaveManager.CancelPendingAutoSave();
        UnbindAutoSave();
        CleanupPresentation();
        _audioSession?.Stop();
        _audioSession = null;
        _initialized = false;
        _hasPendingOutcome = false;
    }
}
