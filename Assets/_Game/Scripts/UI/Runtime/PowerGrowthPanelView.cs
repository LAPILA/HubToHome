using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PowerGrowthTab
{
    Stats,
    Skills
}

/// <summary>
/// POWER 메뉴의 성장 정보를 640x480 기준으로 표현하는 런타임 뷰입니다.
/// 저장 변경은 담당하지 않고 전달받은 상태만 그립니다.
/// </summary>
public sealed class PowerGrowthPanelView : MonoBehaviour
{
    // CategoryWindow의 Content는 프레임 안쪽 여백(-48)을 적용하므로
    // PowerGrowthPanel이 실제로 사용할 수 있는 기준 가로 크기는 483입니다.
    // 이 값보다 큰 좌표를 사용하면 화면 비율이 바뀌지 않아도 우측 프레임 밖으로 넘칩니다.
    private const float ContentWidth = 483f;
    private const float FadeDuration = 0.12f;
    private const float SkillNodeSpacing = 1.5f;
    private const float SkillTreePadding = 12f;
    private const float SkillScrollDuration = 0.16f;
    private static readonly Vector2 SkillNodeSize = new Vector2(108f, 48f);
    private static readonly Color Black = new Color(0f, 0f, 0f, 0.97f);
    private static readonly Color PanelBlack = new Color(0.025f, 0.02f, 0.035f, 0.98f);
    private static readonly Color White = new Color(0.96f, 0.95f, 0.98f, 1f);
    private static readonly Color Yellow = new Color(1f, 0.82f, 0.02f, 1f);
    private static readonly Color Purple = new Color(0.34f, 0.25f, 0.42f, 1f);
    private static readonly Color Muted = new Color(0.52f, 0.46f, 0.57f, 1f);
    private static readonly Color Cyan = new Color(0.3f, 0.86f, 0.9f, 1f);
    private static readonly Color Green = new Color(0.42f, 0.86f, 0.56f, 1f);
    private static readonly Color Red = new Color(0.92f, 0.38f, 0.4f, 1f);

    private readonly List<StatRowVisual> _statRows = new List<StatRowVisual>();
    private RectTransform _root;
    private RectTransform _statsRoot;
    private RectTransform _skillsRoot;
    private RectTransform _treeRoot;
    private RectTransform _treeViewport;
    private RectTransform _skillLinkRoot;
    private RectTransform _skillNodeRoot;
    private RectTransform _skillDetailViewport;
    private CanvasGroup _contentGroup;
    private ScrollRect _treeScrollRect;
    private ScrollRect _skillDetailScrollRect;
    private Scrollbar _treeHorizontalScrollbar;
    private Scrollbar _treeVerticalScrollbar;
    private Scrollbar _skillDetailScrollbar;
    private TMP_FontAsset _font;
    private TextMeshProUGUI _nameLabel;
    private TextMeshProUGUI _levelLabel;
    private TextMeshProUGUI _pointLabel;
    private TextMeshProUGUI _statsTabLabel;
    private TextMeshProUGUI _skillsTabLabel;
    private Image _statsTabFill;
    private Image _skillsTabFill;
    private TextMeshProUGUI _expLabel;
    private Image _expFill;
    private TextMeshProUGUI _statSummaryLabel;
    private TextMeshProUGUI _skillDetailLabel;
    private TextMeshProUGUI _statusLabel;
    private Sequence _actionSequence;
    private PowerGrowthTab _renderedTab;
    private bool _hasRenderedTab;
    private SkillTreeDefinition _focusedSkillTree;
    private string _focusedSkillNodeId;

    private sealed class StatRowVisual
    {
        public Image Fill;
        public Outline Border;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Rank;
        public TextMeshProUGUI Value;
    }

