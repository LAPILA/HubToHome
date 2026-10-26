using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
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
    [FoldoutGroup("상태 변화 반응"), LabelText("새 상태 강조")]
    [SerializeField] private bool _animateChanges = true;
    [FoldoutGroup("상태 변화 반응"), LabelText("강조 배율"), Range(1f, 1.3f)]
    [SerializeField] private float _changeScale = 1.12f;
    [FoldoutGroup("상태 변화 반응"), LabelText("강조 시간"), MinValue(0.05f)]
    [SerializeField] private float _changeDuration = 0.18f;
    // Inspector 재연결/초기화 순서가 달라도 배열 길이에 의존하지 않습니다.
    // 실제 RectTransform별 원본을 한 번만 저장해 압축된 크기를 원본으로 다시 잡지 않습니다.
    private readonly Dictionary<RectTransform, LayoutBaseline> _layoutBaselines =
        new Dictionary<RectTransform, LayoutBaseline>(8);
    private readonly BattleStatusTransitionTracker _transitions = new BattleStatusTransitionTracker();
    private readonly Dictionary<RectTransform, Pulse> _pulses = new Dictionary<RectTransform, Pulse>(8);
    private readonly Dictionary<RectTransform, StatusKey> _displayedKeys = new Dictionary<RectTransform, StatusKey>(8);
    private CharacterBase _boundActor;
    private bool _canAnimateChanges;

    public void RefreshActor(CharacterBase actor, BattleStatusIconDefinition[] definitions)
    {
        if (!ReferenceEquals(_boundActor, actor)) ResetFeedback();
        _boundActor = actor;
        ElementalStatusState state = actor != null ? actor.Elements : null;
        IReadOnlyList<StatusEffect> effects = actor != null ? actor.ActiveStatusEffects : null;
        BeginSnapshot(state, effects);
        int elementalCount = ElementalStatusPresentation.GetEntryCount(state);
        ApplyCompactLayout(elementalCount > 0);
        if (elementalCount == 0)
        {
            RefreshLegacy(effects, definitions);
            CompleteSnapshot();
            return;
        }
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
            if (!visible) { ClearCell(image, label); continue; }
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
                RefreshCell(image, label, StatusKey.Element(kind, frozen));
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
                RefreshCell(image, label, StatusKey.Legacy(effects[i - visibleElements].EffectID));
            }
        }
        if (_overflow != null) _overflow.text = count > capacity ? $"+{count - capacity}" : string.Empty;
        CompleteSnapshot();
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
        BeginSnapshot(null, effects);
        RefreshLegacy(effects, definitions);
        CompleteSnapshot();
    }

    private void RefreshLegacy(IReadOnlyList<StatusEffect> effects, BattleStatusIconDefinition[] definitions)
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
            if (!visible) { ClearCell(image, text); continue; }
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
            RefreshCell(image, text, StatusKey.Legacy(effect.EffectID));
        }
        if (_overflow != null) _overflow.text = count > capacity ? $"+{count - capacity}" : "";
    }

    private void BeginSnapshot(ElementalStatusState state, IReadOnlyList<StatusEffect> effects)
    {
        _transitions.Capture(state, effects);
        _canAnimateChanges = _transitions.HasPreviousSnapshot && _animateChanges && Application.isPlaying
            && isActiveAndEnabled && BattleUIController.JuiceIntensity > 0f;
    }

    private void CompleteSnapshot()
    {
        bool added = _transitions.HasAdded;
        bool removed = _transitions.HasRemoved;
        // 표시 범위 밖의 부여/해제도 +N에만 한 번 반응합니다. 마지막 상태 해제는 즉시 사라집니다.
        if (_canAnimateChanges && (added || removed || _transitions.HasPotencyIncreased)
            && _overflow != null && !string.IsNullOrEmpty(_overflow.text))
            PulseRect(_overflow.rectTransform, removed && !added ? 0.94f : _changeScale);
    }

    private void RefreshCell(Image image, TMP_Text label, StatusKey key)
    {
        RefreshGraphic(image != null ? image.rectTransform : null, key);
        // 자식 글자는 부모 아이콘과 함께 커지므로 이중 배율을 적용하지 않습니다.
        if (label != null && (image == null || !label.transform.IsChildOf(image.transform)))
            RefreshGraphic(label.rectTransform, key);
    }

    private void RefreshGraphic(RectTransform rect, StatusKey key)
    {
        if (rect == null) return;
        if (_displayedKeys.TryGetValue(rect, out StatusKey previous) && !previous.Equals(key)) StopPulse(rect);
        _displayedKeys[rect] = key;
        if (_canAnimateChanges && key.IsNew(_transitions)) PulseRect(rect, _changeScale);
    }

    private void ClearCell(Image image, TMP_Text label)
    {
        if (image != null) { StopPulse(image.rectTransform); _displayedKeys.Remove(image.rectTransform); }
        if (label != null) { StopPulse(label.rectTransform); _displayedKeys.Remove(label.rectTransform); }
    }

    private void PulseRect(RectTransform rect, float scale)
    {
        StopPulse(rect);
        if (rect == null || !rect.gameObject.activeInHierarchy) return;
        Vector3 baseline = rect.localScale;
        float strength = 1f + (scale - 1f) * Mathf.Clamp01(BattleUIController.JuiceIntensity);
        rect.localScale = baseline * strength;
        var pulse = new Pulse(rect, baseline);
        _pulses[rect] = pulse;
        pulse.Tween = DOTween.To(() => strength, value =>
            {
                if (rect != null) rect.localScale = baseline * value;
            }, 1f, Mathf.Max(0.05f, _changeDuration) * BattleUIController.JuiceDurationScale)
            .SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(rect).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => { if (rect != null) rect.localScale = baseline; });
    }

    private void StopPulse(RectTransform rect)
    {
        if (!_pulses.TryGetValue(rect, out Pulse pulse)) return;
        _pulses.Remove(rect);
        pulse.Stop();
    }

    private void ResetFeedback()
    {
        foreach (Pulse pulse in _pulses.Values) pulse.Stop();
        _pulses.Clear();
        _displayedKeys.Clear();
        _transitions.Reset();
        _canAnimateChanges = false;
    }

    private void OnDisable() { ResetFeedback(); _boundActor = null; }
    private void OnDestroy() => ResetFeedback();

    private readonly struct StatusKey : IEquatable<StatusKey>
    {
        private readonly int _element;
        private readonly string _legacy;
        private StatusKey(int element, string legacy) { _element = element; _legacy = legacy; }
        public static StatusKey Element(ElementalStatusKind kind, bool frozen) => new StatusKey(frozen ? -1 : (int)kind + 1, null);
        public static StatusKey Legacy(string id) => new StatusKey(0, id ?? string.Empty);
        public bool IsNew(BattleStatusTransitionTracker tracker) => _element == 0
            ? tracker.IsNewLegacy(_legacy)
            : tracker.IsNewElement((ElementalStatusKind)(_element - 1), _element < 0)
                || (_element > 0 && tracker.IsPotencyIncreased((ElementalStatusKind)(_element - 1)));
        public bool Equals(StatusKey other) => _element == other._element
            && string.Equals(_legacy, other._legacy, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is StatusKey other && Equals(other);
        public override int GetHashCode() => (_element * 397)
            ^ (_legacy != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(_legacy) : 0);
    }

    private sealed class Pulse
    {
        public Tween Tween;
        private readonly RectTransform _rect;
        private readonly Vector3 _baseline;
        public Pulse(RectTransform rect, Vector3 baseline) { _rect = rect; _baseline = baseline; }
        public void Stop()
        {
            if (Tween != null && Tween.IsActive()) Tween.Kill(false);
            if (_rect != null) _rect.localScale = _baseline;
        }
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
