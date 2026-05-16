using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Febucci.UI.Examples;
using Febucci.TextAnimatorForUnity;
using UnityEngine.SceneManagement;

public class DialogueUI : MonoBehaviour
{
    private static readonly Color ChoiceSelectedColor = new Color(1f, 0.95f, 0.3f);

    [SerializeField] private Canvas _rootCanvas;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Text Animator & Sound")]
    [SerializeField] private TypewriterComponent _typewriter; 
    [SerializeField] private TAnimSoundWriter _soundWriter; 

    [Header("UI 참조")]
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogueText; 
    [SerializeField] private RectTransform _choiceRoot;
    [SerializeField] private TextMeshProUGUI _choiceTemplate;

    [Header("기본 오디오 설정")]
    [SerializeField] private AudioClip _defaultVoiceBlip;
    [SerializeField] private float _defaultPitch = 1f;

    public bool IsTyping { get; private set; } = false;
    public bool IsWaitingForChoice { get; private set; } = false;
    private Coroutine _applySpeedRoutine;
    private readonly List<TextMeshProUGUI> _choiceLabels = new List<TextMeshProUGUI>();
    private List<ChoiceData> _activeChoices;
    private System.Action<ChoiceData> _onChoiceSelected;
    private int _selectedChoiceIndex;
    private Coroutine _cameraRebindRoutine;

    private void Awake()
    {   
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>(true);
        if (_typewriter == null) _typewriter = GetComponentInChildren<TypewriterComponent>(true);
        if (_soundWriter == null) _soundWriter = GetComponent<TAnimSoundWriter>();
        PrepareChoiceUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_cameraRebindRoutine != null)
        {
            StopCoroutine(_cameraRebindRoutine);
            _cameraRebindRoutine = null;
        }
    }

    private void Update()
    {
        if (IsWaitingForChoice)
        {
            HandleChoiceInput();
            return;
        }

        // Typewriter Timings/DB가 매 프레임 속도를 덮어쓰는 경우를 강제로 상쇄
        if (IsTyping && _typewriter != null)
            ApplyConfiguredTextSpeed();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        RebindCanvasCameraImmediate();
        StartCameraRebindRetry();
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
        HideChoices();
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
        float boosted = Mathf.Clamp(speed * 2.5f, 1.2f, 8f);
        _typewriter.SetTypewriterSpeed(boosted);
    }

    private void ResolveCanvasCamera()
    {
        if (_rootCanvas == null) return;
        if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        Camera target = Camera.main;
        if (target == null)
        {
            Camera[] cams = Camera.allCameras;
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].isActiveAndEnabled)
                {
                    target = cams[i];
                    break;
                }
            }
        }

        // 씬 전환/카메라 교체 시에도 항상 최신 월드 카메라로 강제 동기화
        _rootCanvas.worldCamera = target;
    }

    public void RebindCanvasCameraImmediate()
    {
        ResolveCanvasCamera();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindCanvasCameraImmediate();
        StartCameraRebindRetry();
    }

    private void StartCameraRebindRetry()
    {
        if (!isActiveAndEnabled) return;
        if (_cameraRebindRoutine != null)
            StopCoroutine(_cameraRebindRoutine);
        _cameraRebindRoutine = StartCoroutine(CoRebindCanvasCameraRetry());
    }

    private IEnumerator CoRebindCanvasCameraRetry()
    {
        for (int i = 0; i < 20; i++)
        {
            ResolveCanvasCamera();
            if (_rootCanvas != null && (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay || _rootCanvas.worldCamera != null))
            {
                _cameraRebindRoutine = null;
                yield break;
            }
            yield return null;
        }

        _cameraRebindRoutine = null;
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
        PrepareChoiceUI();
        if (_choiceRoot == null || _choiceTemplate == null || choices == null || choices.Count == 0)
        {
            onSelected?.Invoke(null);
            return;
        }

        IsWaitingForChoice = true;
        _activeChoices = choices;
        _onChoiceSelected = onSelected;
        _selectedChoiceIndex = 0;

        RebuildChoiceLabels();
        RefreshChoiceVisuals();
        _choiceRoot.gameObject.SetActive(true);
    }

    private void PrepareChoiceUI()
    {
        if (_choiceRoot == null || _choiceTemplate == null) return;

        _choiceRoot.anchorMin = new Vector2(0.5f, 0f);
        _choiceRoot.anchorMax = new Vector2(0.5f, 0f);
        _choiceRoot.pivot = new Vector2(0.5f, 0f);
        _choiceRoot.anchoredPosition = new Vector2(0f, 120f);
        _choiceRoot.sizeDelta = new Vector2(900f, 220f);

        var rootImage = _choiceRoot.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.enabled = false;
            rootImage.raycastTarget = false;
        }

        _choiceRoot.gameObject.SetActive(false);
        _choiceTemplate.gameObject.SetActive(false);
    }

    private void RebuildChoiceLabels()
    {
        for (int i = 0; i < _choiceLabels.Count; i++)
        {
            if (_choiceLabels[i] != null)
                Destroy(_choiceLabels[i].gameObject);
        }
        _choiceLabels.Clear();

        for (int i = 0; i < _activeChoices.Count; i++)
        {
            TextMeshProUGUI label = Instantiate(_choiceTemplate, _choiceRoot);
            label.gameObject.SetActive(true);
            _choiceLabels.Add(label);
        }
    }

    private void RefreshChoiceVisuals()
    {
        if (_activeChoices == null) return;

        for (int i = 0; i < _choiceLabels.Count && i < _activeChoices.Count; i++)
        {
            bool selected = i == _selectedChoiceIndex;
            _choiceLabels[i].text = (selected ? "▶ " : "   ") + _activeChoices[i].ChoiceText;
            _choiceLabels[i].color = selected ? ChoiceSelectedColor : Color.white;
        }
    }

    private void HandleChoiceInput()
    {
        if (_activeChoices == null || _activeChoices.Count == 0) return;

        if (GameInput.UIUpPressed)
        {
            _selectedChoiceIndex = (_selectedChoiceIndex - 1 + _activeChoices.Count) % _activeChoices.Count;
            RefreshChoiceVisuals();
        }
        else if (GameInput.UIDownPressed)
        {
            _selectedChoiceIndex = (_selectedChoiceIndex + 1) % _activeChoices.Count;
            RefreshChoiceVisuals();
        }

        if (GameInput.DialogueAdvancePressed || GameInput.ConfirmPressed)
        {
            CommitChoice(_selectedChoiceIndex);
            return;
        }

        if (GameInput.Choice1Pressed && _activeChoices.Count > 0) CommitChoice(0);
        else if (GameInput.Choice2Pressed && _activeChoices.Count > 1) CommitChoice(1);
        else if (GameInput.Choice3Pressed && _activeChoices.Count > 2) CommitChoice(2);
    }

    private void CommitChoice(int index)
    {
        if (_activeChoices == null || index < 0 || index >= _activeChoices.Count) return;

        ChoiceData selected = _activeChoices[index];
        System.Action<ChoiceData> callback = _onChoiceSelected;
        HideChoices();
        callback?.Invoke(selected);
    }

    private void HideChoices()
    {
        IsWaitingForChoice = false;
        _activeChoices = null;
        _onChoiceSelected = null;

        if (_choiceRoot != null)
            _choiceRoot.gameObject.SetActive(false);
    }
    
}