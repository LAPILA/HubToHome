using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum BattleSpeechTrigger
{
    TurnStart,
    SkillUse,
    DamageTaken,
    LowHp,
    BattleFlavor
}

public enum BattleSpeechBubbleDirection
{
    Up,
    Left,
    Right
}

[System.Serializable]
public class BattleSpeechRule
{
    [BoxGroup("Trigger"), LabelText("Trigger")]
    public BattleSpeechTrigger Trigger = BattleSpeechTrigger.BattleFlavor;

    [BoxGroup("Trigger"), LabelText("Chance")]
    [Range(0f, 1f)] public float Chance = 0.35f;

    [BoxGroup("Condition"), LabelText("Min Battle Turn")]
    [MinValue(0)] public int MinBattleTurn = 0;

    [BoxGroup("Condition"), LabelText("HP Ratio Below")]
    [Range(0f, 1f)] public float HpRatioBelow = 1f;

    [BoxGroup("Condition"), LabelText("Skill ID Filter")]
    [Tooltip("Empty means every skill can match. Used only by SkillUse triggers.")]
    public string SkillIdFilter = "";

    [BoxGroup("Condition"), LabelText("Trigger Once")]
    public bool TriggerOnce = false;

    [BoxGroup("Bubble"), LabelText("Direction")]
    public BattleSpeechBubbleDirection Direction = BattleSpeechBubbleDirection.Up;

    [BoxGroup("Text"), TextArea(2, 4)]
    [Tooltip("Available tokens: {actor}, {target}, {skill}, {turn}, {hp}, {maxHp}")]
    public string Text = "...";

    [HideInInspector] public bool HasTriggered;

    public BattleSpeechRule CloneRuntime()
    {
        return new BattleSpeechRule
        {
            Trigger = Trigger,
            Chance = Chance,
            MinBattleTurn = MinBattleTurn,
            HpRatioBelow = HpRatioBelow,
            SkillIdFilter = SkillIdFilter,
            TriggerOnce = TriggerOnce,
            Direction = Direction,
            Text = Text,
            HasTriggered = false
        };
    }
}

[DisallowMultipleComponent]
public class BattleSpeechBubble : MonoBehaviour
{
    private const int TailStencilRef = 13;

    [BoxGroup("UI References"), SerializeField] private CanvasGroup _canvasGroup;
    [BoxGroup("UI References"), SerializeField] private RectTransform _bubbleRoot;
    [BoxGroup("UI References"), SerializeField] private RectTransform _boxRoot;
    [BoxGroup("UI References"), SerializeField] private Image _bubbleImage;
    [BoxGroup("UI References"), SerializeField] private RectTransform _tailRoot;
    [BoxGroup("UI References"), SerializeField] private Image _tailImage;
    [BoxGroup("UI References"), SerializeField] private TextMeshProUGUI _speechText;
    [BoxGroup("UI References"), SerializeField] private TypewriterComponent _typewriter;
    [BoxGroup("UI References"), SerializeField] private LayoutElement _layoutElement;

    [BoxGroup("Sprites"), SerializeField] private BattleSpeechBubbleDirection _defaultDirection = BattleSpeechBubbleDirection.Up;
    [BoxGroup("Sprites"), FormerlySerializedAs("_upBubbleSprite")]
    [SerializeField] private Sprite _bubbleBodySprite;
    [BoxGroup("Sprites"), SerializeField] private Sprite _downTailSprite;
    [BoxGroup("Sprites"), FormerlySerializedAs("_leftBubbleSprite")]
    [SerializeField] private Sprite _leftTailSprite;
    [BoxGroup("Sprites"), FormerlySerializedAs("_rightBubbleSprite")]
    [SerializeField] private Sprite _rightTailSprite;

    [BoxGroup("Sizing"), LabelText("Horizontal Text Margin")]
    [FormerlySerializedAs("_sideHorizontalTextMargin")]
    [SerializeField] private float _horizontalTextMargin = 40f;
    [BoxGroup("Sizing"), LabelText("Vertical Text Margins (Top, Bottom)")]
    [SerializeField] private Vector2 _verticalTextMargins = new Vector2(6f, 6f);
    [BoxGroup("Sizing"), SerializeField] private float _sideTailSize = 32f;
    [BoxGroup("Sizing"), SerializeField] private float _topTailSize = 32f;
    [BoxGroup("Sizing"), SerializeField] private Vector2 _minSize = new Vector2(120f, 56f);
    [BoxGroup("Sizing"), SerializeField] private Vector2 _maxSize = new Vector2(720f, 540f);

