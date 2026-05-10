using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Febucci.UI.Examples;
using Febucci.TextAnimatorForUnity;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Text Animator & Sound")]
    [SerializeField] private TypewriterComponent _typewriter; 
    [SerializeField] private TAnimSoundWriter _soundWriter; 

    [Header("UI 참조")]
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogueText; 

    [Header("기본 오디오 설정")]
    [SerializeField] private AudioClip _defaultVoiceBlip;
    [SerializeField] private float _defaultPitch = 1f;

    public bool IsTyping { get; private set; } = false;
    public bool IsWaitingForChoice { get; private set; } = false;
    private Coroutine _applySpeedRoutine;

    private void Awake()
    {   
        if (_typewriter == null) _typewriter = GetComponentInChildren<TypewriterComponent>(true);
        if (_soundWriter == null) _soundWriter = GetComponent<TAnimSoundWriter>();
    }

    private void Update()
    {
        // Typewriter Timings/DB가 매 프레임 속도를 덮어쓰는 경우를 강제로 상쇄
        if (IsTyping && _typewriter != null)
            ApplyConfiguredTextSpeed();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        ApplyConfiguredTextSpeed();
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        }
    }

    public void ClosePanel()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.DOFade(0f, 0.2f).OnComplete(() => gameObject.SetActive(false));
        }
        else gameObject.SetActive(false);
    }

    public void DisplayNode(SpeakerData speaker, EmotionType emotion, string text)
    {
        IsTyping = true;
        IsWaitingForChoice = false;
        ApplyConfiguredTextSpeed();

        // 1. 이름 및 초상화 세팅
        if (_speakerNameText != null) 
            _speakerNameText.text = (speaker != null) ? speaker.DisplayName : "???";

        if (_portraitImage != null)
        {
            if (speaker != null)
            {
                Sprite spr = speaker.GetPortrait(emotion);
                _portraitImage.sprite = spr;
                _portraitImage.gameObject.SetActive(spr != null);
            }
            else _portraitImage.gameObject.SetActive(false);
        }

        // 2. 🚨 사운드 교체 (ShowText 호출 전에 수행)
        if (_soundWriter != null)
        {
            AudioClip voice = (speaker != null && speaker.VoiceBlipSound != null) ? speaker.VoiceBlipSound : _defaultVoiceBlip;
            _soundWriter.sounds = new AudioClip[] { voice };
            if (_soundWriter.source != null)
            {
                _soundWriter.source.pitch = (speaker != null) ? speaker.VoicePitch : _defaultPitch;
            }
        }

        // 3. 🚨 텍스트 출력
        if (_typewriter != null)
        {
            _typewriter.ShowText(text);
            if (_applySpeedRoutine != null) StopCoroutine(_applySpeedRoutine);
            _applySpeedRoutine = StartCoroutine(CoReapplyTypewriterSpeed());
        }
        else if (_dialogueText != null)
        {
            _dialogueText.text = text;
            IsTyping = false;
        }
    }

    private void ApplyConfiguredTextSpeed()
    {
        if (_typewriter == null) return;

        float speed = GameConfigManager.EnsureInstance().TextSpeed;
        _typewriter.SetTypewriterSpeed(Mathf.Max(0.05f, speed));
    }

    private IEnumerator CoReapplyTypewriterSpeed()
    {
        yield return null;
        ApplyConfiguredTextSpeed();
        yield return null;
        ApplyConfiguredTextSpeed();
    }

    public void SkipTyping()
    {
        if (IsTyping && _typewriter != null)
        {
            _typewriter.SkipTypewriter();
            IsTyping = false; // 즉시 다음 노드로 넘어갈 수 있게 함
        }
    }

    // 🚨 외부(Text Animator)에서 호출할 수 있는 이벤트 함수 (기존 세팅 유지용)
    public void OnTypingCompleted() { IsTyping = false; }

    public void ShowChoices(List<ChoiceData> choices, System.Action<ChoiceData> onSelected)
    {
        IsWaitingForChoice = true;
        StartCoroutine(TempChoiceRoutine(choices, onSelected));
    }

    private IEnumerator TempChoiceRoutine(List<ChoiceData> choices, System.Action<ChoiceData> onSelected)
    {
        yield return new WaitForSeconds(0.1f);
        while (IsWaitingForChoice)
        {
            if (choices.Count > 0 && GameInput.Choice1Pressed) { IsWaitingForChoice = false; onSelected?.Invoke(choices[0]); yield break; }
            if (choices.Count > 1 && GameInput.Choice2Pressed) { IsWaitingForChoice = false; onSelected?.Invoke(choices[1]); yield break; }
            if (choices.Count > 2 && GameInput.Choice3Pressed) { IsWaitingForChoice = false; onSelected?.Invoke(choices[2]); yield break; }
            yield return null;
        }
    }
    
}