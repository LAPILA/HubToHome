using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _mixer;

    [Header("Sources")]
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _voiceSource;

    private AudioSource _activeBGM;
    private AudioSource _inactiveBGM;

    private const string MixerBGM   = "BGMVolume";
    private const string MixerSFX   = "SFXVolume";
    private const string MixerVoice = "VoiceVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _activeBGM   = _bgmSourceA;
        _inactiveBGM = _bgmSourceB;
    }

    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        if (_activeBGM.clip == clip && _activeBGM.isPlaying) return; // 이미 재생 중이면 무시

        _activeBGM.DOKill(); // 기존 트윈 강제 종료
        _activeBGM.clip   = clip;
        _activeBGM.volume = volume;
        _activeBGM.loop   = true;
        _activeBGM.Play();
    }

    public void CrossFadeBGM(AudioClip clip, float duration = 1f)
    {
        if (_activeBGM.clip == clip) return;
        StartCoroutine(CrossFadeRoutine(clip, duration));
    }

    private IEnumerator CrossFadeRoutine(AudioClip clip, float duration)
    {
        _inactiveBGM.clip   = clip;
        _inactiveBGM.volume = 0f;
        _inactiveBGM.loop   = true;
        _inactiveBGM.Play();

        float elapsed = 0f;
        float startVol = _activeBGM.volume;

        while (elapsed < duration)
        {
            // Time.unscaledDeltaTime을 사용하여 일시정지 중에도 BGM 전환 가능하게 처리
            elapsed += Time.unscaledDeltaTime; 
            float t = elapsed / duration;
            
            _activeBGM.volume   = Mathf.Lerp(startVol, 0f, t);
            _inactiveBGM.volume = Mathf.Lerp(0f, 1f, t); 
            yield return null;
        }

        _activeBGM.Stop();
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
    }

    public void SeamlessTransitionBGM(AudioClip nextPhaseClip)
    {
        double syncTime = _activeBGM.timeSamples / (double)_activeBGM.clip.frequency;

        _inactiveBGM.clip        = nextPhaseClip;
        _inactiveBGM.volume      = _activeBGM.volume;
        _inactiveBGM.loop        = true;
        _inactiveBGM.timeSamples = _activeBGM.timeSamples;
        _inactiveBGM.Play();

        _activeBGM.Stop();
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
    }

    public void DuckBGM(float targetVolume = 0.3f, float duration = 0.3f)
    {
        _activeBGM.DOKill();
        _activeBGM.DOFade(targetVolume, duration).SetUpdate(true);
    }

    public void RestoreBGM(float targetVolume = 1f, float duration = 0.3f)
    {
        _activeBGM.DOKill();
        _activeBGM.DOFade(targetVolume, duration).SetUpdate(true);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f) => _sfxSource.PlayOneShot(clip, volume);
    public void PlayVoice(AudioClip clip, float volume = 1f) => _voiceSource.PlayOneShot(clip, volume);

    public void SetBGMVolume(float normalized) => SetMixerVolume(MixerBGM, normalized);
    public void SetSFXVolume(float normalized) => SetMixerVolume(MixerSFX, normalized);
    public void SetVoiceVolume(float normalized) => SetMixerVolume(MixerVoice, normalized);

    private void SetMixerVolume(string param, float normalized)
    {
        float db = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
        _mixer.SetFloat(param, db);
    }
}