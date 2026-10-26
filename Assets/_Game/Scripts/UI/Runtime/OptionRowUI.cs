using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptionRowUI : MonoBehaviour, IPointerClickHandler
{
    public Image IconImage;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI ValueText;
    public TextMeshProUGUI CursorText;
    public Image SelectionBorder;

    private Tween _cursorTween;
    private Tween _borderTween;
    private Tween _deniedTween;
    private bool _selected;
    private string _entryName;
    private Action<int> _pointerAction;
    private int _pointerIndex;
    private Image _pointerSurface;
    private readonly RectTransform[] _deniedRects = new RectTransform[4];
    private readonly Vector2[] _deniedHomes = new Vector2[4];
    private readonly Graphic[] _deniedGraphics = new Graphic[5];
    private readonly Color[] _deniedColors = new Color[5];
    private bool _deniedActive;

    public void SetPointerAction(int index, Action<int> action)
    {
        _pointerIndex = index;
        _pointerAction = action;
        if (_pointerSurface == null && action != null)
        {
            // 선택 테두리는 미선택 시 꺼지므로 별도 투명 입력 면을 한 번만 만듭니다.
            var root = new GameObject("Row Pointer Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(transform, false);
            root.transform.SetAsFirstSibling();
            _pointerSurface = root.GetComponent<Image>();
            _pointerSurface.color = new Color(0f, 0f, 0f, 0f);
            RectTransform rect = _pointerSurface.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (_pointerSurface != null) _pointerSurface.raycastTarget = action != null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        _pointerAction?.Invoke(_pointerIndex);
    }

    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor,
        float selScale, bool available = true)
    {
        bool changed = selected && (!_selected || _entryName != entry.DisplayName);
        ReleaseDeniedFeedback(); // 행 재사용/잠금 교체는 이전 색·위치 복구 뒤 새 상태를 칠합니다.
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
            NameText.text = entry is ItemMenuEntry item ? item.Data.LocalizedName : entry.DisplayName;
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

    public void SetEmpty()
    {
        _pointerAction = null;
        gameObject.SetActive(false);
    }

    public void PlayDeniedFeedback()
    {
        if (!isActiveAndEnabled) return;
        ReleaseFeedback();
        _deniedRects[0] = NameText != null ? NameText.rectTransform : null;
        _deniedRects[1] = ValueText != null ? ValueText.rectTransform : null;
        _deniedRects[2] = IconImage != null ? IconImage.rectTransform : null;
        _deniedRects[3] = CursorText != null ? CursorText.rectTransform : null;
        _deniedGraphics[0] = NameText;
        _deniedGraphics[1] = ValueText;
        _deniedGraphics[2] = IconImage;
        _deniedGraphics[3] = CursorText;
        _deniedGraphics[4] = SelectionBorder;
        for (int i = 0; i < _deniedRects.Length; i++)
            if (_deniedRects[i] != null) _deniedHomes[i] = _deniedRects[i].anchoredPosition;
        for (int i = 0; i < _deniedGraphics.Length; i++)
            if (_deniedGraphics[i] != null) _deniedColors[i] = _deniedGraphics[i].color;
        _deniedActive = true;
        ApplyDeniedFeedback(0f);
        if (!Application.isPlaying) return;
        _deniedTween = DOTween.To(() => 0f, ApplyDeniedFeedback, 1f,
                0.28f * BattleUIController.JuiceDurationScale)
            .SetEase(Ease.Linear).SetUpdate(true).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(RestoreDeniedFeedback);
    }

    private void ApplyDeniedFeedback(float progress)
    {
        float offset = Mathf.Round(Mathf.Sin(progress * Mathf.PI * 6f) * (1f - progress)
            * 4f * BattleUIController.JuiceIntensity);
        for (int i = 0; i < _deniedRects.Length; i++)
            if (_deniedRects[i] != null) _deniedRects[i].anchoredPosition = _deniedHomes[i] + Vector2.right * offset;
        Color red = new Color(1f, 0.2f, 0.25f);
        for (int i = 0; i < _deniedGraphics.Length; i++)
            if (_deniedGraphics[i] != null)
                _deniedGraphics[i].color = Color.Lerp(red, _deniedColors[i], progress);
    }

    private void RestoreDeniedFeedback()
    {
        if (!_deniedActive) return;
        _deniedActive = false;
        for (int i = 0; i < _deniedRects.Length; i++)
            if (_deniedRects[i] != null) _deniedRects[i].anchoredPosition = _deniedHomes[i];
        for (int i = 0; i < _deniedGraphics.Length; i++)
            if (_deniedGraphics[i] != null) _deniedGraphics[i].color = _deniedColors[i];
    }

    private void ReleaseDeniedFeedback()
    {
        if (_deniedTween != null && _deniedTween.IsActive()) _deniedTween.Kill(false);
        _deniedTween = null;
        RestoreDeniedFeedback();
    }

    public void ClearInteractionFeedback() => ReleaseFeedback();

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
        ReleaseDeniedFeedback();
        if (_cursorTween != null && _cursorTween.IsActive()) _cursorTween.Kill(false);
        if (_borderTween != null && _borderTween.IsActive()) _borderTween.Kill(false);
        _cursorTween = _borderTween = null;
    }

    private void OnDisable() { ReleaseFeedback(); _selected = false; _entryName = null; }
    private void OnDestroy() { _pointerAction = null; ReleaseFeedback(); }
}
