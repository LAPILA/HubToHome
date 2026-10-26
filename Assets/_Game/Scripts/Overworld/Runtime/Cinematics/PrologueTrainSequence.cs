using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 임시 열차 프로토타입의 진입 타이머와 후미 도착을 기존 대화/그림 연출에 연결합니다.
/// 방 수명에만 속하며, 실제 광원이나 전역 시간 배율은 변경하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologueTrainSequence : MonoBehaviour
{
    [TitleGroup("임시 사고 시작"), SerializeField, LabelText("3초 후 사고 재생")]
    private bool _enableTemporaryIncident = true;
    [TitleGroup("임시 사고 시작"), SerializeField, Min(0f), LabelText("탐색 대기 시간")]
    private float _temporaryIncidentDelay = 3f;
    [TitleGroup("임시 사고 시작"), SerializeField, LabelText("위젤 혼잣말")]
    private DialogueData _incidentDialogue;

    [TitleGroup("후미 발견"), SerializeField, LabelText("후미 그림 연출")]
    private IllustratedStoryAsset _rearRevealStory;
    [TitleGroup("후미 발견"), SerializeField, LabelText("방 기준 오른쪽 진입 X")]
    private float _rearTriggerLocalX = 6.5f;
    [TitleGroup("열차 음향"), SerializeField, LabelText("음향 프로필")]
    private PrologueTrainAudioProfile _trainAudioProfile;
    [TitleGroup("열차 음향"), SerializeField, Min(0), LabelText("발걸음 페이지"), Tooltip("후미 그림 연출에서0부터 셉니다. 현재 걷기 애니메이션은0번입니다.")]
    private int _rearWalkingPage;

    [TitleGroup("진행 상태"), SerializeField, LabelText("오프닝 완료 플래그")]
    private string _requiredOpeningFlag = "prologue.opening.completed";
    [TitleGroup("진행 상태"), SerializeField, LabelText("사고 완료 플래그")]
    private string _incidentFlag = "prologue.train.incident.completed";
    [TitleGroup("진행 상태"), SerializeField, LabelText("후미 발견 완료 플래그")]
    private string _revealFlag = "prologue.train.reveal.completed";

    private SceneLoader _subscribedLoader;
    private PlayerController _player;
    private DialogueManager _ownedDialogue;
    private int _ownedDialogueGeneration;
    private IllustratedStoryPlayer _storyPlayer;
    private PrologueTrainAudioSession _trainAudioSession;
    private CanvasGroup _dim;
    private Tween _dimTween;
    private Coroutine _routine;
    private bool _sceneRevealed;
    private bool _busy;
    private bool _ownsCutsceneState;
    private bool _wasInRear;
    private GameState _stateBeforePlayback;
    private float _incidentWait;
    private float _rearRetryAfter;
    private int _generation;
    private bool _tutorialOwnsScene;

    private void OnEnable()
    {
        SubscribeSceneLoader();
    }

    private void Start()
    {
        PrologueTutorialController[] tutorials = FindObjectsByType<PrologueTutorialController>(FindObjectsSortMode.None);
        for (int i = 0; i < tutorials.Length; i++)
        {
            if (tutorials[i].gameObject.scene != gameObject.scene || tutorials[i].Definition == null) continue;
            // 같은 Scene의 새 튜토리얼이 타이머·후미·열차 음향을 함께 소유합니다.
            _tutorialOwnsScene = true;
            if (_subscribedLoader != null) _subscribedLoader.SceneRevealCompleted -= HandleSceneRevealed;
            _subscribedLoader = null;
            return;
        }
        SubscribeSceneLoader();
        // RegionEntryCoordinator가 방을 준비한 뒤 한 번만 플레이어를 찾습니다.
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].gameObject.scene == gameObject.scene)
            {
                _player = players[i];
                break;
            }
        }

        // 직접 Play에는 reveal 이벤트가 없습니다. 오프닝 완료 플래그가 별도로 보호합니다.
        if (_subscribedLoader == null || !_subscribedLoader.IsLoading)
            _sceneRevealed = true;

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || !HasFlag(global, _revealFlag))
        {
            _trainAudioSession = PrologueTrainAudioSession.GetOrCreate(AudioManager.Instance, _trainAudioProfile, gameObject.scene.name);
            if (_trainAudioSession != null) _trainAudioSession.StartRunning();
        }
    }

    private void Update()
    {
        if (_tutorialOwnsScene || _busy || !_sceneRevealed || _player == null || !CanExplore())
            return;

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || !HasFlag(global, _requiredOpeningFlag) || HasFlag(global, _revealFlag))
            return;

        if (!HasFlag(global, _incidentFlag))
        {
            if (!_enableTemporaryIncident)
                return;

            _incidentWait += Time.unscaledDeltaTime;
            if (_incidentWait >= Mathf.Max(0f, _temporaryIncidentDelay))
            {
                _incidentWait = 0f;
                BeginPlayback(PlayIncident(++_generation));
            }
            return;
        }

        bool inRear = transform.InverseTransformPoint(_player.transform.position).x >= _rearTriggerLocalX;
        if (!inRear)
        {
            _wasInRear = false;
            return;
        }
        if (_wasInRear || Time.unscaledTime < _rearRetryAfter)
            return;

        _wasInRear = true;
        BeginPlayback(PlayRearReveal(++_generation));
    }

    private static bool CanExplore()
    {
        return GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Exploration
            && !GameInput.IsConfigModalActive
            && (UIManager.Instance == null || !UIManager.Instance.IsAnyPanelOpen)
            && (DialogueManager.Instance == null || !DialogueManager.Instance.IsPresentationVisible)
            && (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading);
    }

    private static bool IsPresentationPaused => GameInput.IsConfigModalActive
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused);

    private void SubscribeSceneLoader()
    {
        SceneLoader loader = SceneLoader.Instance;
        if (_subscribedLoader == loader)
            return;
        if (_subscribedLoader != null)
            _subscribedLoader.SceneRevealCompleted -= HandleSceneRevealed;
        _subscribedLoader = loader;
        if (_subscribedLoader != null)
            _subscribedLoader.SceneRevealCompleted += HandleSceneRevealed;
    }

    private void HandleSceneRevealed(string sceneName)
    {
        if (string.Equals(gameObject.scene.name, sceneName, StringComparison.Ordinal))
            _sceneRevealed = true;
    }

    private void BeginPlayback(IEnumerator playback)
    {
        _busy = true;
        GameStateManager manager = GameStateManager.Instance;
        _stateBeforePlayback = manager.CurrentState;
        _ownsCutsceneState = _stateBeforePlayback != GameState.Cutscene;
        if (_ownsCutsceneState)
            manager.ChangeState(GameState.Cutscene);
        _routine = StartCoroutine(playback);
    }

    private IEnumerator PlayIncident(int generation)
    {
        bool completed = false;
        try
        {
            if (_incidentDialogue == null || DialogueManager.Instance == null)
            {
                Debug.LogWarning("[PrologueTrainSequence] 사고 대화 또는 DialogueManager가 없습니다.", this);
                yield break;
            }

            EnsureDim();
            if (_trainAudioSession != null) _trainAudioSession.PlayPowerDown();
            CameraController camera = CameraController.Instance;
            if (camera != null)
                camera.TryImpulse(new Vector3(1f, 0.6f, 0f), 1.1f, 0.65f, CameraShakeSafety.Cinematic, out _);

            // 접근성 0이면 점멸하지 않고 동일한 정전 밝기로만 전환합니다.
            // 설정을 닫은 뒤에는 바뀐 강도를 다음 보간 시점부터 즉시 사용합니다.
            yield return PlayIncidentFade(0.66f, 0.16f, generation);
            yield return PlayIncidentFade(0.18f, 0.09f, generation);
            yield return PlayIncidentFade(0.75f, 0.10f, generation);
            yield return PlayIncidentFade(0.30f, 0.10f, generation);
            yield return PlayIncidentFade(0.60f, 0.14f, generation);
            yield return PlayIncidentFade(0.42f, 0.18f, generation);
            if (!IsCurrent(generation))
                yield break;

            bool dialogueFinished = false;
            bool dialogueSucceeded = false;
            _ownedDialogue = DialogueManager.Instance;
            if (!_ownedDialogue.TryStartDialogue(_incidentDialogue,
                    () => { dialogueSucceeded = true; dialogueFinished = true; },
                    () => dialogueFinished = true,
                    null,
                    out _ownedDialogueGeneration))
            {
                yield break;
            }

            while (IsCurrent(generation) && !dialogueFinished && _ownedDialogue != null
                && _ownedDialogue.PlaybackGeneration == _ownedDialogueGeneration)
                yield return null;
            while (IsCurrent(generation) && _ownedDialogue != null
                && _ownedDialogue.PlaybackGeneration == _ownedDialogueGeneration
                && _ownedDialogue.IsPresentationVisible)
                yield return null;

            completed = IsCurrent(generation) && dialogueFinished && dialogueSucceeded;
            if (completed)
                SetFlag(_incidentFlag);
        }
        finally
        {
            if (generation == _generation)
            {
                _ownedDialogue = null;
                _ownedDialogueGeneration = 0;
                if (!completed)
                {
                    if (_trainAudioSession != null) _trainAudioSession.StopEffects();
                    ReleaseDim();
                }
                FinishPlayback();
            }
        }
    }

    private IEnumerator PlayRearReveal(int generation)
    {
        bool completed = false;
        try
        {
            if (_rearRevealStory == null)
            {
                Debug.LogWarning("[PrologueTrainSequence] 후미 그림 연출이 연결되지 않았습니다.", this);
                yield break;
            }

            EnsureDim();
            yield return PlayDimTween(_dim.DOFade(1f, 0.45f).SetEase(Ease.InOutSine).SetId(this), generation);
            if (!IsCurrent(generation))
                yield break;

            if (_storyPlayer == null)
            {
                var storyObject = new GameObject("Prologue Rear Story");
                storyObject.transform.SetParent(transform, false);
                _storyPlayer = storyObject.AddComponent<IllustratedStoryPlayer>();
            }

            // 이야기 Canvas(800)가 검정 덮개(700) 위를 그립니다. 전환 중 필드가 새지 않습니다.
            _storyPlayer.PageStarted += HandleRearPageStarted;
            yield return _storyPlayer.Play(_rearRevealStory);
            if (!IsCurrent(generation) || _storyPlayer == null || !_storyPlayer.Succeeded)
                yield break;

            SetFlag(_revealFlag);
            completed = true;
        }
        finally
        {
            if (generation == _generation)
            {
                if (_storyPlayer != null) _storyPlayer.PageStarted -= HandleRearPageStarted;
                if (_trainAudioSession != null) _trainAudioSession.EndRearWalk();
                if (!completed)
                {
                    if (_trainAudioSession != null) _trainAudioSession.StartRunning();
                    // 구역 안에서 취소/실패해도 문턱을 다시 넘을 필요 없이 재시도합니다.
                    // 누락 데이터일 때 매 프레임 재생/로그가 반복되는 것만 제한합니다.
                    _wasInRear = false;
                    _rearRetryAfter = Time.unscaledTime + 1f;
                }
                ReleaseDim();
                FinishPlayback();
            }
        }
    }

    private void HandleRearPageStarted(int pageIndex)
    {
        if (_trainAudioSession == null || _rearRevealStory == null) return;
        if (pageIndex == _rearWalkingPage && pageIndex < _rearRevealStory.Pages.Count)
        {
            IllustratedStoryPage page = _rearRevealStory.Pages[pageIndex];
            _trainAudioSession.BeginRearWalk(Mathf.Max(page.MinimumDuration, page.AnimationDuration));
        }
        else if (pageIndex > _rearWalkingPage)
        {
            _trainAudioSession.EndRearWalk();
        }
    }

    private bool IsCurrent(int generation)
    {
        return this != null && isActiveAndEnabled && generation == _generation;
    }

    private IEnumerator PlayIncidentFade(float authoredAlpha, float duration, int generation)
    {
        if (!IsCurrent(generation) || _dim == null)
            yield break;
        float startAlpha = _dim.alpha;
        Tween tween = DOTween.To(() => 0f,
            progress =>
            {
                if (_dim == null)
                    return;
                float scale = GameConfigManager.Instance != null
                    ? GameConfigManager.Instance.FlashIntensity
                    : GameConfigManager.DefaultFlashIntensity;
                float targetAlpha = Mathf.Lerp(0.42f, authoredAlpha, VisualAccessibilityPolicy.NormalizeScale(scale));
                _dim.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            }, 1f, duration).SetEase(Ease.InOutSine).SetId(this);
        yield return PlayDimTween(tween, generation);
    }

    private IEnumerator PlayDimTween(Tween tween, int generation)
    {
        // DOTween은 그대로 사용하되 소유자가 탐색/설정 시간을 구분해 진행합니다.
        // 자동 Update가 먼저 실행되는 한 프레임조차 pause 중 진행되지 않습니다.
        _dimTween = tween.SetUpdate(true).SetAutoKill(false).Pause();
        float duration = tween.Duration();
        float elapsed = 0f;
        while (IsCurrent(generation) && tween.IsActive() && elapsed < duration)
        {
            if (!IsPresentationPaused)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                tween.Goto(elapsed, false);
            }
            yield return null;
        }
        if (IsCurrent(generation) && tween.IsActive())
            tween.Goto(duration, false);
        tween.Kill(false);
        if (ReferenceEquals(_dimTween, tween))
            _dimTween = null;
    }

    private void EnsureDim()
    {
        if (_dim != null)
            return;

        var root = new GameObject("Prologue Train Darkness", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = 0;
        canvas.sortingOrder = 700;
        _dim = root.GetComponent<CanvasGroup>();
        _dim.alpha = 0f;
        _dim.interactable = false;
        _dim.blocksRaycasts = false;

        var fill = new GameObject("Black", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        Image image = fill.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ReleaseDim()
    {
        _dimTween?.Kill(false);
        _dimTween = null;
        if (_dim == null)
            return;
        // Destroy는 프레임 끝까지 지연되므로 먼저 투명하게 만들어 잔여 암전을 남기지 않습니다.
        _dim.alpha = 0f;
        Destroy(_dim.gameObject);
        _dim = null;
    }

    private void FinishPlayback()
    {
        _busy = false;
        _routine = null;
        GameStateManager manager = GameStateManager.Instance;
        if (_ownsCutsceneState && manager != null && manager.CurrentState == GameState.Cutscene)
            manager.ChangeState(_stateBeforePlayback);
        _ownsCutsceneState = false;
    }

    private static bool HasFlag(GlobalDataManager global, string key)
    {
        return !string.IsNullOrWhiteSpace(key) && global.GetFlag(key, 0) != 0;
    }

    private static void SetFlag(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            GlobalDataManager.Instance?.SetFlag(key, 1);
    }

    private void OnDisable()
    {
        if (_subscribedLoader != null)
            _subscribedLoader.SceneRevealCompleted -= HandleSceneRevealed;
        _subscribedLoader = null;
        _generation++;
        _wasInRear = false;
        _rearRetryAfter = 0f;
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
        if (_ownedDialogue != null && _ownedDialogueGeneration != 0)
        {
            _ownedDialogue.CancelDialogue(_ownedDialogueGeneration);
            _ownedDialogue.HideFinishedPresentation(_ownedDialogueGeneration);
        }
        _ownedDialogue = null;
        _ownedDialogueGeneration = 0;
        if (_storyPlayer != null)
        {
            _storyPlayer.PageStarted -= HandleRearPageStarted;
            _storyPlayer.Cancel();
        }
        if (_trainAudioSession != null) _trainAudioSession.Stop();
        _trainAudioSession = null;
        ReleaseDim();
        FinishPlayback();
    }

    private void OnValidate()
    {
        _temporaryIncidentDelay = Mathf.Max(0f, _temporaryIncidentDelay);
        _rearWalkingPage = Mathf.Max(0, _rearWalkingPage);
        _requiredOpeningFlag = _requiredOpeningFlag?.Trim() ?? string.Empty;
        _incidentFlag = _incidentFlag?.Trim() ?? string.Empty;
        _revealFlag = _revealFlag?.Trim() ?? string.Empty;
    }
}
