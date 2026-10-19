using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Read-only growth presentation. Only the owning menu dispatches save mutations.</summary>
public sealed class FieldMenuGrowthView : MonoBehaviour
{
    private const float LeftWidth = 342f;
    private const float RightX = 354f;
    private const float RightWidth = 250f;
    private const float TreeHeight = 160f;
    private const float NodeWidth = 88f;
    private const float NodeHeight = 36f;
    private const float TreePadding = 12f;
    private const float AuthoredSpacing = 1.5f;
    private static readonly Color LockedColor = new Color32(108, 95, 124, 255);
    private readonly StatRow[] _rows = new StatRow[5];
    private readonly List<NodeVisual> _nodes = new List<NodeVisual>();
    private readonly List<LinkVisual> _links = new List<LinkVisual>();
    private readonly Dictionary<string, int> _nodeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly StringBuilder _detailBuilder = new StringBuilder(256);
    private FieldMenuWidgets _widgets;
    private RectTransform _statsRoot, _skillsRoot, _treeViewport, _treeContent, _linkRoot, _nodeRoot;
    private ScrollRect _treeScroll, _detailScroll;
    private CanvasGroup _interactionGroup;
    private TextMeshProUGUI _statTitle, _statDetail, _statPoints, _statHint;
    private TextMeshProUGUI _skillTitle, _skillDetail, _skillPoints, _skillActionLabel, _treeEmpty, _treeHint;
    private TextMeshProUGUI _applyLabel, _clearLabel, _resetStatsLabel, _resetSkillsLabel;
    private RectTransform _skillDetailContent;
    private Button _apply, _clear, _resetStats, _skillAction, _resetSkills;
    private bool _mutable;
    private bool _interactable = true;
    private bool _suppressInputSubmit;
    private CharacterSaveData _inputMember;
    private SkillTreeDefinition _layoutTree, _focusedTree;
    private CharacterSaveData _focusedMember;
    private string _focusedNodeId;
    private int _layoutCount = -1;

    private sealed class StatRow
    {
        public Button Select, Minus, Plus;
        public TextMeshProUGUI Label;
        public TMP_InputField Input;
        public int Amount;
    }

    private sealed class NodeVisual
    {
        public Button Button;
        public RectTransform Rect;
        public Image Fill;
        public Outline Border;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI StateText;
        public SkillTreeNodeDefinition Definition;
        public Vector2 AuthoredPosition;
    }

    private sealed class LinkVisual
    {
        public readonly Image[] Segments = new Image[3];
        public int ParentIndex, ChildIndex;
    }

    public bool IsTextEditing
    {
        get
        {
            if (_statsRoot == null || !_statsRoot.gameObject.activeInHierarchy) return false;
            for (int i = 0; i < _rows.Length; i++)
                if (_rows[i].Input.isFocused) return true;
            return false;
        }
    }

