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
    private Sprite _lastPortrait;
    private string _lastName;
    private bool _lastFirst;
    private bool _bound;

    public void Bind(Sprite portrait, string actorName, bool first)
    {
        bool changed = !_bound || _lastPortrait != portrait || _lastName != actorName || _lastFirst != first;
        if (changed) ReleaseFeedback();
        _bound = true; _lastPortrait = portrait; _lastName = actorName; _lastFirst = first;
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
        if (!changed || !Application.isPlaying || !isActiveAndEnabled || BattleUIController.JuiceIntensity <= 0f) return;
        Image image = _portrait;
        Image border = _border;
        RectTransform rect = image != null ? image.rectTransform : null;
        Vector2 home = rect != null ? rect.anchoredPosition : Vector2.zero;
        Color color = first ? _firstColor : _normalColor;
        float intensity = BattleUIController.JuiceIntensity;
        _feedback = DOTween.To(() => 0f, progress =>
            {
                if (rect != null) rect.anchoredPosition = home + Vector2.up * Mathf.Round(3f * intensity * (1f - progress));
                if (border != null) border.color = Color.Lerp(color, Color.white, (1f - progress) * 0.5f * intensity);
            }, 1f, 0.18f * BattleUIController.JuiceDurationScale)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => { if (rect != null) rect.anchoredPosition = home; if (border != null) border.color = color; });
    }

    private void ReleaseFeedback() { if (_feedback != null && _feedback.IsActive()) _feedback.Kill(false); _feedback = null; }
    private void OnDisable() { ReleaseFeedback(); _bound = false; }
    private void OnDestroy() => ReleaseFeedback();
}
