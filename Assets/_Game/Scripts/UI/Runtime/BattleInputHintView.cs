using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleHintContext { Menu, Target, Defense }
public enum BattleHintAction { Move, Confirm, Cancel, Guard, Dodge, Counter }

[Serializable]
public struct BattleInputGlyph
{
    public BattleHintAction Action;
    public string KeyboardText;
    [Tooltip("향후 기기별 표시 이미지를 연결합니다. 입력 동작 자체와는 분리됩니다.")]
    public Sprite Image;
    public Sprite GamepadImage;
    public Sprite TouchImage;
}

[Serializable]
public struct BattleHintSlot
{
    public TMP_Text Key;
    public Image Glyph;
    public TMP_Text Label;
}

/// <summary>입력을 실행하지 않는 안내 View. 동작 ID와 키 이미지/문구를 분리합니다.</summary>
public sealed class BattleInputHintView : MonoBehaviour
{
    [SerializeField] private BattleInputGlyph[] _glyphs;
    [SerializeField] private BattleHintSlot[] _slots;
    private BattleHintContext _context;
    private bool _dialoguePresentation;
    private bool _dialogueChoices;

    public void SetDialoguePresentation(bool active, bool hasChoices = false)
    {
        if (_dialoguePresentation == active && _dialogueChoices == hasChoices) return;
        _dialoguePresentation = active;
        _dialogueChoices = hasChoices;
        Refresh();
    }

    public void SetContext(BattleHintContext context)
    {
        _context = context;
        Refresh();
    }

    private void OnEnable()
    {
        GameInput.InitializeRuntimeInput();
        GameInput.DeviceChanged += OnDeviceChanged;
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameInput.DeviceChanged -= OnDeviceChanged;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnDeviceChanged(GameInputDeviceKind kind) => Refresh();
    private void OnLanguageChanged(LanguageType language) => Refresh();
    private static string L(string key, string fallback) => LocalizationManager.Localize("input." + key, fallback);

    private void Refresh()
    {
        if (_dialoguePresentation)
        {
            if (_dialogueChoices) Show(0, BattleHintAction.Move, L("choices", "선택지 이동"));
            else Clear(0);
            Show(1, BattleHintAction.Confirm, L("advance", "대사 진행"));
            if (_dialogueChoices) Clear(2);
            else Show(2, BattleHintAction.Cancel, L("skip", "대사 넘기기"));
        }
        else if (_context == BattleHintContext.Defense)
        {
            Show(0, BattleHintAction.Guard, L("guard", "방어"));
            Show(1, BattleHintAction.Dodge, L("dodge", "회피"));
            Show(2, BattleHintAction.Counter, L("counter", "특수 반격"));
        }
        else
        {
            Show(0, BattleHintAction.Move, _context == BattleHintContext.Target ? L("target", "대상 변경") : L("navigate", "메뉴 / 항목"));
            Show(1, BattleHintAction.Confirm, L("confirm", "선택"));
            Show(2, BattleHintAction.Cancel, L("cancel", "취소"));
        }
    }

    private void Clear(int index)
    {
        if (_slots == null || index >= _slots.Length) return;
        BattleHintSlot slot = _slots[index];
        if (slot.Label != null) slot.Label.text = string.Empty;
        if (slot.Key != null) { slot.Key.text = string.Empty; slot.Key.enabled = false; }
        if (slot.Glyph != null) slot.Glyph.enabled = false;
    }

    private void Show(int index, BattleHintAction action, string label)
    {
        if (_slots == null || index >= _slots.Length) return;
        BattleInputGlyph glyph = Resolve(action);
        Sprite image = GameInput.ActiveDevice == GameInputDeviceKind.Gamepad ? glyph.GamepadImage
            : GameInput.ActiveDevice == GameInputDeviceKind.Touch ? glyph.TouchImage : glyph.Image;
        BattleHintSlot slot = _slots[index];
        if (slot.Label != null) slot.Label.text = label;
        if (slot.Glyph != null)
        {
            slot.Glyph.sprite = image;
            slot.Glyph.enabled = image != null;
            slot.Glyph.preserveAspect = true;
        }
        if (slot.Key != null)
        {
            slot.Key.enabled = image == null;
            slot.Key.text = KeyboardLabel(action, glyph.KeyboardText);
        }
    }

    private BattleInputGlyph Resolve(BattleHintAction action)
    {
        if (_glyphs != null)
            for (int i = 0; i < _glyphs.Length; i++)
                if (_glyphs[i].Action == action) return _glyphs[i];
        return new BattleInputGlyph { Action = action, KeyboardText = action == BattleHintAction.Move ? "↑↓←→" : "" };
    }

    private static string KeyboardLabel(BattleHintAction action, string fallback)
    {
        if (action == BattleHintAction.Move) return GameInput.ActiveDevice == GameInputDeviceKind.Gamepad ? "✚" : "↑↓←→";
        ConfigurableAction keyAction = action == BattleHintAction.Dodge || action == BattleHintAction.Cancel ? ConfigurableAction.Cancel
            : action == BattleHintAction.Counter
                ? ConfigurableAction.Menu : ConfigurableAction.Confirm;
        return GameInput.GetActionLabel(keyAction);
    }
}