    public static FieldMenuGrowthView Create(RectTransform parent, FieldMenuWidgets widgets)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (widgets == null) throw new ArgumentNullException(nameof(widgets));
        RectTransform root = widgets.Rect(parent, "Growth", 0, 0, 604, 178);
        var view = root.gameObject.AddComponent<FieldMenuGrowthView>();
        view._widgets = widgets;
        view._interactionGroup = root.gameObject.AddComponent<CanvasGroup>();
        view.BuildStats(root);
        view.BuildSkills(root);
        return view;
    }

    public void SetInteractable(bool interactable)
    {
        _interactable = interactable;
        // TMP closes the native keyboard only while its Selectable is still interactable.
        // Reject mutations first, release editing, then disable the parent CanvasGroup.
        if (!interactable) EndTextEditing();
        _interactionGroup.interactable = interactable;
        _interactionGroup.blocksRaycasts = interactable;
    }

    public void EndTextEditing()
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform))
            events.SetSelectedGameObject(null);
        for (int i = 0; i < _rows.Length; i++)
            if (_rows[i].Input.isFocused) _rows[i].Input.DeactivateInputField();
    }

    public void Render(CharacterSaveData member, CharacterData data, PowerGrowthTab tab,
        int selectedStatIndex, CharacterStatInvestments draft, IReadOnlyList<SkillTreeNodeView> nodes,
        int selectedNodeIndex, bool focus, bool mutable)
    {
        _mutable = mutable && member != null && data != null;
        bool stats = tab == PowerGrowthTab.Stats;
        _statsRoot.gameObject.SetActive(stats);
        _skillsRoot.gameObject.SetActive(!stats);
        if (stats) RenderStats(member, data, selectedStatIndex, draft, focus);
        else RenderSkills(member, data, nodes, selectedNodeIndex, focus);
    }

    private void BuildStats(RectTransform root)
    {
        _statsRoot = _widgets.Rect(root, "Attributes", 0, 0, 604, 178);
        RectTransform list = _widgets.Panel(_statsRoot, "AttributeRows", 0, 0, LeftWidth, 178);
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = i;
            Button select = _widgets.Button(list, "Stat" + i, string.Empty, 4, 4 + i * 34, 334, 32,
                new FieldMenuCommand(FieldMenuCommandKind.StatSelect, i));
            var row = new StatRow { Select = select, Label = select.GetComponentInChildren<TextMeshProUGUI>() };
            row.Label.rectTransform.anchoredPosition = new Vector2(6, -1);
            row.Label.rectTransform.sizeDelta = new Vector2(174, 30);
            row.Label.alignment = TextAlignmentOptions.MidlineLeft;
            row.Label.fontSize = 10;
            row.Label.textWrappingMode = TextWrappingModes.NoWrap;
            row.Minus = _widgets.Button(select.transform, "Minus", "−", 182, 0, 32, 32,
                new FieldMenuCommand(FieldMenuCommandKind.StatStep, i, -1), true);
            row.Plus = _widgets.Button(select.transform, "Plus", "+", 302, 0, 32, 32,
                new FieldMenuCommand(FieldMenuCommandKind.StatStep, i, 1), true);
            row.Input = CreateNumberInput(select.transform, i, 218, 0, 80, 32);
            row.Input.onSelect.AddListener(_ =>
            {
                if (_interactable) _widgets.Dispatch(new FieldMenuCommand(FieldMenuCommandKind.StatSelect, index));
            });
            row.Input.onEndEdit.AddListener(value => SubmitAmount(index, value));
            _rows[i] = row;
        }

        RectTransform detail = _widgets.Panel(_statsRoot, "AttributeDetail", RightX, 0, RightWidth, 178);
        _statTitle = _widgets.Text(detail, "Title", string.Empty, 13, 9, 5, 232, 19, FieldMenuPalette.Yellow);
        _statDetail = _widgets.Text(detail, "Detail", string.Empty, 10, 9, 27, 232, 57);
        _statDetail.lineSpacing = 1;
        _statPoints = _widgets.Text(detail, "Points", string.Empty, 11, 9, 88, 232, 19, FieldMenuPalette.Mint);
        _apply = _widgets.Button(detail, "Apply", string.Empty, 8, 112, 234, 28,
            new FieldMenuCommand(FieldMenuCommandKind.Confirm));
        _clear = _widgets.Button(detail, "ClearDraft", string.Empty, 8, 145, 114, 22,
            new FieldMenuCommand(FieldMenuCommandKind.ClearDraft));
        _resetStats = _widgets.Button(detail, "Reset", string.Empty, 128, 145, 114, 22,
            new FieldMenuCommand(FieldMenuCommandKind.Reset));
        _applyLabel = _apply.GetComponentInChildren<TextMeshProUGUI>();
        _clearLabel = _clear.GetComponentInChildren<TextMeshProUGUI>();
        _resetStatsLabel = _resetStats.GetComponentInChildren<TextMeshProUGUI>();
        _statHint = _widgets.Text(detail, "Hint", string.Empty, 8, 8, 168, 234, 10, FieldMenuPalette.Muted);
    }

    private TMP_InputField CreateNumberInput(Transform parent, int index, float x, float y, float width, float height)
    {
        RectTransform rect = _widgets.Panel(parent, "Amount" + index, x, y, width, height);
        Image background = rect.GetComponent<Image>();
        background.color = FieldMenuPalette.Background;
        background.raycastTarget = true;
        RectTransform viewport = _widgets.Rect(rect, "TextArea", 4, 1, width - 8, height - 2);
        viewport.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = _widgets.Text(viewport, "Value", "0", 12, 0, 0, width - 8, height - 2,
            FieldMenuPalette.Yellow, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = false;
        var input = rect.gameObject.AddComponent<FieldMenuNumberInput>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = text;
        input.fontAsset = _widgets.Font;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.keyboardType = TouchScreenKeyboardType.NumberPad;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 9;
        input.richText = false;
        input.caretColor = FieldMenuPalette.Yellow;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.55f, 0.45f, 0.65f, 0.5f);
        Navigation navigation = input.navigation;
        navigation.mode = Navigation.Mode.None;
        input.navigation = navigation;
        input.SetTextWithoutNotify("0");
        return input;
    }

    private void SubmitAmount(int index, string value)
    {
        StatRow row = _rows[index];
        if (!_suppressInputSubmit && _mutable && _interactable && _statsRoot.gameObject.activeInHierarchy
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) && amount >= 0)
            _widgets.Dispatch(new FieldMenuCommand(FieldMenuCommandKind.StatSet, index, amount));
        else
            row.Input.SetTextWithoutNotify(row.Amount.ToString(CultureInfo.InvariantCulture));
    }

    private void RenderStats(CharacterSaveData member, CharacterData data, int selectedIndex,
        CharacterStatInvestments draft, bool focus)
    {
        // A pointer can switch actors while the native keyboard is open. Never submit
        // the previous actor's uncommitted text against the newly selected actor.
        if (_inputMember != member)
        {
            _suppressInputSubmit = true;
            EndTextEditing();
            _suppressInputSubmit = false;
            _inputMember = member;
        }
        bool valid = member != null && data != null;
        CharacterSaveData preview = valid ? CreatePreview(member, draft) : null;
        StatBlock currentStats = valid ? CharacterStatsProjectionService.ResolveFromSave(member, data) : null;
        StatBlock previewStats = valid ? CharacterStatsProjectionService.ResolveFromSave(preview, data) : null;
        CharacterBaseStatSnapshot grown = valid ? CharacterGrowthService.CalculateBaseStats(member, data) : default;
        int spent = draft?.Total ?? 0;
        int available = member?.Growth?.AvailableAttributePoints ?? 0;
        int remaining = Mathf.Max(0, available - spent);
        int maxRank = CharacterGrowthService.ResolveMaxInvestmentRank(data != null ? data.GrowthProfile : null);
        int selected = Mathf.Clamp(selectedIndex, 0, 4);
        bool validDraft = valid && spent <= available;
        for (int i = 0; i < _rows.Length; i++)
        {
            GrowthStat stat = (GrowthStat)i;
            StatRow row = _rows[i];
            int amount = draft?.Get(stat) ?? 0;
            int rank = member?.Growth?.Investments?.Get(stat) ?? 0;
            if (amount > maxRank - rank) validDraft = false;
            row.Amount = amount;
            row.Label.text = StatName(i) + "  " + ReadStat(currentStats, i) + " → " + ReadStat(previewStats, i);
            if (!row.Input.isFocused) row.Input.SetTextWithoutNotify(amount.ToString(CultureInfo.InvariantCulture));
            row.Input.interactable = _mutable;
            FieldMenuWidgets.Style(row.Select, focus && i == selected, valid);
            FieldMenuWidgets.Style(row.Minus, false, _mutable && amount > 0);
            FieldMenuWidgets.Style(row.Plus, false, _mutable && remaining > 0 && rank + (long)amount < maxRank);
        }

        _statTitle.text = StatName(selected) + " · " + StatUnit(selected);
        if (valid)
        {
            int authored = ReadStat(data.BaseStats, selected);
            int grownValue = ReadBaseStat(grown, selected);
            int rank = member.Growth?.Investments?.Get((GrowthStat)selected) ?? 0;
            int amount = draft?.Get((GrowthStat)selected) ?? 0;
            int equipment = ReadStat(currentStats, selected) - grownValue;
            _statDetail.text = string.Format(L("stat_breakdown", "기본 {0} / 투자 {1} / 장비 {2}"),
                    authored, Signed(grownValue - authored), Signed(equipment))
                + "\n" + string.Format(L("stat_rank", "투자 단계 {0} → {1} / {2}"), rank, (long)rank + amount, maxRank)
                + "\n" + string.Format(L("stat_increment", "1포인트당 {0} +{1}"),
                    StatUnit(selected), ResolveIncrement(data.GrowthProfile, selected));
        }
        else _statDetail.text = L("no_member", "성장 정보를 확인할 동료가 없습니다.");
        _statPoints.text = string.Format(L("attribute_points", "남은 포인트 {0}    미리 배분 {1}"), remaining, spent);
        _statPoints.color = validDraft ? FieldMenuPalette.Mint : FieldMenuPalette.Yellow;
        _statHint.text = !_mutable ? L("mutation_locked", "지금은 성장 정보를 변경할 수 없습니다.")
            : L("amount_hint", "입력 숫자는 이번에 추가할 포인트입니다.");
        _applyLabel.text = L("apply_draft", "배분 한 번에 적용");
        _clearLabel.text = L("clear_draft", "미리 배분 취소");
        _resetStatsLabel.text = L("reset_free", "무료 초기화");
        FieldMenuWidgets.Style(_apply, focus && spent > 0, _mutable && validDraft && spent > 0, FieldMenuPalette.Mint);
        FieldMenuWidgets.Style(_clear, false, _mutable && spent > 0);
        FieldMenuWidgets.Style(_resetStats, false, _mutable);
    }

    private static CharacterSaveData CreatePreview(CharacterSaveData member, CharacterStatInvestments draft)
    {
        var preview = new CharacterSaveData
        {
            CharacterDataID = member.CharacterDataID, CharacterID = member.CharacterID,
            Level = member.Level, EXP = member.EXP, HP = member.HP, AP = member.AP,
            MaxHP = member.MaxHP, MaxAP = member.MaxAP, ATK = member.ATK, DEF = member.DEF, SPD = member.SPD,
            HasInitializedEquipment = member.HasInitializedEquipment,
            Growth = member.Growth?.Clone() ?? new CharacterGrowthSaveData(),
            EquippedEquipmentIDs = member.EquippedEquipmentIDs != null
                ? new List<string>(member.EquippedEquipmentIDs) : new List<string>()
        };
        for (int i = 0; i < 5; i++)
        {
            GrowthStat stat = (GrowthStat)i;
            long rank = (long)preview.Growth.Investments.Get(stat) + (draft?.Get(stat) ?? 0);
            preview.Growth.Investments.Set(stat, (int)Math.Min(int.MaxValue, rank));
        }
        return preview;
    }

    private void BuildSkills(RectTransform root)
    {
        _skillsRoot = _widgets.Rect(root, "Skills", 0, 0, 604, 178);
        RectTransform treePanel = _widgets.Panel(_skillsRoot, "SkillTree", 0, 0, LeftWidth, TreeHeight);
        treePanel.GetComponent<Image>().raycastTarget = true;
        _treeViewport = _widgets.Rect(treePanel, "Viewport", 0, 0, LeftWidth, TreeHeight);
        _treeViewport.gameObject.AddComponent<RectMask2D>();
        _treeContent = _widgets.Rect(_treeViewport, "TreeContent", 0, 0, LeftWidth, TreeHeight);
        _linkRoot = _widgets.Rect(_treeContent, "Connections", 0, 0, LeftWidth, TreeHeight);
        _nodeRoot = _widgets.Rect(_treeContent, "Nodes", 0, 0, LeftWidth, TreeHeight);
        _treeScroll = treePanel.gameObject.AddComponent<ScrollRect>();
        _treeScroll.viewport = _treeViewport;
        _treeScroll.content = _treeContent;
        _treeScroll.horizontal = _treeScroll.vertical = true;
        _treeScroll.movementType = ScrollRect.MovementType.Clamped;
        _treeScroll.scrollSensitivity = 24;
        _treeEmpty = _widgets.Text(treePanel, "Empty", string.Empty, 12,
            12, 64, LeftWidth - 24, 36, FieldMenuPalette.Muted, TextAlignmentOptions.Center);
        _treeHint = _widgets.Text(_skillsRoot, "TreeHint", string.Empty, 9,
            0, 164, LeftWidth, 14, FieldMenuPalette.Muted);

        RectTransform detail = _widgets.Panel(_skillsRoot, "SkillDetail", RightX, 0, RightWidth, 178);
        _skillTitle = _widgets.Text(detail, "Title", string.Empty, 13, 8, 5, 234, 19, FieldMenuPalette.Yellow);
        RectTransform viewport = _widgets.Rect(detail, "DetailViewport", 8, 28, 234, 79);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        _skillDetailContent = _widgets.Rect(viewport, "Content", 0, 0, 234, 79);
        _skillDetail = _widgets.Text(_skillDetailContent, "Description", string.Empty, 10, 0, 0, 234, 79);
        _skillDetail.overflowMode = TextOverflowModes.Overflow;
        _skillDetail.lineSpacing = 1;
        _detailScroll = viewport.gameObject.AddComponent<ScrollRect>();
        _detailScroll.viewport = viewport;
        _detailScroll.content = _skillDetailContent;
        _detailScroll.horizontal = false;
        _detailScroll.movementType = ScrollRect.MovementType.Clamped;
        _detailScroll.scrollSensitivity = 18;
        _skillPoints = _widgets.Text(detail, "SkillPoints", string.Empty, 10, 8, 111, 234, 16, FieldMenuPalette.Mint);
        _skillAction = _widgets.Button(detail, "SkillAction", string.Empty, 8, 134, 150, 32,
            new FieldMenuCommand(FieldMenuCommandKind.Confirm));
        _skillActionLabel = _skillAction.GetComponentInChildren<TextMeshProUGUI>();
        _resetSkills = _widgets.Button(detail, "ResetSkills", string.Empty, 164, 134, 78, 32,
            new FieldMenuCommand(FieldMenuCommandKind.Reset));
        _resetSkillsLabel = _resetSkills.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void RenderSkills(CharacterSaveData member, CharacterData data, IReadOnlyList<SkillTreeNodeView> nodes,
        int selectedIndex, bool focus)
    {
        int count = nodes?.Count ?? 0;
        SkillTreeDefinition tree = data != null ? data.SkillTree : null;
        bool layoutChanged = NeedsTreeLayout(tree, nodes);
        if (layoutChanged) LayoutTree(tree, nodes);
        _treeEmpty.gameObject.SetActive(count == 0);
        _treeEmpty.text = L("tree_empty", "등록된 스킬 트리가 없습니다.");
        _treeHint.text = L("tree_hint", "잠김 · 습득 가능 · 습득 · 장착     넓은 트리는 밀어서 이동");
        _resetSkillsLabel.text = L("reset_free", "무료 초기화");
        int selected = count > 0 ? Mathf.Clamp(selectedIndex, 0, count - 1) : -1;
        for (int i = 0; i < count; i++)
        {
            SkillTreeNodeView node = nodes[i];
            NodeVisual visual = _nodes[i];
            Color tone = NodeColor(node.State);
            FieldMenuWidgets.Style(visual.Button, focus && i == selected, true, tone);
            visual.Fill.color = node.State == SkillTreeNodeState.Equipped
                ? new Color32(31, 58, 57, 255) : node.State == SkillTreeNodeState.Locked
                    ? FieldMenuPalette.Background : FieldMenuPalette.Panel;
            visual.Border.effectColor = i == selected && focus ? FieldMenuPalette.Yellow : tone;
            visual.Label.color = tone;
            visual.StateText.color = tone;
            SkillTreeNodeDefinition definition = node.Definition;
            visual.Label.text = definition?.Skill != null ? definition.Skill.SkillName : definition?.ResolveId() ?? L("unset", "미설정");
            visual.StateText.text = StateLabel(node.State) + " · " + Mathf.Max(0, definition?.Cost ?? 0) + " SP";
        }
        for (int i = 0; i < _links.Count; i++)
        {
            LinkVisual link = _links[i];
            if (!link.Segments[0].gameObject.activeSelf) continue;
            Color color = nodes[link.ParentIndex].IsUnlocked && nodes[link.ChildIndex].IsUnlocked
                ? FieldMenuPalette.Mint : FieldMenuPalette.Border;
            for (int j = 0; j < link.Segments.Length; j++) link.Segments[j].color = color;
        }

        SkillTreeNodeDefinition selectedDefinition = selected >= 0 ? nodes[selected].Definition : null;
        string nodeId = selectedDefinition?.ResolveId() ?? string.Empty;
        bool selectionChanged = layoutChanged || _focusedTree != tree || _focusedMember != member
            || !string.Equals(_focusedNodeId, nodeId, StringComparison.Ordinal);
        _skillPoints.text = string.Format(L("skill_points", "남은 SP {0}    장착 {1} / {2}"),
            member?.Growth?.AvailableSkillPoints ?? 0, member?.EquippedSkillIDs?.Count ?? 0,
            tree != null ? Mathf.Max(1, tree.MaximumEquippedSkills) : 0);
        FieldMenuWidgets.Style(_resetSkills, false, _mutable);
        if (selectedDefinition == null)
        {
            _skillTitle.text = L("tree_title", "스킬 트리");
            _skillDetail.text = L("tree_setup", "캐릭터 데이터에 스킬 트리를 연결하면 실제 노드와 선행 관계가 표시됩니다.");
            _skillActionLabel.text = L("no_skill", "스킬 없음");
            FieldMenuWidgets.Style(_skillAction, false, false);
        }
        else
        {
            SkillTreeNodeView selectedNode = nodes[selected];
            SkillData skill = selectedDefinition.Skill;
            _skillTitle.text = skill != null ? skill.SkillName : nodeId;
            _detailBuilder.Clear();
            _detailBuilder.Append(StateLabel(selectedNode.State));
            _detailBuilder.Append("  ·  AP ").Append(skill != null ? skill.APCost.ToString() : "-");
            _detailBuilder.Append("\n").Append(skill != null && !string.IsNullOrWhiteSpace(skill.Description)
                ? skill.Description : L("no_description", "설명이 없습니다."));
            _detailBuilder.Append("\n").Append(string.Format(L("skill_requirements", "필요 Lv {0}  ·  비용 {1} SP"),
                Mathf.Max(1, selectedDefinition.RequiredLevel), Mathf.Max(0, selectedDefinition.Cost)));
            AppendPrerequisites(selectedDefinition, nodes);
            if (!string.IsNullOrWhiteSpace(selectedNode.LockReason))
                _detailBuilder.Append("\n").Append(selectedNode.LockReason);
            _skillDetail.text = _detailBuilder.ToString();
            _skillActionLabel.text = selectedNode.State == SkillTreeNodeState.Equipped ? L("unequip", "장착 해제")
                : selectedNode.State == SkillTreeNodeState.Unlocked ? L("equip", "장착")
                    : selectedNode.State == SkillTreeNodeState.Available ? L("learn_skill", "스킬 습득") : L("requirements_unmet", "조건 미충족");
            FieldMenuWidgets.Style(_skillAction, focus,
                _mutable && skill != null && selectedNode.State != SkillTreeNodeState.Locked, NodeColor(selectedNode.State));
            if (selectionChanged) EnsureSelectedVisible(_nodes[selected].Rect);
        }
        float detailHeight = Mathf.Max(79, _skillDetail.GetPreferredValues(_skillDetail.text, 234, 0).y + 3);
        _skillDetail.rectTransform.sizeDelta = new Vector2(234, detailHeight);
        _skillDetailContent.sizeDelta = new Vector2(234, detailHeight);
        if (selectionChanged) _detailScroll.verticalNormalizedPosition = 1;
        _focusedTree = tree;
        _focusedMember = member;
        _focusedNodeId = nodeId;
    }

    private bool NeedsTreeLayout(SkillTreeDefinition tree, IReadOnlyList<SkillTreeNodeView> nodes)
    {
        int count = nodes?.Count ?? 0;
        if (_layoutTree != tree || _layoutCount != count) return true;
        for (int i = 0; i < count; i++)
            if (_nodes[i].Definition != nodes[i].Definition
                || (_nodes[i].Definition != null && _nodes[i].AuthoredPosition != nodes[i].Definition.Position)) return true;
        return false;
    }

    private void LayoutTree(SkillTreeDefinition tree, IReadOnlyList<SkillTreeNodeView> nodes)
    {
        int count = nodes?.Count ?? 0;
        while (_nodes.Count < count)
        {
            int index = _nodes.Count;
            Button button = _widgets.Button(_nodeRoot, "Node" + index, string.Empty, 0, 0, NodeWidth, NodeHeight,
                new FieldMenuCommand(FieldMenuCommandKind.SkillSelect, index));
            var visual = new NodeVisual
            {
                Button = button, Rect = button.transform as RectTransform,
                Fill = button.targetGraphic as Image, Border = button.GetComponent<Outline>(),
                Label = button.GetComponentInChildren<TextMeshProUGUI>()
            };
            visual.Label.fontSize = 10;
            visual.Label.rectTransform.anchoredPosition = new Vector2(4, -1);
            visual.Label.rectTransform.sizeDelta = new Vector2(NodeWidth - 8, 18);
            visual.Label.textWrappingMode = TextWrappingModes.NoWrap;
            visual.StateText = _widgets.Text(button.transform, "State", string.Empty, 8, 3, 19, NodeWidth - 6, 15,
                FieldMenuPalette.Muted, TextAlignmentOptions.Center);
            visual.StateText.textWrappingMode = TextWrappingModes.NoWrap;
            _nodes.Add(visual);
        }
        _nodeIndices.Clear();
        Vector2 minimum = Vector2.zero, maximum = Vector2.zero;
        bool havePosition = false;
        for (int i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].Button.gameObject.SetActive(i < count);
            if (i >= count) continue;
            SkillTreeNodeDefinition definition = nodes[i].Definition;
            Vector2 authored = definition != null ? definition.Position : Vector2.zero;
            Vector2 position = authored * AuthoredSpacing;
            if (!havePosition) { minimum = maximum = position; havePosition = true; }
            else { minimum = Vector2.Min(minimum, position); maximum = Vector2.Max(maximum, position); }
            _nodes[i].Definition = definition;
            _nodes[i].AuthoredPosition = authored;
            string id = definition?.ResolveId();
            if (!string.IsNullOrEmpty(id)) _nodeIndices[id] = i;
        }
        Vector2 contentSize = new Vector2(Mathf.Max(LeftWidth, maximum.x - minimum.x + NodeWidth + TreePadding * 2),
            Mathf.Max(TreeHeight, maximum.y - minimum.y + NodeHeight + TreePadding * 2));
        _treeContent.sizeDelta = _nodeRoot.sizeDelta = _linkRoot.sizeDelta = contentSize;
        for (int i = 0; i < count; i++)
        {
            Vector2 authored = _nodes[i].AuthoredPosition * AuthoredSpacing;
            // Position uses Cartesian Y-up, matching the existing skill-tree authoring view.
            _nodes[i].Rect.anchoredPosition = new Vector2(TreePadding + authored.x - minimum.x,
                -(TreePadding + maximum.y - authored.y));
        }
        int linkCount = 0;
        for (int i = 0; i < count; i++)
        {
            List<string> prerequisites = nodes[i].Definition?.PrerequisiteNodeIds;
            if (prerequisites == null) continue;
            for (int j = 0; j < prerequisites.Count; j++)
            {
                string id = prerequisites[j]?.Trim();
                if (string.IsNullOrEmpty(id) || !_nodeIndices.TryGetValue(id, out int parentIndex)) continue;
                LinkVisual link = GetLink(linkCount++);
                link.ParentIndex = parentIndex;
                link.ChildIndex = i;
                Vector2 from = _nodes[parentIndex].Rect.anchoredPosition + new Vector2(NodeWidth, -NodeHeight * 0.5f);
                Vector2 to = _nodes[i].Rect.anchoredPosition + new Vector2(0, -NodeHeight * 0.5f);
                float midX = (from.x + to.x) * 0.5f;
                PlaceSegment(link.Segments[0], from, new Vector2(midX, from.y));
                PlaceSegment(link.Segments[1], new Vector2(midX, from.y), new Vector2(midX, to.y));
                PlaceSegment(link.Segments[2], new Vector2(midX, to.y), to);
            }
        }
        for (int i = linkCount; i < _links.Count; i++)
            for (int j = 0; j < 3; j++) _links[i].Segments[j].gameObject.SetActive(false);
        _treeScroll.StopMovement();
        _treeContent.anchoredPosition = Vector2.zero;
        _layoutTree = tree;
        _layoutCount = count;
    }

    private LinkVisual GetLink(int index)
    {
        if (index < _links.Count) return _links[index];
        var link = new LinkVisual();
        for (int i = 0; i < 3; i++)
            link.Segments[i] = _widgets.Fill(_linkRoot, "Link" + index + "_" + i, 0, 0, 1, 1, FieldMenuPalette.Border);
        _links.Add(link);
        return link;
    }

    private static void PlaceSegment(Image image, Vector2 from, Vector2 to)
    {
        image.gameObject.SetActive(true);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = new Vector2(Mathf.Min(from.x, to.x), Mathf.Max(from.y, to.y));
        rect.sizeDelta = new Vector2(Mathf.Max(1, Mathf.Abs(to.x - from.x)), Mathf.Max(1, Mathf.Abs(to.y - from.y)));
    }

    private void EnsureSelectedVisible(RectTransform node)
    {
        Vector2 position = _treeContent.anchoredPosition;
        float left = node.anchoredPosition.x + position.x;
        float top = -node.anchoredPosition.y - position.y;
        if (left < TreePadding) position.x += TreePadding - left;
        else if (left + NodeWidth > LeftWidth - TreePadding) position.x -= left + NodeWidth - LeftWidth + TreePadding;
        if (top < TreePadding) position.y -= TreePadding - top;
        else if (top + NodeHeight > TreeHeight - TreePadding) position.y += top + NodeHeight - TreeHeight + TreePadding;
        position.x = Mathf.Clamp(position.x, Mathf.Min(0, LeftWidth - _treeContent.sizeDelta.x), 0);
        position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, _treeContent.sizeDelta.y - TreeHeight));
        _treeScroll.StopMovement();
        _treeContent.anchoredPosition = position;
    }

    private void AppendPrerequisites(SkillTreeNodeDefinition definition, IReadOnlyList<SkillTreeNodeView> nodes)
    {
        List<string> prerequisites = definition.PrerequisiteNodeIds;
        _detailBuilder.Append("\n").Append(L("prerequisites", "선행: "));
        if (prerequisites == null || prerequisites.Count == 0) { _detailBuilder.Append(L("none", "없음")); return; }
        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (i > 0) _detailBuilder.Append(", ");
            string id = prerequisites[i]?.Trim() ?? string.Empty;
            if (_nodeIndices.TryGetValue(id, out int index))
            {
                SkillTreeNodeView prerequisite = nodes[index];
                _detailBuilder.Append(prerequisite.IsUnlocked ? "✓ " : "· ");
                _detailBuilder.Append(prerequisite.Definition.Skill != null ? prerequisite.Definition.Skill.SkillName : id);
            }
            else _detailBuilder.Append(id);
        }
    }

    private static Color NodeColor(SkillTreeNodeState state)
    {
        return state switch
        {
            SkillTreeNodeState.Available => FieldMenuPalette.Yellow,
            SkillTreeNodeState.Unlocked => FieldMenuPalette.Text,
            SkillTreeNodeState.Equipped => FieldMenuPalette.Mint,
            _ => LockedColor
        };
    }

    private static string StateLabel(SkillTreeNodeState state)
    {
        return state switch
        {
            SkillTreeNodeState.Available => L("state_available", "습득 가능"), SkillTreeNodeState.Unlocked => L("state_unlocked", "습득"),
            SkillTreeNodeState.Equipped => L("state_equipped", "장착"), _ => L("state_locked", "잠김")
        };
    }

    private static string L(string key, string fallback) => LocalizationManager.Localize("field_menu.growth." + key, fallback);

    private static string StatName(int index)
    {
        return index switch
        {
            0 => L("stat_vitality", "생명력"), 1 => L("stat_attack", "공격력"), 2 => L("stat_defense", "방어력"),
            3 => L("stat_speed", "속도"), _ => L("stat_action", "행동력")
        };
    }

    private static string StatUnit(int index)
    {
        return index switch
        {
            0 => L("unit_hp", "최대 HP"), 1 => "ATK", 2 => "DEF", 3 => "SPD", _ => L("unit_ap", "최대 AP")
        };
    }

    private static int ReadStat(StatBlock stats, int index)
    {
        if (stats == null) return 0;
        return index switch { 0 => stats.MaxHP, 1 => stats.ATK, 2 => stats.DEF, 3 => stats.SPD, _ => stats.MaxAP };
    }

    private static int ReadBaseStat(CharacterBaseStatSnapshot stats, int index)
    {
        return index switch { 0 => stats.MaxHP, 1 => stats.Attack, 2 => stats.Defense, 3 => stats.Speed, _ => stats.MaxAP };
    }

    private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();

    private static int ResolveIncrement(GrowthBalanceProfile profile, int index)
    {
        if (profile == null) return index == 0 ? GrowthBalanceProfile.DefaultHealthPerVitalityRank : GrowthBalanceProfile.DefaultStatValuePerRank;
        return Mathf.Max(1, index switch
        {
            0 => profile.HealthPerVitalityRank, 1 => profile.AttackPerRank, 2 => profile.DefensePerRank,
            3 => profile.SpeedPerRank, _ => profile.ActionPointsPerRank
        });
    }
}
