using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>표시 인덱스 0의 테두리만 강조합니다. 턴 계산은 변경하지 않습니다.</summary>
public sealed class BattleTurnQueueIcon : MonoBehaviour
{
    [SerializeField] private Image _border;
    [SerializeField] private Image _portrait;
    [SerializeField] private TMP_Text _fallbackName;
    [SerializeField] private Color _normalColor = new Color(0.52f, 0.46f, 0.66f);
    [SerializeField] private Color _firstColor = new Color(1f, 0.92f, 0.35f);
    private Tween _feedback;
    private RectTransform _rect;
    public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    public void Bind(Sprite portrait, string actorName, bool first)
    {
        if (_border != null) _border.color = first ? _firstColor : _normalColor;
        if (_portrait != null)
        {
            _portrait.sprite = portrait;
            _portrait.color = Color.white;
            _portrait.enabled = portrait != null;
            _portrait.preserveAspect = true;
        }
        if (_fallbackName != null)
            _fallbackName.text = portrait == null ? actorName : string.Empty;
    }

    /// <summary>부모의 레이아웃은 그대로 두고 표시 루트만 이동합니다.</summary>
    public void AnimateFrom(Vector3 worldPosition, float duration, float pulseScale, bool animate)
    {
        ReleaseFeedback();
        if (!animate || !isActiveAndEnabled) return;
        Rect.position = worldPosition;
        duration = Mathf.Max(0.05f, duration);
        _feedback = DOTween.Sequence()
            .Join(Rect.DOAnchorPos(Vector2.zero, duration).SetEase(Ease.OutCubic))
            .Join(Rect.DOScale(pulseScale, duration * 0.4f).SetEase(Ease.OutQuad))
            .Insert(duration * 0.4f, Rect.DOScale(1f, duration * 0.6f).SetEase(Ease.OutQuad))
            .SetUpdate(true).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(RestoreBaseline);
    }

    private void RestoreBaseline()
    {
        if (_rect == null) return;
        _rect.anchoredPosition = Vector2.zero;
        _rect.localScale = Vector3.one;
    }

    public void ReleaseFeedback()
    {
        Tween owned = _feedback;
        _feedback = null;
        if (owned != null && owned.IsActive()) owned.Kill(false);
        RestoreBaseline();
    }
    private void OnDisable() => ReleaseFeedback();
    private void OnDestroy() => ReleaseFeedback();
}
