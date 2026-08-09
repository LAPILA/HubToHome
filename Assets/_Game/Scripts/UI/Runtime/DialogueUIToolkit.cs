using System;
using System.Collections;
using System.Collections.Generic;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.Actions;
using Febucci.TextAnimatorForUnity.Styles;
using Febucci.TextAnimatorForUnity.UIToolkit;
using Febucci.TextAnimatorCore.Text;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class DialogueUIToolkit : MonoBehaviour
{
    private static readonly Color ChoiceSelectedColor = new Color(1f, 0.95f, 0.3f);

    [Header("UI Toolkit Assets")]
    [SerializeField] private UIDocument _document;
    [SerializeField] private VisualTreeAsset _visualTree;
    [SerializeField] private StyleSheet _styleSheet;
    [SerializeField] private StyleSheet _tokenStyleSheet;
    [SerializeField] private Font _silverFont;

    [Header("Febucci UITK Text Animator")]
    [SerializeField] private TypingsTimingsScriptableBase _typingTimingSettings;
    [SerializeField] private TypewriterSettingsScriptable _typewriterSettings;
    [SerializeField] private AnimatorSettingsScriptable _animationSettings;
    [SerializeField] private ActionDatabase _actionsDatabase;
    [SerializeField] private AnimationsDatabase _behaviorsDatabase;
    [SerializeField] private StyleSheetScriptable _animationStyleSheet;

    [Header("Voice")]
    [SerializeField] private AudioClip _defaultVoiceBlip;
    [SerializeField] private float _defaultPitch = 1f;
    [SerializeField, Min(0f)] private float _voiceMinInterval = 0.035f;

    public bool IsTyping => _isTyping && _bodyText != null && _bodyText.Typewriter != null
        ? _bodyText.Typewriter.IsShowingText
        : _isTyping;

    public bool IsWaitingForChoice { get; private set; }

    private VisualElement _root;
    private VisualElement _dialoguePanel;
    private Image _speakerPortrait;
    private Label _speakerName;
    private AnimatedLabel _bodyText;
    private Label _continueIndicator;
    private VisualElement _choicesRoot;
    private readonly List<Label> _choiceLabels = new List<Label>();

    private List<ChoiceData> _activeChoices;
    private Action<ChoiceData> _onChoiceSelected;
    private int _selectedChoiceIndex;
    private bool _isTyping;
    private bool _isInitialized;
    private bool _loggedInitializationError;
    private AudioClip _currentVoiceClip;
    private float _currentVoicePitch = 1f;
    private float _lastVoiceTime = float.NegativeInfinity;
    private Coroutine _closeRoutine;
    private Action<CharacterData> _onCharacterVisible;

    private void Awake()
    {
        if (_document == null)
            _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        StopCloseRoutine();
        UnbindTypewriterEvents();
    }

    private void OnDestroy()
    {
        UnbindTypewriterEvents();
    }

    private void Update()
    {
        if (IsWaitingForChoice)
            HandleChoiceInput();
    }

    public void RebindCanvasCameraImmediate()
    {
        // Kept for DialogueManager's existing command contract. UITK runtime panels do not bind to a world camera.
        EnsureInitialized();
    }

    public void OpenPanel()
    {
        if (!EnsureInitialized()) return;

        gameObject.SetActive(true);
        StopCloseRoutine();
        _root.RemoveFromClassList("dialogue-hidden");
        _root.RemoveFromClassList("dialogue-faded");
        _dialoguePanel.RemoveFromClassList("dialogue-hidden");
        _dialoguePanel.RemoveFromClassList("dialogue-faded");
    }

    public void ClosePanel()
    {
        if (!EnsureInitialized()) return;

        HideChoices();
        StopTypingWork();
        _root.AddToClassList("dialogue-faded");
        StopCloseRoutine();
        _closeRoutine = StartCoroutine(CoClosePanel());
    }

    public void HideImmediate()
    {
        HideChoices();
        StopTypingWork();
        StopCloseRoutine();

        if (_root != null)
        {
            _root.AddToClassList("dialogue-hidden");
            _root.RemoveFromClassList("dialogue-faded");
        }

        gameObject.SetActive(false);
    }

    public void DisplayNode(SpeakerData speaker, EmotionType emotion, string text)
    {
        DisplayText(speaker, emotion, text, true);
    }

    public void DisplayPrompt(string text)
    {
        DisplayText(null, EmotionType.Normal, text, false);
    }

    public void SkipTyping()
    {
        if (!IsTyping || _bodyText == null || _bodyText.Typewriter == null)
            return;

        _bodyText.Typewriter.SkipTypewriter();
        _isTyping = false;
        UpdateContinueIndicator();
    }

    public void OnTypingCompleted()
    {
        _isTyping = false;
        UpdateContinueIndicator();
    }

    public void ShowChoices(List<ChoiceData> choices, Action<ChoiceData> onSelected)
    {
        if (!EnsureInitialized()) return;

        HideChoices();
        if (choices == null || choices.Count == 0)
        {
            onSelected?.Invoke(null);
            return;
        }

        _activeChoices = choices;
        _onChoiceSelected = onSelected;
        _selectedChoiceIndex = 0;
        IsWaitingForChoice = true;

        for (int i = 0; i < _activeChoices.Count; i++)
        {
            Label label = new Label();
            label.name = $"dialogue-choice-{i}";
            label.AddToClassList("dialogue-choice");
            _choiceLabels.Add(label);
            _choicesRoot.Add(label);
        }

        RefreshChoiceVisuals();
        _choicesRoot.RemoveFromClassList("dialogue-hidden");
    }

    private void DisplayText(
        SpeakerData speaker,
        EmotionType emotion,
        string text,
        bool showUnknownSpeaker)
    {
        if (!EnsureInitialized()) return;

        _isTyping = false;
        HideChoices();
        SetSpeaker(speaker, emotion, showUnknownSpeaker);

        _currentVoiceClip = speaker != null && speaker.VoiceBlipSound != null
            ? speaker.VoiceBlipSound
            : _defaultVoiceBlip;
        _currentVoicePitch = speaker != null ? speaker.VoicePitch : _defaultPitch;
        _lastVoiceTime = float.NegativeInfinity;

        if (_bodyText.Typewriter == null)
        {
            Debug.LogError("[DialogueUIToolkit] Febucci AnimatedLabel Typewriter is unavailable.", this);
            return;
        }

        ApplyTypewriterSettings();
        _isTyping = true;
        _bodyText.Typewriter.ShowText(text ?? string.Empty);
        UpdateContinueIndicator();
    }

    private void SetSpeaker(SpeakerData speaker, EmotionType emotion, bool showUnknownSpeaker)
    {
        bool showName = speaker != null || showUnknownSpeaker;
        _speakerName.text = speaker != null ? speaker.DisplayName : "???";
        _speakerName.style.display = showName ? DisplayStyle.Flex : DisplayStyle.None;

        Sprite portrait = speaker != null ? speaker.GetPortrait(emotion) : null;
        _speakerPortrait.sprite = portrait;
        _speakerPortrait.style.display = portrait != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool EnsureInitialized()
    {
        if (_isInitialized)
            return true;

        if (_document == null)
            _document = GetComponent<UIDocument>();

        if (_document == null)
        {
            LogInitializationError("UIDocument is missing.");
            return false;
        }

        if (_document.visualTreeAsset == null && _visualTree != null)
            _document.visualTreeAsset = _visualTree;

        _root = _document.rootVisualElement;
        if (_root == null)
        {
            LogInitializationError("UIDocument rootVisualElement is unavailable.");
            return false;
        }

        AddStyleSheet(_tokenStyleSheet);
        AddStyleSheet(_styleSheet);

        _dialoguePanel = _root.Q<VisualElement>("overworld-dialogue-panel");
        _speakerPortrait = _root.Q<Image>("speaker-portrait");
        _speakerName = _root.Q<Label>("speaker-name");
        _bodyText = _root.Q<AnimatedLabel>("body-text");
        _continueIndicator = _root.Q<Label>("continue-indicator");
        _choicesRoot = _root.Q<VisualElement>("choices-root");

        if (_dialoguePanel == null || _speakerPortrait == null || _speakerName == null
            || _bodyText == null || _continueIndicator == null || _choicesRoot == null)
        {
            LogInitializationError("Dialogue UXML is missing one or more required named elements.");
            return false;
        }

        ApplyFont(_root);
        ApplyTypewriterSettings();
        BindTypewriterEvents();
        _choicesRoot.AddToClassList("dialogue-hidden");
        _root.AddToClassList("dialogue-hidden");
        _isInitialized = true;
        return true;
    }

    private void ApplyFont(VisualElement root)
    {
        if (_silverFont == null) return;

        root.style.unityFont = _silverFont;
        _speakerName.style.unityFont = _silverFont;
        _bodyText.style.unityFont = _silverFont;
        _continueIndicator.style.unityFont = _silverFont;
    }

    private void ApplyTypewriterSettings()
    {
        if (_bodyText == null) return;

        _bodyText.TimingSettings = _typingTimingSettings;
        _bodyText.TypewriterSettings = _typewriterSettings;
        _bodyText.AnimationSettings = _animationSettings;
        _bodyText.ActionsDatabase = _actionsDatabase;
        _bodyText.BehaviorsDatabase = _behaviorsDatabase;
        _bodyText.StyleSheetDatabase = _animationStyleSheet;

        if (_bodyText.Typewriter != null)
        {
            GameConfigManager config = GameConfigManager.Instance;
            float speed = config != null
                ? config.TextSpeed
                : GameConfigManager.DefaultTextSpeed;
            _bodyText.Typewriter.SetTypewriterSpeed(Mathf.Clamp(speed * 2.5f, 1.2f, 8f));
        }
    }

    private void BindTypewriterEvents()
    {
        if (_bodyText == null || _bodyText.Typewriter == null) return;

        UnbindTypewriterEvents();
        _bodyText.Typewriter.OnTextShowed += HandleTextShowed;
        _onCharacterVisible = _ => PlayVoiceBlip();
        _bodyText.Typewriter.OnCharacterVisible += _onCharacterVisible;
    }

    private void UnbindTypewriterEvents()
    {
        // AnimatedLabel's event owner is recreated with the UIDocument. The document is destroyed with this object,
        // so only the completion callback is explicitly detached when the provider is still available.
        if (_bodyText != null && _bodyText.Typewriter != null)
        {
            _bodyText.Typewriter.OnTextShowed -= HandleTextShowed;
            if (_onCharacterVisible != null)
                _bodyText.Typewriter.OnCharacterVisible -= _onCharacterVisible;
        }

        _onCharacterVisible = null;
    }

    private void HandleTextShowed()
    {
        OnTypingCompleted();
    }

    private void PlayVoiceBlip()
    {
        if (_currentVoiceClip == null || Time.unscaledTime - _lastVoiceTime < _voiceMinInterval)
            return;

        _lastVoiceTime = Time.unscaledTime;
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;

        AudioSource voiceSource = audio.VoiceSource;
        if (voiceSource != null)
        {
            voiceSource.pitch = _currentVoicePitch;
            voiceSource.PlayOneShot(_currentVoiceClip);
        }
        else
        {
            audio.PlaySFX(_currentVoiceClip);
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

    private void RefreshChoiceVisuals()
    {
        for (int i = 0; i < _choiceLabels.Count && i < _activeChoices.Count; i++)
        {
            bool selected = i == _selectedChoiceIndex;
            Label label = _choiceLabels[i];
            label.text = (selected ? "▶ " : "   ") + _activeChoices[i].ChoiceText;
            label.EnableInClassList("dialogue-choice--selected", selected);
            label.style.color = selected ? ChoiceSelectedColor : Color.white;
            if (_silverFont != null) label.style.unityFont = _silverFont;
        }
    }

    private void CommitChoice(int index)
    {
        if (_activeChoices == null || index < 0 || index >= _activeChoices.Count) return;

        ChoiceData selected = _activeChoices[index];
        Action<ChoiceData> callback = _onChoiceSelected;
        AudioManager.Instance?.PlaySelectionSfx();
        HideChoices();
        callback?.Invoke(selected);
    }

    private void HideChoices()
    {
        IsWaitingForChoice = false;
        _activeChoices = null;
        _onChoiceSelected = null;
        _selectedChoiceIndex = 0;

        if (_choiceLabels.Count > 0)
        {
            for (int i = 0; i < _choiceLabels.Count; i++)
                _choiceLabels[i]?.RemoveFromHierarchy();
            _choiceLabels.Clear();
        }

        _choicesRoot?.AddToClassList("dialogue-hidden");
    }

    private void StopTypingWork()
    {
        _isTyping = false;
        if (_bodyText != null && _bodyText.Typewriter != null)
            _bodyText.Typewriter.StopShowingText();
        UpdateContinueIndicator();
    }

    private void UpdateContinueIndicator()
    {
        if (_continueIndicator == null) return;
        _continueIndicator.style.display = IsTyping || IsWaitingForChoice
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private IEnumerator CoClosePanel()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        _closeRoutine = null;
        if (_root != null)
            _root.AddToClassList("dialogue-hidden");
        gameObject.SetActive(false);
    }

    private void StopCloseRoutine()
    {
        if (_closeRoutine == null) return;
        StopCoroutine(_closeRoutine);
        _closeRoutine = null;
    }

    private void AddStyleSheet(StyleSheet styleSheet)
    {
        if (styleSheet != null)
            _root.styleSheets.Add(styleSheet);
    }

    private void LogInitializationError(string message)
    {
        if (_loggedInitializationError) return;
        _loggedInitializationError = true;
        Debug.LogError($"[DialogueUIToolkit] {message}", this);
    }
}
