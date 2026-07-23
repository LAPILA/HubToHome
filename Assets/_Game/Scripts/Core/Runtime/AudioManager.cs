using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

public readonly struct BgmPlaybackSnapshot
{
    public BgmPlaybackSnapshot(
        AudioClip clip,
        int timeSamples,
        float baseVolume,
        bool shouldPlay)
    {
        Clip = clip;
        TimeSamples = Mathf.Max(0, timeSamples);
        BaseVolume = Mathf.Clamp01(baseVolume);
        ShouldPlay = shouldPlay && clip != null;
    }

    public AudioClip Clip { get; }
    public int TimeSamples { get; }
    public float BaseVolume { get; }
    public bool ShouldPlay { get; }
    public bool IsValid => ShouldPlay && Clip != null;

    public static BgmPlaybackSnapshot Stopped =>
        new BgmPlaybackSnapshot(null, 0, 1f, false);
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource PrimaryBgmSource => _activeBGM;
    public AudioSource SecondaryBgmSource => _inactiveBGM;
    public AudioSource SfxSource => _sfxSource;
    public AudioSource VoiceSource => _voiceSource;
    public AudioClip RequestedBgmClip => _requestedBgmClip;
    public bool HasRequestedBgm => _requestedBgmShouldPlay && _requestedBgmClip != null;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _mixer;

    [Header("Sources")]
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _voiceSource;

    private AudioSource _activeBGM;
    private AudioSource _inactiveBGM;
    private AudioListener _audioListener;
    private float _configuredMasterVolume = GameConfigManager.DefaultVolume;
    private float _configuredBgmVolume = GameConfigManager.DefaultVolume;
    private float _configuredSfxVolume = GameConfigManager.DefaultVolume;
    private float _activeBgmBaseVolume = 1f;
    private float _inactiveBgmBaseVolume = 1f;
    private float _activeBgmMix = 1f;
    private float _inactiveBgmMix;
    private float _bgmDuckMultiplier = 1f;
    private AudioClip _requestedBgmClip;
    private float _requestedBgmBaseVolume = 1f;
    private bool _requestedBgmShouldPlay;
    private Coroutine _bgmTransitionRoutine;
    private Tween _bgmDuckTween;

    private const string MixerBGM   = "BGMVolume";
    private const string MixerSFX   = "SFXVolume";
    private const string MixerVoice = "VoiceVolume";
    private const string MixerMaster = "MasterVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeRuntimeState();

        // ConfigManager가 AudioManager보다 먼저 초기화된 경우,
        // 기존 ApplyAll()이 AudioManager 부재로 스킵될 수 있으므로 여기서 현재 볼륨 설정을 즉시 재적용합니다.
        GameConfigManager.EnsureInstance().ApplyAudio();
    }

    private void InitializeRuntimeState()
    {
        _audioListener = GetComponent<AudioListener>();
        if (_audioListener != null)
        {
            // 실제 청취 위치는 씬 카메라/가상 카메라 쪽 리스너가 담당해야 합니다.
            // AudioManager 루트 리스너가 살아 있으면 월드 기반 전투 SFX 체감 볼륨이 왜곡될 수 있습니다.
            _audioListener.enabled = false;
        }

        _activeBGM   = _bgmSourceA;
        _inactiveBGM = _bgmSourceB;
        if (_activeBGM == null || _inactiveBGM == null)
        {
            Debug.LogError(
                "[AudioManager] BGM AudioSource A/B 참조가 필요합니다.",
                this);
        }
    }

    private void OnDestroy()
    {
        CancelBgmTransition();
        _bgmDuckTween?.Kill();
        _bgmDuckTween = null;
        _activeBGM?.DOKill();
        _inactiveBGM?.DOKill();
        _sfxSource?.DOKill();
        _voiceSource?.DOKill();

        if (Instance == this)
            Instance = null;
    }

    public void ApplyConfiguredVolumes(float masterVolume, float bgmVolume, float sfxVolume)
    {
        _configuredMasterVolume = Mathf.Clamp01(masterVolume);
        _configuredBgmVolume = Mathf.Clamp01(bgmVolume);
        _configuredSfxVolume = Mathf.Clamp01(sfxVolume);

        SetMixerVolume(MixerMaster, _configuredMasterVolume);
        SetMixerVolume(MixerBGM, 1f);
        SetMixerVolume(MixerSFX, _configuredSfxVolume);

        ApplyBgmSourceVolumes();
    }

    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        PlayBgmImmediate(clip, volume, 0, false);
    }

    public void CrossFadeBGM(AudioClip clip, float duration = 1f)
    {
        RequestCrossFade(clip, duration, 1f, 0);
    }

    public BgmPlaybackSnapshot CaptureBgmPlayback()
    {
        if (!HasRequestedBgm)
            return BgmPlaybackSnapshot.Stopped;

        AudioSource requestedSource = FindSourceForClip(_requestedBgmClip);
        int timeSamples = GetTimeSamples(requestedSource);
        return new BgmPlaybackSnapshot(
            _requestedBgmClip,
            timeSamples,
            _requestedBgmBaseVolume,
            true);
    }

    public void RestoreBgmPlayback(
        BgmPlaybackSnapshot snapshot,
        float fadeDuration = 0.5f)
    {
        if (!snapshot.IsValid)
        {
            FadeOutBGM(fadeDuration);
            return;
        }

        if (fadeDuration <= 0f)
        {
            PlayBgmImmediate(
                snapshot.Clip,
                snapshot.BaseVolume,
                snapshot.TimeSamples,
                true);
            return;
        }

        RequestCrossFade(
            snapshot.Clip,
            fadeDuration,
            snapshot.BaseVolume,
            snapshot.TimeSamples);
    }

    public void RestartBGM(AudioClip clip, float fadeInDuration = 0.08f)
    {
        if (clip == null || !HasBgmSources())
            return;

        SetRequestedBgm(clip, 1f);
        CancelBgmTransition(clip);
        _activeBgmBaseVolume = 1f;
        _activeBgmMix = 1f;
        _inactiveBgmMix = 0f;

        _activeBGM.DOKill();
        StopSourceImmediate(_inactiveBGM);
        _activeBGM.Stop();
        _activeBGM.clip = clip;
        _activeBGM.loop = true;
        _activeBGM.volume = 0f;
        _activeBGM.Play();
        _activeBGM
            .DOFade(
                GetEffectiveBgmSourceVolume(_activeBgmBaseVolume, _activeBgmMix),
                Mathf.Max(0.01f, fadeInDuration))
            .SetUpdate(true);
    }

    public void FadeOutBGM(float duration = 1f)
    {
        ClearRequestedBgm();
        CancelBgmTransition();
        if (duration <= 0f)
        {
            StopSourceImmediate(_activeBGM);
            StopSourceImmediate(_inactiveBGM);
            _activeBgmMix = 0f;
            _inactiveBgmMix = 0f;
            return;
        }

        FadeOutSource(_activeBGM, duration, true);
        FadeOutSource(_inactiveBGM, duration, true);
    }

    public void StopBGM(float fadeDuration = 0.25f)
    {
        if (fadeDuration <= 0f)
        {
            ClearRequestedBgm();
            CancelBgmTransition();
            StopSourceImmediate(_activeBGM);
            StopSourceImmediate(_inactiveBGM);
            _activeBgmMix = 0f;
            _inactiveBgmMix = 0f;
            return;
        }

        FadeOutBGM(fadeDuration);
    }

    private void FadeOutSource(AudioSource source, float duration, bool clearClip)
    {
        if (source == null) return;

        source.DOKill();
        if (!source.isPlaying)
        {
            source.volume = 0f;
            if (clearClip) source.clip = null;
            return;
        }

        source.DOFade(0f, Mathf.Max(0.01f, duration))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                source.Stop();
                if (clearClip) source.clip = null;
                source.volume = 0f;
            });
    }

    private void PlayBgmImmediate(
        AudioClip clip,
        float baseVolume,
        int timeSamples,
        bool restartAtSample)
    {
        if (clip == null || !HasBgmSources())
            return;

        float normalizedVolume = Mathf.Clamp01(baseVolume);
        SetRequestedBgm(clip, normalizedVolume);
        CancelBgmTransition(clip);

        if (!restartAtSample && _activeBGM.clip == clip && _activeBGM.isPlaying)
        {
            _activeBgmBaseVolume = normalizedVolume;
            _activeBgmMix = 1f;
            _activeBGM.volume = GetEffectiveBgmSourceVolume(
                _activeBgmBaseVolume,
                _activeBgmMix);
            return;
        }

        StopSourceImmediate(_inactiveBGM);
        _inactiveBgmMix = 0f;
        _activeBGM.DOKill();
        _activeBGM.Stop();
        _activeBGM.clip = clip;
        _activeBGM.loop = true;
        TrySetTimeSamples(_activeBGM, timeSamples);
        _activeBgmBaseVolume = normalizedVolume;
        _activeBgmMix = 1f;
        _activeBGM.volume = GetEffectiveBgmSourceVolume(
            _activeBgmBaseVolume,
            _activeBgmMix);
        _activeBGM.Play();
    }

    private void RequestCrossFade(
        AudioClip clip,
        float duration,
        float baseVolume,
        int startTimeSamples)
    {
        if (clip == null || !HasBgmSources())
            return;

        float normalizedVolume = Mathf.Clamp01(baseVolume);
        SetRequestedBgm(clip, normalizedVolume);
        if (duration <= 0f)
        {
            PlayBgmImmediate(clip, normalizedVolume, startTimeSamples, true);
            return;
        }

        if (_bgmTransitionRoutine != null
            && _inactiveBGM.clip == clip
            && _inactiveBGM.isPlaying)
        {
            _inactiveBgmBaseVolume = normalizedVolume;
            return;
        }

        CancelBgmTransition(clip);
        if (_activeBGM.clip == clip && _activeBGM.isPlaying)
        {
            _activeBgmBaseVolume = normalizedVolume;
            _activeBgmMix = 1f;
            _activeBGM.volume = GetEffectiveBgmSourceVolume(
                _activeBgmBaseVolume,
                _activeBgmMix);
            return;
        }

        StopSourceImmediate(_inactiveBGM);
        _inactiveBGM.clip = clip;
        _inactiveBGM.loop = true;
        TrySetTimeSamples(_inactiveBGM, startTimeSamples);
        _inactiveBgmBaseVolume = normalizedVolume;
        _inactiveBgmMix = 0f;
        _inactiveBGM.volume = 0f;
        _inactiveBGM.Play();
        _bgmTransitionRoutine = StartCoroutine(
            CrossFadeRoutine(duration));
    }

    private static void StopSourceImmediate(AudioSource source)
    {
        if (source == null) return;
        source.DOKill();
        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private IEnumerator CrossFadeRoutine(float duration)
    {
        AudioSource outgoing = _activeBGM;
        AudioSource incoming = _inactiveBGM;
        float outgoingBaseVolume = _activeBgmBaseVolume;
        float outgoingStartMix = _activeBgmMix;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            _activeBgmMix = Mathf.Lerp(outgoingStartMix, 0f, t);
            _inactiveBgmMix = t;
            outgoing.volume = GetEffectiveBgmSourceVolume(
                outgoingBaseVolume,
                _activeBgmMix);
            incoming.volume = GetEffectiveBgmSourceVolume(
                _inactiveBgmBaseVolume,
                _inactiveBgmMix);
            yield return null;
        }

        StopSourceImmediate(outgoing);
        _activeBGM = incoming;
        _inactiveBGM = outgoing;
        _activeBgmBaseVolume = _inactiveBgmBaseVolume;
        _inactiveBgmBaseVolume = outgoingBaseVolume;
        _activeBgmMix = 1f;
        _inactiveBgmMix = 0f;
        _activeBGM.volume = GetEffectiveBgmSourceVolume(
            _activeBgmBaseVolume,
            _activeBgmMix);
        _bgmTransitionRoutine = null;
    }

    private void CancelBgmTransition(AudioClip preferredClip = null)
    {
        if (_bgmTransitionRoutine != null)
        {
            StopCoroutine(_bgmTransitionRoutine);
            _bgmTransitionRoutine = null;
        }

        _activeBGM?.DOKill();
        _inactiveBGM?.DOKill();
        if (_activeBGM == null || _inactiveBGM == null)
            return;

        bool promoteInactive = _inactiveBGM.clip != null
            && _inactiveBGM.isPlaying
            && ((preferredClip != null && _inactiveBGM.clip == preferredClip)
                || !_activeBGM.isPlaying
                || (preferredClip == null
                    && _inactiveBGM.volume > _activeBGM.volume));
        if (promoteInactive)
        {
            (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
            (_activeBgmBaseVolume, _inactiveBgmBaseVolume) =
                (_inactiveBgmBaseVolume, _activeBgmBaseVolume);
            (_activeBgmMix, _inactiveBgmMix) =
                (_inactiveBgmMix, _activeBgmMix);
        }

        StopSourceImmediate(_inactiveBGM);
        _inactiveBgmMix = 0f;
    }

    private AudioSource FindSourceForClip(AudioClip clip)
    {
        if (clip == null)
            return null;
        if (_activeBGM != null && _activeBGM.clip == clip)
            return _activeBGM;
        if (_inactiveBGM != null && _inactiveBGM.clip == clip)
            return _inactiveBGM;
        return null;
    }

    private bool HasBgmSources()
    {
        return _activeBGM != null && _inactiveBGM != null;
    }

    private void SetRequestedBgm(AudioClip clip, float baseVolume)
    {
        _requestedBgmClip = clip;
        _requestedBgmBaseVolume = Mathf.Clamp01(baseVolume);
        _requestedBgmShouldPlay = clip != null;
    }

    private void ClearRequestedBgm()
    {
        _requestedBgmClip = null;
        _requestedBgmBaseVolume = 1f;
        _requestedBgmShouldPlay = false;
    }

    private static void TrySetTimeSamples(AudioSource source, int timeSamples)
    {
        if (source == null || source.clip == null || source.clip.samples <= 0)
            return;

        try
        {
            source.timeSamples = Mathf.Clamp(
                timeSamples,
                0,
                source.clip.samples - 1);
        }
        catch (UnityException)
        {
            // 일부 스트리밍 클립은 임의 샘플 탐색을 지원하지 않습니다.
        }
    }

    private static int GetTimeSamples(AudioSource source)
    {
        if (source == null || source.clip == null)
            return 0;

        try
        {
            return Mathf.Max(0, source.timeSamples);
        }
        catch (UnityException)
        {
            return 0;
        }
    }

    public void SeamlessTransitionBGM(AudioClip nextPhaseClip)
    {
        if (nextPhaseClip == null || !HasBgmSources() || _activeBGM.clip == null)
            return;

        CancelBgmTransition();
        int timeSamples = GetTimeSamples(_activeBGM);
        float baseVolume = _activeBgmBaseVolume;
        StopSourceImmediate(_inactiveBGM);
        _inactiveBGM.clip = nextPhaseClip;
        _inactiveBGM.loop = true;
        TrySetTimeSamples(_inactiveBGM, timeSamples);
        _inactiveBgmBaseVolume = baseVolume;
        _inactiveBgmMix = _activeBgmMix;
        _inactiveBGM.volume = GetEffectiveBgmSourceVolume(
            _inactiveBgmBaseVolume,
            _inactiveBgmMix);
        _inactiveBGM.Play();
        StopSourceImmediate(_activeBGM);
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
        (_activeBgmBaseVolume, _inactiveBgmBaseVolume) =
            (_inactiveBgmBaseVolume, _activeBgmBaseVolume);
        (_activeBgmMix, _inactiveBgmMix) =
            (_inactiveBgmMix, _activeBgmMix);
        SetRequestedBgm(nextPhaseClip, _activeBgmBaseVolume);
        ApplyBgmSourceVolumes();
    }

    public void DuckBGM(float targetVolume = 0.3f, float duration = 0.3f)
    {
        TweenBgmDuckMultiplier(targetVolume, duration);
    }

    public void RestoreBGM(float targetVolume = 1f, float duration = 0.3f)
    {
        TweenBgmDuckMultiplier(targetVolume, duration);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null && _sfxSource != null)
            _sfxSource.PlayOneShot(clip, Mathf.Max(0f, volume));
    }

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _voiceSource == null)
            return;

        _voiceSource.pitch = Random.Range(0.95f, 1.05f);
        _voiceSource.PlayOneShot(clip, Mathf.Max(0f, volume));
    }

    public void SetMasterVolume(float normalized) => SetMixerVolume(MixerMaster, normalized);
    public void SetBGMVolume(float normalized) => SetMixerVolume(MixerBGM, normalized);
    public void SetSFXVolume(float normalized) => SetMixerVolume(MixerSFX, normalized);
    public void SetVoiceVolume(float normalized) => SetMixerVolume(MixerVoice, normalized);

    private void TweenBgmDuckMultiplier(float targetVolume, float duration)
    {
        float target = Mathf.Clamp01(targetVolume);
        _bgmDuckTween?.Kill();
        if (duration <= 0f)
        {
            _bgmDuckMultiplier = target;
            ApplyBgmSourceVolumes();
            return;
        }

        _bgmDuckTween = DOTween
            .To(
                () => _bgmDuckMultiplier,
                value =>
                {
                    _bgmDuckMultiplier = value;
                    ApplyBgmSourceVolumes();
                },
                target,
                duration)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => _bgmDuckTween = null);
    }

    private float GetEffectiveBgmSourceVolume(
        float sourceVolume,
        float mix)
    {
        return Mathf.Clamp01(
            sourceVolume
            * Mathf.Clamp01(mix)
            * _bgmDuckMultiplier
            * _configuredBgmVolume
            * GameConfigManager.BgmOutputCompensation);
    }

    private void ApplyBgmSourceVolumes()
    {
        if (_activeBGM != null)
        {
            _activeBGM.volume = _activeBGM.isPlaying
                ? GetEffectiveBgmSourceVolume(
                    _activeBgmBaseVolume,
                    _activeBgmMix)
                : 0f;
        }

        if (_inactiveBGM != null)
        {
            _inactiveBGM.volume = _inactiveBGM.isPlaying
                ? GetEffectiveBgmSourceVolume(
                    _inactiveBgmBaseVolume,
                    _inactiveBgmMix)
                : 0f;
        }
    }

    private void SetMixerVolume(string param, float normalized)
    {
        if (_mixer == null) return;

        float db = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
        _mixer.SetFloat(param, db);
    }
}