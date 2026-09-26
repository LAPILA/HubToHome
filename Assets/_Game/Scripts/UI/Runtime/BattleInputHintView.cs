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

    public void SetContext(BattleHintContext context)
    {
        _context = context;
        Refresh();
    }

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        if (_context == BattleHintContext.Defense)
        {
            Show(0, BattleHintAction.Guard, "방어");
            Show(1, BattleHintAction.Dodge, "회피");
            Show(2, BattleHintAction.Counter, "특수 반격");
        }
        else
        {
            Show(0, BattleHintAction.Move, _context == BattleHintContext.Target ? "대상 변경" : "메뉴 / 항목");
            Show(1, BattleHintAction.Confirm, _context == BattleHintContext.Target ? "결정" : "선택");
            Show(2, BattleHintAction.Cancel, "취소");
        }
    }

    private void Show(int index, BattleHintAction action, string label)
    {
        if (_slots == null || index >= _slots.Length) return;
        BattleInputGlyph glyph = Resolve(action);
        BattleHintSlot slot = _slots[index];
        if (slot.Label != null) slot.Label.text = label;
        if (slot.Glyph != null)
        {
            slot.Glyph.sprite = glyph.Image;
            slot.Glyph.enabled = glyph.Image != null;
            slot.Glyph.preserveAspect = true;
        }
        if (slot.Key != null)
        {
            slot.Key.enabled = glyph.Image == null;
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
        if (action == BattleHintAction.Move) return fallback;
        GameConfigManager config = GameConfigManager.Instance;
        if (config == null) return fallback;
        ConfigurableAction keyAction = action == BattleHintAction.Dodge || action == BattleHintAction.Cancel ? ConfigurableAction.Cancel
            : action == BattleHintAction.Counter
                ? ConfigurableAction.Menu : ConfigurableAction.Confirm;
        return config.GetKey(keyAction).ToString().ToUpperInvariant();
    }
}
