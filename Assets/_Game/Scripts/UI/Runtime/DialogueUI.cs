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
    [SerializeField, LabelText("전투 대화 배경 (선택)"), Tooltip("비워 두면 본문을 감싼 배경 Image를 사용합니다. 전투에서만 색을 빌리고 닫을 때 원래 상태로 복원합니다.")]
    private Image _battleBackgroundImage;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogueText; 
    [SerializeField] private RectTransform _choiceRoot;
    [SerializeField] private TextMeshProUGUI _choiceTemplate;

    [Header("선택지 배치 (640 x 480 기준)")]
    [SerializeField] private Vector2 _choiceAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 _choiceSize = new Vector2(520f, 132f);

    [Header("기본 오디오 설정")]
    [SerializeField] private AudioClip _defaultVoiceBlip;
    [SerializeField, Range(0.1f, 3f), Tooltip("화자가 없는 내레이션의 소리 높낮이입니다.")]
    private float _defaultPitch = 0.85f;
    [SerializeField, Range(0f, 1f), Tooltip("내레이션 기본 음량입니다. 설정의 효과음 음량은 기존 SFX 믹서에서 별도로 적용됩니다.")]
    private float _defaultVoiceVolume = 0.25f;

    [BoxGroup("대사 속도"), SerializeField, LabelText("시네마틱 초당 글자 수"), Min(1f),
     Tooltip("시네마틱 스타일 또는 Cutscene 상태의 대화에 적용합니다. 설정창의 대사 속도는 적용하지 않습니다.")]
    private float _cinematicCharactersPerSecond = 14f;

    [BoxGroup("전투 하단 대화"), InlineProperty, HideLabel]
    [SerializeField] private BattleDialogueLayoutSettings _battleLayoutSettings = new BattleDialogueLayoutSettings();

    public bool IsTyping { get; private set; } = false;
    public bool IsWaitingForChoice { get; private set; } = false;
    private readonly List<TextMeshProUGUI> _choiceLabels = new List<TextMeshProUGUI>();
    private List<ChoiceData> _activeChoices;
    private System.Action<ChoiceData> _onChoiceSelected;
    private int _selectedChoiceIndex;
    private bool _allowChoiceShortcuts = true;
    private Tween _panelTween;
    private BattleDialogueLayout _battleLayout;
    private bool _isClosing;
    private int _choiceOpenedFrame = -1;
    private SpeakerData _displayedSpeaker;
    private bool _standaloneNarration;
    private bool _typingSoundMuted;
    private bool _cinematicTyping;
    private bool _revealTextImmediately;
    private float _narrationCharactersPerSecond = 10f;
    private bool _timingsCaptured;
    private TypingsTimingsScriptableBase _originalTypingTimings;
    private TypingDelaysByCharacter _fixedTypingTimings;
    public bool IsBattleDocked => _battleLayout != null;

    /// <summary>다음 본문에 적용합니다. 대화 시작이 거절돼도 공유 AudioSource는 변경하지 않습니다.</summary>
    public void SetTypingSoundMuted(bool muted) => _typingSoundMuted = muted;

    /// <summary>표시 스타일과 별개로 Cutscene이 소유한 대화도 설정 속도에서 분리합니다.</summary>
    public void ConfigureTypingStyle(DialogueStyle style, bool isCutscene = false,
        bool revealTextImmediately = false)
    {
        _cinematicTyping = style == DialogueStyle.Cinematic || isCutscene;
        _revealTextImmediately = revealTextImmediately;
    }

    /// <summary>삽화 재생 데이터에서 설정 속도와 독립적인 본문 속도를 지정합니다.</summary>
    public void SetNarrationCharactersPerSecond(float charactersPerSecond)
        => _narrationCharactersPerSecond = NormalizeCharactersPerSecond(charactersPerSecond, 10f);

    /// <summary>타이핑/음성/입력 정책은 유지하고, 삽화 하단의 테두리 없는 본문만 복제합니다.</summary>
    public DialogueUI CreateNarrationCopy(Transform parent)
    {
        // Awake가 원본 Canvas를 재설정하기 전에 복제본의 소유권을 지정합니다.
        var holder = new GameObject("Narration construction", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(parent, false);
        DialogueUI copy = Instantiate(this, holder.transform, false);
        copy.gameObject.SetActive(false);
        copy._standaloneNarration = true;
        copy._rootCanvas = parent.GetComponentInParent<Canvas>(true);
        copy.transform.SetParent(parent, false);
        copy.name = "Illustrated narration";
        Destroy(holder);

        RectTransform panel = (RectTransform)copy.transform;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
        foreach (Image image in copy.GetComponentsInChildren<Image>(true))
        {
            image.enabled = false;
            image.raycastTarget = false;
        }
        if (copy._portraitImage != null) copy._portraitImage.gameObject.SetActive(false);
        if (copy._speakerNameText != null) copy._speakerNameText.gameObject.SetActive(false);
        copy._portraitImage = null;
        copy._speakerNameText = null;
        if (copy._dialogueText != null)
        {
            RectTransform body = copy._dialogueText.rectTransform;
            body.SetParent(panel, false);
            body.anchorMin = body.anchorMax = new Vector2(.5f, 0f);
            body.pivot = new Vector2(.5f, 0f);
            body.sizeDelta = new Vector2(560f, 128f);
            body.anchoredPosition = new Vector2(0f, 12f);
            body.localScale = Vector3.one;
            body.localRotation = Quaternion.identity;
            copy._dialogueText.enableAutoSizing = false;
            copy._dialogueText.fontSize = 40f;
            copy._dialogueText.alignment = TextAlignmentOptions.Center;
            copy._dialogueText.overflowMode = TextOverflowModes.Page;
            copy._dialogueText.pageToDisplay = 1;
            copy._dialogueText.margin = Vector4.zero;
            copy._dialogueText.color = Color.white;
            copy._dialogueText.raycastTarget = false;
        }
        if (copy._canvasGroup != null)
        {
            copy._canvasGroup.interactable = false;
            copy._canvasGroup.blocksRaycasts = false;
        }
        return copy;
    }

    private void Awake()
    {
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>(true);
        if (!_standaloneNarration)
        {
            DialogueCanvasViewport.Ensure(_rootCanvas, _rootCanvas != null ? _rootCanvas.worldCamera : null);
            UIRuntimeGuard.NormalizeCanvas(gameObject);
        }
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
        // 일반 필드/시네마틱 패널도 손실되면 대기 중인 재생을 취소합니다.
        // 활성 패널 일치 여부는 DialogueManager가 검사하므로 다른 대화는 건드리지 않습니다.
        bool unexpectedClose = !_isClosing;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        StopPanelTween();
        HideChoices();
        StopTypingWork();
        RestoreTypingTimings();
        _cinematicTyping = false;
        _revealTextImmediately = false;
        ReleaseBattleLayout();
        if (unexpectedClose) DialogueManager.Instance?.CancelPresentation(this);
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        RestoreTypingTimings();
        if (_fixedTypingTimings == null) return;
        if (Application.isPlaying) Destroy(_fixedTypingTimings);
        else DestroyImmediate(_fixedTypingTimings);
        _fixedTypingTimings = null;
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
        DialogueManager.Instance?.RefreshLocalizedPresentation(this);
    }

    /// <summary>대화 소유권·선택·이벤트를 유지하고 현재 문장만 교체합니다.</summary>
    public void RefreshLocalizedBody(string text)
    {
        if (_isClosing || !isActiveAndEnabled) return;
        bool wasTyping = IsTyping;
        int page = _dialogueText != null ? _dialogueText.pageToDisplay : 1;
        AudioSource voice = _soundWriter != null ? _soundWriter.source : null;
        float volume = voice != null ? voice.volume : 0f;
        try
        {
            // 이미 읽은 문장을 번역할 때 첫 글자 음성을 중복 재생하지 않습니다.
            if (!wasTyping && voice != null) voice.volume = 0f;
            if (_typewriter != null)
            {
                if (_revealTextImmediately)
                    DisplayBodyImmediately(text);
                else
                {
                    IsTyping = wasTyping;
                    _typewriter.ShowText(text ?? string.Empty);
                    ApplyConfiguredTextSpeed();
                    if (!wasTyping)
                    {
                        _typewriter.StopShowingText();
                        _typewriter.TextAnimator?.SetVisibilityEntireText(true, false);
                        IsTyping = false;
                    }
                }
            }
            else if (_dialogueText != null)
            {
                _dialogueText.text = text ?? string.Empty;
                IsTyping = false;
            }
            if (_dialogueText != null)
            {
                _dialogueText.ForceMeshUpdate();
                _dialogueText.pageToDisplay = Mathf.Clamp(page, 1, Mathf.Max(1, _dialogueText.textInfo.pageCount));
            }
        }
        finally { if (voice != null) voice.volume = volume; }
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
        if (!_standaloneNarration) UIRuntimeGuard.NormalizeCanvas(gameObject);
        RebindCanvasCameraImmediate();
        BattleMenuUI menu = BattleUIController.Instance != null ? BattleUIController.Instance.MainDialogueMenu : null;
        if (!_standaloneNarration && menu != null && transform is RectTransform panel && _dialogueText != null
            && _dialogueText.transform.IsChildOf(panel) && menu.BeginDialoguePresentation())
        {
            _battleLayout = new BattleDialogueLayout(menu, panel, _portraitImage,
                _speakerNameText, _dialogueText, _choiceRoot, _battleLayoutSettings,
                ResolveBattleBackgroundImage(panel));
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

    private Image ResolveBattleBackgroundImage(RectTransform panel)
    {
        if (_battleBackgroundImage != null && _battleBackgroundImage != _portraitImage
            && _battleBackgroundImage.transform.IsChildOf(panel)) return _battleBackgroundImage;
        // 기존 프리팹은 본문을 배경 아래에 두므로 이름/색상에 의존하지 않고 조상만 확인합니다.
        Transform candidate = _dialogueText != null ? _dialogueText.transform.parent : null;
        while (candidate != null && (candidate == panel || candidate.IsChildOf(panel)))
        {
            if (candidate.TryGetComponent(out Image background) && background != _portraitImage)
                return background;
            if (candidate == panel) break;
            candidate = candidate.parent;
        }
        return null;
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
                // 기존 SFX 믹서 경로를 유지합니다. 설정 음량을 여기서 다시 곱하지 않습니다.
                _soundWriter.source.volume = _typingSoundMuted ? 0f
                    : Mathf.Clamp01(speaker != null ? speaker.VoiceVolume : _defaultVoiceVolume);
            }
        }

        // 초상/이름 변경에 따른 폭과 페이지를 먼저 확정한 뒤 타이핑을 시작합니다.
        if ((_battleLayout != null || _standaloneNarration) && _dialogueText != null) _dialogueText.pageToDisplay = 1;
        RefreshBattleLayout();

        // 3. 🚨 텍스트 출력
        if (_revealTextImmediately)
            DisplayBodyImmediately(text);
        else if (_typewriter != null)
        {
            _typewriter.ShowText(text);
            // ShowText resets the package's speed multiplier. Apply immediately, not two frames later.
            ApplyConfiguredTextSpeed();
        }
        else if (_dialogueText != null)
        {
            _dialogueText.text = text;
            IsTyping = false;
        }
    }

    private void DisplayBodyImmediately(string text)
    {
        // ShowText/SkipTypewriter를 거치지 않아 글자별 음성과 대기 명령을 한꺼번에 재생하지 않습니다.
        if (_typewriter != null)
        {
            _typewriter.StopShowingText();
            var animator = _typewriter.TextAnimator;
            if (animator != null)
            {
                animator.SetText(text ?? string.Empty, false);
                animator.SetVisibilityEntireText(true, false);
            }
            else if (_dialogueText != null)
                _dialogueText.text = text ?? string.Empty;
        }
        else if (_dialogueText != null)
            _dialogueText.text = text ?? string.Empty;
        IsTyping = false;
    }

    private void ApplyConfiguredTextSpeed()
    {
        if (_typewriter == null) return;

        if (ApplyFixedTypingTiming())
        {
            _typewriter.SetTypewriterSpeed(1f);
            return;
        }

        GameConfigManager config = GameConfigManager.Instance;
        float speed = config != null
            ? config.TextSpeed
            : GameConfigManager.DefaultTextSpeed;
        float boosted = Mathf.Clamp(speed * 2.5f, 1.2f, 8f);
        _typewriter.SetTypewriterSpeed(boosted);
    }

    private bool ApplyFixedTypingTiming()
    {
        if (_typewriter == null) return false;
        if (!_cinematicTyping && !_standaloneNarration)
        {
            RestoreTypingTimings();
            return false;
        }
        if (!_timingsCaptured)
        {
            _originalTypingTimings = _typewriter.TimingSettings;
            _timingsCaptured = true;
        }
        if (_fixedTypingTimings == null)
        {
            _fixedTypingTimings = ScriptableObject.CreateInstance<TypingDelaysByCharacter>();
            _fixedTypingTimings.name = "Dialogue fixed typing (runtime)";
            _fixedTypingTimings.hideFlags = HideFlags.HideAndDontSave;
            _fixedTypingTimings.avoidMultiplePunctuationWait = true;
            _fixedTypingTimings.waitForNewLines = false;
            _fixedTypingTimings.waitForLastCharacter = true;
            _fixedTypingTimings.skipLastPunctuationWait = true;
        }
        float cps = _standaloneNarration ? _narrationCharactersPerSecond
            : NormalizeCharactersPerSecond(_cinematicCharactersPerSecond, 14f);
        _fixedTypingTimings.waitForNormalChars = 1f / cps;
        _fixedTypingTimings.waitMiddle = 0.2f;
        _fixedTypingTimings.waitLong = 0.4f;
        _typewriter.TimingSettings = _fixedTypingTimings;
        return true;
    }

    private void RestoreTypingTimings()
    {
        if (_timingsCaptured && _typewriter != null && _typewriter.TimingSettings == _fixedTypingTimings)
            _typewriter.TimingSettings = _originalTypingTimings;
    }

    private static float NormalizeCharactersPerSecond(float value, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, 0.01f, 120f);

    private void StopTypingWork()
    {
        IsTyping = false;
        if (_typewriter != null) _typewriter.StopShowingText();
    }

    private void ResolveCanvasCamera()
    {
        if (_standaloneNarration) return; // 상위 IllustratedStoryPlayer가 고정 Viewport를 소유합니다.
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
        if ((_battleLayout == null && !_standaloneNarration) || _dialogueText == null) return false;
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

    public void ShowChoices(List<ChoiceData> choices, System.Action<ChoiceData> onSelected,
        bool allowShortcuts = true)
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
        _allowChoiceShortcuts = allowShortcuts;
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

        // 저장/불러오기 prompt의 X는 소유자가 취소로 처리합니다. 같은 X를
        // Choice2로 먼저 확정하면 Update 실행 순서에 따라 다른 슬롯을 선택하게 됩니다.
        if (!_allowChoiceShortcuts) return;
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
