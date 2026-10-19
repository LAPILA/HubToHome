using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct FieldMenuListEntry
{
    public FieldMenuListEntry(string name, string hint, Sprite icon = null, bool enabled = true)
    { Name = name; Hint = hint; Icon = icon; Enabled = enabled; }
    public string Name { get; }
    public string Hint { get; }
    public Sprite Icon { get; }
    public bool Enabled { get; }
}

/// <summary>Cached visual surface. Game rules and save mutations belong to the menu controller/services.</summary>
public sealed class FieldMenuView : MonoBehaviour
{
    public static readonly Vector2 FrameSize = new Vector2(592f, 416f);
    private const float ContentWidth = 568f;
    private const float ListWidth = 322f;
    private const float DetailWidth = 234f;
    private const float BodyTop = 130f;
    private const float BodyHeight = 184f;
    private const float PartyBarWidth = 136f;
    private FieldMenuWidgets _w;
    private RectTransform _frame, _body, _listPanel, _listViewport, _listContent, _detailPanel, _detailViewport, _detailContent;
    private RectTransform _actor, _growth, _growthBody, _modal;
    private TextMeshProUGUI _title, _level, _exp, _money, _objective, _actorName, _points, _status;
    private TextMeshProUGUI _detailTitle, _detailSubtitle, _detailText, _partyLabel, _modalTitle, _modalBody;
    private Image _expFill, _actorPortrait;
    private Button _detailAction, _settings, _back;
    private readonly Button[] _tabs = new Button[4];
    private readonly Button[] _filters = new Button[6];
    private readonly Button[] _subTabs = new Button[2];
    private readonly Button[] _modalButtons = new Button[2];
    private readonly PartyCard[] _party = new PartyCard[3];
    private readonly List<Row> _rows = new List<Row>();
    private ScrollRect _listScroll, _detailScroll;
    private FieldMenuGrowthView _growthView;
    private int _page;
    private string _detailValue;
    private float _detailHeight;
    public RectTransform Frame => _frame;
    public IReadOnlyList<Button> NavigationButtons => _w.Buttons;
    public RectTransform FocusedNumberRect => _growthView != null ? _growthView.FocusedNumberRect : null;
    public bool IsTextEditing => _growthView != null && _growthView.IsTextEditing;
    public void EndTextEditing() { if (_growthView != null) _growthView.EndTextEditing(); }
    public TMP_InputField BeginStatEditing(int index) => _growthView != null ? _growthView.BeginStatEditing(index) : null;

    private sealed class Row { public Button Button; public Image Icon; public TMP_Text Name, Hint; }
    private sealed class PartyCard { public Button Button; public Image Portrait, Hp, Ap; public TMP_Text Name, Level, HpText, ApText; }
    private static string L(string key, string text) => LocalizationManager.Localize("field_menu." + key, text);

    public static FieldMenuView Create(RectTransform parent, TMP_FontAsset font, Action<FieldMenuCommand> dispatch)
    {
        var go = new GameObject("FieldMenu", typeof(RectTransform), typeof(FieldMenuView));
        RectTransform frame = go.GetComponent<RectTransform>();
        frame.SetParent(parent, false); frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.sizeDelta = FrameSize;
        var view = go.GetComponent<FieldMenuView>(); view._frame = frame;
        view._w = new FieldMenuWidgets(font, dispatch); view.Build();
        return view;
    }

