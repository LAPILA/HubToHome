using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum OverworldMenuCategory
{
    Item,
    Equip,
    Power,
    Config
}

/// <summary>
/// Deltarune-style overworld menu shell. The static UI hierarchy is owned by the prefab;
/// this component only drives state, animation, and prefab-backed view state.
/// 표시 모드: FixedViewport. 메뉴/인벤토리/장비/POWER는 공통 UIRuntimeGuard 계약을 따른다.
/// </summary>
public sealed class OverworldMenuUI : UIPanel
{
    private const float TopPanelHeight = 96f;
    private const float BottomPanelHeight = 78f;
    private const float SlideDuration = 0.22f;
    private const float WindowDuration = 0.12f;
    private const int GrowthStatCount = 5;

    private static readonly Color Black = new Color(0f, 0f, 0f, 0.96f);
    private static readonly Color Yellow = new Color(1f, 0.82f, 0.02f, 1f);
    private static readonly Color MutedPurple = new Color(0.34f, 0.25f, 0.42f, 1f);
    private static readonly Color MutedText = new Color(0.48f, 0.42f, 0.52f, 1f);

    [SerializeField] private CanvasGroup _rootGroup;
    [SerializeField] private RectTransform _topPanel;
    [SerializeField] private RectTransform _bottomPanel;
    [SerializeField] private RectTransform _categoryWindow;
    [SerializeField] private CanvasGroup _categoryWindowGroup;
    [SerializeField] private TextMeshProUGUI _categoryLabel;
    [SerializeField] private TextMeshProUGUI _moneyLabel;
    [SerializeField] private Sprite _fallbackPortraitSprite;
    [SerializeField] private List<MenuSlotView> _slotViews = new List<MenuSlotView>();
    [SerializeField] private List<RectTransform> _partySlotAnchors = new List<RectTransform>();
    [SerializeField] private List<CategoryWindowPanelView> _categoryPanels = new List<CategoryWindowPanelView>();
    [SerializeField] private TextMeshProUGUI _emptyInventoryLabel;
    [SerializeField] private List<TextMeshProUGUI> _itemLabels = new List<TextMeshProUGUI>();

    private readonly OverworldMenuCategory[] _categories =
    {
        OverworldMenuCategory.Item,
        OverworldMenuCategory.Equip,
        OverworldMenuCategory.Power,
        OverworldMenuCategory.Config
    };

    private int _selectedIndex;
    private bool _isOpen;
    private bool _isAnimating;
    private bool _isCategoryWindowOpen;
    private int _ignoreInputUntilFrame;
    private int _displayedMoney = int.MinValue;
    private GameState _stateBeforeOpen = GameState.Exploration;
    private bool _ownsPauseState;
    private Sequence _panelSequence;
    private bool _openOptionsAfterClose;
    private TextMeshProUGUI _equipContentLabel;
    private PowerGrowthPanelView _powerGrowthView;
    private int _categoryCharacterIndex;
    private int _equipSlotIndex;
    private int _growthStatIndex;
    private int _skillNodeIndex;
    private PowerGrowthTab _powerTab;
    private string _categoryStatus = string.Empty;

    [Serializable]
    private sealed class MenuSlotView
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _fill;
        [SerializeField] private List<Image> _borderImages = new List<Image>();

        public bool IsValid => _label != null && _fill != null && _borderImages != null && _borderImages.Count > 0;