    public static PowerGrowthPanelView Ensure(
        RectTransform parent,
        TMP_FontAsset font)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find("PowerGrowthPanel");
        PowerGrowthPanelView view = existing != null
            ? existing.GetComponent<PowerGrowthPanelView>()
            : null;
        if (view == null && existing != null)
            view = existing.gameObject.AddComponent<PowerGrowthPanelView>();
        if (view == null)
        {
            var go = new GameObject("PowerGrowthPanel", typeof(RectTransform), typeof(PowerGrowthPanelView));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect, Vector2.zero, Vector2.zero);
            view = go.GetComponent<PowerGrowthPanelView>();
        }

        view.EnsureBuilt(font);
        return view;
    }

    public void Render(
        string displayName,
        CharacterSaveData member,
        CharacterData data,
        PowerGrowthTab tab,
        int selectedStatIndex,
        IReadOnlyList<SkillTreeNodeView> nodes,
        int selectedNodeIndex,
        string status)
    {
        if (member == null)
        {
            RenderEmpty("능력을 확인할 파티원이 없습니다.");
            return;
        }

        EnsureBuilt(_font);
        CharacterGrowthService.EnsureInitialized(member, data);
        SkillTreeProgressionService.Synchronize(member, data);

        CharacterGrowthSaveData growth = member.Growth;
        int maximumLevel = CharacterGrowthService.ResolveMaxLevel(data);
        int requiredExp = CharacterProgressionService.ExperienceRequiredForNextLevel(data, member.Level);
        _nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? "PLAYER" : displayName;
        _levelLabel.text = "LV " + Mathf.Max(1, member.Level).ToString("00");
        _pointLabel.text = tab == PowerGrowthTab.Stats
            ? "ATTRIBUTE  " + growth.AvailableAttributePoints
            : "SKILL  " + growth.AvailableSkillPoints;

        if (member.Level >= maximumLevel)
        {
            _expLabel.text = "EXP  MAX";
            _expFill.fillAmount = 1f;
        }
        else
        {
            int currentExp = Mathf.Clamp(member.EXP, 0, Mathf.Max(1, requiredExp));
            _expLabel.text = "EXP  " + currentExp + " / " + requiredExp;
            _expFill.fillAmount = requiredExp > 0
                ? Mathf.Clamp01((float)currentExp / requiredExp)
                : 0f;
        }

        ApplyTab(tab);
        if (tab == PowerGrowthTab.Stats)
            RenderStats(member, data, selectedStatIndex);
        else
            RenderSkills(member, data, nodes, selectedNodeIndex);

        _statusLabel.text = string.IsNullOrWhiteSpace(status)
            ? "Q / E  PARTY     C  TAB     R  RESET"
            : status;
    }

    public void RenderEmpty(string message)
    {
        EnsureBuilt(_font);
        _nameLabel.text = "POWER";
        _levelLabel.text = string.Empty;
        _pointLabel.text = string.Empty;
        _expLabel.text = string.Empty;
        _expFill.fillAmount = 0f;
        _statsRoot.gameObject.SetActive(false);
        _skillsRoot.gameObject.SetActive(false);
        _statusLabel.text = message ?? string.Empty;
    }

    public void PlayActionPulse(bool positive)
    {
        if (_statusLabel == null)
            return;

        _actionSequence?.Kill();
        Color target = positive ? Green : Red;
        _statusLabel.color = target;
        _statusLabel.rectTransform.localScale = Vector3.one * 0.94f;
        _actionSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_statusLabel.rectTransform.DOScale(1.08f, 0.08f).SetEase(Ease.OutQuad))
            .Append(_statusLabel.rectTransform.DOScale(1f, 0.09f).SetEase(Ease.InOutQuad))
            .AppendInterval(0.28f)
            .Append(_statusLabel.DOColor(White, 0.18f));
    }

    private void EnsureBuilt(TMP_FontAsset font)
    {
        if (_root != null)
        {
            if (font != null && font != _font)
                ApplyFont(font);
            return;
        }

        _font = font;
        _root = transform as RectTransform;
        Stretch(_root, Vector2.zero, Vector2.zero);

        Image background = gameObject.AddComponent<Image>();
        background.color = Black;
        background.raycastTarget = false;

        _nameLabel = CreateText(_root, "CharacterName", 16f, Yellow, TextAlignmentOptions.Left);
        PlaceTopLeft(_nameLabel.rectTransform, 14f, 8f, 180f, 26f);
        _nameLabel.fontStyle = FontStyles.Bold;

        _levelLabel = CreateText(_root, "Level", 14f, White, TextAlignmentOptions.Center);
        PlaceTopLeft(_levelLabel.rectTransform, 188f, 8f, 64f, 26f);

        _pointLabel = CreateText(_root, "Points", 13f, Cyan, TextAlignmentOptions.Right);
        PlaceTopLeft(_pointLabel.rectTransform, 230f, 8f, 110f, 26f);

        CreateTab(_root, "StatsTab", "STATS", 350f, out _statsTabFill, out _statsTabLabel);
        CreateTab(_root, "SkillsTab", "SKILLS", 419f, out _skillsTabFill, out _skillsTabLabel);

        Image divider = CreateImage(_root, "HeaderDivider", Purple);
        PlaceTopLeft(divider.rectTransform, 14f, 38f, 455f, 2f);

        _expLabel = CreateText(_root, "ExpLabel", 11f, Muted, TextAlignmentOptions.Left);
        PlaceTopLeft(_expLabel.rectTransform, 14f, 43f, 175f, 18f);
        Image expBack = CreateImage(_root, "ExpBar", new Color(0.12f, 0.09f, 0.15f, 1f));
        PlaceTopLeft(expBack.rectTransform, 190f, 48f, 188f, 8f);
        _expFill = CreateImage(expBack.rectTransform, "Fill", Cyan);
        Stretch(_expFill.rectTransform, Vector2.zero, Vector2.zero);
        _expFill.type = Image.Type.Filled;
        _expFill.fillMethod = Image.FillMethod.Horizontal;
        _expFill.fillOrigin = 0;
        _expFill.fillAmount = 0f;

        RectTransform content = CreateRect(_root, "Content");
        PlaceTopLeft(content, 0f, 64f, ContentWidth, 184f);
        _contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        _statsRoot = CreateRect(content, "StatsContent");
        Stretch(_statsRoot, Vector2.zero, Vector2.zero);
        BuildStatsView();

        _skillsRoot = CreateRect(content, "SkillsContent");
        Stretch(_skillsRoot, Vector2.zero, Vector2.zero);
        BuildSkillsView();

        _statusLabel = CreateText(_root, "Status", 12f, White, TextAlignmentOptions.Left);
        PlaceTopLeft(_statusLabel.rectTransform, 14f, 249f, 455f, 18f);
        _statusLabel.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void BuildStatsView()
    {
        Image summaryPanel = CreateFramedImage(_statsRoot, "Summary", PanelBlack, Purple, 1f);
        PlaceTopLeft(summaryPanel.rectTransform, 14f, 0f, 136f, 178f);
        _statSummaryLabel = CreateText(summaryPanel.rectTransform, "SummaryText", 13f, White, TextAlignmentOptions.TopLeft);
        Stretch(_statSummaryLabel.rectTransform, new Vector2(10f, 8f), new Vector2(-10f, -8f));
        _statSummaryLabel.lineSpacing = 5f;

        string[] names = { "VITALITY", "ATTACK", "DEFENSE", "SPEED", "ACTION" };
        for (int i = 0; i < names.Length; i++)
        {
            Image row = CreateFramedImage(_statsRoot, "Stat_" + i, PanelBlack, Purple, 1f);
            PlaceTopLeft(row.rectTransform, 150f, i * 35f, 319f, 31f);

            var visual = new StatRowVisual
            {
                Fill = row,
                Border = row.GetComponent<Outline>(),
                Name = CreateText(row.rectTransform, "Name", 13f, White, TextAlignmentOptions.Left),
                Rank = CreateText(row.rectTransform, "Rank", 12f, Muted, TextAlignmentOptions.Center),
                Value = CreateText(row.rectTransform, "Value", 13f, White, TextAlignmentOptions.Right)
            };
            PlaceTopLeft(visual.Name.rectTransform, 10f, 2f, 100f, 27f);
            PlaceTopLeft(visual.Rank.rectTransform, 110f, 2f, 80f, 27f);
            PlaceTopLeft(visual.Value.rectTransform, 194f, 2f, 110f, 27f);
            visual.Name.text = names[i];
            _statRows.Add(visual);
        }
    }

    private void BuildSkillsView()
    {
        Image treePanel = CreateFramedImage(_skillsRoot, "TreePanel", PanelBlack, Purple, 1f);
        PlaceTopLeft(treePanel.rectTransform, 14f, 0f, 285f, 178f);
        treePanel.raycastTarget = true;

        _treeViewport = CreateViewport(treePanel.rectTransform, "Viewport");
        Stretch(_treeViewport, new Vector2(5f, 9f), new Vector2(-9f, -5f));
        _treeRoot = CreateRect(_treeViewport, "TreeRoot");
        _treeRoot.anchorMin = _treeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _treeRoot.pivot = new Vector2(0.5f, 0.5f);
        _treeRoot.anchoredPosition = Vector2.zero;
        _treeRoot.sizeDelta = _treeViewport.rect.size;
        _skillLinkRoot = CreateRect(_treeRoot, "Links");
        Stretch(_skillLinkRoot, Vector2.zero, Vector2.zero);
        _skillNodeRoot = CreateRect(_treeRoot, "Nodes");
        Stretch(_skillNodeRoot, Vector2.zero, Vector2.zero);

        _treeHorizontalScrollbar = CreateScrollbar(treePanel.rectTransform, "HorizontalScrollbar", false);
        PlaceBottomStretch(_treeHorizontalScrollbar.transform as RectTransform, 5f, 9f, 4f, 3f);
        _treeVerticalScrollbar = CreateScrollbar(treePanel.rectTransform, "VerticalScrollbar", true);
        PlaceRightStretch(_treeVerticalScrollbar.transform as RectTransform, 5f, 9f, 4f, 3f);

        _treeScrollRect = treePanel.gameObject.AddComponent<ScrollRect>();
        ConfigureScrollRect(
            _treeScrollRect,
            _treeViewport,
            _treeRoot,
            true,
            true,
            _treeHorizontalScrollbar,
            _treeVerticalScrollbar);

        Image detailPanel = CreateFramedImage(_skillsRoot, "DetailPanel", PanelBlack, Purple, 1f);
        PlaceTopLeft(detailPanel.rectTransform, 307f, 0f, 162f, 178f);
        detailPanel.raycastTarget = true;

        _skillDetailViewport = CreateViewport(detailPanel.rectTransform, "Viewport");
        Stretch(_skillDetailViewport, new Vector2(9f, 7f), new Vector2(-13f, -7f));
        _skillDetailLabel = CreateText(_skillDetailViewport, "DetailText", 13f, White, TextAlignmentOptions.TopLeft);
        RectTransform detailRect = _skillDetailLabel.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 1f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.pivot = new Vector2(0.5f, 1f);
        detailRect.anchoredPosition = Vector2.zero;
        detailRect.sizeDelta = new Vector2(0f, 164f);
        _skillDetailLabel.textWrappingMode = TextWrappingModes.Normal;
        _skillDetailLabel.overflowMode = TextOverflowModes.Overflow;
        _skillDetailLabel.lineSpacing = 2f;

        _skillDetailScrollbar = CreateScrollbar(detailPanel.rectTransform, "VerticalScrollbar", true);
        PlaceRightStretch(_skillDetailScrollbar.transform as RectTransform, 5f, 7f, 5f, 3f);
        _skillDetailScrollRect = detailPanel.gameObject.AddComponent<ScrollRect>();
        ConfigureScrollRect(
            _skillDetailScrollRect,
            _skillDetailViewport,
            detailRect,
            false,
            true,
            null,
            _skillDetailScrollbar);
    }

    private void RenderStats(
        CharacterSaveData member,
        CharacterData data,
        int selectedStatIndex)
    {
        CharacterGrowthSaveData growth = member.Growth;
        CharacterStatInvestments ranks = growth.Investments;
        int maximumRank = CharacterGrowthService.ResolveMaxInvestmentRank(data != null ? data.GrowthProfile : null);
        int[] rankValues =
        {
            ranks.Vitality,
            ranks.Attack,
            ranks.Defense,
            ranks.Speed,
            ranks.ActionPoints
        };
        StatBlock resolvedStats = CharacterStatsProjectionService.ResolveFromSave(member, data);
        int[] finalValues =
        {
            resolvedStats.MaxHP,
            resolvedStats.ATK,
            resolvedStats.DEF,
            resolvedStats.SPD,
            resolvedStats.MaxAP
        };
        string[] valueNames = { "HP", "ATK", "DEF", "SPD", "AP" };

        int selected = Mathf.Clamp(selectedStatIndex, 0, _statRows.Count - 1);
        for (int i = 0; i < _statRows.Count; i++)
        {
            bool isSelected = i == selected;
            StatRowVisual row = _statRows[i];
            row.Fill.color = isSelected ? new Color(0.11f, 0.085f, 0.13f, 1f) : PanelBlack;
            row.Border.effectColor = isSelected ? Yellow : Purple;
            row.Border.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            row.Name.color = isSelected ? Yellow : White;
            row.Rank.color = rankValues[i] >= maximumRank ? Green : Muted;
            row.Rank.text = rankValues[i].ToString("00") + " / " + maximumRank.ToString("00");
            row.Value.text = valueNames[i] + "  " + finalValues[i];
        }

        _statSummaryLabel.text =
            "AVAILABLE\n<color=#FFD105><size=21>" + growth.AvailableAttributePoints + "</size></color> PT\n\n" +
            "INVESTED\n" + ranks.Total + " PT\n\n" +
            "<color=#8F779B>◀ REFUND\n▶ / Z INVEST</color>";
    }

    private void RenderSkills(
        CharacterSaveData member,
        CharacterData data,
        IReadOnlyList<SkillTreeNodeView> nodes,
        int selectedNodeIndex)
    {
        ClearChildren(_skillLinkRoot);
        ClearChildren(_skillNodeRoot);

        if (data?.SkillTree == null || nodes == null || nodes.Count == 0)
        {
            _skillDetailLabel.text = "<color=#FFD105>NO SKILL TREE</color>\n\n캐릭터 데이터에 스킬 트리를 연결해 주세요.";
            return;
        }

        var nodeRects = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        var nodeViews = new Dictionary<string, SkillTreeNodeView>(StringComparer.Ordinal);
        float maximumX = 0f;
        float maximumY = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            Vector2 position = ScaleSkillNodePosition(nodes[i].Definition.Position);
            maximumX = Mathf.Max(maximumX, Mathf.Abs(position.x));
            maximumY = Mathf.Max(maximumY, Mathf.Abs(position.y));
        }

        Canvas.ForceUpdateCanvases();
        Vector2 viewportSize = _treeViewport.rect.size;
        float contentWidth = Mathf.Max(
            viewportSize.x,
            Mathf.Ceil(maximumX * 2f + SkillNodeSize.x + SkillTreePadding * 2f));
        float contentHeight = Mathf.Max(
            viewportSize.y,
            Mathf.Ceil(maximumY * 2f + SkillNodeSize.y + SkillTreePadding * 2f));
        _treeRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        _treeRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTreeNodeView node = nodes[i];
            string nodeId = node.Definition.ResolveId();
            if (!string.IsNullOrEmpty(nodeId))
                nodeViews[nodeId] = node;

            bool selected = i == Mathf.Clamp(selectedNodeIndex, 0, nodes.Count - 1);
            Color stateColor = NodeStateColor(node.State);
            Image box = CreateFramedImage(_skillNodeRoot, "Node_" + i, PanelBlack, selected ? Yellow : stateColor, selected ? 2f : 1f);
            RectTransform rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = SkillNodeSize;
            rect.anchoredPosition = ScaleSkillNodePosition(node.Definition.Position);

            TextMeshProUGUI label = CreateText(rect, "Label", 13f, selected ? Yellow : stateColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, new Vector2(5f, 3f), new Vector2(-5f, -3f));
            string skillName = node.Definition.Skill != null
                ? node.Definition.Skill.SkillName
                : node.Definition.ResolveId();
            label.text = skillName + "\n<size=10>" + NodeStateText(node) + "</size>";
            if (!string.IsNullOrEmpty(nodeId))
                nodeRects[nodeId] = rect;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTreeNodeView node = nodes[i];
            if (node.Definition.PrerequisiteNodeIds == null)
                continue;

            string childId = node.Definition.ResolveId();
            if (!nodeRects.TryGetValue(childId, out RectTransform child))
                continue;

            for (int prerequisiteIndex = 0; prerequisiteIndex < node.Definition.PrerequisiteNodeIds.Count; prerequisiteIndex++)
            {
                string prerequisiteId = node.Definition.PrerequisiteNodeIds[prerequisiteIndex];
                if (!nodeRects.TryGetValue(prerequisiteId, out RectTransform parent))
                    continue;

                bool active = node.IsUnlocked
                    && nodeViews.TryGetValue(prerequisiteId, out SkillTreeNodeView prerequisite)
                    && prerequisite.IsUnlocked;
                CreateLink(parent.anchoredPosition, child.anchoredPosition, active ? Green : Purple);
            }
        }

        SkillTreeNodeView selectedNode = nodes[Mathf.Clamp(selectedNodeIndex, 0, nodes.Count - 1)];
        SkillTreeNodeDefinition definition = selectedNode.Definition;
        SkillData skill = definition.Skill;
        string state = NodeStateText(selectedNode);
        string description = skill != null && !string.IsNullOrWhiteSpace(skill.Description)
            ? skill.Description
            : "설명이 없습니다.";
        string apCost = skill != null ? skill.APCost.ToString() : "-";
        string selectedNodeId = definition.ResolveId();
        bool selectionChanged = _focusedSkillTree != data.SkillTree
            || !string.Equals(_focusedSkillNodeId, selectedNodeId, StringComparison.Ordinal);
        _skillDetailLabel.text =
            "<color=#FFD105><b>" + (skill != null ? skill.SkillName : selectedNodeId) + "</b></color>\n" +
            "<color=#8F779B><size=11>" + state + "</size></color>\n\n" +
            description + "\n\n" +
            "<color=#4DDCE6>AP  " + apCost + "</color>\n" +
            "COST  " + Mathf.Max(0, definition.Cost) + " SP\n" +
            "REQ  LV " + Mathf.Max(1, definition.RequiredLevel);

        UpdateTreeScrollbars(contentWidth, contentHeight, viewportSize);
        UpdateSkillDetailScroll(selectionChanged);
        if (selectionChanged && nodeRects.TryGetValue(selectedNodeId, out RectTransform selectedRect))
            FocusSkillNode(selectedRect);

        _focusedSkillTree = data.SkillTree;
        _focusedSkillNodeId = selectedNodeId;
    }

    private void ApplyTab(PowerGrowthTab tab)
    {
        bool stats = tab == PowerGrowthTab.Stats;
        _statsRoot.gameObject.SetActive(stats);
        _skillsRoot.gameObject.SetActive(!stats);
        _statsTabFill.color = stats ? new Color(0.15f, 0.11f, 0.18f, 1f) : Black;
        _skillsTabFill.color = stats ? Black : new Color(0.15f, 0.11f, 0.18f, 1f);
        _statsTabLabel.color = stats ? Yellow : Muted;
        _skillsTabLabel.color = stats ? Muted : Yellow;

        bool tabChanged = !_hasRenderedTab || _renderedTab != tab;
        _renderedTab = tab;
        _hasRenderedTab = true;
        _contentGroup.DOKill();
        if (tabChanged)
        {
            _contentGroup.alpha = 0.35f;
            _contentGroup.DOFade(1f, FadeDuration).SetUpdate(true).SetEase(Ease.OutQuad);
        }
        else
        {
            _contentGroup.alpha = 1f;
        }
    }

    private void CreateTab(
        RectTransform parent,
        string objectName,
        string labelText,
        float x,
        out Image fill,
        out TextMeshProUGUI label)
    {
        fill = CreateFramedImage(parent, objectName, Black, Purple, 1f);
        PlaceTopLeft(fill.rectTransform, x, 7f, 64f, 27f);
        label = CreateText(fill.rectTransform, "Label", 11f, Muted, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
        label.text = labelText;
    }

    private void CreateLink(Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
            return;

        Image line = CreateImage(_skillLinkRoot, "Link", color);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(distance, 2f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void UpdateTreeScrollbars(
        float contentWidth,
        float contentHeight,
        Vector2 viewportSize)
    {
        _treeHorizontalScrollbar.gameObject.SetActive(contentWidth > viewportSize.x + 0.5f);
        _treeVerticalScrollbar.gameObject.SetActive(contentHeight > viewportSize.y + 0.5f);
    }

    private void UpdateSkillDetailScroll(bool resetToTop)
    {
        Canvas.ForceUpdateCanvases();
        float viewportWidth = Mathf.Max(1f, _skillDetailViewport.rect.width);
        float viewportHeight = Mathf.Max(1f, _skillDetailViewport.rect.height);
        float preferredHeight = _skillDetailLabel.GetPreferredValues(
            _skillDetailLabel.text,
            viewportWidth,
            Mathf.Infinity).y;
        float contentHeight = Mathf.Max(viewportHeight, Mathf.Ceil(preferredHeight + 2f));
        _skillDetailLabel.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            contentHeight);
        _skillDetailScrollbar.gameObject.SetActive(contentHeight > viewportHeight + 0.5f);

        Canvas.ForceUpdateCanvases();
        if (resetToTop)
            _skillDetailScrollRect.verticalNormalizedPosition = 1f;
    }

    private void FocusSkillNode(RectTransform selectedNode)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 viewportSize = _treeViewport.rect.size;
        Vector2 contentSize = _treeRoot.rect.size;
        Vector2 hidden = new Vector2(
            Mathf.Max(0f, contentSize.x - viewportSize.x),
            Mathf.Max(0f, contentSize.y - viewportSize.y));
        Vector2 nodePosition = selectedNode.anchoredPosition;
        Vector2 target = new Vector2(
            hidden.x > 0.5f ? Mathf.Clamp01(0.5f + nodePosition.x / hidden.x) : 0.5f,
            hidden.y > 0.5f ? Mathf.Clamp01(0.5f + nodePosition.y / hidden.y) : 0.5f);

        DOTween.Kill(_treeScrollRect);
        DOTween.To(
                () => _treeScrollRect.normalizedPosition,
                value => _treeScrollRect.normalizedPosition = value,
                target,
                SkillScrollDuration)
            .SetTarget(_treeScrollRect)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad)
            .OnComplete(SnapTreeContentToPixels);
    }

    private void SnapTreeContentToPixels()
    {
        if (_treeRoot == null)
            return;

        Vector2 position = _treeRoot.anchoredPosition;
        _treeRoot.anchoredPosition = new Vector2(
            Mathf.Round(position.x),
            Mathf.Round(position.y));
    }

    private static Vector2 ScaleSkillNodePosition(Vector2 authoredPosition)
    {
        return new Vector2(
            Mathf.Round(authoredPosition.x * SkillNodeSpacing),
            Mathf.Round(authoredPosition.y * SkillNodeSpacing));
    }

    private static Color NodeStateColor(SkillTreeNodeState state)
    {
        return state switch
        {
            SkillTreeNodeState.Available => Yellow,
            SkillTreeNodeState.Unlocked => White,
            SkillTreeNodeState.Equipped => Cyan,
            _ => Muted
        };
    }

    private static string NodeStateText(SkillTreeNodeView node)
    {
        return node.State switch
        {
            SkillTreeNodeState.Available => "AVAILABLE  " + Mathf.Max(0, node.Definition.Cost) + " SP",
            SkillTreeNodeState.Unlocked => "UNLOCKED",
            SkillTreeNodeState.Equipped => "EQUIPPED",
            _ => string.IsNullOrWhiteSpace(node.LockReason) ? "LOCKED" : node.LockReason
        };
    }

    private void ApplyFont(TMP_FontAsset font)
    {
        _font = font;
        TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
            labels[i].font = font;
    }

    private TextMeshProUGUI CreateText(
        RectTransform parent,
        string objectName,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private static Image CreateImage(
        RectTransform parent,
        string objectName,
        Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateViewport(
        RectTransform parent,
        string objectName)
    {
        Image inputSurface = CreateImage(parent, objectName, new Color(0f, 0f, 0f, 0.001f));
        inputSurface.raycastTarget = true;
        RectTransform viewport = inputSurface.rectTransform;
        viewport.gameObject.AddComponent<RectMask2D>();
        return viewport;
    }

    private static Scrollbar CreateScrollbar(
        RectTransform parent,
        string objectName,
        bool vertical)
    {
        var go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Scrollbar));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image track = go.GetComponent<Image>();
        track.color = new Color(Purple.r, Purple.g, Purple.b, 0.42f);
        track.raycastTarget = true;

        Image handle = CreateImage(rect, "Handle", Muted);
        handle.raycastTarget = true;
        Stretch(handle.rectTransform, Vector2.zero, Vector2.zero);

        Scrollbar scrollbar = go.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = vertical
            ? Scrollbar.Direction.BottomToTop
            : Scrollbar.Direction.LeftToRight;
        scrollbar.numberOfSteps = 0;
        return scrollbar;
    }

    private static void ConfigureScrollRect(
        ScrollRect scrollRect,
        RectTransform viewport,
        RectTransform content,
        bool horizontal,
        bool vertical,
        Scrollbar horizontalScrollbar,
        Scrollbar verticalScrollbar)
    {
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = horizontal;
        scrollRect.vertical = vertical;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.horizontalScrollbar = horizontalScrollbar;
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private static Image CreateFramedImage(
        RectTransform parent,
        string objectName,
        Color fillColor,
        Color borderColor,
        float borderSize)
    {
        Image image = CreateImage(parent, objectName, fillColor);
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderSize, -borderSize);
        outline.useGraphicAlpha = true;
        return image;
    }

    private static RectTransform CreateRect(RectTransform parent, string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void PlaceTopLeft(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void PlaceBottomStretch(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    private static void PlaceRightStretch(
        RectTransform rect,
        float top,
        float bottom,
        float right,
        float width)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-right - width, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void OnDestroy()
    {
        _actionSequence?.Kill();
        if (_treeScrollRect != null)
            DOTween.Kill(_treeScrollRect);
        if (_skillDetailScrollRect != null)
            DOTween.Kill(_skillDetailScrollRect);
        if (_contentGroup != null)
            _contentGroup.DOKill();
    }
}
