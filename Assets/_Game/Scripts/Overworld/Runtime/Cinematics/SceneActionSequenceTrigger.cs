using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepares a scene sequence below the reveal fade and starts it after reveal.
/// </summary>
public sealed class SceneActionSequenceTrigger : MonoBehaviour, ISceneRevealGate, IActionSequenceLiveContextSource
{
    [Header("시작 시퀀스")]
    [SerializeField] private ActionSequenceAsset _sequence;
    [SerializeField] private OverworldCinematicStage _cinematicStage;
    [SerializeField] private string _initialShotId = string.Empty;
    [SerializeField] private List<ScenarioDialogueReferenceData> _dialogues = new List<ScenarioDialogueReferenceData>();

    [Header("저장 / 상태")]
    [SerializeField] private bool _runOncePerSave = true;
    [SerializeField] private string _completionFlagId = "overworld.intro.subway.completed";
    [SerializeField] private bool _setExplorationWhenFinished = true;

    private SceneActionSequencePlayer _player;
    private bool _isReadyToReveal = true;
    private bool _shouldPlayAfterReveal;
    private bool _hasStarted;

    public bool IsReadyToReveal => _isReadyToReveal;
    public ActionSequenceAsset Sequence => _sequence;
    public int LiveContextPriority => 50;
    public string LiveContextLabel => "Scene Action Sequence Trigger: " + name;

    public bool TryCreateLiveContext(
        BattleScenarioData battle,
        ActionSequenceAsset requestedSequence,
        out ActionDirector director,
        out ActionExecutionContext context,
        out string error)
    {
        director = null;
        context = null;
        if (battle != null || _sequence == null || requestedSequence != _sequence)
        {
            error = string.Empty;
            return false;
        }

        if (!Application.isPlaying)
        {
            error = "Play Mode에서만 씬 시퀀스를 실동작 테스트할 수 있습니다.";
            return false;
        }

        EnsurePlayer();
        return _player.TryCreateLiveContext(requestedSequence, out director, out context, out error);
    }

    private void Awake()
    {
        EnsurePlayer();
        PrepareForReveal();
    }

    private void OnEnable()
    {
        SubscribeSceneLoader();
    }

    private void Start()
    {
        SubscribeSceneLoader();
    }

    private void OnDisable()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.SceneRevealCompleted -= HandleSceneRevealCompleted;

        _player?.Stop("Scene action sequence trigger was disabled.");
    }

    private void EnsurePlayer()
    {
        if (_player == null)
            _player = GetComponent<SceneActionSequencePlayer>();
        if (_player == null)
            _player = gameObject.AddComponent<SceneActionSequencePlayer>();

        if (!_player.IsPlaying)
            _player.Configure(_sequence, _cinematicStage, _initialShotId, _dialogues);
    }

    private void SubscribeSceneLoader()
    {
        if (SceneLoader.Instance == null)
            return;

        SceneLoader.Instance.SceneRevealCompleted -= HandleSceneRevealCompleted;
        SceneLoader.Instance.SceneRevealCompleted += HandleSceneRevealCompleted;
    }

    private void PrepareForReveal()
    {
        _isReadyToReveal = false;
        _shouldPlayAfterReveal = false;

        if (!Application.isPlaying || _sequence == null || HasCompletedForCurrentSave())
        {
            _isReadyToReveal = true;
            return;
        }

        EnsurePlayer();
        if (!_player.PrepareForSceneReveal(out string error))
        {
            Debug.LogWarning("[SceneActionSequenceTrigger] Failed to prepare cinematic stage: " + error, this);
            _cinematicStage?.Release();
            _isReadyToReveal = true;
            return;
        }

        _shouldPlayAfterReveal = true;
        _isReadyToReveal = true;
    }

    private void HandleSceneRevealCompleted(string sceneName)
    {
        if (_hasStarted || !_shouldPlayAfterReveal || !IsOwnScene(sceneName))
            return;

        _hasStarted = true;
        _shouldPlayAfterReveal = false;
        EnsurePlayer();
        if (!_player.TryPlay(HandlePlaybackFinished))
        {
            _hasStarted = false;
            _cinematicStage?.Release();
        }
    }

    private void HandlePlaybackFinished(ActionExecutionResult result)
    {
        if (result != null && result.Status == ActionExecutionStatus.Succeeded)
        {
            MarkCompletedForCurrentSave();
        }
        else
        {
            string message = result != null ? result.Message : "No result was returned.";
            Debug.LogWarning("[SceneActionSequenceTrigger] Scene sequence ended without success: " + message, this);
        }

        if (_setExplorationWhenFinished
            && GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Cutscene)
        {
            GameStateManager.Instance.ChangeState(GameState.Exploration);
        }
    }

    private bool HasCompletedForCurrentSave()
    {
        if (!_runOncePerSave || string.IsNullOrWhiteSpace(_completionFlagId))
            return false;

        return GlobalDataManager.Instance != null
            && GlobalDataManager.Instance.GetFlag(_completionFlagId.Trim()) != 0;
    }

    private void MarkCompletedForCurrentSave()
    {
        if (_runOncePerSave && !string.IsNullOrWhiteSpace(_completionFlagId))
            GlobalDataManager.Instance?.SetFlag(_completionFlagId.Trim(), 1);
    }

    private bool IsOwnScene(string sceneName)
    {
        Scene ownScene = gameObject.scene;
        return ownScene.IsValid()
            && string.Equals(ownScene.name, sceneName, System.StringComparison.Ordinal);
    }
}