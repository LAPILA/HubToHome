using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("선택적 컴포넌트 (시네마틱에선 비워두세요)")]
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    
    [Header("Text Animator")]
    [SerializeField] private MonoBehaviour _typewriterComponent;
    
    public bool IsWaitingForChoice { get; private set; } = false;
    public bool IsTyping { get; private set; } = false;

    private SpeakerData _currentSpeaker; 

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        _canvasGroup.alpha = 0f;
        _canvasGroup.DOFade(1f, 0.2f);
    }

    public void ClosePanel()
    {
        _canvasGroup.DOFade(0f, 0.2f).OnComplete(() => gameObject.SetActive(false));
    }

    public void DisplayNode(SpeakerData speaker, EmotionType emotion, string text)
    {
        IsWaitingForChoice = false;
        IsTyping = true;
        _currentSpeaker = speaker; 

        // 🚨 이름 텍스트가 연결되어 있을 때만 처리 (시네마틱은 무시됨)
        if (_speakerNameText != null) 
        {
            _speakerNameText.text = speaker != null ? speaker.DisplayName : "";
        }
            
        // 🚨 초상화 이미지가 연결되어 있을 때만 처리
        if (_portraitImage != null)
        {
            Sprite portrait = speaker?.GetPortrait(emotion);
            if (portrait != null && emotion != EmotionType.None)
            {
                _portraitImage.sprite = portrait;
                _portraitImage.gameObject.SetActive(true);
            }
            else
            {
                _portraitImage.gameObject.SetActive(false);
            }
        }

        if (_typewriterComponent != null)
        {
            _typewriterComponent.SendMessage("ShowText", text, SendMessageOptions.DontRequireReceiver);
        }
    }

    public void SkipTyping()
    {
        if (_typewriterComponent != null)
            _typewriterComponent.SendMessage("SkipTypewriter", SendMessageOptions.DontRequireReceiver);
    }

    public void OnTypingCompleted() { IsTyping = false; }

    public void PlayVoiceBlip(char c)
    {
        if (_currentSpeaker == null || _currentSpeaker.VoiceBlipSound == null) return;
        if (!char.IsWhiteSpace(c) && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(_currentSpeaker.VoiceBlipSound, Camera.main.transform.position, 0.6f);
        }
    }

    public void ShowChoices(System.Collections.Generic.List<ChoiceData> choices, System.Action<ChoiceData> onSelected)
    {
        IsWaitingForChoice = true;
        StartCoroutine(TempChoiceRoutine(choices, onSelected));
    }

    private System.Collections.IEnumerator TempChoiceRoutine(System.Collections.Generic.List<ChoiceData> choices, System.Action<ChoiceData> onSelected)
    {
        // ... (기존과 동일한 Z/X/C 키 선택지 로직) ...
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