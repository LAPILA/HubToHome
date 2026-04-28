using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

/// <summary>
/// BGM / SFX / Voice 채널을 분리 관리하는 오디오 싱글톤.
/// Limbus Company 스타일의 Seamless BGM Phase Transition을 지원합니다.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _mixer;

    [Header("Sources")]
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _voiceSource;

    // 현재 활성 BGM 소스 추적
    private AudioSource _activeBGM;
    private AudioSource _inactiveBGM;

    // 캐싱
    private WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    // ── Mixer 파라미터 이름 상수 ──────────────────────────────
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

    // ── BGM 재생 ──────────────────────────────────────────────
    /// <summary>BGM을 즉시 재생합니다.</summary>
    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        _activeBGM.clip   = clip;
        _activeBGM.volume = volume;
        _activeBGM.loop   = true;
        _activeBGM.Play();
    }

    /// <summary>Cross-fade로 BGM을 전환합니다.</summary>
    public void CrossFadeBGM(AudioClip clip, float duration = 1f)
    {
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
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _activeBGM.volume   = Mathf.Lerp(startVol, 0f, t);
            _inactiveBGM.volume = Mathf.Lerp(0f, startVol, t);
            yield return _waitEOF;
        }

        _activeBGM.Stop();
        // 소스 스왑
        (_activeBGM, _inactiveBGM) = (_inactiveBGM, _activeBGM);
    }

    /// <summary>
    /// Seamless Phase Transition: 같은 템포의 다른 페이즈 곡으로 전환.
    /// 현재 재생 시간을 동기화하여 끊김 없이 전환합니다. (Limbus 보스전 스타일)
    /// </summary>
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

    // ── BGM Ducking (대화 중 볼륨 낮추기) ────────────────────
    public void DuckBGM(float targetVolume = 0.3f, float duration = 0.3f)
    {
        _activeBGM.DOFade(targetVolume, duration);
    }

    public void RestoreBGM(float targetVolume = 1f, float duration = 0.3f)
    {
        _activeBGM.DOFade(targetVolume, duration);
    }

    // ── SFX 재생 ──────────────────────────────────────────────
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        _sfxSource.PlayOneShot(clip, volume);
    }

    // ── Voice 재생 ────────────────────────────────────────────
    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        _voiceSource.PlayOneShot(clip, volume);
    }

    // ── 볼륨 설정 (AudioMixer) ────────────────────────────────
    /// <param name="normalizedVolume">0~1 범위</param>
    public void SetBGMVolume(float normalizedVolume)
        => SetMixerVolume(MixerBGM, normalizedVolume);

    public void SetSFXVolume(float normalizedVolume)
        => SetMixerVolume(MixerSFX, normalizedVolume);

    public void SetVoiceVolume(float normalizedVolume)
        => SetMixerVolume(MixerVoice, normalizedVolume);

    private void SetMixerVolume(string param, float normalized)
    {
        // AudioMixer는 dB 단위 사용 (0 → -80dB, 1 → 0dB)
        float db = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
        _mixer.SetFloat(param, db);
    }
}