        public void ApplySelected(bool selected)
        {
            Color border = selected ? Yellow : MutedPurple;
            Color label = selected ? Yellow : MutedText;
            _fill.color = Black;
            _label.color = label;

            for (int i = 0; i < _borderImages.Count; i++)
            {
                if (_borderImages[i] != null)
                    _borderImages[i].color = border;
            }
        }
    }

    [Serializable]
    private sealed class CategoryWindowPanelView
    {
        [SerializeField] private OverworldMenuCategory _category;
        [SerializeField] private RectTransform _panel;

        public OverworldMenuCategory Category => _category;
        public bool IsValid => _panel != null;
        public RectTransform Panel => _panel;

        public void SetActive(bool active)
        {
            if (_panel != null)
                _panel.gameObject.SetActive(active);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        ResolvePartySlotAnchors();
        EnsureCategoryContentViews();
        HideOverworldImmediate();
    }

    public override void Show()
    {
        OpenInternal();
    }

    public override void Hide()
    {
        CloseInternal();
    }

    public override void HideImmediate()
    {
        KillMenuTweens();
        RestorePreviousGameState();
        HideOverworldImmediate();
    }

    protected override void OnDisable()
    {
        KillMenuTweens();
        RestorePreviousGameState();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        KillMenuTweens();
        RestorePreviousGameState();
        base.OnDestroy();
    }

    public override bool TryHandleCancelInput()
    {
        if (!_isOpen || _isAnimating)
            return true;

        if (_isCategoryWindowOpen)
        {
            HideCategoryWindow();
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (!_isOpen || _isAnimating) return;
        if (Time.frameCount <= _ignoreInputUntilFrame) return;

        UpdateMoneyLabel();

        if (_isCategoryWindowOpen)
        {
            bool powerTabRequested = _categories[_selectedIndex] == OverworldMenuCategory.Power
                && GameInput.PowerTabPressed;
            if (powerTabRequested)
                HandleCategoryWindowInput();
            else if (GameInput.CancelPressed || GameInput.MenuPressed)
                HideCategoryWindow();
            else
                HandleCategoryWindowInput();
            return;
        }

        if (GameInput.UILeftPressed || GameInput.BattleLeftPressed || PressedLeftFallback())
            MoveSelection(-1);
        else if (GameInput.UIRightPressed || GameInput.BattleRightPressed || PressedRightFallback())
            MoveSelection(1);
        else if (GameInput.ConfirmPressed || GameInput.UISubmitPressed)
            ShowCategoryWindow();
        else if (GameInput.MenuPressed)
            RequestCloseFromUIManager();
    }

    private void OpenInternal()
    {
        if (_isOpen || _isAnimating) return;

        _isOpen = true;
        _isAnimating = true;
        _isCategoryWindowOpen = false;
        _ignoreInputUntilFrame = Time.frameCount + 2;

        if (GameStateManager.Instance != null)
        {
            _stateBeforeOpen = GameStateManager.Instance.CurrentState;
            _ownsPauseState = true;
            GameStateManager.Instance.ChangeState(GameState.Paused);
        }

        GameInput.ResetCachedState();
        RefreshAll();
        gameObject.SetActive(true);
        SetRootGroupVisible(true);
        _categoryWindow.gameObject.SetActive(false);

        SetPanelHiddenPositions();

        KillPanelSequence();
        _panelSequence = DOTween.Sequence().SetUpdate(true);
        _panelSequence.Join(_topPanel.DOAnchorPosY(0f, SlideDuration).SetEase(Ease.OutCubic));
        _panelSequence.Join(_bottomPanel.DOAnchorPosY(0f, SlideDuration).SetEase(Ease.OutCubic));
        _panelSequence.OnComplete(() =>
        {
            _panelSequence = null;
            _isAnimating = false;
        });
    }

    private void CloseInternal()
    {
        if (!_isOpen || _isAnimating) return;

        _isAnimating = true;
        _ignoreInputUntilFrame = Time.frameCount + 2;
        GameInput.ResetCachedState();

        if (_isCategoryWindowOpen)
            HideCategoryWindowImmediate();

        KillPanelSequence();
        _panelSequence = DOTween.Sequence().SetUpdate(true);
        _panelSequence.Join(_topPanel.DOAnchorPosY(TopPanelHeight, SlideDuration).SetEase(Ease.InCubic));
        _panelSequence.Join(_bottomPanel.DOAnchorPosY(-BottomPanelHeight, SlideDuration).SetEase(Ease.InCubic));
        _panelSequence.OnComplete(() =>
        {
            _panelSequence = null;
            _isOpen = false;
            _isAnimating = false;
            bool openOptions = _openOptionsAfterClose;
            _openOptionsAfterClose = false;
            RestorePreviousGameState();
            SetRootGroupVisible(false);
            gameObject.SetActive(false);
            if (openOptions)
                OptionsPanelService.Open();
        });
    }

    private void HideOverworldImmediate()
    {
        _isOpen = false;
        _isAnimating = false;
        _isCategoryWindowOpen = false;
        SetRootGroupVisible(false);
        SetPanelHiddenPositions();
        HideCategoryWindowImmediate();
        gameObject.SetActive(false);
    }

    private void RestorePreviousGameState()
    {
        if (!_ownsPauseState)
            return;

        _ownsPauseState = false;
        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager != null && stateManager.CurrentState == GameState.Paused)
            stateManager.ChangeState(_stateBeforeOpen);
    }

    private void KillMenuTweens()
    {
        KillPanelSequence();

        if (_categoryWindowGroup != null)
            _categoryWindowGroup.DOKill(false);
        if (_categoryWindow != null)
            _categoryWindow.DOKill(false);

    }

    private void KillPanelSequence()
    {
        if (_panelSequence == null)
            return;

        _panelSequence.Kill(false);
        _panelSequence = null;
    }

    private void SetRootGroupVisible(bool visible)
    {
        _rootGroup.alpha = visible ? 1f : 0f;
        _rootGroup.interactable = visible;
        _rootGroup.blocksRaycasts = visible;
    }

    private void RequestCloseFromUIManager()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseTopPanel();
            return;
        }

        CloseInternal();
    }

    private void SetPanelHiddenPositions()
    {
        _topPanel.anchoredPosition = new Vector2(0f, TopPanelHeight);
        _bottomPanel.anchoredPosition = new Vector2(0f, -BottomPanelHeight);
    }

    private void MoveSelection(int delta)
    {
        _selectedIndex = (_selectedIndex + delta + _categories.Length) % _categories.Length;
        RefreshCategorySelection();
    }

    private void ShowCategoryWindow()
    {
        if (_categories[_selectedIndex] == OverworldMenuCategory.Config)
        {
            OpenConfigPanel();
            return;
        }

        _categoryCharacterIndex = Mathf.Max(0, _categoryCharacterIndex);
        _categoryStatus = string.Empty;
        RefreshCategoryWindowPanels();
        _isCategoryWindowOpen = true;
        _categoryWindow.gameObject.SetActive(true);
        _categoryWindowGroup.DOKill();
        _categoryWindow.DOKill();
        _categoryWindowGroup.alpha = 0f;
        _categoryWindow.localScale = new Vector3(1f, 0.96f, 1f);
        _categoryWindowGroup.DOFade(1f, WindowDuration).SetUpdate(true);
        _categoryWindow.DOScale(Vector3.one, WindowDuration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void HideCategoryWindow()
    {
        _categoryWindowGroup.DOKill();
        _categoryWindow.DOKill();
        _categoryWindowGroup.DOFade(0f, WindowDuration).SetUpdate(true).OnComplete(HideCategoryWindowImmediate);
    }

    private void HideCategoryWindowImmediate()
    {
        _isCategoryWindowOpen = false;
        _categoryWindowGroup.alpha = 0f;
        _categoryWindow.localScale = Vector3.one;
        SetAllCategoryPanelsActive(false);
        _categoryWindow.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        RefreshCategorySelection();
        UpdateMoneyLabel();
        RebuildPartyPanel();
    }

    private void RefreshCategorySelection()
    {
        _categoryLabel.text = GetCategoryName(_categories[_selectedIndex]);

        for (int i = 0; i < _slotViews.Count; i++)
            _slotViews[i].ApplySelected(i == _selectedIndex);
    }

    private void UpdateMoneyLabel()
    {
        GlobalDataManager data = ResolveGlobalData();
        int money = data != null ? data.Money : 0;
        if (_displayedMoney == money) return;

        _displayedMoney = money;
        _moneyLabel.SetText("{0}", money);
    }

    private void RebuildPartyPanel()
    {
        GlobalDataManager data = ResolveGlobalData();
        List<CharacterSaveData> party = data != null ? data.Party : null;

        for (int i = 0; i < _partySlotAnchors.Count; i++)
        {
            RectTransform anchor = _partySlotAnchors[i];
            if (anchor == null) continue;

            bool hasPartyMember = party != null && i < party.Count && party[i] != null;
            anchor.gameObject.SetActive(hasPartyMember);
            if (!hasPartyMember) continue;

            OverworldPartySlotView view = anchor.GetComponentInChildren<OverworldPartySlotView>(true);
            if (view == null)
            {
                Debug.LogError($"[OverworldMenuUI] PartySlotAnchor_{i + 1} has no OverworldPartySlotView child.", anchor);
                continue;
            }

            view.gameObject.SetActive(true);
            CharacterSaveData member = party[i];
            view.Apply(
                member,
                ResolveCharacterData(member),
                GetPartyDisplayName(member),
                ResolvePortraitSprite(member));
        }
    }

    private Sprite ResolvePortraitSprite(CharacterSaveData data)
    {
        CharacterData characterData = ResolveCharacterData(data);

        return characterData != null && characterData.Portrait != null
            ? characterData.Portrait
            : _fallbackPortraitSprite;
    }

    private static CharacterData ResolveCharacterData(CharacterSaveData data)
    {
        if (data == null)
            return null;

        CharacterData characterData = CharacterDatabase.FindById(data.CharacterDataID);
        return characterData != null
            ? characterData
            : CharacterDatabase.FindById(data.CharacterID);
    }

    private static bool PressedLeftFallback()
    {
        return UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame;
    }

    private static bool PressedRightFallback()
    {
        return UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame;
    }

    private static string GetCategoryName(OverworldMenuCategory category)
    {
        return category switch
        {
            OverworldMenuCategory.Equip => "EQUIP",
            OverworldMenuCategory.Power => "POWER",
            OverworldMenuCategory.Config => "CONFIG",
            _ => "ITEM"
        };
    }

    private static string GetPartyDisplayName(CharacterSaveData data)
    {
        if (data == null) return "PLAYER";

        GlobalDataManager globalData = ResolveGlobalData();
        CharacterData characterData = CharacterDatabase.FindById(data.CharacterDataID);
        if (characterData == null)
            characterData = CharacterDatabase.FindById(data.CharacterID);

        string raw = characterData != null
            ? characterData.ResolveDisplayName(globalData != null ? globalData.PlayerName : null)
            : data.CharacterID;

        if (string.IsNullOrWhiteSpace(raw))
            raw = "PLAYER";

        raw = raw.Trim().ToUpperInvariant();
        return raw.Length > 8 ? raw.Substring(0, 8) : raw;
    }

    private void RefreshCategoryWindowPanels()
    {
        OverworldMenuCategory selectedCategory = _categories[_selectedIndex];

        for (int i = 0; i < _categoryPanels.Count; i++)
        {
            if (_categoryPanels[i] != null)
                _categoryPanels[i].SetActive(_categoryPanels[i].Category == selectedCategory);
        }

        if (selectedCategory == OverworldMenuCategory.Item)
            RefreshItemCategoryPanel();
        else if (selectedCategory == OverworldMenuCategory.Equip)
            RefreshEquipCategoryPanel();
        else if (selectedCategory == OverworldMenuCategory.Power)
            RefreshPowerCategoryPanel();
    }

    private void SetAllCategoryPanelsActive(bool active)
    {
        for (int i = 0; i < _categoryPanels.Count; i++)
        {
            if (_categoryPanels[i] != null)
                _categoryPanels[i].SetActive(active);
        }
    }

    private void RefreshItemCategoryPanel()
    {
        GlobalDataManager data = ResolveGlobalData();
        IReadOnlyDictionary<string, int> inventory = data != null ? data.GetInventory() : null;

        for (int i = 0; i < _itemLabels.Count; i++)
        {
            _itemLabels[i].text = string.Empty;
            _itemLabels[i].gameObject.SetActive(false);
        }

        if (inventory == null || inventory.Count == 0)
        {
            _emptyInventoryLabel.gameObject.SetActive(true);
            return;
        }

        _emptyInventoryLabel.gameObject.SetActive(false);

        int row = 0;
        foreach (KeyValuePair<string, int> item in inventory)
        {
            if (row >= _itemLabels.Count) break;

            string label = item.Value > 1 ? $"{item.Key} x{item.Value}" : item.Key;
            _itemLabels[row].text = label;
            _itemLabels[row].gameObject.SetActive(true);
            row++;
        }
    }

    private void ResolvePartySlotAnchors()
    {
        for (int i = 0; i < _partySlotAnchors.Count; i++)
        {
            if (_partySlotAnchors[i] != null)
                continue;

            Transform found = FindDescendant(transform, $"PartySlotAnchor_{i + 1}");
            if (found != null)
                _partySlotAnchors[i] = found as RectTransform;
        }
    }

    private void EnsureCategoryContentViews()
    {
        for (int i = 0; i < _categoryPanels.Count; i++)
        {
            CategoryWindowPanelView panel = _categoryPanels[i];
            if (panel == null || !panel.IsValid)
                continue;

            if (panel.Category == OverworldMenuCategory.Equip)
            {
                _equipContentLabel = EnsureContentLabel(panel.Panel, "EquipContent");
            }
            else if (panel.Category == OverworldMenuCategory.Power)
            {
                TMP_FontAsset font = GameContentCatalog.Instance != null
                    ? GameContentCatalog.Instance.DefaultUiFont
                    : _categoryLabel != null ? _categoryLabel.font : null;
                _powerGrowthView = PowerGrowthPanelView.Ensure(panel.Panel, font);
            }
        }
    }

    private TextMeshProUGUI EnsureContentLabel(RectTransform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (label != null)
            return label;

        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(28f, 22f);
        rect.offsetMax = new Vector2(-28f, -22f);

        label = go.GetComponent<TextMeshProUGUI>();
        label.font = GameContentCatalog.Instance != null && GameContentCatalog.Instance.DefaultUiFont != null
            ? GameContentCatalog.Instance.DefaultUiFont
            : _categoryLabel != null ? _categoryLabel.font : null;
        label.fontSize = 20f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private void HandleCategoryWindowInput()
    {
        OverworldMenuCategory category = _categories[_selectedIndex];
        if (category == OverworldMenuCategory.Equip)
            HandleEquipInput();
        else if (category == OverworldMenuCategory.Power)
            HandlePowerInput();
    }

    private void HandleEquipInput()
    {
        if (GameInput.UIUpPressed || GameInput.BattleUpPressed)
        {
            _equipSlotIndex = (_equipSlotIndex - 1 + EquipmentLoadoutService.SlotCount) % EquipmentLoadoutService.SlotCount;
            _categoryStatus = string.Empty;
            RefreshEquipCategoryPanel();
        }
        else if (GameInput.UIDownPressed || GameInput.BattleDownPressed)
        {
            _equipSlotIndex = (_equipSlotIndex + 1) % EquipmentLoadoutService.SlotCount;
            _categoryStatus = string.Empty;
            RefreshEquipCategoryPanel();
        }
        else if (GameInput.UILeftPressed || GameInput.BattleLeftPressed)
            MoveCategoryCharacter(-1, RefreshEquipCategoryPanel);
        else if (GameInput.UIRightPressed || GameInput.BattleRightPressed)
            MoveCategoryCharacter(1, RefreshEquipCategoryPanel);
        else if (GameInput.ConfirmPressed || GameInput.UISubmitPressed)
            CycleSelectedEquipment();
    }

    private void HandlePowerInput()
    {
        if (GameInput.PowerPreviousCharacterPressed)
        {
            MoveCategoryCharacter(-1, RefreshPowerCategoryPanel);
            return;
        }
        if (GameInput.PowerNextCharacterPressed)
        {
            MoveCategoryCharacter(1, RefreshPowerCategoryPanel);
            return;
        }
        if (GameInput.PowerTabPressed)
        {
            _powerTab = _powerTab == PowerGrowthTab.Stats
                ? PowerGrowthTab.Skills
                : PowerGrowthTab.Stats;
            _categoryStatus = string.Empty;
            RefreshPowerCategoryPanel();
            return;
        }
        if (GameInput.PowerResetPressed)
        {
            ResetCurrentPowerTab();
            return;
        }

        if (_powerTab == PowerGrowthTab.Stats)
            HandleGrowthStatsInput();
        else
            HandleSkillTreeInput();
    }

    private void HandleGrowthStatsInput()
    {
        if (GameInput.UIUpPressed || GameInput.BattleUpPressed)
        {
            _growthStatIndex = (_growthStatIndex - 1 + GrowthStatCount) % GrowthStatCount;
            _categoryStatus = string.Empty;
            RefreshPowerCategoryPanel();
        }
        else if (GameInput.UIDownPressed || GameInput.BattleDownPressed)
        {
            _growthStatIndex = (_growthStatIndex + 1) % GrowthStatCount;
            _categoryStatus = string.Empty;
            RefreshPowerCategoryPanel();
        }
        else if (GameInput.UILeftPressed || GameInput.BattleLeftPressed)
        {
            RefundSelectedGrowthStat();
        }
        else if (GameInput.UIRightPressed
            || GameInput.BattleRightPressed
            || GameInput.ConfirmPressed
            || GameInput.UISubmitPressed)
        {
            InvestSelectedGrowthStat();
        }
    }

    private void HandleSkillTreeInput()
    {
        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        CharacterData data = member != null
            ? CharacterDatabase.FindById(member.CharacterDataID)
            : null;
        List<SkillTreeNodeView> nodes = SkillTreeProgressionService.BuildViews(member, data);
        if (nodes.Count == 0)
            return;

        Vector2 direction = Vector2.zero;
        if (GameInput.UIUpPressed || GameInput.BattleUpPressed)
            direction = Vector2.up;
        else if (GameInput.UIDownPressed || GameInput.BattleDownPressed)
            direction = Vector2.down;
        else if (GameInput.UILeftPressed || GameInput.BattleLeftPressed)
            direction = Vector2.left;
        else if (GameInput.UIRightPressed || GameInput.BattleRightPressed)
            direction = Vector2.right;

        if (direction.sqrMagnitude > 0f)
        {
            _skillNodeIndex = SkillTreeProgressionService.FindDirectionalNodeIndex(
                nodes,
                _skillNodeIndex,
                direction);
            _categoryStatus = string.Empty;
            RefreshPowerCategoryPanel();
            return;
        }

        if (GameInput.ConfirmPressed || GameInput.UISubmitPressed)
            ActivateSelectedSkillNode(global, member, nodes);
    }

    private void MoveCategoryCharacter(int delta, Action refresh)
    {
        GlobalDataManager global = ResolveGlobalData();
        int count = global != null && global.Party != null ? global.Party.Count : 0;
        if (count <= 0)
            return;

        _categoryCharacterIndex = (_categoryCharacterIndex + delta + count) % count;
        _growthStatIndex = 0;
        _skillNodeIndex = 0;
        _categoryStatus = string.Empty;
        refresh?.Invoke();
    }

    private void RefreshEquipCategoryPanel()
    {
        if (_equipContentLabel == null)
            return;

        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (member == null)
        {
            _equipContentLabel.text = "장비를 변경할 파티원이 없습니다.";
            return;
        }

        EquipmentLoadoutService.NormalizeSlots(member);
        var text = new StringBuilder(512);
        text.Append("<color=#FFD105>").Append(GetPartyDisplayName(member)).Append("</color>");
        text.Append("   ◀ 파티원 ▶\n\n");
        for (int i = 0; i < EquipmentLoadoutService.SlotCount; i++)
        {
            EquipmentSlot slot = EquipmentLoadoutService.OrderedSlots[i];
            EquipmentData equipment = EquipmentLoadoutService.GetEquipped(member, slot);
            bool selected = i == _equipSlotIndex;
            if (selected)
                text.Append("<color=#FFD105>▶ ");
            else
                text.Append("  ");

            text.Append(EquipmentSlotName(slot)).Append("  ")
                .Append(equipment != null ? equipment.ItemName : "-");
            if (selected)
                text.Append("</color>");
            text.Append('\n');
        }

        StatBlock resolvedStats = CharacterStatsProjectionService.ResolveFromSave(
            member,
            ResolveCharacterData(member));
        text.Append("\nHP ").Append(resolvedStats.MaxHP);
        text.Append("  AP ").Append(resolvedStats.MaxAP);
        text.Append("  ATK ").Append(resolvedStats.ATK);
        text.Append("  DEF ").Append(resolvedStats.DEF);
        text.Append("  SPD ").Append(resolvedStats.SPD);
        text.Append("\n<color=#8F779B>↑↓ 슬롯  Z 변경  X 뒤로</color>");
        if (!string.IsNullOrEmpty(_categoryStatus))
            text.Append("\n").Append(_categoryStatus);
        _equipContentLabel.text = text.ToString();
    }

    private void CycleSelectedEquipment()
    {
        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (global == null || member == null)
            return;

        EquipmentSlot slot = EquipmentLoadoutService.OrderedSlots[_equipSlotIndex];
        var candidates = new List<EquipmentData>();
        foreach (EquipmentData equipment in EquipmentDatabase.GetAll())
        {
            if (equipment != null
                && equipment.Slot == slot
                && equipment.CanEquip(member.CharacterDataID)
                && global.GetEquipmentCount(equipment.ItemID) > 0)
            {
                candidates.Add(equipment);
            }
        }
        candidates.Sort((left, right) => string.Compare(left.ItemName, right.ItemName, StringComparison.Ordinal));

        string currentId = EquipmentLoadoutService.GetEquippedId(member, slot);
        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i].ItemID, currentId, StringComparison.Ordinal))
            {
                currentIndex = i;
                break;
            }
        }

        int next = currentIndex + 1;
        EquipmentChangeResult result = next >= candidates.Count
            ? EquipmentLoadoutService.TryUnequip(member, slot)
            : EquipmentLoadoutService.TryEquip(global, member, slot, candidates[next]);
        _categoryStatus = result.Message;
        RefreshRuntimePartyMember(member);
        RefreshEquipCategoryPanel();
        RebuildPartyPanel();
    }

    private void RefreshPowerCategoryPanel()
    {
        if (_powerGrowthView == null)
            return;

        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (member == null)
        {
            _powerGrowthView.RenderEmpty("능력을 확인할 파티원이 없습니다.");
            return;
        }

        CharacterData data = CharacterDatabase.FindById(member.CharacterDataID);
        CharacterGrowthService.EnsureInitialized(member, data);
        SkillTreeProgressionService.Synchronize(member, data);
        List<SkillTreeNodeView> nodes = SkillTreeProgressionService.BuildViews(member, data);
        _growthStatIndex = Mathf.Clamp(_growthStatIndex, 0, GrowthStatCount - 1);
        _skillNodeIndex = nodes.Count > 0
            ? Mathf.Clamp(_skillNodeIndex, 0, nodes.Count - 1)
            : 0;

        _powerGrowthView.Render(
            GetPartyDisplayName(member),
            member,
            data,
            _powerTab,
            _growthStatIndex,
            nodes,
            _skillNodeIndex,
            _categoryStatus);
    }

    private void InvestSelectedGrowthStat()
    {
        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (global == null || member == null)
            return;

        GrowthStat stat = (GrowthStat)Mathf.Clamp(_growthStatIndex, 0, GrowthStatCount - 1);
        GrowthInvestmentResult result = global.TryInvestGrowthStat(member, stat);
        _categoryStatus = GrowthStatusMessage(result.Status, stat, true);
        CompletePowerMutation(member, result.Succeeded);
    }

    private void RefundSelectedGrowthStat()
    {
        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (global == null || member == null)
            return;

        GrowthStat stat = (GrowthStat)Mathf.Clamp(_growthStatIndex, 0, GrowthStatCount - 1);
        bool changed = global.TryRefundGrowthStat(member, stat);
        _categoryStatus = changed
            ? GrowthStatName(stat) + " 1단계 반환"
            : "반환할 투자 포인트가 없습니다.";
        CompletePowerMutation(member, changed);
    }

    private void ResetCurrentPowerTab()
    {
        GlobalDataManager global = ResolveGlobalData();
        CharacterSaveData member = GetSelectedPartyMember(global);
        if (global == null || member == null)
            return;

        if (_powerTab == PowerGrowthTab.Stats)
        {
            bool accepted = global.TryResetGrowthStats(member, out int refunded);
            bool changed = accepted && refunded > 0;
            _categoryStatus = changed
                ? "능력치 포인트 " + refunded + " 반환"
                : "초기화할 능력치 투자가 없습니다.";
            CompletePowerMutation(member, changed);
            return;
        }

        SkillTreeActionResult result = global.TryResetSkillTree(member);
        _categoryStatus = result.Message;
        CompletePowerMutation(member, result.Succeeded);
    }

    private void ActivateSelectedSkillNode(
        GlobalDataManager global,
        CharacterSaveData member,
        IReadOnlyList<SkillTreeNodeView> nodes)
    {
        if (global == null || member == null || nodes == null || nodes.Count == 0)
            return;

        SkillTreeNodeView selected = nodes[Mathf.Clamp(_skillNodeIndex, 0, nodes.Count - 1)];
        string nodeId = selected.Definition.ResolveId();
        SkillTreeActionResult result = selected.IsUnlocked
            ? global.TryToggleSkillEquipped(member, nodeId)
            : global.TryUnlockSkillTreeNode(member, nodeId);
        _categoryStatus = result.Message;
        CompletePowerMutation(member, result.Succeeded);
    }

    private void CompletePowerMutation(CharacterSaveData member, bool succeeded)
    {
        if (succeeded)
        {
            RefreshRuntimePartyMember(member);
            RebuildPartyPanel();
        }

        RefreshPowerCategoryPanel();
        _powerGrowthView?.PlayActionPulse(succeeded);
    }

    private static string GrowthStatusMessage(
        GrowthInvestmentStatus status,
        GrowthStat stat,
        bool investing)
    {
        if (status == GrowthInvestmentStatus.Success)
            return GrowthStatName(stat) + (investing ? " 1단계 투자" : " 1단계 반환");

        return status switch
        {
            GrowthInvestmentStatus.InsufficientPoints => "사용 가능한 능력치 포인트가 없습니다.",
            GrowthInvestmentStatus.RankCapReached => "해당 능력치가 최대 단계입니다.",
            GrowthInvestmentStatus.MutationLocked => "전투 중에는 성장 설정을 변경할 수 없습니다.",
            GrowthInvestmentStatus.InvalidCharacter => "캐릭터 정보를 찾을 수 없습니다.",
            _ => "능력치를 변경하지 못했습니다."
        };
    }

    private static string GrowthStatName(GrowthStat stat)
    {
        return stat switch
        {
            GrowthStat.Vitality => "생명력",
            GrowthStat.Attack => "공격력",
            GrowthStat.Defense => "방어력",
            GrowthStat.Speed => "속도",
            GrowthStat.ActionPoints => "행동력",
            _ => "능력치"
        };
    }

    private CharacterSaveData GetSelectedPartyMember(GlobalDataManager global)
    {
        if (global == null || global.Party == null || global.Party.Count == 0)
            return null;

        _categoryCharacterIndex = (_categoryCharacterIndex % global.Party.Count + global.Party.Count) % global.Party.Count;
        return global.Party[_categoryCharacterIndex];
    }

    private static void RefreshRuntimePartyMember(CharacterSaveData member)
    {
        if (member == null)
            return;

        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerCharacter player = players[i];
            if (player != null && string.Equals(player.CharacterID, member.CharacterDataID, StringComparison.Ordinal))
                player.LoadDataFromGlobal(member);
        }
    }

    private void OpenConfigPanel()
    {
        _openOptionsAfterClose = true;
        RequestCloseFromUIManager();
    }

    private static string EquipmentSlotName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => "WEAPON",
            EquipmentSlot.Accessory1 => "ACCESSORY 1",
            EquipmentSlot.Accessory2 => "ACCESSORY 2",
            EquipmentSlot.Head => "HEAD",
            EquipmentSlot.Body => "BODY",
            EquipmentSlot.Shoes => "SHOES",
            _ => slot.ToString().ToUpperInvariant()
        };
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GlobalDataManager ResolveGlobalData()
    {
        if (GlobalDataManager.Instance != null)
            return GlobalDataManager.Instance;

        return FindFirstObjectByType<GlobalDataManager>(FindObjectsInactive.Include);
    }

}