    private void Build()
    {
        Image backdrop = _frame.gameObject.AddComponent<Image>();
        backdrop.color = FieldMenuPalette.Background; backdrop.raycastTarget = true;
        _w.Panel(_frame, "Mark", 12, 8, 32, 32);
        _w.Text(_frame, "MarkLabel", "▥", 28, 12, 6, 32, 36, FieldMenuPalette.Yellow, TextAlignmentOptions.Center);
        _title = _w.Text(_frame, "Title", "", 20, 54, 5, 280, 27);
        _w.Text(_frame, "Subtitle", "HUB TO HOME", 8, 55, 32, 280, 12, FieldMenuPalette.Muted);
        _level = _w.Text(_frame, "Level", "", 10, 348, 7, 116, 16, FieldMenuPalette.Yellow);
        _exp = _w.Text(_frame, "Experience", "", 8, 348, 24, 116, 12, FieldMenuPalette.Muted);
        _w.Fill(_frame, "ExpTrack", 348, 39, 116, 3, Color.black);
        _expFill = _w.Fill(_frame, "ExpFill", 348, 39, 116, 3, FieldMenuPalette.Mint);
        _money = _w.Text(_frame, "Money", "", 17, 476, 15, 104, 27, FieldMenuPalette.Yellow, TextAlignmentOptions.Right);
        for (int i = 0; i < 4; i++) _tabs[i] = _w.Button(_frame, "Tab" + i, "", 12 + i * 144, 48, 136, 30, new FieldMenuCommand(FieldMenuCommandKind.Page, i));
        RectTransform objective = _w.Panel(_frame, "Objective", 12, 82, ContentWidth, 18);
        _objective = _w.Text(objective, "Text", "", 9, 6, 1, ContentWidth - 12, 16, FieldMenuPalette.Muted);
        _actor = _w.Panel(_frame, "SelectedCompanion", 12, 100, ContentWidth, 30);
        _w.Button(_actor, "Previous", "‹", 2, 1, 28, 28, new FieldMenuCommand(FieldMenuCommandKind.Actor, -1, -1));
        _actorPortrait = _w.Fill(_actor, "Portrait", 34, 2, 26, 26, Color.white); _actorPortrait.preserveAspect = true;
        _w.Button(_actor, "Next", "›", 64, 1, 28, 28, new FieldMenuCommand(FieldMenuCommandKind.Actor, -1, 1));
        _actorName = _w.Text(_actor, "Name", "", 12, 100, 6, 246, 20);
        _points = _w.Text(_actor, "Points", "", 9, 350, 9, 210, 16, FieldMenuPalette.Mint, TextAlignmentOptions.Right);
        _body = _w.Rect(_frame, "Content", 12, BodyTop, ContentWidth, BodyHeight);
        _listPanel = _w.Panel(_body, "List", 0, 0, ListWidth, BodyHeight);
        for (int i = 0; i < 6; i++) _filters[i] = _w.Button(_listPanel, "Filter" + i, "", 5 + i * 51, 3, 48, 28, new FieldMenuCommand(FieldMenuCommandKind.Filter, i));
        _listViewport = _w.Rect(_listPanel, "Viewport", 5, 36, ListWidth - 10, BodyHeight - 42);
        _listScroll = MakeScroll(_listViewport, "Rows", out _listContent);
        _detailPanel = _w.Panel(_body, "Detail", 334, 0, DetailWidth, BodyHeight);
        _detailTitle = _w.Text(_detailPanel, "Title", "", 15, 8, 5, DetailWidth - 16, 34);
        _detailSubtitle = _w.Text(_detailPanel, "Type", "", 9, 8, 40, DetailWidth - 16, 15, FieldMenuPalette.Mint);
        _detailViewport = _w.Rect(_detailPanel, "DescriptionViewport", 8, 60, DetailWidth - 16, BodyHeight - 100);
        _detailScroll = MakeScroll(_detailViewport, "Description", out _detailContent);
        _detailText = _w.Text(_detailContent, "Text", "", 11, 0, 0, DetailWidth - 20, BodyHeight - 100);
        _detailText.overflowMode = TextOverflowModes.Overflow;
        _detailAction = _w.Button(_detailPanel, "Action", "", 76, BodyHeight - 36, DetailWidth - 84, 28, new FieldMenuCommand(FieldMenuCommandKind.Confirm));
        _w.Button(_detailPanel, "ScrollUp", "↑", 8, BodyHeight - 36, 28, 28, new FieldMenuCommand(FieldMenuCommandKind.DetailScroll, 0, -1));
        _w.Button(_detailPanel, "ScrollDown", "↓", 40, BodyHeight - 36, 28, 28, new FieldMenuCommand(FieldMenuCommandKind.DetailScroll, 0, 1));
        _growth = _w.Rect(_body, "Companion", 0, 0, ContentWidth, BodyHeight);
        _subTabs[0] = _w.Button(_growth, "StatsTab", "", 0, 0, 146, 28, new FieldMenuCommand(FieldMenuCommandKind.CompanionTab, 0));
        _subTabs[1] = _w.Button(_growth, "SkillsTab", "", 152, 0, 146, 28, new FieldMenuCommand(FieldMenuCommandKind.CompanionTab, 1));
        _growthBody = _w.Rect(_growth, "Body", 0, 28, ContentWidth, BodyHeight - 28);
        _growthView = FieldMenuGrowthView.Create(_growthBody, _w);
        _partyLabel = _w.Text(_frame, "PartyLabel", "", 9, 12, 318, ContentWidth, 12, FieldMenuPalette.Muted);
        for (int i = 0; i < 3; i++) _party[i] = BuildPartyCard(i);
        _w.Fill(_frame, "FooterRule", 12, 378, ContentWidth, 1, FieldMenuPalette.Border);
        _status = _w.Text(_frame, "Status", "", 9, 12, 384, 408, 26, FieldMenuPalette.Muted);
        _settings = _w.Button(_frame, "Settings", L("settings", "설정"), 430, 382, 68, 28, new FieldMenuCommand(FieldMenuCommandKind.Settings));
        _back = _w.Button(_frame, "Back", L("back", "뒤로"), 506, 382, 74, 28, new FieldMenuCommand(FieldMenuCommandKind.Cancel));
        _modal = _w.Rect(_frame, "Confirmation", 0, 0, FrameSize.x, FrameSize.y);
        _w.Fill(_modal, "Cover", 0, 0, FrameSize.x, FrameSize.y, new Color(0, 0, 0, .9f)).raycastTarget = true;
        RectTransform card = _w.Panel(_modal, "Card", 106, 113, 380, 190);
        _modalTitle = _w.Text(card, "Title", "", 16, 14, 12, 352, 42, FieldMenuPalette.Yellow);
        _modalBody = _w.Text(card, "Body", "", 12, 14, 58, 352, 70);
        _modalButtons[0] = _w.Button(card, "Cancel", "", 14, 140, 170, 34, new FieldMenuCommand(FieldMenuCommandKind.ModalAccept, 0));
        _modalButtons[1] = _w.Button(card, "Accept", "", 196, 140, 170, 34, new FieldMenuCommand(FieldMenuCommandKind.ModalAccept, 1));
        _modal.gameObject.SetActive(false);
    }