    [BoxGroup("Positioning"), SerializeField] private string _sidePivotName = "FrontMiddle";
    [BoxGroup("Positioning"), SerializeField] private string _topPivotName = "Top";
    [BoxGroup("Positioning"), SerializeField] private float _sideGap = -0.4f;
    [BoxGroup("Positioning"), SerializeField] private float _topGap = -0.2f;
    [BoxGroup("Positioning"), SerializeField] private Vector3 _sideOffset = Vector3.zero;
    [BoxGroup("Positioning"), SerializeField] private Vector3 _topOffset = Vector3.zero;

    [BoxGroup("Timing"), SerializeField] private float _defaultHoldDuration = 1.7f;
    [BoxGroup("Timing"), SerializeField] private float _fadeDuration = 0.16f;
    [BoxGroup("Timing"), SerializeField] private float _popScale = 1.08f;
    [BoxGroup("Timing"), SerializeField] private bool _allowConfirmSkip = true;

    [BoxGroup("Rules"), ListDrawerSettings(ShowIndexLabels = true)]
    [SerializeField] private BattleSpeechConfig _config;
    [BoxGroup("Rules"), ListDrawerSettings(ShowIndexLabels = true)]
    [SerializeField] private List<BattleSpeechRule> _rules = new List<BattleSpeechRule>();

    private Coroutine _hideRoutine;
    private Vector3 _baseScale = Vector3.one;
    private List<BattleSpeechRule> _runtimeRules;
    private bool _isShowing;
    private bool _confirmWasDown;
    private Image _tailStencilWriteImage;
    private Image _tailStencilClearImage;
    private Material _bodyStencilMaterial;
    private Material _tailStencilWriteMaterial;
    private Material _tailStencilClearMaterial;

    public bool IsShowing => _isShowing;

    private void Awake()
    {
        if (_bubbleRoot == null) _bubbleRoot = transform as RectTransform;
        if (_boxRoot == null) _boxRoot = _bubbleRoot;
        if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (_bubbleImage == null && _boxRoot != null) _bubbleImage = _boxRoot.GetComponent<Image>();
        if (_bubbleImage == null) _bubbleImage = GetComponentInChildren<Image>(true);
        if (_tailImage == null) _tailImage = FindTailImage();
        if (_tailRoot == null && _tailImage != null) _tailRoot = _tailImage.rectTransform;
        if (_speechText == null) _speechText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (_typewriter == null) _typewriter = GetComponentInChildren<TypewriterComponent>(true);
        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);
        if (_layoutElement == null && _boxRoot != null) _layoutElement = _boxRoot.GetComponent<LayoutElement>();

