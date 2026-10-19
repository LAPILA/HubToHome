using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 삽화 전용 표시 수명입니다. 대사 진행/스킵/번역은 DialogueManager에 위임하며,
/// 월드 카메라나 공유 스프라이트 자산을 영구 변경하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class IllustratedStoryPlayer : MonoBehaviour
{
    private readonly Dictionary<Texture, FilterMode> _previousFilters = new Dictionary<Texture, FilterMode>();
    private GameObject _overlay;
    private RectTransform _pictureMask;
    private Image _picture;
    private TextMeshProUGUI _caption;
    private CanvasGroup _pageGroup;
    private DialogueUI _narration;
    private DialogueManager _dialogueOwner;
    private Tween _fadeTween;
    private IllustratedStoryPage _page;
    private Sprite _lastSprite;
    private float _pictureScale;
    private GameState _stateBeforeStory;
    private bool _ownsCutsceneState;
    private bool _dialogueFinished;
    private bool _dialogueCancelled;
    private bool _tweenPaused;
    private float _pageElapsed;
    private int _captionCharacterCount;
    private int _lastCaptionVisible;
    private int _dialogueGeneration;
    private int _runVersion;

    public bool Succeeded { get; private set; }
    public bool IsPlaying { get; private set; }
    public event System.Action<int> PageStarted;

    public IEnumerator Play(IllustratedStoryAsset story)
    {
        if (IsPlaying)
        {
            Debug.LogWarning("[IllustratedStoryPlayer] 이미 삽화를 표시하고 있습니다.", this);
            yield break;
        }

        Succeeded = false;
        if (story == null)
        {
            Debug.LogError("[IllustratedStoryPlayer] illustrated_story_missing", this);
            yield break;
        }
        if (!story.TryValidate(out string error))
        {
            Debug.LogError("[IllustratedStoryPlayer] " + error, this);
            yield break;
        }
        if (!isActiveAndEnabled)
        {
            Debug.LogError("[IllustratedStoryPlayer] illustrated_story_owner_disabled", this);
            yield break;
        }

        int run = ++_runVersion;
        IsPlaying = true;
        AcquireCutsceneState();
        try
        {
            CreateView();
            for (int i = 0; i < story.Pages.Count && IsCurrent(run); i++)
            {
                _page = story.Pages[i];
                _pageElapsed = 0f;
                _lastSprite = null;
                _pageGroup.alpha = 0f;
                ConfigurePicture(_page);
                if (!RefreshPicture()) yield break;
                PageStarted?.Invoke(i);

                yield return FadePage(1f, _page.FadeInDuration, run);
                if (!IsCurrent(run)) yield break;

                if (_page.Dialogue != null)
                {
                    _dialogueOwner = DialogueManager.Instance;
                    if (_dialogueOwner == null)
                    {
                        Fail("illustrated_story_dialogue_manager_missing");
                        yield break;
                    }
                    if (_narration == null)
                        _narration = _dialogueOwner.CreateNarrationPanel(_pageGroup.transform);
                    if (_narration == null)
                    {
                        Fail("illustrated_story_narration_panel_missing");
                        yield break;
                    }

                    _narration.SetNarrationCharactersPerSecond(story.NarrationCharactersPerSecond);
                    _narration.SetTypingSoundMuted(_page.MuteTypingSound);
                    _dialogueFinished = false;
                    _dialogueCancelled = false;
                    bool started = _dialogueOwner.TryStartDialogueOnPanel(_page.Dialogue, _narration,
                        () => _dialogueFinished = true, () => _dialogueCancelled = true,
                        out _dialogueGeneration);
                    if (!started)
                    {
                        Fail("illustrated_story_dialogue_busy_or_invalid");
                        yield break;
                    }
                    while (IsCurrent(run) && !_dialogueFinished && !_dialogueCancelled)
                    {
                        if (_dialogueOwner == null || _narration == null || !_narration.isActiveAndEnabled)
                        {
                            Fail("illustrated_story_dialogue_owner_lost");
                            yield break;
                        }
                        yield return null;
                    }
                    if (!IsCurrent(run) || _dialogueCancelled) yield break;
                    _dialogueGeneration = 0;
                    // Logical completion precedes the panel's own fade-out.
                    while (IsCurrent(run) && _narration != null && _narration.isActiveAndEnabled)
                        yield return null;
                }

                float minimum = GetMinimumPageDuration();
                while (IsCurrent(run) && _pageElapsed < minimum) yield return null;
                if (!IsCurrent(run)) yield break;

                if (_page.Dialogue == null && _page.WaitForConfirmWithoutDialogue)
                {
                    // A confirm that closed the preceding page must not also dismiss this one.
                    int openedFrame = Time.frameCount;
                    do { yield return null; }
                    while (IsCurrent(run) && (Time.frameCount <= openedFrame || IsPaused
                        || !GameInput.DialogueAdvancePressed));
                    if (!IsCurrent(run)) yield break;
                    ConsumeTransitionInput();
                }

                yield return FadePage(0f, _page.FadeOutDuration, run);
            }
            if (IsCurrent(run)) Succeeded = true;
        }
        finally
        {
            if (run == _runVersion)
            {
                Cleanup();
                IsPlaying = false;
            }
        }
    }

    public void Cancel()
    {
        if (!IsPlaying && _overlay == null && _dialogueGeneration == 0) return;
        Succeeded = false;
        ++_runVersion;
        IsPlaying = false;
        Cleanup();
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (_overlay == null || _picture == null || _caption == null) { Cancel(); return; }
        bool paused = IsPaused;
        if (_fadeTween != null && _fadeTween.IsActive())
        {
            if (paused && !_tweenPaused) { _fadeTween.Pause(); _tweenPaused = true; }
            else if (!paused && _tweenPaused) { _fadeTween.Play(); _tweenPaused = false; }
        }
        if (paused || _page == null) return;
        _pageElapsed += Time.unscaledDeltaTime;
        if (_page.IsCaptionPage) UpdateCaption();
        if (_page.Animation != null && !RefreshPicture()) Cancel();
    }

    private static bool IsPaused => GameInput.IsConfigModalActive
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused);

    private bool IsCurrent(int run) => IsPlaying && run == _runVersion
        && isActiveAndEnabled && _overlay != null;

    private void CreateView()
    {
        _overlay = new GameObject("Illustrated Story", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        int uiLayer = LayerMask.NameToLayer("UI");
        _overlay.layer = uiLayer >= 0 ? uiLayer : 0;
        Canvas canvas = _overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        var viewport = _overlay.AddComponent<FixedCanvasViewport>();
        viewport.Configure(Camera.main);

        Image background = CreateImage("Black Background", viewport.ContentRect);
        Stretch(background.rectTransform);
        background.color = Color.black;
        RectTransform pageRoot = CreateRect("Page", viewport.ContentRect);
        Stretch(pageRoot);
        _pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
        _pageGroup.interactable = false;
        _pageGroup.blocksRaycasts = false;

        _pictureMask = CreateRect("Picture Canvas", pageRoot);
        _pictureMask.anchorMin = _pictureMask.anchorMax = new Vector2(0.5f, 0.5f);
        _pictureMask.anchoredPosition = new Vector2(0f, 60f);
        _pictureMask.gameObject.AddComponent<RectMask2D>();
        _picture = CreateImage("Picture", _pictureMask);
        _picture.type = Image.Type.Simple;
        _picture.preserveAspect = false;

        RectTransform captionRect = CreateRect("Centered Caption", pageRoot);
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(.5f, .5f);
        captionRect.sizeDelta = new Vector2(560f, 128f);
        captionRect.anchoredPosition = Vector2.zero;
        _caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
        _caption.font = GameContentCatalog.Instance != null
            ? GameContentCatalog.Instance.DefaultUiFont : TMP_Settings.defaultFontAsset;
        _caption.fontSize = 40f;
        _caption.enableAutoSizing = false;
        _caption.alignment = TextAlignmentOptions.Center;
        _caption.color = Color.white;
        _caption.raycastTarget = false;
    }

    private void ConfigurePicture(IllustratedStoryPage page)
    {
        _pictureMask.gameObject.SetActive(!page.IsCaptionPage);
        _caption.gameObject.SetActive(page.IsCaptionPage);
        _caption.text = page.IsCaptionPage
            ? LocalizationManager.Localize(page.CaptionLocalizationKey, page.Caption) : string.Empty;
        _caption.maxVisibleCharacters = int.MaxValue;
        _captionCharacterCount = 0;
        _lastCaptionVisible = 0;
        if (page.IsCaptionPage)
        {
            _picture.sprite = null;
            // 번역된 전체 문구를 한 번만 분석합니다. 매 프레임 문자열을 잘라 만들지 않습니다.
            _caption.ForceMeshUpdate(ignoreActiveState: true);
            _captionCharacterCount = _caption.textInfo.characterCount;
            _caption.maxVisibleCharacters = 0;
            return;
        }
        Vector2 size = page.Animation != null
            ? new Vector2(page.CanvasSize.x, page.CanvasSize.y) : page.Picture.rect.size;
        _pictureScale = page.FullScreen
            ? Mathf.Max(640f / size.x, 480f / size.y) : page.PixelScale;
        _pictureMask.sizeDelta = page.FullScreen ? new Vector2(640f, 480f) : size * _pictureScale;
        _pictureMask.anchoredPosition = page.FullScreen ? Vector2.zero : new Vector2(0f, 60f);
        _picture.rectTransform.anchorMin = _picture.rectTransform.anchorMax = page.Animation != null
            ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
    }

    private void UpdateCaption()
    {
        if (_page == null || !_page.IsCaptionPage || _caption == null || IsPaused) return;
        int visible = Mathf.Clamp(Mathf.FloorToInt(_pageElapsed * _page.CaptionCharactersPerSecond),
            0, _captionCharacterCount);
        if (visible == _lastCaptionVisible) return;
        _caption.maxVisibleCharacters = visible;
        _lastCaptionVisible = visible;
    }

    private float GetMinimumPageDuration()
    {
        float minimum = _page.MinimumDuration;
        if (_page.IsCaptionPage)
        {
            float captionDuration = _captionCharacterCount / _page.CaptionCharactersPerSecond + _page.CaptionHoldDuration;
            minimum = Mathf.Max(minimum, captionDuration);
        }
        else if (_page.Animation != null && !_page.LoopAnimation)
        {
            minimum = Mathf.Max(minimum, _page.Animation.length);
        }
        return minimum;
    }

    private bool RefreshPicture()
    {
        if (_page.IsCaptionPage) return true;
        Sprite sprite = _page.Picture;
        if (_page.Animation != null)
        {
            // 비 Legacy 클립의 PPtr 평가/Animator 초기화 없이 미리 추출한 원본 프레임을 표시합니다.
            sprite = _page.ResolveAnimationSprite(_pageElapsed);
        }
        if (sprite == null)
        {
            Fail("illustrated_story_sprite_missing: " + (_page.Animation != null ? _page.Animation.name : "picture"));
            return false;
        }
        if (sprite == _lastSprite) return true;
        _lastSprite = sprite;
        _picture.sprite = sprite;
        RectTransform rect = _picture.rectTransform;
        Vector2 size = sprite.rect.size;
        rect.pivot = _page.Animation != null
            ? new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y) : new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size * _pictureScale;
        rect.anchoredPosition = Vector2.zero;
        Texture texture = sprite.texture;
        if (texture != null && texture.filterMode != FilterMode.Point && !_previousFilters.ContainsKey(texture))
        {
            _previousFilters.Add(texture, texture.filterMode);
            texture.filterMode = FilterMode.Point;
        }
        return true;
    }

    private IEnumerator FadePage(float alpha, float duration, int run)
    {
        _fadeTween?.Kill(false);
        _fadeTween = null;
        _tweenPaused = false;
        if (!IsCurrent(run)) yield break;
        if (duration <= 0f) { _pageGroup.alpha = alpha; yield break; }
        _fadeTween = _pageGroup.DOFade(alpha, duration).SetEase(Ease.InOutSine).SetRecyclable(false).SetAutoKill(true)
            .SetUpdate(true).SetLink(_overlay, LinkBehaviour.KillOnDestroy);
        if (IsPaused) { _fadeTween.Pause(); _tweenPaused = true; }
        while (IsCurrent(run) && _fadeTween != null && _fadeTween.IsActive()) yield return null;
        _fadeTween = null;
    }

    private void AcquireCutsceneState()
    {
        GameStateManager state = GameStateManager.Instance;
        _ownsCutsceneState = state != null && state.CurrentState != GameState.Cutscene;
        if (!_ownsCutsceneState) return;
        _stateBeforeStory = state.CurrentState;
        state.ChangeState(GameState.Cutscene);
    }

    private void Cleanup()
    {
        _fadeTween?.Kill(false);
        _fadeTween = null;
        _tweenPaused = false;
        int dialogueGeneration = _dialogueGeneration;
        _dialogueGeneration = 0;
        if (_dialogueOwner != null && dialogueGeneration > 0)
            _dialogueOwner.CancelDialogue(dialogueGeneration);
        if (_narration != null) _narration.HideImmediate();
        _narration = null;
        _dialogueOwner = null;
        if (_overlay != null) { _overlay.SetActive(false); Destroy(_overlay); }
        _overlay = null;
        _picture = null;
        _caption = null;
        _pageGroup = null;
        _pictureMask = null;
        _lastSprite = null;
        _page = null;
        _captionCharacterCount = 0;
        _lastCaptionVisible = 0;
        foreach (KeyValuePair<Texture, FilterMode> pair in _previousFilters)
            if (pair.Key != null && pair.Key.filterMode == FilterMode.Point) pair.Key.filterMode = pair.Value;
        _previousFilters.Clear();
        GameStateManager state = GameStateManager.Instance;
        if (_ownsCutsceneState && state != null && state.CurrentState == GameState.Cutscene)
            state.ChangeState(_stateBeforeStory);
        _ownsCutsceneState = false;
        ConsumeTransitionInput();
    }

    private void Fail(string error) => Debug.LogError("[IllustratedStoryPlayer] " + error, this);

    private static void ConsumeTransitionInput()
    {
        if (!Application.isPlaying) return;
        GameInput.SuppressPlayerConfirmForCurrentFrame();
        GameInput.SuppressMenuInputsForCurrentFrame();
        GameInput.ConsumeBattleUIInput();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
}