    private ScrollRect MakeScroll(RectTransform viewport, string name, out RectTransform content)
    {
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        content = _w.Rect(viewport, name, 0, 0, viewport.rect.width, viewport.rect.height);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = true;
        return scroll;
    }

    private PartyCard BuildPartyCard(int index)
    {
        var card = new PartyCard();
        card.Button = _w.Button(_frame, "Party" + index, "", 12 + index * 192, 332, 184, 44, new FieldMenuCommand(FieldMenuCommandKind.Actor, index));
        card.Portrait = _w.Fill(card.Button.transform, "Portrait", 4, 4, 32, 36, Color.white); card.Portrait.preserveAspect = true;
        card.Name = _w.Text(card.Button.transform, "Name", "", 10, 42, 2, 98, 14);
        card.Name.textWrappingMode = TextWrappingModes.NoWrap;
        card.Level = _w.Text(card.Button.transform, "Level", "", 8, 142, 3, 36, 12, FieldMenuPalette.Muted, TextAlignmentOptions.Right);
        card.HpText = _w.Text(card.Button.transform, "HP", "", 9, 42, 16, PartyBarWidth, 12);
        _w.Fill(card.Button.transform, "HpTrack", 42, 27, PartyBarWidth, 2, Color.black);
        card.Hp = _w.Fill(card.Button.transform, "HpFill", 42, 27, PartyBarWidth, 2, FieldMenuPalette.Mint);
        card.ApText = _w.Text(card.Button.transform, "AP", "", 9, 42, 30, PartyBarWidth, 12);
        _w.Fill(card.Button.transform, "ApTrack", 42, 41, PartyBarWidth, 2, Color.black);
        card.Ap = _w.Fill(card.Button.transform, "ApFill", 42, 41, PartyBarWidth, 2, FieldMenuPalette.Yellow);
        return card;
    }

