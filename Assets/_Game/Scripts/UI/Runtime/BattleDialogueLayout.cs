using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class BattleDialogueLayoutSettings
{
    [LabelText("안쪽 여백"), Range(4f, 16f)] public float Padding = 8f;
    [LabelText("초상 크기"), Range(32f, 80f)] public float PortraitSize = 64f;
    [LabelText("본문 크기"), Range(10f, 20f)] public float FontSize = 14f;
    [LabelText("화자 이름 크기"), Range(10f, 20f)] public float NameFontSize = 14f;
    [LabelText("선택지 행 높이"), Range(16f, 24f)] public float ChoiceRowHeight = 18f;
    [LabelText("전투 배경 색")] public Color BackgroundColor = Color.black;
}

/// <summary>
/// 기존 DialogueUI의 표시 범위만 빌립니다. 대본/입력/전투 흐름은 소유하지 않습니다.
/// 전역 모달 Canvas를 유지하므로 카메라 줌 및 레터박스에 가려지지 않습니다.
/// </summary>
public sealed class BattleDialogueLayout : IDisposable
{
    private readonly BattleMenuUI _menu;
    private readonly RectTransform _panel, _content, _body, _portrait, _name, _choices;
    private readonly TextMeshProUGUI _bodyText, _nameText;
    private readonly Image _portraitImage;
    private readonly Image _backgroundImage;
    private readonly Color _backgroundColor;
    private readonly bool _backgroundEnabled;
    private readonly bool _portraitPreserveAspect;
    private readonly BattleDialogueLayoutSettings _settings;
    private readonly List<RectState> _rects = new List<RectState>(6);
    private readonly TextState _bodyState, _nameState;
    private readonly LayoutGroup _choiceGroup;
    private readonly bool _choiceGroupEnabled;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly Canvas _targetCanvas, _panelCanvas;
    private bool _disposed;
    private bool _hasLayout, _lastPortrait, _lastName;
    private Rect _lastBounds;
    private Vector4 _lastSettings;
    private float _lastRowHeight;
    private int _lastChoiceCount, _lastSelectedChoice;

    public bool IsHostAvailable => !_disposed && _menu != null && _menu.isActiveAndEnabled
        && _menu.IsDialoguePresentationActive && _menu.SubMenuRoot != null && _panel != null && _bodyText != null;

    public BattleDialogueLayout(BattleMenuUI menu, RectTransform panel, Image portrait,
        TextMeshProUGUI speaker, TextMeshProUGUI body, RectTransform choices,
        BattleDialogueLayoutSettings settings, Image background = null)
    {
        _menu = menu;
        _panel = panel;
        _bodyText = body;
        _nameText = speaker;
        _body = body.rectTransform;
        _content = _body.parent as RectTransform;
        _portrait = portrait != null ? portrait.rectTransform : null;
        _portraitImage = portrait;
        _portraitPreserveAspect = portrait != null && portrait.preserveAspect;
        if (portrait != null) portrait.preserveAspect = true;
        _name = speaker != null ? speaker.rectTransform : null;
        _choices = choices;
        _settings = settings ?? new BattleDialogueLayoutSettings();
        // 표시 범위를 빌리는 동안만 변경합니다. 필드 대화의 프리팹/재질/그림은 보존합니다.
        _backgroundImage = background;
        _backgroundColor = background != null ? background.color : Color.clear;
        _backgroundEnabled = background != null && background.enabled;
        if (background != null)
        {
            background.color = _settings.BackgroundColor;
            background.enabled = true;
        }
        _bodyState = new TextState(body);
        _nameState = new TextState(speaker);
        _targetCanvas = menu.SubMenuRoot.GetComponentInParent<Canvas>(true);
        _panelCanvas = panel.GetComponentInParent<Canvas>(true);
        Capture(_panel); Capture(_content); Capture(_body);
        Capture(_portrait); Capture(_name); Capture(_choices);
        if (_choices != null)
        {
            _choiceGroup = _choices.GetComponent<LayoutGroup>();
            if (_choiceGroup != null)
            {
                _choiceGroupEnabled = _choiceGroup.enabled;
                _choiceGroup.enabled = false;
            }
        }
        body.fontSize = Mathf.Clamp(_settings.FontSize, 10f, 20f);
        body.enableAutoSizing = false;
        body.fontStyle = FontStyles.Normal;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Page;
        body.margin = Vector4.zero;
        body.pageToDisplay = 1;
        if (speaker != null)
        {
            speaker.fontSize = Mathf.Clamp(_settings.NameFontSize, 10f, 20f);
            speaker.enableAutoSizing = false;
            speaker.alignment = TextAlignmentOptions.MidlineLeft;
            speaker.textWrappingMode = TextWrappingModes.NoWrap;
            speaker.overflowMode = TextOverflowModes.Ellipsis;
            speaker.margin = Vector4.zero;
        }
    }

