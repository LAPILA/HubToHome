using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns one scene action sequence execution and its presentation cleanup.
/// </summary>
public sealed class SceneActionSequencePlayer : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField] private ActionSequenceAsset _sequence;
    [SerializeField] private OverworldCinematicStage _cinematicStage;
    [SerializeField] private string _initialShotId = string.Empty;

    [Header("Dialogue References")]
    [SerializeField] private List<ScenarioDialogueReferenceData> _dialogues = new List<ScenarioDialogueReferenceData>();

    private ActionExecutionContext _executionContext;
    private DialogueManagerRunner _dialogueRunner;
    private Coroutine _playRoutine;
    private Action<ActionExecutionResult> _onFinished;
    private int _playbackGeneration;
    private bool _isPlaying;
    private bool _ownsCutsceneState;
    private GameState _stateBeforePlayback = GameState.Exploration;

    public bool IsPlaying => _isPlaying;
    public ActionSequenceAsset Sequence => _sequence;
    public ActionExecutionContext ExecutionContext => _executionContext;

    public void Configure(
        ActionSequenceAsset sequence,
        OverworldCinematicStage cinematicStage,
        string initialShotId,
        IEnumerable<ScenarioDialogueReferenceData> dialogues = null)
    {
        if (_isPlaying)
            throw new InvalidOperationException("Cannot reconfigure a playing scene sequence.");

        _sequence = sequence;
        _cinematicStage = cinematicStage;
        _initialShotId = initialShotId ?? string.Empty;
        _dialogues.Clear();
        if (dialogues != null)
            _dialogues.AddRange(dialogues);
    }

    public bool PrepareForSceneReveal(out string error)
    {
        error = string.Empty;
        if (_cinematicStage == null)
        {
            error = "Cinematic Stage is missing.";
            return false;
        }

        return _cinematicStage.PrepareForSceneReveal(_initialShotId, out error);
    }

    public bool TryValidateConfiguration(out string error)
    {
        if (_sequence == null)
        {
            error = "Action Sequence is missing.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _dialogues.Count; i++)
        {
            ScenarioDialogueReferenceData reference = _dialogues[i];
            if (reference == null)
            {
                error = "Dialogue reference at index " + i + " is null.";
                return false;
            }

            string dialogueId = Normalize(reference.DialogueId);
            if (string.IsNullOrEmpty(dialogueId))
            {
                error = "Dialogue reference at index " + i + " has no ID.";
                return false;
            }

            if (reference.Dialogue == null)
            {
                error = "Dialogue reference is missing DialogueData: " + dialogueId;
                return false;
            }

            if (!ids.Add(dialogueId))
            {
                error = "Duplicate dialogue ID: " + dialogueId;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TryCreateLiveContext(
        ActionSequenceAsset requestedSequence,
        out ActionDirector director,
        out ActionExecutionContext context,
        out string error)
    {
        director = null;
        context = null;
        if (requestedSequence == null || requestedSequence != _sequence)
        {
            error = "Requested sequence is not owned by this player.";
            return false;
        }

        if (!TryBuildExecution(out director, out context, out _, out error))
            return false;

        return true;
    }

    public bool TryPlay(Action<ActionExecutionResult> onFinished = null)
    {
        if (_isPlaying)
            return false;

        if (!TryBuildExecution(
                out ActionDirector director,
                out ActionExecutionContext context,
                out DialogueManagerRunner dialogueRunner,
                out string error))
        {
            Debug.LogWarning("[SceneActionSequencePlayer] " + error, this);
            onFinished?.Invoke(ActionExecutionResult.Failed(error));
            return false;
        }

        int generation = ++_playbackGeneration;
        _executionContext = context;
        _dialogueRunner = dialogueRunner;
        _onFinished = onFinished;
        _isPlaying = true;
        AcquireCutsceneState();

        Coroutine started = StartCoroutine(PlayRoutine(generation, director));
        if (_isPlaying && generation == _playbackGeneration)
            _playRoutine = started;
        return true;
    }

    public void Stop(string reason = "Scene action sequence player was stopped.")
    {
        if (!_isPlaying)
            return;

        int generation = _playbackGeneration;
        ActionExecutionResult result = ActionExecutionResult.Canceled(reason);
        _executionContext?.Handle.Cancel(reason);
        _dialogueRunner?.Cancel();

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        CompletePlayback(generation, result);
    }

    private IEnumerator PlayRoutine(int generation, ActionDirector director)
    {
        IEnumerator routine = director.Play(_sequence, _executionContext);
        while (_isPlaying && generation == _playbackGeneration)
        {
            bool moved;
            object current = null;
            try
            {
                moved = routine.MoveNext();
                if (moved)
                    current = routine.Current;
            }
            catch (Exception exception)
            {
                _executionContext.Handle.Fail("Scene action sequence threw during playback.", exception);
                break;
            }

            if (!moved)
                break;

            yield return current;
        }

        if (!_isPlaying || generation != _playbackGeneration)
            yield break;

        CompletePlayback(generation, _executionContext.Handle.Result);
    }

    private bool TryBuildExecution(
        out ActionDirector director,
        out ActionExecutionContext context,
        out DialogueManagerRunner dialogueRunner,
        out string error)
    {
        director = null;
        context = null;
        dialogueRunner = null;
        if (!TryValidateConfiguration(out error))
            return false;

        if (_dialogues.Count > 0)
        {
            DialogueManager manager = DialogueManager.Instance;
            if (manager == null)
            {
                error = "Dialogue references are configured but DialogueManager is missing.";
                return false;
            }

            dialogueRunner = new DialogueManagerRunner(manager);
            var registry = new ScenarioDialogueRegistry(_dialogues);
            registry.RegisterInto(dialogueRunner);
        }

        director = SceneActionSequenceContextFactory.CreateDirector();
        context = SceneActionSequenceContextFactory.Create(
            _sequence,
            _cinematicStage,
            new ScreenTransitionRunner(),
            dialogueRunner: dialogueRunner);
        error = string.Empty;
        return true;
    }

    private void CompletePlayback(int generation, ActionExecutionResult result)
    {
        if (!_isPlaying || generation != _playbackGeneration)
            return;

        Action<ActionExecutionResult> callback = _onFinished;
        _isPlaying = false;
        _playRoutine = null;
        _onFinished = null;

        _dialogueRunner?.Cancel();
        _dialogueRunner = null;
        _cinematicStage?.Release();
        RestoreCutsceneStateIfOwned();
        _executionContext = null;

        callback?.Invoke(result ?? ActionExecutionResult.Failed("Scene action sequence returned no result."));
    }

    private void AcquireCutsceneState()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager == null)
        {
            _ownsCutsceneState = false;
            return;
        }

        _stateBeforePlayback = stateManager.CurrentState;
        _ownsCutsceneState = _stateBeforePlayback != GameState.Cutscene;
        if (_ownsCutsceneState)
            stateManager.ChangeState(GameState.Cutscene);
    }

    private void RestoreCutsceneStateIfOwned()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (_ownsCutsceneState
            && stateManager != null
            && stateManager.CurrentState == GameState.Cutscene)
        {
            stateManager.ChangeState(_stateBeforePlayback);
        }

        _ownsCutsceneState = false;
    }

    private void OnDisable()
    {
        Stop("Scene action sequence player was disabled.");
    }

    private void OnDestroy()
    {
        Stop("Scene action sequence player was destroyed.");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}