using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Febucci.UI.Examples;
using Febucci.TextAnimatorCore.Typing;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Text Animator & Sound")]
    [SerializeField] private TypewriterCore _typewriter; 
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

    private void Awake()
    {   
        if (_soundWriter == null) _soundWriter = GetComponent<TAnimSoundWriter>();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
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
        }
        else if (_dialogueText != null)
        {
            _dialogueText.text = text;
            IsTyping = false;
        }
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
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) { yield return null; continue; }

            if (choices.Count > 0 && kb.zKey.wasPressedThisFrame) { IsWaitingForChoice = false; onSelected?.Invoke(choices[0]); yield break; }
            if (choices.Count > 1 && kb.xKey.wasPressedThisFrame) { IsWaitingForChoice = false; onSelected?.Invoke(choices[1]); yield break; }
            if (choices.Count > 2 && kb.cKey.wasPressedThisFrame) { IsWaitingForChoice = false; onSelected?.Invoke(choices[2]); yield break; }
            yield return null;
        }
    }
    
}