    public void Refresh(IReadOnlyList<TextMeshProUGUI> labels, int selectedChoice, bool force = false)
    {
        if (!IsHostAvailable || _panel == null || !(_panel.parent is RectTransform parent)) return;
        if (!_menu.TryGetDialogueWorldCorners(_corners)) return;
        Camera source = CameraFor(_targetCanvas), destination = CameraFor(_panelCanvas);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                RectTransformUtility.WorldToScreenPoint(source, _corners[0]), destination, out Vector2 min)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                RectTransformUtility.WorldToScreenPoint(source, _corners[2]), destination, out Vector2 max)) return;

        Rect bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        bool hasPortrait = _portrait != null && _portrait.gameObject.activeSelf;
        bool hasName = _name != null && _name.gameObject.activeSelf;
        int count = _choices != null && _choices.gameObject.activeSelf && labels != null ? labels.Count : 0;
        var settings = new Vector4(_settings.Padding, _settings.PortraitSize, _settings.FontSize, _settings.NameFontSize);
        if (!force && _hasLayout && bounds == _lastBounds && hasPortrait == _lastPortrait && hasName == _lastName
            && count == _lastChoiceCount && selectedChoice == _lastSelectedChoice
            && settings == _lastSettings && _settings.ChoiceRowHeight == _lastRowHeight) return;
        _hasLayout = true; _lastBounds = bounds; _lastPortrait = hasPortrait; _lastName = hasName;
        _lastChoiceCount = count; _lastSelectedChoice = selectedChoice;
        _lastSettings = settings; _lastRowHeight = _settings.ChoiceRowHeight;
        _bodyText.fontSize = Mathf.Clamp(_settings.FontSize, 10f, 20f);
        if (_nameText != null) _nameText.fontSize = Mathf.Clamp(_settings.NameFontSize, 10f, 20f);

        _panel.anchorMin = _panel.anchorMax = _panel.pivot = Vector2.one * .5f;
        _panel.anchoredPosition = (min + max) * .5f - parent.rect.center;
        _panel.sizeDelta = new Vector2(Mathf.Max(1f, max.x - min.x), Mathf.Max(1f, max.y - min.y));
        _panel.localScale = Vector3.one;
        _panel.localRotation = Quaternion.identity;
        if (_content != null && _content != _panel) Stretch(_content, 0f, 0f, 0f, 0f);

        float padding = Mathf.Clamp(_settings.Padding, 4f, 16f);
        float portraitSize = Mathf.Min(Mathf.Clamp(_settings.PortraitSize, 32f, 80f), Mathf.Max(16f, _panel.rect.height - padding * 2));
        float textLeft = padding + (hasPortrait ? portraitSize + padding : 0f);
        if (_portrait != null) Place(_portrait, padding, padding, portraitSize, portraitSize);
        float nameHeight = hasName ? Mathf.Clamp(_settings.NameFontSize, 10f, 20f) + 6f : 0f;
        if (_name != null) Stretch(_name, textLeft, padding, padding, Mathf.Max(padding, _panel.rect.height - padding - nameHeight));

        float rowHeight = Mathf.Clamp(_settings.ChoiceRowHeight, 16f, 24f);
        int visible = Mathf.Min(count, Mathf.Clamp(Mathf.FloorToInt((_panel.rect.height - padding * 2 - nameHeight - 24f) / rowHeight), 1, 3));
        float choiceHeight = visible * rowHeight;
        Stretch(_body, textLeft, padding + nameHeight, padding, padding + choiceHeight);
        if (_choices == null) return;
        Stretch(_choices, textLeft, Mathf.Max(padding, _panel.rect.height - padding - choiceHeight), padding, padding);
        int first = Mathf.Clamp(selectedChoice - visible + 1, 0, Mathf.Max(0, count - visible));
        for (int i = 0; i < count; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null) continue;
            bool shown = i >= first && i < first + visible;
            label.gameObject.SetActive(shown);
            if (!shown) continue;
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Clamp(_settings.FontSize, 10f, 20f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = Vector4.zero;
            Place(label.rectTransform, 0f, (i - first) * rowHeight, _choices.rect.width, rowHeight);
        }
    }

    private static Camera CameraFor(Canvas canvas)
    {
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
    }

    private void Capture(RectTransform rect)
    {
        if (rect == null) return;
        foreach (RectState state in _rects) if (state.Rect == rect) return;
        _rects.Add(new RectState(rect));
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * .5f;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = _rects.Count - 1; i >= 0; i--) _rects[i].Restore();
        _bodyState.Restore(_bodyText); _nameState.Restore(_nameText);
        if (_backgroundImage != null)
        {
            _backgroundImage.color = _backgroundColor;
            _backgroundImage.enabled = _backgroundEnabled;
        }
        if (_portraitImage != null) _portraitImage.preserveAspect = _portraitPreserveAspect;
        if (_choiceGroup != null) _choiceGroup.enabled = _choiceGroupEnabled;
        if (_menu != null) _menu.EndDialoguePresentation();
    }

    private readonly struct RectState
    {
        public readonly RectTransform Rect;
        private readonly Vector2 _min, _max, _pivot, _size;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        public RectState(RectTransform rect)
        {
            Rect = rect; _min = rect.anchorMin; _max = rect.anchorMax; _pivot = rect.pivot;
            _size = rect.sizeDelta; _position = rect.anchoredPosition3D;
            _scale = rect.localScale; _rotation = rect.localRotation;
        }
        public void Restore()
        {
            if (Rect == null) return;
            Rect.anchorMin = _min; Rect.anchorMax = _max; Rect.pivot = _pivot;
            Rect.sizeDelta = _size; Rect.anchoredPosition3D = _position;
            Rect.localScale = _scale; Rect.localRotation = _rotation;
        }
    }

    private readonly struct TextState
    {
        private readonly float _size;
        private readonly bool _auto;
        private readonly FontStyles _style;
        private readonly TextAlignmentOptions _alignment;
        private readonly TextOverflowModes _overflow;
        private readonly TextWrappingModes _wrap;
        private readonly Vector4 _margin;
        private readonly int _page;
        public TextState(TMP_Text text)
        {
            _size = text != null ? text.fontSize : 0f; _auto = text != null && text.enableAutoSizing;
            _style = text != null ? text.fontStyle : FontStyles.Normal;
            _alignment = text != null ? text.alignment : TextAlignmentOptions.TopLeft;
            _overflow = text != null ? text.overflowMode : TextOverflowModes.Overflow;
            _wrap = text != null ? text.textWrappingMode : TextWrappingModes.Normal;
            _margin = text != null ? text.margin : Vector4.zero;
            _page = text != null ? text.pageToDisplay : 1;
        }
        public void Restore(TMP_Text text)
        {
            if (text == null) return;
            text.fontSize = _size; text.enableAutoSizing = _auto; text.fontStyle = _style;
            text.alignment = _alignment; text.overflowMode = _overflow; text.textWrappingMode = _wrap;
            text.margin = _margin; text.pageToDisplay = _page;
        }
    }
}
