using System;
using DG.Tweening;
using Sirenix.OdinInspector;
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
        LaunchOffset = new Vector2(8f, 14f),
        SettleOffset = new Vector2(12f, 10f),
        FadeOffset = new Vector2(16f, 24f),
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

[Serializable]
public struct BattleDamagePopupPulseSettings
{
    [LabelText("일반 타격 확대"), Range(1f, 1.35f)] public float NormalScale;
    [LabelText("치명타 확대"), Range(1f, 1.35f)] public float CriticalScale;
    [LabelText("커지는 시간"), Range(.01f, .1f)] public float GrowDuration;
    [LabelText("복귀 시간"), Range(.03f, .3f)] public float ReturnDuration;
    [LabelText("정점 밝기 강조"), Range(0f, .4f)] public float Highlight;

    public static BattleDamagePopupPulseSettings Default => new BattleDamagePopupPulseSettings
    {
        NormalScale = 1.12f, CriticalScale = 1.18f,
        GrowDuration = .04f, ReturnDuration = .12f, Highlight = .2f
    };

    public BattleDamagePopupPulseSettings Sanitized()
    {
        var value = this;
        if (value.NormalScale < 1f || value.CriticalScale < 1f) value = Default;
        value.NormalScale = Mathf.Clamp(value.NormalScale, 1f, 1.35f);
        value.CriticalScale = Mathf.Clamp(value.CriticalScale, value.NormalScale, 1.35f);
        value.GrowDuration = Mathf.Clamp(value.GrowDuration, .01f, .1f);
        value.ReturnDuration = Mathf.Clamp(value.ReturnDuration, .03f, .3f);
        value.Highlight = Mathf.Clamp(value.Highlight, 0f, .4f);
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
    private Sequence _totalPulse;
    private string _pendingTotalText;
    private Color _totalColor;
    private bool _totalGrowing;
    private bool _pendingCritical;
    private Action<BattleDamagePopupView> _release;
    private bool _completionIssued;

    public RectTransform PopupRect => _rectTransform;
    public TMP_Text Label => _label;
    public CanvasGroup CanvasGroup => _canvasGroup;
    public Sequence ActiveSequence => _sequence;
    public Sequence ActiveTotalPulse => _totalPulse;
    public bool IsReady => _rectTransform != null && _label != null && _canvasGroup != null;
    // 정점에서 숫자를 교체하기 전에 넓어진 새 숫자의 영역부터 확보합니다.
    public string LayoutContent => _pendingTotalText ?? (_label != null ? _label.text : string.Empty);

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

    public void SetFontSize(float size)
    {
        if (_label != null) _label.fontSize = Mathf.Max(1f, size);
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

        // 풀 재사용 전 외부 Kill이 일어나도 다른 재활용 트윈의 핸들을 가리키지 않습니다.
        _sequence = DOTween.Sequence().SetRecyclable(false).SetId(this);
        _sequence.Append(MoveTo(launchPosition, timing.LaunchDuration).SetEase(Ease.OutQuad));
        if (isCritical)
        {
            RectTransform rect = _rectTransform;
            _sequence.Join(DOTween.To(() => rect != null ? rect.localScale : Vector3.one,
                scale => { if (rect != null) rect.localScale = scale; },
                Vector3.one * timing.CriticalEndScale, timing.CriticalGrowDuration).SetEase(Ease.OutExpo));
        }

        _sequence.Append(MoveTo(settlePosition, timing.SettleDuration).SetEase(Ease.InQuad));
        _sequence.AppendInterval(timing.HoldDuration);
        _sequence.Append(MoveTo(fadePosition, timing.FadeDuration).SetEase(Ease.OutSine));
        _sequence.Join(FadeToZero(timing.FadeDuration).SetEase(Ease.InSine));
        _sequence.OnComplete(CompleteOnce);
    }

    private Tween MoveTo(Vector2 target, float duration)
    {
        RectTransform rect = _rectTransform;
        return DOTween.To(() => rect != null ? rect.anchoredPosition : target,
            position => { if (rect != null) rect.anchoredPosition = position; }, target, duration);
    }

    private Tween FadeToZero(float duration)
    {
        CanvasGroup group = _canvasGroup;
        return DOTween.To(() => group != null ? group.alpha : 0f,
            alpha => { if (group != null) group.alpha = alpha; }, 0f, Mathf.Max(0.01f, duration));
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

    /// <summary>누적 숫자는 자리에서 갱신합니다. 연타마다 이동/전체 등장 모션을 다시 시작하지 않습니다.</summary>
    public void ShowTotal(long total, Color color, bool critical, Action<BattleDamagePopupView> release)
        => ShowTotal(total, color, critical, release, BattleDamagePopupPulseSettings.Default);

    public void ShowTotal(long total, Color color, bool critical, Action<BattleDamagePopupView> release,
        BattleDamagePopupPulseSettings settings)
    {
        if (!IsReady)
        {
            StopAndReset();
            release?.Invoke(this);
            return;
        }
        // 페이드만 중단합니다. 확대 도중의 연타는 정점을 미루지 않고 최신 합계를 덮어씁니다.
        if (_sequence != null) _sequence.Kill(false);
        _sequence = null;
        gameObject.SetActive(true);
        _release = release;
        _completionIssued = false;
        bool first = _pendingTotalText == null;
        _pendingTotalText = total.ToString();
        _totalColor = color;
        _canvasGroup.alpha = 1f;
        if (first)
        {
            _label.text = _pendingTotalText;
            _label.color = color;
            _rectTransform.localScale = Vector3.one;
        }
        if (_totalGrowing && _totalPulse != null && _totalPulse.IsActive())
        {
            _pendingCritical |= critical;
            return;
        }

        _totalPulse?.Kill(false);
        _totalGrowing = true;
        _pendingCritical = critical;
        BattleDamagePopupPulseSettings timing = settings.Sanitized();
        RectTransform rect = _rectTransform;
        TextMeshProUGUI label = _label;
        float startScale = Mathf.Clamp(rect.localScale.x, 1f, timing.CriticalScale);
        float peakScale = timing.NormalScale;
        _totalPulse = DOTween.Sequence().SetUpdate(true).SetRecyclable(false).SetId(this);
        _totalPulse.Append(DOTween.To(() => 0f, progress =>
        {
            peakScale = Mathf.Max(startScale, _pendingCritical ? timing.CriticalScale : timing.NormalScale);
            if (rect != null) rect.localScale = Vector3.one * Mathf.Lerp(startScale, peakScale, progress);
        }, 1f, timing.GrowDuration).SetEase(Ease.OutQuad));
        _totalPulse.AppendCallback(() =>
        {
            _totalGrowing = false;
            if (label != null)
            {
                label.text = _pendingTotalText;
                label.color = Color.Lerp(_totalColor, Color.white, timing.Highlight);
            }
        });
        _totalPulse.Append(DOTween.To(() => 0f, progress =>
        {
            if (rect != null) rect.localScale = Vector3.one * Mathf.Lerp(peakScale, 1f, progress);
            if (label != null) label.color = Color.Lerp(
                Color.Lerp(_totalColor, Color.white, timing.Highlight), _totalColor, progress);
        }, 1f, timing.ReturnDuration).SetEase(Ease.OutCubic));
        _totalPulse.OnComplete(() =>
        {
            _totalPulse = null;
            _totalGrowing = false;
            if (rect != null) rect.localScale = Vector3.one;
            if (label != null) label.color = _totalColor;
        });
    }

    public void SetTotalPosition(Vector2 position)
    {
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
    }

    public void FinishTotal(float hold, float fade)
    {
        if (_sequence != null) _sequence.Kill(false);
        _sequence = DOTween.Sequence().SetId(this).SetUpdate(true).SetRecyclable(false);
        float pulseRemaining = _totalPulse != null && _totalPulse.IsActive()
            ? Mathf.Max(0f, _totalPulse.Duration() - _totalPulse.Elapsed()) : 0f;
        _sequence.AppendInterval(Mathf.Max(pulseRemaining, Mathf.Max(0f, hold)));
        _sequence.Append(FadeToZero(fade));
        _sequence.OnComplete(CompleteOnce);
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
        _totalPulse = null;
        _totalGrowing = false;
        _pendingCritical = false;
        _pendingTotalText = null;
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
