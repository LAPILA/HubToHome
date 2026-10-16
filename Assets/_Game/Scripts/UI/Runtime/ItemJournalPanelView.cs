using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>기존 ITEM 창 안의 소지품/일지. 재고와 파티 변경은 기존 서비스에만 위임합니다.</summary>
public sealed class ItemJournalPanelView : MonoBehaviour
{
    private readonly List<ItemData> _items = new List<ItemData>();
    private readonly List<JournalEntry> _entries = new List<JournalEntry>();
    private readonly StringBuilder _builder = new StringBuilder(768);
    private TMP_Text _heading, _list, _body, _hint;
    private JournalCatalog _catalog;
    private bool _journal, _choosingTarget;
    private int _selected, _targetIndex;
    private string _status = string.Empty;
    private System.Func<string, int> _readFlag;
    private GlobalDataManager _global;

    public static ItemJournalPanelView Ensure(RectTransform parent, TMP_FontAsset font, JournalCatalog catalog)
    {
        var view = parent.GetComponentInChildren<ItemJournalPanelView>(true);
        if (view == null)
        {
            var go = new GameObject("ItemJournalContent", typeof(RectTransform), typeof(ItemJournalPanelView));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18, 16); rect.offsetMax = new Vector2(-18, -16);
            view = go.GetComponent<ItemJournalPanelView>();
            view.Build(font);
        }
        view._catalog = catalog != null ? catalog : JournalCatalog.LoadDefault();
        return view;
    }

    private static string L(string key, string fallback) => LocalizationManager.Localize("journal." + key, fallback);

    private void Build(TMP_FontAsset font)
    {
        _heading = Text("Heading", font, 14, new Vector2(0, 0.84f), Vector2.one);
        _list = Text("List", font, 14, new Vector2(0, 0.16f), new Vector2(0.43f, 0.82f));
        _list.textWrappingMode = TextWrappingModes.NoWrap;
        _body = Text("Body", font, 13, new Vector2(0.47f, 0.16f), new Vector2(1, 0.82f));
        _hint = Text("Hint", font, 11, Vector2.zero, new Vector2(1, 0.14f));
        _body.overflowMode = TextOverflowModes.Page;
    }

    private TMP_Text Text(string name, TMP_FontAsset font, float size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    public void Refresh()
    {
        _global = GlobalDataManager.Instance;
        _readFlag = _global != null ? ReadFlag : null;
        _items.Clear(); _entries.Clear();
        if (_global != null)
        {
            foreach (var pair in _global.GetInventory())
            {
                if (pair.Value <= 0) continue;
                ItemData item = ItemDatabase.FindById(pair.Key);
                if (item != null) _items.Add(item);
            }
            _items.Sort((a, b) => string.CompareOrdinal(a.ItemID, b.ItemID));
        }
        if (_catalog != null && _catalog.Entries != null)
            for (int i = 0; i < _catalog.Entries.Count; i++)
            {
                JournalEntry entry = _catalog.Entries[i];
                if (entry != null && entry.IsVisible(_readFlag)) _entries.Add(entry);
            }
        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, (_journal ? _entries.Count : _items.Count) - 1));
        Render();
    }

    private int ReadFlag(string key) => _global != null ? _global.GetFlag(key) : 0;

    public bool TryCancelTarget()
    {
        if (!_choosingTarget) return false;
        _choosingTarget = false; _status = string.Empty; Render(); return true;
    }

    public void HandleInput()
    {
        if (GameInput.PowerTabPressed && !_choosingTarget)
        {
            _journal = !_journal; _selected = 0; _status = string.Empty; Refresh(); return;
        }
        int delta = GameInput.UIUpPressed ? -1 : GameInput.UIDownPressed ? 1 : 0;
        if (delta != 0)
        {
            int count = _choosingTarget ? (_global != null ? _global.Party.Count : 0) : (_journal ? _entries.Count : _items.Count);
            if (count > 0)
            {
                if (_choosingTarget) _targetIndex = (_targetIndex + delta + count) % count;
                else _selected = (_selected + delta + count) % count;
            }
            _status = string.Empty; Render(); return;
        }
        if (_journal)
        {
            if (GameInput.UILeftPressed) _body.pageToDisplay = Mathf.Max(1, _body.pageToDisplay - 1);
            if (GameInput.UIRightPressed || GameInput.UISubmitPressed)
                _body.pageToDisplay = Mathf.Min(_body.textInfo.pageCount, _body.pageToDisplay + 1);
            return;
        }
        if (!GameInput.UISubmitPressed || _items.Count == 0 || _global == null) return;
        if (!_choosingTarget)
        {
            ItemData item = _items[_selected];
            if (item.Type != ItemType.Consumable || !item.UsableInOverworld)
            { _status = L("cannot_use", "지금은 사용할 수 없습니다."); Render(); return; }
            if (_global.Party.Count == 0) return;
            _choosingTarget = true; _targetIndex = 0; Render(); return;
        }
        if (_targetIndex >= _global.Party.Count) { _choosingTarget = false; Refresh(); return; }
        string error = string.Empty;
        bool used = InventoryManager.TryUsePartyItem(_items[_selected].ItemID,
            _global.Party[_targetIndex], out error);
        _status = used ? L("used", "사용했습니다.") : L("cannot_use", "지금은 사용할 수 없습니다.");
        _choosingTarget = false;
        Refresh();
        GameInput.SuppressPlayerConfirmForCurrentFrame();
    }

    private void Render()
    {
        JournalEntry objective = _catalog != null ? _catalog.GetCurrentObjective(_readFlag) : null;
        _heading.text = "<color=#FFD105>" + (_journal ? L("tab_notes", "일지") : L("tab_items", "소지품")) + "</color>  |  "
            + L("objective", "현재 목표") + ": " + (objective != null ? objective.LocalizedTitle : L("objective_empty", "아직 기록된 목표가 없습니다."));
        _builder.Clear();
        int count = _choosingTarget && _global != null ? _global.Party.Count : _journal ? _entries.Count : _items.Count;
        int selection = _choosingTarget ? _targetIndex : _selected;
        int start = Mathf.Max(0, selection - 6);
        for (int i = start; i < Mathf.Min(count, start + 7); i++)
        {
            string title;
            if (_choosingTarget)
            {
                CharacterSaveData member = _global.Party[i];
                CharacterData data = CharacterDatabase.FindById(member.CharacterDataID);
                title = data != null ? data.ResolveDisplayName(_global.PlayerName) : member.CharacterID;
                title += "  HP " + member.HP;
            }
            else title = _journal ? _entries[i].LocalizedTitle : ItemName(_items[i]) + " ×" + _global.GetItemCount(_items[i].ItemID);
            if (i == selection) _builder.Append("<color=#FFD105>▶ ");
            else _builder.Append("  ");
            _builder.Append(title);
            if (i == selection) _builder.Append("</color>");
            _builder.Append('\n');
        }
        if (count == 0) _builder.Append(L("empty", "아직 기록이 없습니다."));
        _list.text = _builder.ToString();
        _body.pageToDisplay = 1;
        _body.text = _journal ? (_entries.Count > 0 ? _entries[_selected].LocalizedBody : string.Empty)
            : _items.Count > 0 ? LocalizationManager.Localize("item." + _items[_selected].ItemID + ".description", _items[_selected].Description) : string.Empty;
        _hint.text = !string.IsNullOrEmpty(_status) ? _status : string.Format(L("hint", "↑↓ 선택  {0} 확인  {1} 뒤로  {2} 소지품/일지\n일지: ←→ 페이지"),
            GameInput.GetActionLabel(ConfigurableAction.Confirm), GameInput.GetActionLabel(ConfigurableAction.Cancel), GameInput.GetActionLabel(ConfigurableAction.Menu));
    }

    private static string ItemName(ItemData item)
        => LocalizationManager.Localize("item." + item.ItemID + ".name", item.ItemName);
}
