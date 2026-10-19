using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum FieldMenuCommandKind
{
    Page, Filter, Actor, Row, Confirm, Cancel, CompanionTab,
    StatSelect, StatStep, StatSet, ClearDraft, Reset, SkillSelect,
    ModalAccept, Settings, DetailScroll
}

public readonly struct FieldMenuCommand
{
    public FieldMenuCommand(FieldMenuCommandKind kind, int index = 0, int amount = 0)
    { Kind = kind; Index = index; Amount = amount; }
    public FieldMenuCommandKind Kind { get; }
    public int Index { get; }
    public int Amount { get; }
}

public static class FieldMenuPalette
{
    public static readonly Color Background = new Color32(18, 19, 29, 255);
    public static readonly Color Panel = new Color32(29, 25, 39, 255);
    public static readonly Color Border = new Color32(87, 74, 105, 255);
    public static readonly Color Text = new Color32(237, 230, 244, 255);
    public static readonly Color Muted = new Color32(164, 146, 181, 255);
    public static readonly Color Yellow = new Color32(245, 217, 129, 255);
    public static readonly Color Mint = new Color32(140, 211, 195, 255);
}

/// <summary>One-time uGUI construction in the menu's 640x480 reference frame.</summary>
public sealed class FieldMenuWidgets
{
    public readonly TMP_FontAsset Font;
    private readonly Action<FieldMenuCommand> _dispatch;
    public FieldMenuWidgets(TMP_FontAsset font, Action<FieldMenuCommand> dispatch)
    { Font = font; _dispatch = dispatch; }
    public void Dispatch(FieldMenuCommand command) { _dispatch?.Invoke(command); }

    public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public Image Fill(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        RectTransform rect = Rect(parent, name, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    public RectTransform Panel(Transform parent, string name, float x, float y, float width, float height)
    {
        Image image = Fill(parent, name, x, y, width, height, FieldMenuPalette.Panel);
        Outline border = image.gameObject.AddComponent<Outline>();
        border.effectColor = FieldMenuPalette.Border; border.effectDistance = new Vector2(1, -1);
        return image.rectTransform;
    }

    public TextMeshProUGUI Text(Transform parent, string name, string value, float size,
        float x, float y, float width, float height, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        RectTransform rect = Rect(parent, name, x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Font; label.fontSize = size; label.color = color ?? FieldMenuPalette.Text;
        label.alignment = alignment; label.text = value ?? string.Empty;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    public Button Button(Transform parent, string name, string label, float x, float y,
        float width, float height, FieldMenuCommand command, bool repeat = false)
    {
        RectTransform rect = Panel(parent, name, x, y, width, height);
        Image image = rect.GetComponent<Image>(); image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        var relay = rect.gameObject.AddComponent<FieldMenuButtonRelay>();
        relay.Configure(_dispatch, command, repeat);
        button.onClick.AddListener(relay.Submit);
        Text(rect, "Label", label, 11, 4, 1, width - 8, height - 2,
            FieldMenuPalette.Text, TextAlignmentOptions.Center);
        return button;
    }

    public static void Style(Button button, bool selected, bool enabled = true, Color? tone = null)
    {
        if (button == null) return;
        button.interactable = enabled;
        Image fill = button.targetGraphic as Image;
        Color accent = tone ?? FieldMenuPalette.Yellow;
        if (fill != null) fill.color = selected ? new Color32(58, 49, 67, 255) : FieldMenuPalette.Panel;
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.effectColor = selected ? accent : FieldMenuPalette.Border;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = !enabled ? FieldMenuPalette.Muted : selected ? accent : FieldMenuPalette.Text;
    }
}

