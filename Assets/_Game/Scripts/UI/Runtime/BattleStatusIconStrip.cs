using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct BattleStatusIconDefinition
{
    public string EffectId;
    public Sprite Icon;
    [Tooltip("아이콘이 없을 때 보이는 한 글자 표식")] public string ShortLabel;
    public Color Color;
}

/// <summary>상태 목록의 표시만 담당합니다. 중첩/기간/효과 규칙은 CharacterBase에 남깁니다.</summary>
public sealed class BattleStatusIconStrip : MonoBehaviour
{
    [SerializeField] private Image[] _icons;
    [SerializeField] private TMP_Text[] _fallbackLabels;
    [SerializeField] private TMP_Text _overflow;
    // Inspector 재연결/초기화 순서가 달라도 배열 길이에 의존하지 않습니다.
    // 실제 RectTransform별 원본을 한 번만 저장해 압축된 크기를 원본으로 다시 잡지 않습니다.
    private readonly Dictionary<RectTransform, LayoutBaseline> _layoutBaselines =
        new Dictionary<RectTransform, LayoutBaseline>(8);

    public void RefreshActor(CharacterBase actor, BattleStatusIconDefinition[] definitions)
    {
        ElementalStatusState state = actor != null ? actor.Elements : null;
        int elementalCount = ElementalStatusPresentation.GetEntryCount(state);
        ApplyCompactLayout(elementalCount > 0);
        if (elementalCount == 0)
        {
            Refresh(actor != null ? actor.ActiveStatusEffects : null, definitions);
            return;
        }
        IReadOnlyList<StatusEffect> effects = actor.ActiveStatusEffects;
        int count = elementalCount + effects.Count;
        int capacity = Mathf.Min(2, _icons != null ? _icons.Length : 0);
        // 일반 효과가 함께 있으면 한 자리를 남겨 기존 아이콘도 계속 식별합니다.
        int visibleElements = effects.Count > 0 ? Mathf.Min(1, capacity) : capacity;
        for (int i = 0; _icons != null && i < _icons.Length; i++)
        {
            Image image = _icons[i];
            TMP_Text label = _fallbackLabels != null && i < _fallbackLabels.Length ? _fallbackLabels[i] : null;
            bool visible = i < capacity && i < count;
            if (image != null) image.gameObject.SetActive(visible);
            if (label != null) label.gameObject.SetActive(visible);
            if (!visible) continue;
            if (i < visibleElements && ElementalStatusPresentation.TryGetEntry(state, i, out ElementalStatusKind kind, out int value, out bool frozen))
            {
                if (image != null) { image.sprite = null; image.color = new Color(0.05f, 0.06f, 0.09f, 0.95f); }
                if (label != null)
                {
                    label.text = (frozen ? "빙" : ElementalStatusPresentation.GetShortLabel(kind)) + value;
                    label.color = frozen ? new Color32(178, 233, 255, 255) : ElementalStatusPresentation.GetColor(kind);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 6f;
                    label.fontSizeMax = 9f;
                }
            }
            else
            {
                BattleStatusIconDefinition definition = Find(effects[i - visibleElements].EffectID, definitions);
                if (image != null) { image.sprite = definition.Icon; image.color = definition.Color.a > 0f ? definition.Color : Color.white; }
                if (label != null)
                {
                    label.text = definition.Icon == null ? string.IsNullOrEmpty(definition.ShortLabel) ? "?" : definition.ShortLabel : string.Empty;
                    label.color = Color.black;
                }
            }
        }
        if (_overflow != null) _overflow.text = count > capacity ? $"+{count - capacity}" : string.Empty;
    }

    private void ApplyCompactLayout(bool compact)
    {
        for (int i = 0; _icons != null && i < _icons.Length; i++)
        {
            if (_icons[i] != null)
                ApplyLayout(_icons[i].rectTransform, compact, new Vector2(i * 19f, 0f), new Vector2(18f, 10f));
            if (_fallbackLabels != null && i < _fallbackLabels.Length && _fallbackLabels[i] != null)
                ApplyLayout(_fallbackLabels[i].rectTransform, compact, null, new Vector2(18f, 10f));
        }
        if (_overflow != null)
            ApplyLayout(_overflow.rectTransform, compact, new Vector2(38f, 0f), new Vector2(11f, 10f));
    }

    private void ApplyLayout(RectTransform rect, bool compact, Vector2? compactPosition, Vector2 compactSize)
    {
        if (!_layoutBaselines.TryGetValue(rect, out LayoutBaseline baseline))
        {
            baseline = new LayoutBaseline(rect);
            _layoutBaselines.Add(rect, baseline);
        }
        rect.anchoredPosition = compact && compactPosition.HasValue ? compactPosition.Value : baseline.Position;
        rect.sizeDelta = compact ? compactSize : baseline.Size;
    }

    private readonly struct LayoutBaseline
    {
        public readonly Vector2 Position;
        public readonly Vector2 Size;

        public LayoutBaseline(RectTransform rect)
        {
            Position = rect.anchoredPosition;
            Size = rect.sizeDelta;
        }
    }

    public void Refresh(IReadOnlyList<StatusEffect> effects, BattleStatusIconDefinition[] definitions)
    {
        int count = effects != null ? effects.Count : 0;
        int capacity = _icons != null ? _icons.Length : 0;
        for (int i = 0; i < capacity; i++)
        {
            Image image = _icons[i];
            TMP_Text text = _fallbackLabels != null && i < _fallbackLabels.Length ? _fallbackLabels[i] : null;
            bool visible = i < count;
            if (image != null) image.gameObject.SetActive(visible);
            if (text != null) text.gameObject.SetActive(visible);
            if (!visible) continue;
            StatusEffect effect = effects[i];
            BattleStatusIconDefinition definition = Find(effect.EffectID, definitions);
            if (image != null)
            {
                image.sprite = definition.Icon;
                image.color = definition.Color.a > 0f ? definition.Color : Color.white;
                image.preserveAspect = true;
            }
            if (text != null)
            {
                text.text = definition.Icon == null
                    ? string.IsNullOrEmpty(definition.ShortLabel) ? "?" : definition.ShortLabel
                    : string.Empty;
                text.color = Color.black;
            }
        }
        if (_overflow != null) _overflow.text = count > capacity ? $"+{count - capacity}" : "";
    }

    private static BattleStatusIconDefinition Find(string id, BattleStatusIconDefinition[] definitions)
    {
        if (definitions != null)
            for (int i = 0; i < definitions.Length; i++)
                if (string.Equals(definitions[i].EffectId, id, StringComparison.OrdinalIgnoreCase))
                    return definitions[i];
        return default;
    }
}
