using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>새 게임의 삽화 프롤로그를 재생하고 암전 중 기존 열차 방으로 진입합니다.</summary>
public class IntroManager : MonoBehaviour
{
    [TitleGroup("프롤로그"), SerializeField, LabelText("삽화와 대본")]
    private IllustratedStoryAsset _openingStory;
    [TitleGroup("열차 음향"), SerializeField, LabelText("음향 프로필")]
    private PrologueTrainAudioProfile _trainAudioProfile;
    [TitleGroup("열차 음향"), SerializeField, Min(0), LabelText("주행음 시작 페이지"), Tooltip("0부터 셉니다. 현재 창가 반복 장면은5번입니다.")]
    private int _trainAudioStartPage = 5;
    [TitleGroup("초반 내레이션 음악"), SerializeField, Min(0), LabelText("음악 종료 페이지"), Tooltip("0부터 셉니다. 현재 '그리고... 현재.' 장면은4번입니다.")]
    private int _openingMusicFadeOutPage = 4;
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
    private AudioManager _openingAudio;
    private AudioClip _openingMusic;
    private BgmPlaybackSnapshot _previousBgm;
    private ulong _openingMusicRequestVersion;
    private bool _openingMusicStarted;
    private bool _ownsOpeningMusic;
    private bool _handingOffAudio;
    private bool _ownsCutsceneState;
    private GameState _previousState;

    private IEnumerator Start()
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        SceneLoader loader = SceneLoader.Instance;
        if (_openingStory == null || global == null || loader == null || DialogueManager.Instance == null)
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
        yield return PrepareOpeningMusic();
        global.PlayerName = GlobalDataManager.DefaultPlayerName;
        _storyPlayer = GetComponent<IllustratedStoryPlayer>();
        if (_storyPlayer == null) _storyPlayer = gameObject.AddComponent<IllustratedStoryPlayer>();
        _trainAudioSession = PrologueTrainAudioSession.GetOrCreate(AudioManager.Instance, _trainAudioProfile, _nextSceneName);
        _storyPlayer.PageStarted += HandleOpeningPageStarted;
        yield return _storyPlayer.Play(_openingStory);
        if (_storyPlayer != null) _storyPlayer.PageStarted -= HandleOpeningPageStarted;
        if (_storyPlayer == null || !_storyPlayer.Succeeded)
        {
            RestoreOpeningMusicIfOwned();
            if (_trainAudioSession != null) _trainAudioSession.Stop();
            ReleaseCutsceneState();
            yield break;
        }

        // 대본의 종료 페이지가 잘못 지정되어도 인트로 음악을 다음 맵으로 넘기지 않습니다.
        FadeOutOpeningMusic();

        string previousScene = global.SpawnScene;
        string previousRoom = global.CurrentRoomId;
        string previousSpawn = global.SpawnPointId;
        bool previousFallback = global.SpawnFallbackAllowed;
        int previousDirection = global.LookingDir;
        int previousFlag = global.GetFlag("prologue.opening.completed", 0);
        global.SpawnScene = _nextSceneName;
        global.CurrentRoomId = _nextRoomId;
        global.SpawnPointId = _nextSpawnPointId;
        global.SpawnFallbackAllowed = false;
        global.LookingDir = 0;
        global.SetFlag("prologue.opening.completed", 1);
        // 주행 소스는 기존 AudioManager 수명에 남겨 열차 씬으로 끊김 없이 넘깁니다.
        _handingOffAudio = true;
        loader.LoadSceneWithResult(_nextSceneName, _nextSceneFadeDuration, result =>
        {
            if (result != SceneLoadResult.Succeeded)
            {
                RestoreOpeningMusicIfOwned();
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
        if (pageIndex == 0) StartOpeningMusic();
        if (pageIndex == _openingMusicFadeOutPage) FadeOutOpeningMusic();
        if (pageIndex == _trainAudioStartPage && _trainAudioSession != null)
            _trainAudioSession.StartRunning();
    }

    private IEnumerator PrepareOpeningMusic()
    {
        AudioClip clip = _trainAudioProfile != null ? _trainAudioProfile.OpeningMusic : null;
        if (clip == null) yield break;
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
        {
            Debug.LogWarning("[IntroManager] 초반 내레이션 음악을 준비하지 못했습니다.", this);
            yield break;
        }
        while (clip != null && clip.loadState == AudioDataLoadState.Loading) yield return null;
    }

    private void StartOpeningMusic()
    {
        if (_openingMusicStarted) return;
        _openingMusicStarted = true;
        AudioManager audio = AudioManager.Instance;
        AudioClip clip = _trainAudioProfile != null ? _trainAudioProfile.OpeningMusic : null;
        if (audio == null || clip == null || clip.loadState != AudioDataLoadState.Loaded) return;

        _openingAudio = audio;
        _openingMusic = clip;
        _previousBgm = audio.CaptureBgmPlayback();
        ulong previousVersion = audio.BgmRequestVersion;
        // Restore는 복원뿐 아니라 시작 샘플/기본 음량/페이드를 가진 명시적 BGM 요청도 처리합니다.
        audio.RestoreBgmPlayback(new BgmPlaybackSnapshot(clip, 0,
            _trainAudioProfile.OpeningMusicVolume, true), _trainAudioProfile.OpeningMusicFadeIn);
        _openingMusicRequestVersion = audio.BgmRequestVersion;
        _ownsOpeningMusic = previousVersion != _openingMusicRequestVersion && audio.RequestedBgmClip == clip;
    }

    private bool HasOwnedOpeningMusicRequest()
        => _ownsOpeningMusic && _openingAudio != null && _openingAudio == AudioManager.Instance
            && _openingAudio.BgmRequestVersion == _openingMusicRequestVersion
            && _openingMusic != null && _openingAudio.RequestedBgmClip == _openingMusic;

    private void FadeOutOpeningMusic()
    {
        if (HasOwnedOpeningMusicRequest())
            _openingAudio.FadeOutBGM(_trainAudioProfile != null ? _trainAudioProfile.OpeningMusicFadeOut : 1.2f);
        ReleaseOpeningMusicOwnership();
    }

    private void RestoreOpeningMusicIfOwned()
    {
        if (HasOwnedOpeningMusicRequest())
            _openingAudio.RestoreBgmPlayback(_previousBgm, 0.3f);
        ReleaseOpeningMusicOwnership();
    }

    private void ReleaseOpeningMusicOwnership()
    {
        _ownsOpeningMusic = false;
        _openingAudio = null;
        _openingMusic = null;
        _openingMusicRequestVersion = 0;
        _previousBgm = BgmPlaybackSnapshot.Stopped;
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
        RestoreOpeningMusicIfOwned();
        if (!_handingOffAudio && _trainAudioSession != null) _trainAudioSession.Stop();
        ReleaseCutsceneState();
    }
}
