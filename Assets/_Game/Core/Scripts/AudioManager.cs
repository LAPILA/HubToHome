using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource PrimaryBgmSource => _activeBGM;
    public AudioSource SecondaryBgmSource => _inactiveBGM;
    public AudioSource SfxSource => _sfxSource;
    public AudioSource VoiceSource => _voiceSource;

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

    private const string MixerBGM   = "BGMVolume";
    private const string MixerSFX   = "SFXVolume";
    private const string MixerVoice = "VoiceVolume";
    private const string MixerMaster = "MasterVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioListener = GetComponent<AudioListener>();
        if (_audioListener != null)
        {
            // 실제 청취 위치는 씬 카메라/가상 카메라 쪽 리스너가 담당해야 합니다.
            // AudioManager 루트 리스너가 살아 있으면 월드 기반 전투 SFX 체감 볼륨이 왜곡될 수 있습니다.
            _audioListener.enabled = false;
        }

        _activeBGM   = _bgmSourceA;
        _inactiveBGM = _bgmSourceB;

        // ConfigManager가 AudioManager보다 먼저 초기화된 경우,
        // 기존 ApplyAll()이 AudioManager 부재로 스킵될 수 있으므로 여기서 현재 볼륨 설정을 즉시 재적용합니다.
        GameConfigManager.EnsureInstance().ApplyAudio();
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
        if (clip == null) return;
        if (_activeBGM.clip == clip && _activeBGM.isPlaying) return; // 이미 같은 곡이면 무시

        _activeBgmBaseVolume = Mathf.Clamp01(volume);
        _activeBGM.DOKill(); 
        _activeBGM.clip   = clip;
        _activeBGM.volume = GetEffectiveBgmSourceVolume(_activeBgmBaseVolume);
        _activeBGM.loop   = true;
        _activeBGM.Play();
    }

    public void CrossFadeBGM(AudioClip clip, float duration = 1f)
    {
        if (clip == null) return;

        if (_activeBGM.clip == clip && _activeBGM.isPlaying)
        {
            _activeBGM.DOKill();
            _activeBGM.volume = GetEffectiveBgmSourceVolume(_activeBgmBaseVolume);
            return;
        }
        
        StartCoroutine(CrossFadeRoutine(clip, duration));
    }

    public void RestartBGM(AudioClip clip, float fadeInDuration = 0.08f)
    {
        if (clip == null || _activeBGM == null) return;

        _activeBgmBaseVolume = 1f;

        _activeBGM.DOKill();
        _inactiveBGM.DOKill();
        StopAllCoroutines();

        _activeBGM.Stop();
        _activeBGM.clip = clip;
        _activeBGM.loop = true;
        _activeBGM.volume = 0f;
        _activeBGM.Play();
        _activeBGM.DOFade(GetEffectiveBgmSourceVolume(_activeBgmBaseVolume), Mathf.Max(0.01f, fadeInDuration)).SetUpdate(true);
    }

    public void FadeOutBGM(float duration = 1f)
    {
        FadeOutSource(_activeBGM, duration, true);
        FadeOutSource(_inactiveBGM, duration, true);
    }

    public void StopBGM(float fadeDuration = 0.25f)
    {
        if (fadeDuration <= 0f)
        {
            StopSourceImmediate(_activeBGM);
            StopSourceImmediate(_inactiveBGM);
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

    private static void StopSourceImmediate(AudioSource source)
    {
        if (source == null) return;
        source.DOKill();
        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private IEnumerator CrossFadeRoutine(AudioClip clip, float duration)
    {
        _inactiveBgmBaseVolume = 1f;
        _inactiveBGM.clip   = clip;
        _inactiveBGM.volume = 0f;
        _inactiveBGM.loop   = true;
        _inactiveBGM.Play();

        float elapsed = 0f;
        float startVol = _activeBGM.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; 
            float t = elapsed / duration;
            
            _activeBGM.volume   = Mathf.Lerp(startVol, 0f, t);
            _inactiveBGM.volume = Mathf.Lerp(0f, GetEffectiveBgmSourceVolume(_inactiveBgmBaseVolume), t); 
            yield return null;
        }

        _activeBGM.Stop();
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
        (_activeBgmBaseVolume, _inactiveBgmBaseVolume) = (_inactiveBgmBaseVolume, _activeBgmBaseVolume);
    }

    public void SeamlessTransitionBGM(AudioClip nextPhaseClip)
    {
        if (nextPhaseClip == null || _activeBGM.clip == null) return;
        
        double syncTime = _activeBGM.timeSamples / (double)_activeBGM.clip.frequency;

        _inactiveBGM.clip        = nextPhaseClip;
        _inactiveBGM.volume      = _activeBGM.volume;
        _inactiveBGM.loop        = true;
        _inactiveBGM.timeSamples = _activeBGM.timeSamples;
        _inactiveBGM.Play();

        _inactiveBgmBaseVolume = _activeBgmBaseVolume;

        _activeBGM.Stop();
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
        (_activeBgmBaseVolume, _inactiveBgmBaseVolume) = (_inactiveBgmBaseVolume, _activeBgmBaseVolume);

        ApplyBgmSourceVolumes();
    }

    public void DuckBGM(float targetVolume = 0.3f, float duration = 0.3f)
    {
        _activeBGM.DOKill();
        _activeBGM.DOFade(GetEffectiveBgmSourceVolume(targetVolume), duration).SetUpdate(true);
    }

    public void RestoreBGM(float targetVolume = 1f, float duration = 0.3f)
    {
        _activeBGM.DOKill();
        _activeBGM.DOFade(GetEffectiveBgmSourceVolume(targetVolume), duration).SetUpdate(true);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f) { if (clip != null) _sfxSource.PlayOneShot(clip, volume); }
    public void PlayVoice(AudioClip clip, float volume = 1f)
{
    if (clip == null) return;
    _voiceSource.pitch = Random.Range(0.95f, 1.05f); 
    _voiceSource.PlayOneShot(clip, volume);
}

    public void SetMasterVolume(float normalized) => SetMixerVolume(MixerMaster, normalized);
    public void SetBGMVolume(float normalized) => SetMixerVolume(MixerBGM, normalized);
    public void SetSFXVolume(float normalized) => SetMixerVolume(MixerSFX, normalized);
    public void SetVoiceVolume(float normalized) => SetMixerVolume(MixerVoice, normalized);

    private float GetEffectiveBgmSourceVolume(float sourceVolume)
    {
        return Mathf.Clamp01(sourceVolume * _configuredBgmVolume * GameConfigManager.BgmOutputCompensation);
    }

    private void ApplyBgmSourceVolumes()
    {
        if (_activeBGM != null)
            _activeBGM.volume = _activeBGM.isPlaying ? GetEffectiveBgmSourceVolume(_activeBgmBaseVolume) : 0f;

        if (_inactiveBGM != null)
            _inactiveBGM.volume = _inactiveBGM.isPlaying ? GetEffectiveBgmSourceVolume(_inactiveBgmBaseVolume) : 0f;
    }

    private void SetMixerVolume(string param, float normalized)
    {
        if (_mixer == null) return;

        float db = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
        _mixer.SetFloat(param, db);
    }
}