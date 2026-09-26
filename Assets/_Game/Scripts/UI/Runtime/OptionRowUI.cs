using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionRowUI : MonoBehaviour
{
    public Image IconImage;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI ValueText;
    public TextMeshProUGUI CursorText;
    public Image SelectionBorder;

    private Tween _cursorTween;
    private Tween _borderTween;
    private bool _selected;
    private string _entryName;

    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor,
        float selScale, bool available = true)
    {
        bool changed = selected && (!_selected || _entryName != entry.DisplayName);
        if (!selected || changed) ReleaseFeedback();
        _selected = selected;
        _entryName = entry.DisplayName;
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        Color textColor = available ? (selected ? selColor : normalColor) : new Color(0.48f, 0.48f, 0.54f);
        if (IconImage != null)
        {
            IconImage.sprite = entry.Icon;
            IconImage.enabled = entry.Icon != null;
            IconImage.preserveAspect = true;
            IconImage.color = available ? Color.white : textColor;
        }
        if (NameText != null)
        {
            NameText.text = entry is ItemMenuEntry item ? item.Data.ItemName : entry.DisplayName;
            NameText.color = textColor;
        }
        if (ValueText != null)
        {
            ValueText.text = entry is SkillMenuEntry skill ? skill.Data.APCost.ToString()
                : entry is ItemMenuEntry item ? $"×{item.Count}" : string.Empty;
            ValueText.color = textColor;
        }
        if (CursorText != null) { CursorText.text = selected ? "›" : ""; CursorText.color = selColor; }
        if (SelectionBorder != null)
        {
            SelectionBorder.enabled = selected;
            if (_borderTween == null || !_borderTween.IsActive()) SelectionBorder.color = selColor;
        }
        if (changed && Application.isPlaying) PlaySelectionFeedback(selColor);
    }

    public void SetEmpty() => gameObject.SetActive(false);

    private void PlaySelectionFeedback(Color color)
    {
        float intensity = BattleUIController.JuiceIntensity;
        if (intensity <= 0f) return;
        float durationScale = BattleUIController.JuiceDurationScale;
        if (CursorText != null)
        {
            RectTransform cursor = CursorText.rectTransform;
            Vector2 home = cursor.anchoredPosition;
            _cursorTween = DOTween.To(() => -2f * intensity, offset =>
                {
                    if (cursor != null) cursor.anchoredPosition = home + Vector2.right * Mathf.Round(offset);
                }, 0f, 0.12f * durationScale)
                .SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(false)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() => { if (cursor != null) cursor.anchoredPosition = home; });
        }
        if (SelectionBorder != null)
        {
            Image border = SelectionBorder;
            border.color = Color.Lerp(color, Color.white, 0.65f * intensity);
            _borderTween = DOTween.To(() => border != null ? border.color : color,
                    value => { if (border != null) border.color = value; }, color, 0.16f * durationScale)
                .SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(false)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() => { if (border != null) border.color = color; });
        }
    }

    private void ReleaseFeedback()
    {
        if (_cursorTween != null && _cursorTween.IsActive()) _cursorTween.Kill(false);
        if (_borderTween != null && _borderTween.IsActive()) _borderTween.Kill(false);
        _cursorTween = _borderTween = null;
    }

    private void OnDisable() { ReleaseFeedback(); _selected = false; _entryName = null; }
    private void OnDestroy() => ReleaseFeedback();
}
