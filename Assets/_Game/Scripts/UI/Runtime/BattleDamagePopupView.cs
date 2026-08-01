using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

[Serializable]
public struct BattleDamagePopupAnimationSettings
{
    public float LaunchDuration;
    public float SettleDuration;
    public float HoldDuration;
    public float FadeDuration;
    public Vector2 LaunchOffset;
    public Vector2 SettleOffset;
    public Vector2 FadeOffset;
    public float CriticalGrowDuration;
    public float CriticalStartScale;
    public float CriticalEndScale;

    public static BattleDamagePopupAnimationSettings Default => new BattleDamagePopupAnimationSettings
    {
        LaunchDuration = 0.16f,
        SettleDuration = 0.12f,
        HoldDuration = 0.30f,
        FadeDuration = 0.24f,
        LaunchOffset = new Vector2(18f, 30f),
        SettleOffset = new Vector2(24f, 22f),
        FadeOffset = new Vector2(30f, 42f),
        CriticalGrowDuration = 0.09f,
        CriticalStartScale = 0.45f,
        CriticalEndScale = 1.65f
    };

    public BattleDamagePopupAnimationSettings Sanitized()
    {
        BattleDamagePopupAnimationSettings value = this;
        value.LaunchDuration = Mathf.Max(0.01f, value.LaunchDuration);
        value.SettleDuration = Mathf.Max(0.01f, value.SettleDuration);
        value.HoldDuration = Mathf.Max(0.30f, value.HoldDuration);
        value.FadeDuration = Mathf.Max(0.01f, value.FadeDuration);
        value.CriticalGrowDuration = Mathf.Clamp(value.CriticalGrowDuration, 0.01f, value.LaunchDuration);
        value.CriticalStartScale = Mathf.Max(0.01f, value.CriticalStartScale);
        value.CriticalEndScale = Mathf.Max(value.CriticalStartScale, value.CriticalEndScale);
        return value;
    }
}

[DisallowMultipleComponent]
public sealed class BattleDamagePopupView : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private CanvasGroup _canvasGroup;

    private Sequence _sequence;
    private Action<BattleDamagePopupView> _release;
    private bool _completionIssued;

    public RectTransform PopupRect => _rectTransform;
    public TMP_Text Label => _label;
    public CanvasGroup CanvasGroup => _canvasGroup;
    public Sequence ActiveSequence => _sequence;

    public void Initialize(
        RectTransform rectTransform,
        TextMeshProUGUI label,
        CanvasGroup canvasGroup,
        TMP_FontAsset font,
        float fontSize,
        float outlineWidth)
    {
        _rectTransform = rectTransform != null ? rectTransform : transform as RectTransform;
        _label = label != null ? label : GetComponent<TextMeshProUGUI>();
        _canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();

        if (_label != null)
        {
            if (font != null)
                _label.font = font;

            _label.fontSize = Mathf.Max(1f, fontSize);
            _label.enableAutoSizing = false;
            _label.alignment = TextAlignmentOptions.Center;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.raycastTarget = false;
            _label.outlineColor = Color.black;
            _label.outlineWidth = Mathf.Clamp(outlineWidth, 0f, 0.25f);
        }

        ResetVisualState(Vector2.zero);
    }

    public void Play(
        string content,
        Color color,
        bool isCritical,
        Vector2 startPosition,
        float horizontalDirection,
        BattleDamagePopupAnimationSettings settings,
        Action<BattleDamagePopupView> release)
    {
        if (_rectTransform == null || _label == null || _canvasGroup == null)
            throw new InvalidOperationException("BattleDamagePopupView is not initialized.");

        CancelOwnedTween();
        gameObject.SetActive(true);

        BattleDamagePopupAnimationSettings timing = settings.Sanitized();
        float direction = horizontalDirection < 0f ? -1f : 1f;
        _release = release;
        _completionIssued = false;

        _rectTransform.anchoredPosition = startPosition;
        _rectTransform.localScale = isCritical
            ? Vector3.one * timing.CriticalStartScale
            : Vector3.one;
        _canvasGroup.alpha = 1f;
        _label.text = content ?? string.Empty;
        _label.color = color;

        Vector2 launchPosition = startPosition + new Vector2(timing.LaunchOffset.x * direction, timing.LaunchOffset.y);
        Vector2 settlePosition = startPosition + new Vector2(timing.SettleOffset.x * direction, timing.SettleOffset.y);
        Vector2 fadePosition = startPosition + new Vector2(timing.FadeOffset.x * direction, timing.FadeOffset.y);

        _sequence = DOTween.Sequence().SetId(this);
        _sequence.Append(_rectTransform.DOAnchorPos(launchPosition, timing.LaunchDuration).SetEase(Ease.OutQuad));
        if (isCritical)
        {
            _sequence.Join(_rectTransform
                .DOScale(Vector3.one * timing.CriticalEndScale, timing.CriticalGrowDuration)
                .SetEase(Ease.OutExpo));
        }

        _sequence.Append(_rectTransform.DOAnchorPos(settlePosition, timing.SettleDuration).SetEase(Ease.InQuad));
        _sequence.AppendInterval(timing.HoldDuration);
        _sequence.Append(_rectTransform.DOAnchorPos(fadePosition, timing.FadeDuration).SetEase(Ease.OutSine));
        _sequence.Join(_canvasGroup.DOFade(0f, timing.FadeDuration).SetEase(Ease.InSine));
        _sequence.OnComplete(CompleteOnce);
    }

    public void StopAndReset()
    {
        _release = null;
        _completionIssued = true;
        CancelOwnedTween();
        ResetVisualState(Vector2.zero);
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void CompleteOnce()
    {
        if (_completionIssued)
            return;

        _completionIssued = true;
        Action<BattleDamagePopupView> release = _release;
        _release = null;
        _sequence = null;
        release?.Invoke(this);
    }

    private void CancelOwnedTween()
    {
        DOTween.Kill(this, false);
        _sequence = null;
    }

    private void ResetVisualState(Vector2 anchoredPosition)
    {
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = anchoredPosition;
            _rectTransform.localScale = Vector3.one;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        if (_label != null)
        {
            _label.text = string.Empty;
            _label.color = Color.white;
        }
    }

    private void OnDisable()
    {
        _release = null;
        _completionIssued = true;
        CancelOwnedTween();
    }

    private void OnDestroy()
    {
        _release = null;
        _completionIssued = true;
        CancelOwnedTween();
    }
}