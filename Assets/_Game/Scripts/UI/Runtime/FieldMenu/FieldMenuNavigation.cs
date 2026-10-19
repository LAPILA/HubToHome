using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메뉴 내부의 방향 탐색과 포커스 테두리만 소유합니다. 게임 상태/저장값은 변경하지 않습니다.</summary>
public sealed class FieldMenuNavigation
{
    private readonly RectTransform _frame, _ring;
    private readonly IReadOnlyList<Button> _buttons;
    private readonly Action<FieldMenuCommand> _preview;
    private readonly List<Button> _candidates = new List<Button>(64);
    private readonly List<Rect> _rects = new List<Rect>(64);
    private readonly Vector3[] _corners = new Vector3[4];
    private ScrollRect _selectedScroll;
    public Button Selected { get; private set; }
    public FieldMenuCommand Command => Selected != null
        && Selected.TryGetComponent(out FieldMenuButtonRelay relay) ? relay.Command : default;

    public FieldMenuNavigation(RectTransform frame, IReadOnlyList<Button> buttons, Action<FieldMenuCommand> preview)
    {
        _frame = frame; _buttons = buttons; _preview = preview;
        _ring = new GameObject("KeyboardFocus", typeof(RectTransform)).GetComponent<RectTransform>();
        _ring.SetParent(frame, false);
        _ring.anchorMin = _ring.anchorMax = _ring.pivot = new Vector2(.5f, .5f);
        for (int i = 0; i < 4; i++)
        {
            var edge = new GameObject("Edge" + i, typeof(RectTransform), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>(); rect.SetParent(_ring, false);
            bool horizontal = i < 2;
            rect.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
            rect.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
            rect.sizeDelta = new Vector2(2, 2);
            rect.anchoredPosition = Vector2.zero;
            Image image = edge.GetComponent<Image>(); image.color = FieldMenuPalette.Yellow; image.raycastTarget = false;
        }
        _ring.gameObject.SetActive(false);
    }

    public static bool IsAvailable(Button button) => button != null && button.isActiveAndEnabled && button.IsInteractable();

    public Button Find(FieldMenuCommandKind kind, int index = 0, int amount = int.MinValue)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            Button button = _buttons[i];
            if (!IsAvailable(button) || !button.TryGetComponent(out FieldMenuButtonRelay relay)) continue;
            FieldMenuCommand command = relay.Command;
            if (command.Kind == kind && command.Index == index && (amount == int.MinValue || command.Amount == amount)) return button;
        }
        return null;
    }

    public void Select(Button button, bool preview = true)
    {
        if (!IsAvailable(button)) return;
        bool changed = Selected != button;
        Selected = button;
        if (changed) _selectedScroll = button.GetComponentInParent<ScrollRect>();
        if (preview && changed) _preview?.Invoke(Command);
        RefreshVisual();
    }

    public void Move(Vector2 direction)
    {
        if (!IsAvailable(Selected)) { Select(First()); return; }
        _candidates.Clear(); _rects.Clear();
        int selectedIndex = -1;
        ScrollRect sourceScroll = Selected.GetComponentInParent<ScrollRect>();
        for (int i = 0; i < _buttons.Count; i++)
        {
            Button button = _buttons[i];
            if (!IsAvailable(button)) continue;
            ScrollRect scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll != sourceScroll && scroll.viewport != null
                && !GetRect(button).Overlaps(GetRect(scroll.viewport))) continue;
            if (button == Selected) selectedIndex = _candidates.Count;
            _candidates.Add(button); _rects.Add(GetRect(button));
        }
        int next = FindDirectional(_rects, selectedIndex, direction);
        if (next >= 0) Select(_candidates[next]);
    }

    public Button First()
    {
        for (int i = 0; i < _buttons.Count; i++) if (IsAvailable(_buttons[i])) return _buttons[i];
        return null;
    }

    // Rectangle overlap gives cardinal neighbours priority without hardcoding a party size or tree layout.
    public static int FindDirectional(IReadOnlyList<Rect> rectangles, int current, Vector2 direction)
    {
        if (rectangles == null || current < 0 || current >= rectangles.Count || direction.sqrMagnitude < .5f) return -1;
        bool horizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
        float sign = horizontal ? Mathf.Sign(direction.x) : Mathf.Sign(direction.y);
        Rect origin = rectangles[current];
        int best = -1; float bestScore = float.PositiveInfinity;
        for (int i = 0; i < rectangles.Count; i++)
        {
            if (i == current) continue;
            Rect candidate = rectangles[i];
            Vector2 delta = candidate.center - origin.center;
            float forward = (horizontal ? delta.x : delta.y) * sign;
            if (forward <= .5f) continue;
            float side = Mathf.Abs(horizontal ? delta.y : delta.x);
            float span = horizontal ? (origin.height + candidate.height) * .5f : (origin.width + candidate.width) * .5f;
            float outside = Mathf.Max(0, side - span + 1f);
            float score = forward + side * .3f + outside * 3f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    public void RefreshVisual(bool visible = true)
    {
        if (_ring == null) return;
        bool show = visible && IsAvailable(Selected);
        _ring.gameObject.SetActive(show); if (!show) return;
        Rect rect = GetRect(Selected);
        ScrollRect scroll = _selectedScroll;
        if (scroll != null && scroll.viewport != null)
        {
            Rect viewport = GetRect(scroll.viewport);
            if (!rect.Overlaps(viewport)) { _ring.gameObject.SetActive(false); return; }
            rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, viewport.xMin), Mathf.Max(rect.yMin, viewport.yMin),
                Mathf.Min(rect.xMax, viewport.xMax), Mathf.Min(rect.yMax, viewport.yMax));
        }
        _ring.localPosition = new Vector3(Mathf.Round(rect.center.x), Mathf.Round(rect.center.y), 0);
        _ring.sizeDelta = new Vector2(Mathf.Round(rect.width) + 2, Mathf.Round(rect.height) + 2);
        if (_ring.GetSiblingIndex() != _frame.childCount - 1) _ring.SetAsLastSibling();
    }

    private Rect GetRect(Button button)
        => GetRect((RectTransform)button.transform);

    private Rect GetRect(RectTransform target)
    {
        target.GetWorldCorners(_corners);
        Vector3 min = _frame.InverseTransformPoint(_corners[0]);
        Vector3 max = _frame.InverseTransformPoint(_corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
