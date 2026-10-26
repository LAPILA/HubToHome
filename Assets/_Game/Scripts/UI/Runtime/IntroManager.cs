using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>새 게임의 삽화 프롤로그를 재생하고 암전 중 기존 열차 방으로 진입합니다.</summary>
public class IntroManager : MonoBehaviour
{
    [TitleGroup("프롤로그"), SerializeField, LabelText("삽화와 대본")]
    private IllustratedStoryAsset _openingStory;
    [TitleGroup("프롤로그"), SerializeField, LabelText("교체형 튜토리얼 설정")]
    private PrologueTutorialDefinition _tutorialDefinition;
    [TitleGroup("열차 음향"), SerializeField, LabelText("음향 프로필")]
    private PrologueTrainAudioProfile _trainAudioProfile;
    [TitleGroup("열차 음향"), SerializeField, Min(0), LabelText("주행음 시작 페이지"), Tooltip("0부터 셉니다. 창가 반복 장면에서 시작합니다.")]
    private int _trainAudioStartPage;
    [TitleGroup("열차 도착"), SerializeField, LabelText("다음 씬")]
    private string _nextSceneName = "Region_Train";
    [TitleGroup("열차 도착"), SerializeField, LabelText("방 ID")]
    private string _nextRoomId = "all.trainq0";
    [TitleGroup("열차 도착"), SerializeField, LabelText("도착 지점")]
    private string _nextSpawnPointId = "default";
    [TitleGroup("열차 도착"), SerializeField, Min(0f), LabelText("입장 페이드 시간")]
    private float _nextSceneFadeDuration = 1f;

    private IllustratedStoryPlayer _storyPlayer;
    private PrologueTrainAudioSession _trainAudioSession;
    private bool _handingOffAudio;
    private bool _ownsCutsceneState;
    private GameState _previousState;

    private IEnumerator Start()
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        SceneLoader loader = SceneLoader.Instance;
        IllustratedStoryAsset opening = _tutorialDefinition != null && _tutorialDefinition.OpeningStory != null
            ? _tutorialDefinition.OpeningStory : _openingStory;
        PrologueTrainAudioProfile audio = _tutorialDefinition != null && _tutorialDefinition.TrainAudio != null
            ? _tutorialDefinition.TrainAudio : _trainAudioProfile;
        PrologueTutorialRoomBinding startRoom = _tutorialDefinition != null
            ? _tutorialDefinition.GetRoomBinding(PrologueTutorialRoom.Repair) : null;
        if (opening == null || global == null || loader == null || DialogueManager.Instance == null)
        {
            Debug.LogError("[IntroManager] 프롤로그 데이터 또는 GameBootstrap 연결이 없습니다.", this);
            yield break;
        }
        if (!loader.CanLoadScene(_nextSceneName))
        {
            Debug.LogError("[IntroManager] 열차 씬이 Build Settings에 없습니다: " + _nextSceneName, this);
            yield break;
        }

        AcquireCutsceneState();
        // 직접 Play에서도 모든 MapSettings.Start의 음악 정리가 먼저 끝나게 합니다.
        yield return null;
        // 타이틀의 공개 페이드가 끝난 다음 첫 그림의 표시 시간을 셉니다.
        while (loader != null && loader.IsLoading) yield return null;
        if (loader == null) { ReleaseCutsceneState(); yield break; }
        global.PlayerName = GlobalDataManager.DefaultPlayerName;
        _storyPlayer = GetComponent<IllustratedStoryPlayer>();
        if (_storyPlayer == null) _storyPlayer = gameObject.AddComponent<IllustratedStoryPlayer>();
        _trainAudioSession = PrologueTrainAudioSession.GetOrCreate(AudioManager.Instance, audio, _nextSceneName);
        _storyPlayer.PageStarted += HandleOpeningPageStarted;
        yield return _storyPlayer.Play(opening);
        if (_storyPlayer != null) _storyPlayer.PageStarted -= HandleOpeningPageStarted;
        if (_storyPlayer == null || !_storyPlayer.Succeeded)
        {
            if (_trainAudioSession != null) _trainAudioSession.Stop();
            ReleaseCutsceneState();
            yield break;
        }

        string previousScene = global.SpawnScene;
        string previousRoom = global.CurrentRoomId;
        string previousSpawn = global.SpawnPointId;
        bool previousFallback = global.SpawnFallbackAllowed;
        int previousDirection = global.LookingDir;
        int previousFlag = global.GetFlag("prologue.opening.completed", 0);
        global.SpawnScene = _nextSceneName;
        global.CurrentRoomId = startRoom != null && startRoom.Definition != null
            ? startRoom.Definition.RoomId : _nextRoomId;
        global.SpawnPointId = startRoom != null && !string.IsNullOrWhiteSpace(startRoom.SpawnPointId)
            ? startRoom.SpawnPointId : _nextSpawnPointId;
        global.SpawnFallbackAllowed = false;
        global.LookingDir = 0;
        global.SetFlag("prologue.opening.completed", 1);
        // 주행 소스는 기존 AudioManager 수명에 남겨 열차 씬으로 끊김 없이 넘깁니다.
        _handingOffAudio = true;
        loader.LoadSceneWithResult(_nextSceneName, _nextSceneFadeDuration, result =>
        {
            if (result != SceneLoadResult.Succeeded)
            {
                if (_trainAudioSession != null) _trainAudioSession.Stop();
                _handingOffAudio = false;
            }
            // 도착 실패로 이전 씬에 남았을 때만 준비한 도착 정보를 되돌립니다.
            if (SceneLoadResultUtility.WasDestinationActivated(result)) return;
            if (global == null) return;
            global.SpawnScene = previousScene;
            global.CurrentRoomId = previousRoom;
            global.SpawnPointId = previousSpawn;
            global.SpawnFallbackAllowed = previousFallback;
            global.LookingDir = previousDirection;
            global.SetFlag("prologue.opening.completed", previousFlag);
            if (this != null)
            {
                ReleaseCutsceneState();
                Debug.LogError("[IntroManager] 열차 입장에 실패했습니다: " + result, this);
            }
        });
    }

    private void HandleOpeningPageStarted(int pageIndex)
    {
        if (pageIndex == _trainAudioStartPage && _trainAudioSession != null)
            _trainAudioSession.StartRunning();
    }

    private void AcquireCutsceneState()
    {
        GameStateManager manager = GameStateManager.Instance;
        if (manager == null) return;
        _previousState = manager.CurrentState;
        _ownsCutsceneState = _previousState != GameState.Cutscene;
        if (_ownsCutsceneState) manager.ChangeState(GameState.Cutscene);
    }

    private void ReleaseCutsceneState()
    {
        GameStateManager manager = GameStateManager.Instance;
        if (_ownsCutsceneState && manager != null && manager.CurrentState == GameState.Cutscene)
            manager.ChangeState(_previousState);
        _ownsCutsceneState = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (_storyPlayer != null) _storyPlayer.PageStarted -= HandleOpeningPageStarted;
        if (_storyPlayer != null) _storyPlayer.Cancel();
        if (!_handingOffAudio && _trainAudioSession != null) _trainAudioSession.Stop();
        ReleaseCutsceneState();
    }
}
