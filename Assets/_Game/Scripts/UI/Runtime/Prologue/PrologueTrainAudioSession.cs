using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>AudioManager 아래에서 프롤로그 전용 음원만 소유합니다. 공용 BGM/환경음/효과음 소스는 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PrologueTrainAudioSession : MonoBehaviour
{
    private PrologueTrainAudioProfile _profile;
    private AudioSource _trainSource;
    private AudioSource _effectSource;
    private Tween _trainFade;
    private Tween _effectFade;
    private string _openingSceneName;
    private string _trainSceneName;
    private float _trainFadeElapsed;
    private float _trainFadeDuration;
    private float _effectElapsed;
    private float _effectDuration;
    private float _effectFadeDelay;
    private bool _ready;
    private bool _subscribed;
    private bool _ending;
    private bool _runningRequested;
    private bool _stopTrainAfterFade;
    private bool _walking;
    private bool _effectPaused;

    public static PrologueTrainAudioSession GetOrCreate(
        AudioManager manager, PrologueTrainAudioProfile profile, string trainSceneName)
    {
        if (manager == null || profile == null) return null;

        PrologueTrainAudioSession session = manager.GetComponentInChildren<PrologueTrainAudioSession>(true);
        if (session == null || session._ending)
        {
            var owner = new GameObject("Prologue Train Audio");
            owner.transform.SetParent(manager.transform, false);
            session = owner.AddComponent<PrologueTrainAudioSession>();
        }

        // Intro → 열차에서 다시 요청해도 재생 위치와 진행 중인 페이드를 초기화하지 않습니다.
        if (session._profile != profile)
            session.Configure(manager, profile, trainSceneName);
        return session;
    }

    private void Configure(AudioManager manager, PrologueTrainAudioProfile profile, string trainSceneName)
    {
        Stop();
        _profile = profile;
        _openingSceneName = SceneManager.GetActiveScene().name;
        _trainSceneName = string.IsNullOrWhiteSpace(trainSceneName) ? string.Empty : trainSceneName.Trim();
        if (!_subscribed)
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _subscribed = true;
        }

        AudioSource template = manager.SfxSource;
        AudioMixerGroup mixer = template != null ? template.outputAudioMixerGroup : null;
        _ready = mixer != null;
        if (!_ready)
        {
            Debug.LogWarning("[PrologueTrainAudioSession] SFX 믹서 연결이 없어 프롤로그 음향을 재생하지 않습니다.", this);
            return;
        }

        if (_trainSource == null) _trainSource = CreateSource("Train Loop", mixer, true);
        else _trainSource.outputAudioMixerGroup = mixer;
        if (_effectSource == null) _effectSource = CreateSource("Sequence SFX", mixer, false);
        else _effectSource.outputAudioMixerGroup = mixer;
        PrepareClip(profile.TrainLoop);
        PrepareClip(profile.PowerDown);
        PrepareClip(profile.Footsteps);
    }

    private AudioSource CreateSource(string sourceName, AudioMixerGroup mixer, bool loop)
    {
        var owner = new GameObject(sourceName);
        owner.transform.SetParent(transform, false);
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
        source.volume = 0f;
        source.outputAudioMixerGroup = mixer;
        return source;
    }

    private void PrepareClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            Debug.LogWarning("[PrologueTrainAudioSession] 음원을 준비하지 못했습니다: " + clip.name, this);
    }

    public void StartRunning()
    {
        if (!_ready || _profile == null || _trainSource == null || _profile.TrainLoop == null) return;
        KillTween(ref _trainFade);
        _stopTrainAfterFade = false;
        _runningRequested = true;
        TryStartRunning();
    }

    private void TryStartRunning()
    {
        AudioClip clip = _profile.TrainLoop;
        if (clip.loadState != AudioDataLoadState.Loaded) return;
        _runningRequested = false;
        if (_trainSource.clip != clip || !_trainSource.isPlaying)
        {
            _trainSource.Stop();
            _trainSource.clip = clip;
            _trainSource.volume = 0f;
            _trainSource.Play();
        }
        BeginTrainFade(Unit(_profile.TrainVolume), Duration(_profile.TrainFadeIn), false);
    }

    public void PlayPowerDown()
    {
        if (!_ready || _profile == null || !CanPlayEffect(_profile.PowerDown)) return;
        StopEffect();
        _effectSource.clip = _profile.PowerDown;
        _effectSource.volume = Unit(_profile.PowerDownVolume);
        _effectSource.Play();
        ApplyEffectPause(IsPresentationPaused);
    }

    public void BeginRearWalk(float duration)
    {
        if (!_ready || _profile == null) return;
        _runningRequested = false;
        BeginTrainFade(0f, Duration(_profile.TrainFadeOut), true);
        StopEffect();
        if (!CanPlayEffect(_profile.Footsteps)) return;

        _walking = true;
        _effectSource.clip = _profile.Footsteps;
        _effectSource.volume = Unit(_profile.FootstepsVolume);
        _effectSource.Play();
        _effectElapsed = 0f;
        _effectDuration = Duration(duration);
        float fade = Mathf.Min(_effectDuration, Duration(_profile.FootstepsFadeOut));
        _effectFadeDelay = _effectDuration - fade;
        if (fade > 0f)
            _effectFade = CreateFade(GetEffectVolume, SetEffectVolume, 0f, fade);
        ApplyEffectPause(IsPresentationPaused);
        if (_effectDuration <= 0f) StopEffect();
    }

    public void EndRearWalk()
    {
        if (!_walking || _effectSource == null) return;
        KillTween(ref _effectFade);
        _effectElapsed = 0f;
        _effectFadeDelay = 0f;
        _effectDuration = _profile != null ? Duration(_profile.FootstepsFadeOut) : 0f;
        if (_effectDuration <= 0f) StopEffect();
        else _effectFade = CreateFade(GetEffectVolume, SetEffectVolume, 0f, _effectDuration);
    }

    private bool CanPlayEffect(AudioClip clip)
    {
        if (_effectSource != null && clip != null && clip.loadState == AudioDataLoadState.Loaded) return true;
        Debug.LogWarning("[PrologueTrainAudioSession] 연출 음원 또는 준비된 SFX 소스가 없어 재생을 건너뜁니다.", this);
        return false;
    }

    private void BeginTrainFade(float volume, float duration, bool stopAfterFade)
    {
        KillTween(ref _trainFade);
        _stopTrainAfterFade = stopAfterFade;
        _trainFadeElapsed = 0f;
        _trainFadeDuration = duration;
        if (_trainSource == null) return;
        if (duration <= 0f)
        {
            SetTrainVolume(volume);
            if (stopAfterFade) StopSource(_trainSource);
            return;
        }
        _trainFade = CreateFade(GetTrainVolume, SetTrainVolume, volume, duration);
    }

    private Tween CreateFade(DG.Tweening.Core.DOGetter<float> getter,
        DG.Tweening.Core.DOSetter<float> setter, float target, float duration)
        => DOTween.To(getter, setter, target, duration).SetEase(Ease.Linear)
            .SetTarget(this).SetRecyclable(false).SetAutoKill(false).Pause();

    private void Update()
    {
        if (!_ready || _ending) return;
        if (_trainSource == null || _effectSource == null) { Stop(); _ready = false; return; }
        if (_runningRequested && _profile != null && _profile.TrainLoop != null) TryStartRunning();

        bool paused = IsPresentationPaused;
        ApplyEffectPause(paused);
        if (paused) return;
        float delta = Time.unscaledDeltaTime;
        if (_trainFade != null && _trainFade.IsActive())
        {
            _trainFadeElapsed = Mathf.Min(_trainFadeDuration, _trainFadeElapsed + delta);
            _trainFade.Goto(_trainFadeElapsed, false);
            if (_trainFadeElapsed >= _trainFadeDuration)
            {
                KillTween(ref _trainFade);
                if (_stopTrainAfterFade) StopSource(_trainSource);
            }
        }
        if (!_walking) return;
        _effectElapsed = Mathf.Min(_effectDuration, _effectElapsed + delta);
        if (_effectFade != null && _effectFade.IsActive() && _effectElapsed >= _effectFadeDelay)
            _effectFade.Goto(_effectElapsed - _effectFadeDelay, false);
        if (_effectElapsed >= _effectDuration) StopEffect();
    }

    private static bool IsPresentationPaused => GameInput.IsConfigModalActive
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused);

    private void ApplyEffectPause(bool paused)
    {
        if (_effectSource == null) return;
        if (paused && !_effectPaused && _effectSource.isPlaying)
        {
            _effectSource.Pause();
            _effectPaused = true;
        }
        else if (!paused && _effectPaused)
        {
            _effectSource.UnPause();
            _effectPaused = false;
        }
    }

    private float GetTrainVolume() => _trainSource != null ? _trainSource.volume : 0f;
    private void SetTrainVolume(float value) { if (_trainSource != null) _trainSource.volume = value; }
    private float GetEffectVolume() => _effectSource != null ? _effectSource.volume : 0f;
    private void SetEffectVolume(float value) { if (_effectSource != null) _effectSource.volume = value; }

    public void Stop()
    {
        _runningRequested = false;
        _stopTrainAfterFade = false;
        KillTween(ref _trainFade);
        StopSource(_trainSource);
        StopEffect();
    }

    /// <summary>사고/발걸음만 정리하고 주행 반복음과 주행 페이드는 유지합니다.</summary>
    public void StopEffects() => StopEffect();

    private void StopEffect()
    {
        _walking = false;
        _effectPaused = false;
        _effectElapsed = 0f;
        _effectDuration = 0f;
        _effectFadeDelay = 0f;
        KillTween(ref _effectFade);
        StopSource(_effectSource);
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private static void KillTween(ref Tween tween)
    {
        if (tween != null && tween.IsActive()) tween.Kill(false);
        tween = null;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (next.name == _openingSceneName || next.name == _trainSceneName) return;
        _ending = true;
        Stop();
        Destroy(gameObject);
    }

    private static float Duration(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    private static float Unit(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

    private void OnDisable() => Stop();
    private void OnDestroy()
    {
        if (_subscribed) SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        _subscribed = false;
        Stop();
    }
}
