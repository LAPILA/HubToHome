using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 페이드 아래에서 Cinematic Stage를 준비하고, 화면 공개 직후 독립 Action Sequence를 재생합니다.
/// 완료 여부는 GlobalDataManager의 event flag에만 기록하므로 전투 런타임 상태와 섞이지 않습니다.
/// </summary>
public sealed class SceneActionSequenceTrigger : MonoBehaviour, ISceneRevealGate, IActionSequenceLiveContextSource
{
    [Header("시작 시퀀스")]
    [SerializeField] private ActionSequenceAsset _sequence;
    [SerializeField] private OverworldCinematicStage _cinematicStage;
    [SerializeField] private string _initialShotId = string.Empty;

    [Header("저장 / 상태")]
    [SerializeField] private bool _runOncePerSave = true;
    [SerializeField] private string _completionFlagId = "overworld.intro.subway.completed";
    [SerializeField] private bool _setExplorationWhenFinished = true;

    private bool _isReadyToReveal = true;
    private bool _shouldPlayAfterReveal;
    private bool _hasStarted;
    private ActionExecutionContext _executionContext;

    public bool IsReadyToReveal
    {
        get { return _isReadyToReveal; }
    }

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
        if (_cinematicStage == null)
        {
            error = "실동작 테스트에 필요한 Cinematic Stage가 없습니다.";
            return false;
        }

        director = SceneActionSequenceContextFactory.CreateDirector();
        context = SceneActionSequenceContextFactory.Create(
            _sequence,
            _cinematicStage,
            new ScreenTransitionRunner());
        error = string.Empty;
        return true;
    }

    private void Awake()
    {
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
        {
            SceneLoader.Instance.SceneRevealCompleted -= HandleSceneRevealCompleted;
        }

        _executionContext?.Handle.Cancel("Scene action sequence trigger was disabled.");
        _cinematicStage?.Release();
    }

    private void SubscribeSceneLoader()
    {
        if (SceneLoader.Instance == null)
        {
            return;
        }

        SceneLoader.Instance.SceneRevealCompleted -= HandleSceneRevealCompleted;
        SceneLoader.Instance.SceneRevealCompleted += HandleSceneRevealCompleted;
    }

    private void PrepareForReveal()
    {
        _isReadyToReveal = false;
        _shouldPlayAfterReveal = false;

        if (!Application.isPlaying || _sequence == null)
        {
            _isReadyToReveal = true;
            return;
        }

        if (HasCompletedForCurrentSave())
        {
            _isReadyToReveal = true;
            return;
        }

        if (_cinematicStage == null)
        {
            Debug.LogWarning("[SceneActionSequenceTrigger] Cinematic Stage reference is missing. Scene will reveal normally.", this);
            _isReadyToReveal = true;
            return;
        }

        string error;
        if (!_cinematicStage.PrepareForSceneReveal(_initialShotId, out error))
        {
            Debug.LogWarning("[SceneActionSequenceTrigger] Failed to prepare cinematic stage: " + error, this);
            _cinematicStage.Release();
            _isReadyToReveal = true;
            return;
        }

        _shouldPlayAfterReveal = true;
        _isReadyToReveal = true;
    }

    private void HandleSceneRevealCompleted(string sceneName)
    {
        if (_hasStarted || !_shouldPlayAfterReveal || !IsOwnScene(sceneName))
        {
            return;
        }

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        _hasStarted = true;
        _shouldPlayAfterReveal = false;

        GameStateManager stateManager = GameStateManager.Instance;
        stateManager?.ChangeState(GameState.Cutscene);

        ActionDirector director = SceneActionSequenceContextFactory.CreateDirector();
        _executionContext = SceneActionSequenceContextFactory.Create(
            _sequence,
            _cinematicStage,
            new ScreenTransitionRunner());

        yield return director.Play(_sequence, _executionContext);

        bool succeeded = _executionContext.Handle.Status == ActionExecutionStatus.Succeeded;
        if (succeeded)
        {
            MarkCompletedForCurrentSave();
        }
        else
        {
            Debug.LogWarning("[SceneActionSequenceTrigger] Scene sequence ended without success: " + _executionContext.Handle.Result.Message, this);
        }

        _cinematicStage?.Release();
        if (_setExplorationWhenFinished && stateManager != null)
        {
            stateManager.ChangeState(GameState.Exploration);
        }

        _executionContext = null;
    }

    private bool HasCompletedForCurrentSave()
    {
        if (!_runOncePerSave || string.IsNullOrWhiteSpace(_completionFlagId))
        {
            return false;
        }

        return GlobalDataManager.Instance != null && GlobalDataManager.Instance.GetFlag(_completionFlagId.Trim()) != 0;
    }

    private void MarkCompletedForCurrentSave()
    {
        if (!_runOncePerSave || string.IsNullOrWhiteSpace(_completionFlagId))
        {
            return;
        }

        GlobalDataManager.Instance?.SetFlag(_completionFlagId.Trim(), 1);
    }

    private bool IsOwnScene(string sceneName)
    {
        Scene ownScene = gameObject.scene;
        return ownScene.IsValid() && string.Equals(ownScene.name, sceneName, System.StringComparison.Ordinal);
    }
}