        EnsureTailStencilCutout();
        DisableTmpAutomaticWrapping();
        NormalizeBoxRect(_defaultDirection);
        NormalizeTextRect();
        if (_bubbleRoot != null) _baseScale = _bubbleRoot.localScale;
        RebuildRuntimeRules();
        HideImmediate();
    }

    private void OnDisable()
    {
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = null;
        _isShowing = false;
        _confirmWasDown = false;
        KillPresentationTweens();
    }

    private void OnDestroy()
    {
        KillPresentationTweens();
        DestroyRuntimeMaterial(_bodyStencilMaterial);
        DestroyRuntimeMaterial(_tailStencilWriteMaterial);
        DestroyRuntimeMaterial(_tailStencilClearMaterial);
    }

    public bool TryShow(
        BattleSpeechTrigger trigger,
        CharacterBase actor,
        SkillData skill = null,
        CharacterBase target = null,
        int battleTurn = 0,
        float holdOverride = -1f,
        BattleSpeechBubbleDirection? directionOverride = null)
    {
        BattleSpeechRule rule = PickRule(trigger, actor, skill, battleTurn);
        if (rule == null) return false;

        if (rule.TriggerOnce) rule.HasTriggered = true;
        Show(Format(rule.Text, actor, skill, target, battleTurn), holdOverride > 0f ? holdOverride : _defaultHoldDuration, directionOverride ?? rule.Direction, actor);
        return true;
    }

    public void Show(string text, float holdDuration = -1f)
    {
        Show(text, holdDuration, _defaultDirection);
    }

    public void Show(string text, float holdDuration, BattleSpeechBubbleDirection direction)
    {
        Show(text, holdDuration, direction, GetComponentInParent<CharacterBase>());
    }

    public void Show(string text, float holdDuration, BattleSpeechBubbleDirection direction, CharacterBase actor)
    {
        if (string.IsNullOrWhiteSpace(text) || _speechText == null) return;

        gameObject.SetActive(true);
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _isShowing = true;
        _confirmWasDown = IsConfirmPressed();

        NormalizeBoxRect(direction);
        ApplyBubbleSprites(direction);
        DisableTmpAutomaticWrapping();
        string wrappedText = WrapTextForBubble(text);
        ApplyText(wrappedText);
        Vector2 size = ResizeToText(wrappedText, direction);
        PositionTail(direction, size);
        SyncTailStencilCutout();
        PositionForActor(actor, direction);
        PlayShowTween();

        _hideRoutine = StartCoroutine(CoHideAfter(holdDuration > 0f ? holdDuration : _defaultHoldDuration));
    }

    public void HideImmediate()
    {
        KillPresentationTweens();
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        if (_bubbleRoot != null)
            _bubbleRoot.localScale = _baseScale;

        if (_speechText != null) _speechText.text = string.Empty;
        _isShowing = false;
        _confirmWasDown = false;
        gameObject.SetActive(false);
    }

    private void KillPresentationTweens()
    {
        if (_canvasGroup != null)
            _canvasGroup.DOKill(false);
        if (_bubbleRoot != null)
            _bubbleRoot.DOKill(false);
    }

    public IEnumerator WaitUntilHidden()
    {
        while (_isShowing)
            yield return null;
    }

    private BattleSpeechRule PickRule(BattleSpeechTrigger trigger, CharacterBase actor, SkillData skill, int battleTurn)
    {
        List<BattleSpeechRule> rules = _runtimeRules;
        if (rules == null || rules.Count == 0) return null;

        List<BattleSpeechRule> candidates = null;
        for (int i = 0; i < rules.Count; i++)
        {
            BattleSpeechRule rule = rules[i];
            if (rule == null || rule.Trigger != trigger) continue;
            if (rule.TriggerOnce && rule.HasTriggered) continue;
            if (battleTurn < rule.MinBattleTurn) continue;
            if (actor != null && actor.MaxHP > 0 && ((float)actor.CurrentHP / actor.MaxHP) > rule.HpRatioBelow) continue;
            if (!string.IsNullOrWhiteSpace(rule.SkillIdFilter) && (skill == null || skill.SkillID != rule.SkillIdFilter)) continue;
            if (UnityEngine.Random.value > rule.Chance) continue;

            candidates ??= new List<BattleSpeechRule>();
            candidates.Add(rule);
        }

        if (candidates == null || candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void RebuildRuntimeRules()
    {
        List<BattleSpeechRule> source = _config != null && _config.Rules != null && _config.Rules.Count > 0
            ? _config.Rules
            : _rules;

        _runtimeRules = new List<BattleSpeechRule>();
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            BattleSpeechRule rule = source[i];
            if (rule != null)
                _runtimeRules.Add(rule.CloneRuntime());
        }
    }

    private void ApplyBubbleSprites(BattleSpeechBubbleDirection direction)
    {
        if (_bubbleImage != null)
        {
            if (_bubbleBodySprite != null)
                _bubbleImage.sprite = _bubbleBodySprite;

            _bubbleImage.type = Image.Type.Sliced;
            _bubbleImage.preserveAspect = false;
        }

        if (_tailImage == null) return;

        Sprite tailSprite = GetTailSprite(direction);
        _tailImage.gameObject.SetActive(tailSprite != null);
        if (tailSprite == null) return;

        _tailImage.sprite = tailSprite;
        _tailImage.type = Image.Type.Simple;
        _tailImage.preserveAspect = true;
    }

    private Sprite GetTailSprite(BattleSpeechBubbleDirection direction)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
                return _rightTailSprite != null ? _rightTailSprite : _downTailSprite;
            case BattleSpeechBubbleDirection.Right:
                return _leftTailSprite != null ? _leftTailSprite : _downTailSprite;
            default:
                return _downTailSprite;
        }
    }

    private void ApplyText(string text)
    {
        if (_typewriter != null)
            _typewriter.ShowText(text);
        else
            _speechText.text = text;
    }

    private void DisableTmpAutomaticWrapping()
    {
        if (_speechText == null) return;

        // SmartTextWrapper owns line breaks here; TMP auto wrapping can split Korean tokens like "있어.".
        _speechText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private string WrapTextForBubble(string text)
    {
        if (_speechText == null || string.IsNullOrWhiteSpace(text))
            return text;

        // Shared dialogue wrapping rule: keep words together first, then let the box resize from the wrapped text.
        float maxWidth = Mathf.Max(_minSize.x, _maxSize.x);
        float textMaxWidth = Mathf.Max(1f, maxWidth - GetTextExtraSize().x);
        return SmartTextWrapper.Wrap(_speechText, text, textMaxWidth);
    }

    private Vector2 ResizeToText(string text, BattleSpeechBubbleDirection direction)
    {
        if (_speechText == null || string.IsNullOrWhiteSpace(text))
            return _minSize;

        _speechText.text = text;
        _speechText.ForceMeshUpdate();

        float maxWidth = Mathf.Max(_minSize.x, _maxSize.x);
        Vector2 extraSize = GetTextExtraSize();
        float textMaxWidth = Mathf.Max(1f, maxWidth - extraSize.x);

        Vector2 preferred = _speechText.GetPreferredValues(text, textMaxWidth, Mathf.Infinity);
        Vector2 size = new Vector2(
            Mathf.Clamp(preferred.x + extraSize.x, _minSize.x, maxWidth),
            Mathf.Max(preferred.y + extraSize.y, _minSize.y));
        BattleSpeechBubbleLayoutResult layout = GetLayout(direction, size);

        if (_layoutElement != null)
        {
            _layoutElement.preferredWidth = size.x;
            _layoutElement.preferredHeight = size.y;
        }

        if (_boxRoot != null)
        {
            _boxRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _boxRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        RectTransform textRect = _speechText.rectTransform;
        if (textRect != null)
        {
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layout.TextSize.x);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.TextSize.y);
            textRect.anchoredPosition = layout.TextAnchoredPosition;
        }

        Canvas.ForceUpdateCanvases();
        return size;
    }

    private Vector2 GetTextExtraSize()
    {
        return new Vector2(
            GetMarginLeft() + GetMarginRight(),
            GetMarginTop() + GetMarginBottom());
    }

    private float GetMarginLeft()
    {
        return Mathf.Max(0f, _horizontalTextMargin);
    }

    private float GetMarginTop()
    {
        return Mathf.Max(0f, _verticalTextMargins.x);
    }

    private float GetMarginRight()
    {
        return Mathf.Max(0f, _horizontalTextMargin);
    }

    private float GetMarginBottom()
    {
        return Mathf.Max(0f, _verticalTextMargins.y);
    }

    private void NormalizeBoxRect(BattleSpeechBubbleDirection direction)
    {
        if (_boxRoot == null) return;

        _boxRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _boxRoot.anchorMax = new Vector2(0.5f, 0.5f);
        BattleSpeechBubbleLayoutResult layout = GetLayout(direction, _minSize);
        _boxRoot.pivot = layout.BoxPivot;
        _boxRoot.anchoredPosition = layout.BoxAnchoredPosition;
    }

    private void NormalizeTextRect()
    {
        if (_speechText == null) return;

        RectTransform textRect = _speechText.rectTransform;
        if (textRect == null) return;

        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
    }

    private void PositionTail(BattleSpeechBubbleDirection direction, Vector2 boxSize)
    {
        if (_tailRoot == null) return;

        _tailRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _tailRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _tailRoot.pivot = new Vector2(0.5f, 0.5f);

        BattleSpeechBubbleLayoutResult layout = GetLayout(direction, boxSize);
        SetTailSize(layout.TailSize.x, layout.TailSize.y);
        _tailRoot.localScale = new Vector3(layout.TailScale.x, layout.TailScale.y, 1f);
        _tailRoot.anchoredPosition = layout.TailAnchoredPosition;
    }

    private void SetTailSize(float width, float height)
    {
        if (_tailRoot == null) return;

        _tailRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, width));
        _tailRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height));
    }

    private void EnsureTailStencilCutout()
    {
        if (_bubbleRoot == null || _boxRoot == null || _bubbleImage == null || _tailRoot == null || _tailImage == null)
            return;

        EnsureTailStencilMaterials();

        if (_tailStencilWriteImage == null)
            _tailStencilWriteImage = CreateTailStencilImage("TailStencilWrite", _tailStencilWriteMaterial);

        if (_tailStencilClearImage == null)
            _tailStencilClearImage = CreateTailStencilImage("TailStencilClear", _tailStencilClearMaterial);

        _bubbleImage.material = _bodyStencilMaterial;
        SetTailStencilSiblingOrder();
        SyncTailStencilCutout();
    }

    private void EnsureTailStencilMaterials()
    {
        if (_bodyStencilMaterial != null && _tailStencilWriteMaterial != null && _tailStencilClearMaterial != null)
            return;

        Material baseMaterial = Graphic.defaultGraphicMaterial;
        _bodyStencilMaterial = CreateStencilMaterial(
            "BattleSpeech_BodySkipTailStencil",
            baseMaterial,
            TailStencilRef,
            CompareFunction.NotEqual,
            StencilOp.Keep,
            ColorWriteMask.All,
            useAlphaClip: false);

        _tailStencilWriteMaterial = CreateStencilMaterial(
            "BattleSpeech_TailStencilWrite",
            baseMaterial,
            TailStencilRef,
            CompareFunction.Always,
            StencilOp.Replace,
            0,
            useAlphaClip: true);

        _tailStencilClearMaterial = CreateStencilMaterial(
            "BattleSpeech_TailStencilClear",
            baseMaterial,
            0,
            CompareFunction.Always,
            StencilOp.Replace,
            0,
            useAlphaClip: true);
    }

    private static Material CreateStencilMaterial(
        string materialName,
        Material baseMaterial,
        int stencilRef,
        CompareFunction stencilComp,
        StencilOp stencilOp,
        ColorWriteMask colorMask,
        bool useAlphaClip)
    {
        Material material = new Material(baseMaterial)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };

        material.SetFloat("_Stencil", stencilRef);
        material.SetFloat("_StencilComp", (float)stencilComp);
        material.SetFloat("_StencilOp", (float)stencilOp);
        material.SetFloat("_StencilReadMask", 255f);
        material.SetFloat("_StencilWriteMask", 255f);
        material.SetFloat("_ColorMask", (float)colorMask);
        material.SetFloat("_UseUIAlphaClip", useAlphaClip ? 1f : 0f);

        if (useAlphaClip)
            material.EnableKeyword("UNITY_UI_ALPHACLIP");
        else
            material.DisableKeyword("UNITY_UI_ALPHACLIP");

        return material;
    }

    private Image CreateTailStencilImage(string objectName, Material material)
    {
        GameObject stencilObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stencilObject.layer = gameObject.layer;
        stencilObject.transform.SetParent(_bubbleRoot, false);

        Image image = stencilObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.maskable = false;
        image.material = material;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.canvasRenderer.cullTransparentMesh = false;
        return image;
    }

    private void SetTailStencilSiblingOrder()
    {
        if (_tailStencilWriteImage == null || _tailStencilClearImage == null || _boxRoot == null || _tailRoot == null)
            return;

        _tailStencilWriteImage.rectTransform.SetSiblingIndex(0);
        _boxRoot.SetSiblingIndex(1);
        _tailStencilClearImage.rectTransform.SetSiblingIndex(2);
        _tailRoot.SetSiblingIndex(3);
    }

    private void SyncTailStencilCutout()
    {
        if (_tailRoot == null || _tailImage == null || _tailStencilWriteImage == null || _tailStencilClearImage == null)
            return;

        SyncTailStencilImage(_tailStencilWriteImage);
        SyncTailStencilImage(_tailStencilClearImage);
    }

    private void SyncTailStencilImage(Image stencilImage)
    {
        RectTransform stencilRect = stencilImage.rectTransform;
        stencilImage.sprite = _tailImage.sprite;
        stencilImage.type = _tailImage.type;
        stencilImage.preserveAspect = _tailImage.preserveAspect;
        stencilImage.gameObject.SetActive(_tailImage.gameObject.activeSelf && _tailImage.sprite != null);

        stencilRect.anchorMin = _tailRoot.anchorMin;
        stencilRect.anchorMax = _tailRoot.anchorMax;
        stencilRect.pivot = _tailRoot.pivot;
        stencilRect.anchoredPosition = _tailRoot.anchoredPosition;
        stencilRect.sizeDelta = _tailRoot.sizeDelta;
        stencilRect.localScale = _tailRoot.localScale;
        stencilRect.localRotation = _tailRoot.localRotation;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null) return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

    private BattleSpeechBubbleLayoutResult GetLayout(BattleSpeechBubbleDirection direction, Vector2 boxSize)
    {
        return BattleSpeechBubbleLayout.Calculate(
            direction,
            boxSize,
            _horizontalTextMargin,
            _verticalTextMargins,
            _sideTailSize,
            _topTailSize);
    }

    private Image FindTailImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image != _bubbleImage && image.name == "Tail")
                return image;
        }

        return null;
    }

    private void PositionForActor(CharacterBase actor, BattleSpeechBubbleDirection direction)
    {
        if (actor == null || _bubbleRoot == null) return;

        Transform pivot = direction == BattleSpeechBubbleDirection.Up
            ? actor.GetPivot(_topPivotName)
            : actor.GetPivot(_sidePivotName);

        if (pivot == null) return;

        Vector3 offset = GetWorldOffset(direction);
        _bubbleRoot.position = pivot.position + offset;
    }

    private Vector3 GetWorldOffset(BattleSpeechBubbleDirection direction)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
                return new Vector3(-_sideGap, 0f, 0f) + _sideOffset;
            case BattleSpeechBubbleDirection.Right:
                return new Vector3(_sideGap, 0f, 0f) + _sideOffset;
            default:
                return new Vector3(0f, _topGap + _topOffset.y, _topOffset.z);
        }
    }

    private void PlayShowTween()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, _fadeDuration).SetUpdate(true);
        }

        if (_bubbleRoot != null)
        {
            _bubbleRoot.DOKill();
            _bubbleRoot.localScale = _baseScale * 0.88f;
            _bubbleRoot.DOScale(_baseScale * _popScale, _fadeDuration).SetEase(Ease.OutBack).SetUpdate(true)
                .OnComplete(() => _bubbleRoot.DOScale(_baseScale, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true));
        }
    }

    private IEnumerator CoHideAfter(float holdDuration)
    {
        float remaining = Mathf.Max(0f, holdDuration);
        while (remaining > 0f)
        {
            if (_allowConfirmSkip && ConsumeConfirmPress())
                break;

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_canvasGroup != null)
            yield return _canvasGroup.DOFade(0f, _fadeDuration).SetUpdate(true).WaitForCompletion();

        HideImmediate();
        _hideRoutine = null;
    }

    private bool ConsumeConfirmPress()
    {
        bool isDown = IsConfirmPressed();
        bool pressedThisFrame = isDown && !_confirmWasDown;
        _confirmWasDown = isDown;
        return pressedThisFrame;
    }

    private static bool IsConfirmPressed()
    {
        return GameInput.BattleConfirmPressed || GameInput.DialogueAdvancePressed || GameInput.ConfirmPressed;
    }

    private static string Format(string template, CharacterBase actor, SkillData skill, CharacterBase target, int battleTurn)
    {
        string actorName = BattleNarrationFormatter.ActorName(actor);
        string targetName = target != null ? BattleNarrationFormatter.ActorName(target) : string.Empty;
        string skillName = skill != null && !string.IsNullOrWhiteSpace(skill.SkillName) ? skill.SkillName : string.Empty;

        return (template ?? string.Empty)
            .Replace("{actor}", actorName)
            .Replace("{target}", targetName)
            .Replace("{skill}", skillName)
            .Replace("{turn}", battleTurn.ToString())
            .Replace("{hp}", actor != null ? actor.CurrentHP.ToString() : "0")
            .Replace("{maxHp}", actor != null ? actor.MaxHP.ToString() : "0");
    }
}
