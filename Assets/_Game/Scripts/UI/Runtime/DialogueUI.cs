using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Febucci.UI.Examples;
using Febucci.TextAnimatorForUnity;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

/// <summary>
/// 공용 메인 대화창 소유자. 전투에서는 서브메뉴 영역에 표시하며, Battle Speech Bubble처럼
/// 월드를 따라가는 별도 WorldTracked UI와 혼동하지 않는다.
/// </summary>
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

    [Header("선택지 배치 (640 x 480 기준)")]
    [SerializeField] private Vector2 _choiceAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 _choiceSize = new Vector2(520f, 132f);

    [Header("기본 오디오 설정")]
    [SerializeField] private AudioClip _defaultVoiceBlip;
    [SerializeField] private float _defaultPitch = 1f;

    [BoxGroup("전투 하단 대화"), InlineProperty, HideLabel]
    [SerializeField] private BattleDialogueLayoutSettings _battleLayoutSettings = new BattleDialogueLayoutSettings();

    public bool IsTyping { get; private set; } = false;
    public bool IsWaitingForChoice { get; private set; } = false;
    private Coroutine _applySpeedRoutine;
    private readonly List<TextMeshProUGUI> _choiceLabels = new List<TextMeshProUGUI>();
    private List<ChoiceData> _activeChoices;
    private System.Action<ChoiceData> _onChoiceSelected;
    private int _selectedChoiceIndex;
    private Tween _panelTween;
    private BattleDialogueLayout _battleLayout;
    private bool _isClosing;
    private int _choiceOpenedFrame = -1;
    private SpeakerData _displayedSpeaker;
    public bool IsBattleDocked => _battleLayout != null;

    private void Awake()
    {
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>(true);
        DialogueCanvasViewport.Ensure(_rootCanvas, _rootCanvas != null ? _rootCanvas.worldCamera : null);
        UIRuntimeGuard.NormalizeCanvas(gameObject);
        if (_typewriter == null) _typewriter = GetComponentInChildren<TypewriterComponent>(true);
        if (_soundWriter == null) _soundWriter = GetComponent<TAnimSoundWriter>();
        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);
        PrepareChoiceUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        LocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        bool unexpectedClose = IsBattleDocked && !_isClosing;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        StopPanelTween();
        HideChoices();
        StopTypingWork();
        ReleaseBattleLayout();
        if (unexpectedClose) DialogueManager.Instance?.CancelPresentation(this);
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (IsWaitingForChoice)
            HandleChoiceInput();
    }

    private void HandleLanguageChanged(LanguageType language)
    {
        if (_displayedSpeaker != null && _speakerNameText != null)
            _speakerNameText.text = _displayedSpeaker.ResolveDisplayName();
        if (IsWaitingForChoice) RefreshChoiceVisuals();
    }

    private void LateUpdate()
    {
        if (_battleLayout == null) return;
        if (!_battleLayout.IsHostAvailable)
        {
            DialogueManager.Instance?.CancelPresentation(this);
            HideImmediate();
            return;
        }
        if (!_isClosing) RefreshBattleLayout();
    }

    private void RefreshBattleLayout(bool force = false) => _battleLayout?.Refresh(_choiceLabels, _selectedChoiceIndex, force);

    private void ReleaseBattleLayout()
    {
        BattleDialogueLayout owned = _battleLayout;
        _battleLayout = null;
        owned?.Dispose();
    }

    public void OpenPanel()
    {
        StopPanelTween();
        ReleaseBattleLayout();
        _isClosing = false;
        gameObject.SetActive(true);
        // 비활성 상태에서 처음 표시되는 UI도 공통 Canvas 정책을 통과시킨다.
        UIRuntimeGuard.NormalizeCanvas(gameObject);
        RebindCanvasCameraImmediate();
        BattleMenuUI menu = BattleUIController.Instance != null ? BattleUIController.Instance.MainDialogueMenu : null;
        if (menu != null && transform is RectTransform panel && _dialogueText != null
            && _dialogueText.transform.IsChildOf(panel) && menu.BeginDialoguePresentation())
        {
            _battleLayout = new BattleDialogueLayout(menu, panel, _portraitImage,
                _speakerNameText, _dialogueText, _choiceRoot, _battleLayoutSettings);
            RefreshBattleLayout();
        }
        ApplyConfiguredTextSpeed();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            float delay = _battleLayout != null ? menu.DialogueTransitionDuration : 0f;
            _panelTween = _canvasGroup.DOFade(1f, _battleLayout != null ? .1f : .2f)
                .SetDelay(delay).SetUpdate(true).SetRecyclable(false)
                .OnComplete(() => _panelTween = null);
        }
    }

    public void ClosePanel()
    {
        StopPanelTween();
        _isClosing = true;
        HideChoices();
        StopTypingWork();
        if (_canvasGroup != null && isActiveAndEnabled)
        {
            _panelTween = _canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
            {
                _panelTween = null;
                gameObject.SetActive(false);
            });
        }
        else gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        StopPanelTween();
        HideChoices();
        StopTypingWork();
        StopAllCoroutines();
        _applySpeedRoutine = null;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
        ReleaseBattleLayout();
    }

    private void StopPanelTween()
    {
        _panelTween?.Kill(false);
        _panelTween = null;
    }

    public void DisplayNode(SpeakerData speaker, EmotionType emotion, string text)
    {
        DisplayText(speaker, emotion, text, true);
    }

    public void DisplayPrompt(string text)
    {
        DisplayText(null, EmotionType.Normal, text, false);
    }

    private void DisplayText(
        SpeakerData speaker,
        EmotionType emotion,
        string text,
        bool showUnknownSpeaker)
    {
        IsTyping = true;
        IsWaitingForChoice = false;
        _displayedSpeaker = speaker;
        ApplyConfiguredTextSpeed();

        // 1. 이름 및 초상화 세팅
        if (_speakerNameText != null)
        {
            _speakerNameText.gameObject.SetActive(speaker != null || showUnknownSpeaker);
            _speakerNameText.text = speaker != null ? speaker.ResolveDisplayName() : "???";
        }

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

        // 초상/이름 변경에 따른 폭과 페이지를 먼저 확정한 뒤 타이핑을 시작합니다.
        if (_battleLayout != null && _dialogueText != null) _dialogueText.pageToDisplay = 1;
        RefreshBattleLayout();

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

        GameConfigManager config = GameConfigManager.Instance;
        float speed = config != null
            ? config.TextSpeed
            : GameConfigManager.DefaultTextSpeed;
        float boosted = Mathf.Clamp(speed * 2.5f, 1.2f, 8f);
        _typewriter.SetTypewriterSpeed(boosted);
    }

    private void StopTypingWork()
    {
        IsTyping = false;
        if (_typewriter != null) _typewriter.StopShowingText();
        if (_applySpeedRoutine == null)
            return;

        StopCoroutine(_applySpeedRoutine);
        _applySpeedRoutine = null;
    }

    private void ResolveCanvasCamera()
    {
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>(true);
        if (_rootCanvas == null) return;
        Camera output = Application.isPlaying
            ? UIViewportService.GetOrCreate().ResolveSharedCamera()
            : _rootCanvas.worldCamera;
        DialogueCanvasViewport.Ensure(_rootCanvas, output);
        if (Application.isPlaying)
            UIViewportService.GetOrCreate().RegisterFixedViewport(_rootCanvas);
    }

    public void RebindCanvasCameraImmediate()
    {
        ResolveCanvasCamera();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 카메라 지연 생성/교체 재시도는 공통 UIViewportService가 담당합니다.
        RebindCanvasCameraImmediate();
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

    public bool TryAdvancePage()
    {
        if (_battleLayout == null || _dialogueText == null) return false;
        _dialogueText.ForceMeshUpdate();
        if (_dialogueText.pageToDisplay >= _dialogueText.textInfo.pageCount) return false;
        _dialogueText.pageToDisplay++;
        _dialogueText.ForceMeshUpdate();
        // 다음 페이지는 같은 노드의 나머지 글입니다. ShowText를 재호출해 음성/이벤트를 반복하지 않습니다.
        if (_typewriter != null)
        {
            _typewriter.StopShowingText();
            _typewriter.TextAnimator?.SetVisibilityEntireText(true, false);
        }
        IsTyping = false;
        return true;
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
        _choiceOpenedFrame = Time.frameCount;

        RebuildChoiceLabels();
        RefreshChoiceVisuals();
        _choiceRoot.gameObject.SetActive(true);
        RefreshBattleLayout(true);
    }

    private void PrepareChoiceUI()
    {
        if (_choiceRoot == null || _choiceTemplate == null) return;

        _choiceRoot.anchorMin = new Vector2(0.5f, 0f);
        _choiceRoot.anchorMax = new Vector2(0.5f, 0f);
        _choiceRoot.pivot = new Vector2(0.5f, 0f);
        _choiceRoot.anchoredPosition = _choiceAnchoredPosition;
        _choiceRoot.sizeDelta = new Vector2(
            Mathf.Clamp(_choiceSize.x, 160f, GameConfigPolicy.ReferenceWidth),
            Mathf.Clamp(_choiceSize.y, 48f, GameConfigPolicy.ReferenceHeight));

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
            _choiceLabels[i].text = (selected ? "▶ " : "   ") + _activeChoices[i].ResolveText();
            _choiceLabels[i].color = selected ? ChoiceSelectedColor : Color.white;
        }
    }

    private void HandleChoiceInput()
    {
        if (_activeChoices == null || _activeChoices.Count == 0) return;
        if (Time.frameCount <= _choiceOpenedFrame || GameInput.IsConfigModalActive
            || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused)) return;

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
            if (IsTyping) { SkipTyping(); return; }
            if (TryAdvancePage()) return;
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
        GameInput.ConsumeBattleUIInput();
        AudioManager.Instance?.PlaySelectionSfx();
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