    public void Header(int page, GlobalDataManager global, JournalEntry objective)
    {
        _page = page;
        string[] titles = { L("bag", "소지품"), L("equipment", "장비 정비"), L("companion", "동료와 성장"), L("journal", "수집한 기록") };
        string[] tabs = { L("tab_bag", "가방"), L("tab_equipment", "장비"), L("tab_companion", "동료"), L("tab_journal", "일지") };
        _title.text = titles[Mathf.Clamp(page, 0, 3)];
        for (int i = 0; i < 4; i++) { Label(_tabs[i], tabs[i]); FieldMenuWidgets.Style(_tabs[i], page == i); }
        int level = global != null ? global.PartyLevel : 1, required = global != null ? global.PartyExperienceToNextLevel : 0;
        int exp = global != null ? global.PartyExperience : 0;
        _level.text = L("shared_level", "파티 공용") + "   Lv." + level;
        _exp.text = level >= 99 ? "MAX" : "EXP " + exp + " / " + required;
        _expFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 116 * (level >= 99 ? 1 : required > 0 ? Mathf.Clamp01((float)exp / required) : 0));
        _money.text = "G " + (global != null ? global.Money : 0).ToString("N0");
        _objective.text = L("objective", "현재 목표") + "   " + (objective != null ? objective.LocalizedTitle : L("objective_empty", "아직 기록된 목표가 없습니다."));
        // Bag targeting also needs previous/next on touch when the party exceeds three.
        bool actor = page != 3;
        _actor.gameObject.SetActive(actor);
        _body.anchoredPosition = new Vector2(12, actor ? -BodyTop : -(BodyTop - 30));
        float height = actor ? BodyHeight : BodyHeight + 30;
        _body.sizeDelta = new Vector2(ContentWidth, height); _listPanel.sizeDelta = new Vector2(ListWidth, height); _detailPanel.sizeDelta = new Vector2(DetailWidth, height);
        _listViewport.sizeDelta = new Vector2(ListWidth - 10, height - 42);
        _detailViewport.sizeDelta = new Vector2(DetailWidth - 16, height - 100);
        for (int i = 0; i < _detailPanel.childCount; i++)
        {
            Transform child = _detailPanel.GetChild(i);
            if (child.TryGetComponent(out Button button))
            {
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(height - 36));
            }
        }
        _growth.gameObject.SetActive(page == 2); _listPanel.gameObject.SetActive(page != 2); _detailPanel.gameObject.SetActive(page != 2);
        Label(_subTabs[0], L("stats", "상태 · 능력치")); Label(_subTabs[1], L("skills", "스킬 트리"));
        Label(_settings, L("settings", "설정")); Label(_back, L("back", "뒤로"));
    }

    public void Actor(CharacterSaveData member, CharacterData data, string name, int pending)
    {
        _actorName.text = name ?? string.Empty;
        Sprite portrait = data != null ? data.Portrait : null;
        _actorPortrait.sprite = portrait; _actorPortrait.enabled = portrait != null;
        _points.text = member != null && member.Growth != null
            ? "P " + Mathf.Max(0, member.Growth.AvailableAttributePoints - pending) + "  /  SP " + member.Growth.AvailableSkillPoints + (pending > 0 ? "  (" + pending + ")" : "") : string.Empty;
    }

    public void Filters(IReadOnlyList<string> labels, int selected)
    {
        int count = labels != null ? Mathf.Min(labels.Count, 6) : 0;
        for (int i = 0; i < 6; i++)
        {
            _filters[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            var rect = (RectTransform)_filters[i].transform;
            float step = (ListWidth - 6) / Mathf.Max(1, count);
            float left = Mathf.Round(i * step), right = Mathf.Round((i + 1) * step) - 4;
            rect.anchoredPosition = new Vector2(5 + left, -3); rect.sizeDelta = new Vector2(right - left, 28);
            Label(_filters[i], labels[i]);
            _filters[i].GetComponentInChildren<TMP_Text>().fontSize = count > 3 ? 9 : 11;
            FieldMenuWidgets.Style(_filters[i], i == selected);
        }
    }

    public void List(IReadOnlyList<FieldMenuListEntry> entries, int selected)
    {
        int count = entries != null ? entries.Count : 0;
        while (_rows.Count < count)
        {
            int i = _rows.Count;
            Button button = _w.Button(_listContent, "Row" + i, "", 0, i * 33, ListWidth - 12, 30, new FieldMenuCommand(FieldMenuCommandKind.Row, i));
            Image icon = _w.Fill(button.transform, "Icon", 5, 3, 24, 24, Color.white); icon.preserveAspect = true;
            _rows.Add(new Row { Button = button, Icon = icon, Name = _w.Text(button.transform, "Name", "", 11, 36, 5, 198, 22), Hint = _w.Text(button.transform, "Hint", "", 9, 238, 7, 64, 20, FieldMenuPalette.Muted, TextAlignmentOptions.Right) });
        }
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i]; row.Button.gameObject.SetActive(i < count); if (i >= count) continue;
            FieldMenuListEntry entry = entries[i]; row.Name.text = entry.Name; row.Hint.text = entry.Hint;
            row.Icon.sprite = entry.Icon; row.Icon.enabled = entry.Icon != null;
            // Unavailable rows stay selectable so their restrictions can be read.
            FieldMenuWidgets.Style(row.Button, i == selected); row.Name.color = entry.Enabled ? (i == selected ? FieldMenuPalette.Yellow : FieldMenuPalette.Text) : FieldMenuPalette.Muted;
        }
        _listContent.sizeDelta = new Vector2(ListWidth - 10, Mathf.Max(_listViewport.rect.height, count * 33));
        float start = Mathf.Clamp(_listContent.anchoredPosition.y, 0, Mathf.Max(0, count * 33 - _listViewport.rect.height));
        if (selected * 33 < start) start = selected * 33;
        if (selected * 33 + 30 > start + _listViewport.rect.height) start = selected * 33 + 30 - _listViewport.rect.height;
        _listContent.anchoredPosition = new Vector2(0, Mathf.Max(0, start));
    }

    public void Detail(string title, string subtitle, string description, string action, bool enabled)
    {
        _detailTitle.text = title ?? string.Empty; _detailSubtitle.text = subtitle ?? string.Empty;
        if (!string.Equals(_detailValue, description, StringComparison.Ordinal)
            || !Mathf.Approximately(_detailHeight, _detailViewport.rect.height))
        {
            _detailHeight = _detailViewport.rect.height;
            _detailValue = description; _detailText.text = description ?? string.Empty;
            float preferred = _detailText.GetPreferredValues(_detailText.text, DetailWidth - 20, 100000).y;
            _detailContent.sizeDelta = new Vector2(DetailWidth - 16, Mathf.Max(_detailViewport.rect.height, preferred + 8));
            _detailText.rectTransform.sizeDelta = _detailContent.sizeDelta;
            _detailContent.anchoredPosition = Vector2.zero;
        }
        Label(_detailAction, action ?? string.Empty); FieldMenuWidgets.Style(_detailAction, true, enabled);
    }

    public void ScrollDetail(float direction)
    {
        if (_page == 2) { _growthView.ScrollDetail(direction); return; }
        float max = Mathf.Max(0, _detailContent.rect.height - _detailViewport.rect.height);
        _detailContent.anchoredPosition = new Vector2(0, Mathf.Clamp(_detailContent.anchoredPosition.y + direction * 34, 0, max));
    }

    public void Growth(CharacterSaveData member, CharacterData data, PowerGrowthTab tab, int statIndex,
        CharacterStatInvestments draft, IReadOnlyList<SkillTreeNodeView> nodes, int nodeIndex, bool focus, bool mutable)
    {
        FieldMenuWidgets.Style(_subTabs[0], tab == PowerGrowthTab.Stats); FieldMenuWidgets.Style(_subTabs[1], tab == PowerGrowthTab.Skills);
        _growthView.Render(member, data, tab, statIndex, draft, nodes, nodeIndex, focus, mutable);
    }

    public void Party(GlobalDataManager global, int selected)
    {
        List<CharacterSaveData> members = global != null ? global.Party : null;
        int count = members != null ? members.Count : 0, start = Mathf.Max(0, selected / 3 * 3);
        _partyLabel.text = L("party", "함께하는 동료") + "   " + (count > 0 ? (start + 1) + "–" + Mathf.Min(start + 3, count) + " / " + count : "0");
        for (int i = 0; i < 3; i++)
        {
            int index = start + i; PartyCard card = _party[i]; bool valid = index < count && members[index] != null;
            card.Button.gameObject.SetActive(valid); if (!valid) continue;
            CharacterSaveData member = members[index]; CharacterData data = CharacterDatabase.FindById(member.CharacterDataID);
            StatBlock stats = data != null ? CharacterStatsProjectionService.ResolveFromSave(member, data) : null;
            int hp = stats != null ? stats.MaxHP : member.MaxHP, ap = stats != null ? stats.MaxAP : member.MaxAP;
            card.Name.text = data != null ? data.ResolveDisplayName(global.PlayerName) : member.CharacterID;
            card.Level.text = "Lv." + global.PartyLevel;
            card.Portrait.sprite = data != null ? data.Portrait : null; card.Portrait.enabled = card.Portrait.sprite != null;
            card.HpText.text = "HP " + member.HP + "/" + hp; card.ApText.text = "AP " + member.AP + "/" + ap;
            card.Hp.color = data != null ? data.BattleSymbolColor : FieldMenuPalette.Mint;
            card.Hp.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PartyBarWidth * (hp > 0 ? Mathf.Clamp01((float)member.HP / hp) : 0));
            card.Ap.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PartyBarWidth * (ap > 0 ? Mathf.Clamp01((float)member.AP / ap) : 0));
            card.Button.GetComponent<FieldMenuButtonRelay>().Configure(Dispatch, new FieldMenuCommand(FieldMenuCommandKind.Actor, index), false);
            FieldMenuWidgets.Style(card.Button, index == selected, true, FieldMenuPalette.Mint);
        }
    }

    private Action<FieldMenuCommand> _command;
    public void EnsureNavigationVisible(Button button)
    {
        if (button == null) return;
        ScrollRect scroll = button.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        RectTransform target = (RectTransform)button.transform;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
        Rect viewport = scroll.viewport.rect;
        Vector2 adjustment = Vector2.zero;
        if (scroll.vertical)
        {
            if (bounds.max.y > viewport.yMax) adjustment.y = viewport.yMax - bounds.max.y;
            else if (bounds.min.y < viewport.yMin) adjustment.y = viewport.yMin - bounds.min.y;
        }
        if (scroll.horizontal)
        {
            if (bounds.min.x < viewport.xMin) adjustment.x = viewport.xMin - bounds.min.x;
            else if (bounds.max.x > viewport.xMax) adjustment.x = viewport.xMax - bounds.max.x;
        }
        scroll.StopMovement();
        Vector2 position = scroll.content.anchoredPosition + adjustment;
        position.x = Mathf.Clamp(position.x, Mathf.Min(0, viewport.width - scroll.content.rect.width), 0);
        position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, scroll.content.rect.height - viewport.height));
        scroll.content.anchoredPosition = position;
    }
    public void Bind(Action<FieldMenuCommand> command) { _command = command; }
    private void Dispatch(FieldMenuCommand command) { _command?.Invoke(command); }
    public void Status(string value) { _status.text = value ?? string.Empty; }
    public void Modal(bool visible, string title = null, string body = null, string accept = null, bool choice = false)
    {
        _growthView.SetInteractable(!visible);
        _modal.gameObject.SetActive(visible); if (!visible) return;
        _modalTitle.text = title; _modalBody.text = body;
        Label(_modalButtons[0], L("back", "돌아가기")); Label(_modalButtons[1], accept);
        FieldMenuWidgets.Style(_modalButtons[0], !choice); FieldMenuWidgets.Style(_modalButtons[1], choice);
    }
    private static void Label(Button button, string text)
    { TMP_Text label = button.GetComponentInChildren<TMP_Text>(); if (label != null) label.text = text ?? string.Empty; }
}
