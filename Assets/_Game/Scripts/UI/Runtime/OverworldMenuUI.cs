using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum OverworldMenuCategory
{
    Item,
    Equip,
    Power,
    Config
}

/// <summary>
/// 기존 프리팹 진입점을 유지하는 필드 메뉴 제어기. 뷰는 한 번 생성해 재사용하고,
/// 저장 변경은 기존 인벤토리·장비·성장 서비스에 위임합니다.
/// 표시 모드: FixedViewport. 공통 UIRuntimeGuard 계약을 따릅니다.
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
    [SerializeField, Tooltip("비우면 Resources/HubToHome/JournalCatalog를 한 번 읽습니다.")]
    private JournalCatalog _journalCatalog;

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


    private FieldMenuView _view;
    private FieldMenuTouchInput _touch;
    private GlobalDataManager _global;
    private readonly List<ItemData> _items = new List<ItemData>();
    private readonly List<EquipmentData> _equipment = new List<EquipmentData>();
    private readonly List<JournalEntry> _entries = new List<JournalEntry>();
    private readonly List<FieldMenuListEntry> _rows = new List<FieldMenuListEntry>();
    private readonly Dictionary<CharacterSaveData, CharacterStatInvestments> _drafts = new Dictionary<CharacterSaveData, CharacterStatInvestments>();
    private readonly List<string> _filterLabels = new List<string>();
    private List<SkillTreeNodeView> _nodes = new List<SkillTreeNodeView>();
    private PlayerCharacter[] _runtimeParty = Array.Empty<PlayerCharacter>();
    private int _page, _filter, _row, _actor, _stat, _node;
    private bool _isOpen, _isAnimating, _contentFocus, _targeting, _reading, _dirty;
    private bool _ownsPauseState, _openOptionsAfterClose, _modalChoice;
    private float _timeScaleBeforeOpen = 1f, _confirmAfter;
    private int _ignoreInputUntilFrame;
    private GameState _stateBeforeOpen;
    private PowerGrowthTab _tab;
    private Sequence _panelSequence;
    private string _status = string.Empty;
    private MenuModal _modal;
    private CharacterSaveData _modalMember;
    private enum MenuModal { None, Stats, Skills, Discard }
    private static string L(string key, string fallback) => LocalizationManager.Localize("field_menu." + key, fallback);

    protected override void Awake()
    {
        base.Awake();
        if (_rootGroup == null) _rootGroup = _canvasGroup;
        // Preserve serialized legacy anchors for existing prefab/editor references.
        if (_topPanel != null) _topPanel.gameObject.SetActive(false);
        if (_bottomPanel != null) _bottomPanel.gameObject.SetActive(false);
        if (_categoryWindow != null) _categoryWindow.gameObject.SetActive(false);
        UIRuntimeGuard.NormalizeCanvas(gameObject);
        TMP_FontAsset font = GameContentCatalog.Instance != null ? GameContentCatalog.Instance.DefaultUiFont : null;
        if (font == null) font = _categoryLabel != null ? _categoryLabel.font : TMP_Settings.defaultFontAsset;
        _view = FieldMenuView.Create((RectTransform)transform, font, Dispatch);
        _view.Bind(Dispatch);
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        gameObject.AddComponent<FieldMenuViewport>().Initialize(_view.Frame, canvas);
        _touch = gameObject.AddComponent<FieldMenuTouchInput>();
        _touch.Initialize(this, canvas, _view.Frame, CanInteract);
        _journalCatalog = _journalCatalog != null ? _journalCatalog : JournalCatalog.LoadDefault();
        SetVisible(false);
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += LanguageChanged;
        GameInput.DeviceChanged += DeviceChanged;
    }
    protected override void OnDisable()
    {
        LocalizationManager.LanguageChanged -= LanguageChanged;
        GameInput.DeviceChanged -= DeviceChanged;
        UnbindData(); KillMenuTweens(); RestorePreviousGameState(); _isOpen = false;
        _drafts.Clear(); _openOptionsAfterClose = false; base.OnDisable();
    }
    protected override void OnDestroy()
    {
        UnbindData(); KillMenuTweens(); RestorePreviousGameState(); base.OnDestroy();
    }
    private void LanguageChanged(LanguageType language) { _dirty = true; }
    private void DeviceChanged(GameInputDeviceKind device) { _dirty = true; }
    private void PartyChanged() { _dirty = true; }
    private void GrowthChanged(CharacterSaveData member) { _dirty = true; }
    private void FlagChanged(string key, int oldValue, int value) { _dirty = true; }
    private void BindData()
    {
        UnbindData(); _global = GlobalDataManager.Instance;
        if (_global == null) return;
        _global.PartyChanged += PartyChanged;
        _global.CharacterGrowthChanged += GrowthChanged;
        _global.FlagChanged += FlagChanged;
    }
    private void UnbindData()
    {
        if (_global == null) return;
        _global.PartyChanged -= PartyChanged;
        _global.CharacterGrowthChanged -= GrowthChanged;
        _global.FlagChanged -= FlagChanged;
        _global = null;
    }

    public override void Show()
    {
        if (_isOpen) return;
        gameObject.SetActive(true);
        BindData(); _drafts.Clear(); _status = string.Empty;
        _runtimeParty = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _isOpen = true; _isAnimating = true; _targeting = _reading = _contentFocus = false;
        _modal = MenuModal.None; _view.Modal(false); _ignoreInputUntilFrame = Time.frameCount + 2;
        _timeScaleBeforeOpen = Time.timeScale; Time.timeScale = 0;
        _stateBeforeOpen = GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState : GameState.Exploration;
        _ownsPauseState = true;
        GameStateManager.Instance?.ChangeState(GameState.Paused);
        GameInput.ConsumeBattleUIInput(); GameInput.ResetCachedState();
        _canvasGroup.alpha = 0; _canvasGroup.interactable = true; _canvasGroup.blocksRaycasts = true;
        if (_rootGroup != _canvasGroup) { _rootGroup.alpha = 1; _rootGroup.interactable = true; _rootGroup.blocksRaycasts = true; }
        Refresh();
        KillMenuTweens();
        _panelSequence = DOTween.Sequence().SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        _panelSequence.Append(_canvasGroup.DOFade(1, .16f));
        _panelSequence.OnComplete(() => { if (this == null) return; _panelSequence = null; _isAnimating = false; });
    }
    public override void ShowImmediate() { Show(); if (_panelSequence != null) _panelSequence.Complete(); }
    public override void FocusDefaultSelection()
    {
        // 메뉴의 방향 입력은 제어기가 소유합니다. 이전 UI 선택에 Submit을 중복 전달하지 않습니다.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public override void Hide()
    {
        if (!_isOpen) return;
        KillMenuTweens(); _isAnimating = true; _view.EndTextEditing();
        GameInput.ConsumeBattleUIInput(); GameInput.SuppressMenuInputsForCurrentFrame();
        _panelSequence = DOTween.Sequence().SetUpdate(true).SetRecyclable(false).SetLink(gameObject);
        _panelSequence.Append(_canvasGroup.DOFade(0, .12f));
        _panelSequence.OnComplete(() =>
        {
            if (this == null) return;
            _panelSequence = null; _isOpen = _isAnimating = false;
            bool options = _openOptionsAfterClose; _openOptionsAfterClose = false;
            RestorePreviousGameState(); gameObject.SetActive(false);
            if (options) OptionsPanelService.Open();
        });
    }
    public override void HideImmediate()
    {
        KillMenuTweens(); _openOptionsAfterClose = false; _isOpen = _isAnimating = false;
        _view?.EndTextEditing(); RestorePreviousGameState(); SetVisible(false); gameObject.SetActive(false);
    }
    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1 : 0; _canvasGroup.interactable = visible; _canvasGroup.blocksRaycasts = visible;
    }
    private void KillMenuTweens() { if (_panelSequence != null) _panelSequence.Kill(false); _panelSequence = null; }
    private void RestorePreviousGameState()
    {
        if (!_ownsPauseState) return;
        _ownsPauseState = false;
        if (!GameInput.IsConfigModalActive && Mathf.Approximately(Time.timeScale, 0)) Time.timeScale = _timeScaleBeforeOpen;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused)
            GameStateManager.Instance.ChangeState(_stateBeforeOpen);
        GameInput.ConsumeBattleUIInput();
    }
    private bool CanInteract()
    {
        return _isOpen && !_isAnimating && Time.frameCount > _ignoreInputUntilFrame
            && Time.unscaledTime >= _confirmAfter && !GameInput.IsConfigModalActive
            && (UIManager.Instance == null || UIManager.Instance.TopPanel == this);
    }
    private bool Mutable => BattleManager.Instance == null || !BattleManager.Instance.IsBattleSessionActive;

    private void Update()
    {
        if (_dirty && _isOpen) Refresh();
        if (!CanInteract() || _view.IsTextEditing) return;
        if (GameInput.CancelPressed) { TryHandleCancelInput(); return; }
        if (_modal != MenuModal.None)
        {
            if (GameInput.UILeftPressed || GameInput.UIRightPressed) { _modalChoice = !_modalChoice; RenderModal(); }
            else if (GameInput.ConfirmPressed || GameInput.UISubmitPressed) ResolveModal(_modalChoice);
            return;
        }
        if (GameInput.PowerPreviousCharacterPressed) { SelectActor(-1, -1); return; }
        if (GameInput.PowerNextCharacterPressed) { SelectActor(-1, 1); return; }
        if (GameInput.PowerResetPressed && _page == 2) { RequestReset(); return; }
        if (GameInput.PowerTabPressed)
        {
            if (_page == 2) { _tab = _tab == PowerGrowthTab.Stats ? PowerGrowthTab.Skills : PowerGrowthTab.Stats; _contentFocus = true; }
            else { _filter = (_filter + 1) % Mathf.Max(1, FilterCount); _row = 0; }
            _status = string.Empty; Refresh(); return;
        }
        Vector2 direction = Vector2.zero;
        if (GameInput.UIUpPressed) direction = Vector2.up;
        else if (GameInput.UIDownPressed) direction = Vector2.down;
        else if (GameInput.UILeftPressed) direction = Vector2.left;
        else if (GameInput.UIRightPressed) direction = Vector2.right;
        if (direction != Vector2.zero) { Navigate(direction); return; }
        if (GameInput.ConfirmPressed || GameInput.UISubmitPressed) Confirm();
    }

    private void Dispatch(FieldMenuCommand command)
    {
        if (!CanInteract()) return;
        if (command.Kind != FieldMenuCommandKind.StatSelect && command.Kind != FieldMenuCommandKind.StatSet)
            _view.EndTextEditing();
        if (_modal != MenuModal.None)
        {
            if (command.Kind == FieldMenuCommandKind.ModalAccept) ResolveModal(command.Index == 1);
            else if (command.Kind == FieldMenuCommandKind.Cancel) ResolveModal(false);
            return;
        }
        _status = string.Empty;
        switch (command.Kind)
        {
            case FieldMenuCommandKind.Page: ChangePage(command.Index); break;
            case FieldMenuCommandKind.Filter: if (!_targeting) { _filter = Mathf.Clamp(command.Index, 0, FilterCount - 1); _row = 0; _reading = false; Refresh(); } break;
            case FieldMenuCommandKind.Actor: SelectActor(command.Index, command.Amount); break;
            case FieldMenuCommandKind.Row: _row = Mathf.Clamp(command.Index, 0, Mathf.Max(0, _rows.Count - 1)); _reading = false; Refresh(); break;
            case FieldMenuCommandKind.Cancel: TryHandleCancelInput(); break;
            case FieldMenuCommandKind.Confirm: Confirm(); break;
            case FieldMenuCommandKind.CompanionTab: _tab = (PowerGrowthTab)Mathf.Clamp(command.Index, 0, 1); _contentFocus = true; Refresh(); break;
            case FieldMenuCommandKind.StatSelect: _stat = Mathf.Clamp(command.Index, 0, 4); _contentFocus = true; Refresh(); break;
            case FieldMenuCommandKind.StatStep: StageStat(command.Index, Draft.Get((GrowthStat)command.Index) + command.Amount); break;
            case FieldMenuCommandKind.StatSet: StageStat(command.Index, command.Amount); break;
            case FieldMenuCommandKind.ClearDraft: Draft.Reset(); Refresh(); break;
            case FieldMenuCommandKind.Reset: RequestReset(); break;
            case FieldMenuCommandKind.SkillSelect: _node = Mathf.Clamp(command.Index, 0, Mathf.Max(0, _nodes.Count - 1)); _contentFocus = true; Refresh(); break;
            case FieldMenuCommandKind.Settings: _openOptionsAfterClose = true; RequestClose(); break;
            case FieldMenuCommandKind.DetailScroll: _view.ScrollDetail(command.Amount); break;
        }
    }
    private void ChangePage(int page)
    {
        if (_targeting) return;
        _view.EndTextEditing();
        _page = (page % 4 + 4) % 4; _filter = _row = 0; _reading = _contentFocus = false;
        Refresh();
    }
    private void Navigate(Vector2 direction)
    {
        if (_targeting) { SelectActor(-1, direction.x < 0 || direction.y > 0 ? -1 : 1); return; }
        if (_reading) { _view.ScrollDetail(direction.y < 0 || direction.x > 0 ? 1 : -1); return; }
        if (_page == 2 && _contentFocus)
        {
            if (_tab == PowerGrowthTab.Skills)
                _node = SkillTreeProgressionService.FindDirectionalNodeIndex(_nodes, _node, direction);
            else if (direction.y != 0) _stat = (_stat + (direction.y > 0 ? 4 : 1)) % 5;
            else StageStat(_stat, Draft.Get((GrowthStat)_stat) + (direction.x > 0 ? 1 : -1));
            Refresh(); return;
        }
        if (direction.x != 0) { ChangePage(_page + (direction.x > 0 ? 1 : -1)); return; }
        if (_page == 2) _contentFocus = true;
        else if (_rows.Count > 0) _row = (_row + (direction.y > 0 ? _rows.Count - 1 : 1)) % _rows.Count;
        Refresh();
    }
    private void SelectActor(int index, int delta)
    {
        if (_global == null || _global.Party.Count == 0) return;
        _view.EndTextEditing();
        int previous = _actor;
        _actor = index >= 0 ? Mathf.Clamp(index, 0, _global.Party.Count - 1) : (_actor + delta + _global.Party.Count) % _global.Party.Count;
        if (_targeting && index >= 0 && previous == _actor) { Confirm(); return; }
        Refresh();
    }
    public override bool TryHandleCancelInput()
    {
        if (!CanInteract()) return true;
        _ignoreInputUntilFrame = Time.frameCount;
        GameInput.SuppressMenuInputsForCurrentFrame();
        GameInput.ConsumeBattleUIInput();
        if (_view.IsTextEditing) { _view.EndTextEditing(); return true; }
        if (_modal != MenuModal.None) { ResolveModal(false); return true; }
        if (_targeting) { _targeting = false; _status = string.Empty; Refresh(); return true; }
        if (_reading) { _reading = false; Refresh(); return true; }
        if (_contentFocus) { _contentFocus = false; Refresh(); return true; }
        RequestClose(); return true;
    }
    public void TryCloseFromPauseInput()
    {
        if (!CanInteract()) return;
        GameInput.ConsumeBattleUIInput(); GameInput.SuppressMenuInputsForCurrentFrame(); RequestClose();
    }
    private void RequestClose()
    {
        _view.EndTextEditing();
        if (HasDrafts())
        {
            _modal = MenuModal.Discard; _modalChoice = false; RenderModal(); return;
        }
        CloseThroughManager();
    }
    private void CloseThroughManager()
    {
        if (UIManager.Instance != null && UIManager.Instance.TopPanel == this) UIManager.Instance.CloseTopPanel();
        else Hide();
    }

    private CharacterSaveData Member => _global != null && _actor >= 0 && _actor < _global.Party.Count ? _global.Party[_actor] : null;
    private CharacterData Data => ResolveData(Member);
    private static CharacterData ResolveData(CharacterSaveData member)
    {
        if (member == null) return null;
        return CharacterDatabase.FindById(member.CharacterDataID) ?? CharacterDatabase.FindById(member.CharacterID);
    }
    private CharacterStatInvestments Draft
    {
        get
        {
            CharacterSaveData member = Member;
            if (member == null) return new CharacterStatInvestments();
            if (!_drafts.TryGetValue(member, out CharacterStatInvestments draft)) { draft = new CharacterStatInvestments(); _drafts.Add(member, draft); }
            return draft;
        }
    }
    private bool HasDrafts() { foreach (var pair in _drafts) if (pair.Value.Total > 0) return true; return false; }
    private void StageStat(int index, int amount)
    {
        CharacterSaveData member = Member;
        if (_page != 2 || _tab != PowerGrowthTab.Stats || member == null || !Mutable || index < 0 || index >= 5) return;
        CharacterStatInvestments draft = Draft; GrowthStat stat = (GrowthStat)index;
        int remaining = member.Growth.AvailableAttributePoints - draft.Total + draft.Get(stat);
        int cap = CharacterGrowthService.ResolveMaxInvestmentRank(Data != null ? Data.GrowthProfile : null) - member.Growth.Investments.Get(stat);
        draft.Set(stat, Mathf.Clamp(amount, 0, Mathf.Max(0, Mathf.Min(remaining, cap))));
        _stat = index; _contentFocus = true; Refresh();
    }
    private int FilterCount => _page == 1 ? 6 : _page == 0 ? 3 : 1;

    private void Refresh()
    {
        _dirty = false;
        if (_view == null || !_isOpen) return;
        if (_global != null) _actor = Mathf.Clamp(_actor, 0, Mathf.Max(0, _global.Party.Count - 1));
        CharacterSaveData member = Member; CharacterData data = Data;
        if (member != null) { CharacterGrowthService.EnsureInitialized(member, data); SkillTreeProgressionService.Synchronize(member, data); }
        JournalEntry objective = _journalCatalog != null ? _journalCatalog.GetCurrentObjective(ReadFlag) : null;
        _view.Header(_page, _global, objective); _view.Actor(member, data, DisplayName(member), Draft.Total);
        _view.Party(_global, _actor);
        _filterLabels.Clear(); _rows.Clear();
        if (_page == 2)
        {
            _nodes = SkillTreeProgressionService.BuildViews(member, data);
            _node = Mathf.Clamp(_node, 0, Mathf.Max(0, _nodes.Count - 1));
            _view.Growth(member, data, _tab, _stat, Draft, _nodes, _node, _contentFocus, Mutable);
        }
        else
        {
            if (_page == 0) BuildItems(); else if (_page == 1) BuildEquipment(); else BuildJournal();
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _rows.Count - 1));
            _view.Filters(_filterLabels, _filter); _view.List(_rows, _row); RenderDetail();
        }
        string hint = _targeting ? L("target_hint", "동료 선택 · Z 사용 · X 취소")
            : _reading ? L("reading_hint", "↑↓ 읽기 · X 목록")
            : _page == 2 && _contentFocus ? L("growth_hint", "방향키 능력치/트리 · Z 확정 · X 메뉴 · Q/E 동료 · C 탭")
            : L("menu_hint", "←→ 메뉴 · ↑↓ 목록 · Z 선택 · X 뒤로 · Q/E 동료 · C 분류");
        if (GameInput.ActiveDevice == GameInputDeviceKind.Touch)
            hint = _targeting ? L("touch_target_hint", "동료 카드를 고른 뒤 사용 · 뒤로 취소")
                : _reading ? L("touch_reading_hint", "밀어서 읽기 · 뒤로 목록")
                : _page == 2 ? L("touch_growth_hint", "탭·노드 터치 · 수량 입력 또는 ± 길게 누르기")
                : L("touch_menu_hint", "탭·목록·동료 터치 · 목록은 밀어서 이동");
        else if (GameInput.ActiveDevice == GameInputDeviceKind.Gamepad)
        {
            string template = _targeting ? L("pad_target_hint", "방향키 동료 · {0} 사용 · {1} 취소")
                : _reading ? L("pad_reading_hint", "↑↓ 읽기 · {1} 목록")
                : _page == 2 && _contentFocus ? L("pad_growth_hint", "방향키 능력치/트리 · {0} 확정 · {1} 메뉴 · LB/RB 동료")
                : L("pad_menu_hint", "←→ 메뉴 · ↑↓ 목록 · {0} 선택 · {1} 뒤로 · LB/RB 동료");
            hint = string.Format(template, GameInput.GetActionLabel(ConfigurableAction.Confirm),
                GameInput.GetActionLabel(ConfigurableAction.Cancel));
        }
        _view.Status(string.IsNullOrEmpty(_status) ? hint : _status);
    }
    private int ReadFlag(string id) => _global != null ? _global.GetFlag(id) : 0;
    private int ReadItem(string id) => _global != null ? _global.GetItemCount(id) : 0;
    private string DisplayName(CharacterSaveData member)
    {
        CharacterData data = ResolveData(member);
        return data != null ? data.ResolveDisplayName(_global != null ? _global.PlayerName : null) : member != null ? member.CharacterID : L("no_party", "동료 없음");
    }
    private static string ItemName(ItemData item) => LocalizationManager.Localize("item." + item.ItemID + ".name", item.ItemName);
    private void BuildItems()
    {
        _filterLabels.Add(L("all", "전체")); _filterLabels.Add(L("consumable", "소모품")); _filterLabels.Add(L("important", "중요 물품"));
        _items.Clear();
        if (_global == null) return;
        foreach (var pair in _global.GetInventory())
        {
            ItemData item = pair.Value > 0 ? ItemDatabase.FindById(pair.Key) : null;
            if (item == null || item.Type == ItemType.Equipment) continue;
            if (_filter == 1 && item.Type != ItemType.Consumable || _filter == 2 && item.Type != ItemType.KeyItem) continue;
            _items.Add(item);
        }
        _items.Sort((a, b) => string.CompareOrdinal(a.ItemID, b.ItemID));
        for (int i = 0; i < _items.Count; i++) _rows.Add(new FieldMenuListEntry(ItemName(_items[i]), "×" + ReadItem(_items[i].ItemID), _items[i].Icon));
    }
    private void BuildEquipment()
    {
        for (int i = 0; i < 6; i++) _filterLabels.Add(SlotName((EquipmentSlot)i));
        _equipment.Clear(); CharacterSaveData member = Member;
        if (member != null && _global != null)
            foreach (EquipmentData equipment in EquipmentDatabase.GetAll())
                if (equipment != null && (int)equipment.Slot == _filter && equipment.CanEquip(member.CharacterDataID) && _global.GetEquipmentCount(equipment.ItemID) > 0)
                    _equipment.Add(equipment);
        _equipment.Sort((a, b) => string.CompareOrdinal(a.ItemID, b.ItemID));
        string equipped = EquipmentLoadoutService.GetEquippedId(member, (EquipmentSlot)_filter);
        for (int i = 0; i < _equipment.Count; i++)
        {
            EquipmentData equipment = _equipment[i];
            int available = EquipmentLoadoutService.GetAvailableCount(_global, member, (EquipmentSlot)_filter, equipment.ItemID);
            _rows.Add(new FieldMenuListEntry(equipment.ItemName, equipped == equipment.ItemID ? L("equipped", "장착 중") : "×" + available, equipment.Icon, available > 0));
        }
        _rows.Add(new FieldMenuListEntry(L("unequip", "장비 해제"), string.Empty));
    }
    private void BuildJournal()
    {
        _filterLabels.Add(L("collected_notes", "수집한 메모 · 책 · 기록")); _entries.Clear();
        _journalCatalog?.GetCollectedEntries(_entries, ReadFlag, ReadItem);
        for (int i = 0; i < _entries.Count; i++)
            _rows.Add(new FieldMenuListEntry(_entries[i].LocalizedTitle, L("read", "읽기"), _entries[i].Icon));
    }

    private void RenderDetail()
    {
        if (_rows.Count == 0)
        {
            _view.Detail(_page == 3 ? L("journal_empty", "아직 수집한 기록이 없습니다.") : L("empty", "소지한 물품이 없습니다."), string.Empty,
                _page == 3 ? L("journal_help", "월드에서 얻은 메모나 책을 이곳에서 다시 읽을 수 있습니다.") : string.Empty, string.Empty, false); return;
        }
        if (_page == 3)
        {
            JournalEntry entry = _entries[_row];
            _view.Detail(entry.LocalizedTitle, L("collected", "획득한 기록"), entry.LocalizedBody, _reading ? L("reading", "읽는 중 · X 목록") : L("read", "읽기"), true); return;
        }
        if (_page == 0)
        {
            ItemData item = _items[_row];
            bool usable = Mutable && item.Type == ItemType.Consumable && item.UsableInOverworld;
            bool all = item.IsAoE || item.TargetType == TargetAreaType.AoEAll;
            string info = item.Description + "\n\n" + (all ? L("all_allies", "아군 전체") : _targeting ? DisplayName(Member) : L("one_ally", "아군 한 명"));
            _view.Detail(ItemName(item), item.Type == ItemType.KeyItem ? L("important", "중요 물품") : L("consumable", "소모품"),
                info, _targeting ? L("use_on_target", "선택한 동료에게 사용") : usable ? L("use", "사용하기") : L("cannot_use", "지금은 사용할 수 없습니다."), usable); return;
        }
        EquipmentData candidate = _row < _equipment.Count ? _equipment[_row] : null;
        CharacterSaveData member = Member; CharacterData data = Data;
        if (member == null || data == null) { _view.Detail(L("no_party", "동료 없음"), "", "", "", false); return; }
        CharacterSaveData copy = CopyMember(member);
        copy.HasInitializedEquipment = true;
        while (copy.EquippedEquipmentIDs.Count < 6) copy.EquippedEquipmentIDs.Add(string.Empty);
        copy.EquippedEquipmentIDs[_filter] = candidate != null ? candidate.ItemID : string.Empty;
        StatBlock before = CharacterStatsProjectionService.ResolveFromSave(member, data), after = CharacterStatsProjectionService.ResolveFromSave(copy, data);
        var text = new StringBuilder(candidate != null ? candidate.Description : string.Empty);
        text.Append("\n\nHP ").Append(before.MaxHP).Append(" → ").Append(after.MaxHP);
        text.Append("\nAP ").Append(before.MaxAP).Append(" → ").Append(after.MaxAP);
        text.Append("\nATK ").Append(before.ATK).Append(" → ").Append(after.ATK);
        text.Append("\nDEF ").Append(before.DEF).Append(" → ").Append(after.DEF);
        text.Append("\nSPD ").Append(before.SPD).Append(" → ").Append(after.SPD);
        bool same = EquipmentLoadoutService.GetEquippedId(member, (EquipmentSlot)_filter) == (candidate != null ? candidate.ItemID : string.Empty);
        bool available = candidate == null || EquipmentLoadoutService.GetAvailableCount(_global, member, (EquipmentSlot)_filter, candidate.ItemID) > 0;
        _view.Detail(candidate != null ? candidate.ItemName : L("unequip", "장비 해제"), SlotName((EquipmentSlot)_filter), text.ToString(),
            !Mutable ? L("battle_locked", "전투 중 변경 불가") : same ? L("equipped", "장착 중") : !available ? L("in_use", "다른 동료가 사용 중") : candidate == null ? L("unequip", "장비 해제") : L("equip", "장착하기"), Mutable && !same && available);
    }
    public static CharacterSaveData CopyMember(CharacterSaveData member)
    {
        return new CharacterSaveData { CharacterDataID = member.CharacterDataID, CharacterID = member.CharacterID,
            Level = member.Level, EXP = member.EXP, HP = member.HP, AP = member.AP, MaxHP = member.MaxHP, MaxAP = member.MaxAP,
            ATK = member.ATK, DEF = member.DEF, SPD = member.SPD, Growth = member.Growth != null ? member.Growth.Clone() : new CharacterGrowthSaveData(),
            HasInitializedEquipment = member.HasInitializedEquipment,
            EquippedEquipmentIDs = member.EquippedEquipmentIDs != null ? new List<string>(member.EquippedEquipmentIDs) : new List<string>(),
            EquippedSkillIDs = member.EquippedSkillIDs != null ? new List<string>(member.EquippedSkillIDs) : new List<string>(),
            UnlockedSkillIDs = member.UnlockedSkillIDs != null ? new List<string>(member.UnlockedSkillIDs) : new List<string>() };
    }
    private void Confirm()
    {
        _view.EndTextEditing();
        if (_modal != MenuModal.None) { ResolveModal(_modalChoice); return; }
        if (_page == 2)
        {
            if (!_contentFocus) { _contentFocus = true; Refresh(); return; }
            if (Member == null) return;
            if (_tab == PowerGrowthTab.Stats)
            {
                GrowthInvestmentResult result = _global.TryInvestGrowthStats(Member, Draft);
                _status = result.Succeeded ? L("applied", "능력치 배분을 적용했습니다.") : L("apply_failed", "포인트·최대 단계 또는 전투 상태를 확인해 주세요.");
                if (result.Succeeded) { Draft.Reset(); _confirmAfter = Time.unscaledTime + .18f; RefreshRuntime(Member); }
            }
            else if (_nodes.Count > 0)
            {
                SkillTreeNodeView node = _nodes[_node];
                SkillTreeActionResult result = node.IsUnlocked ? _global.TryToggleSkillEquipped(Member, node.Definition.ResolveId()) : _global.TryUnlockSkillTreeNode(Member, node.Definition.ResolveId());
                _status = result.Message;
                if (result.Succeeded) { RefreshRuntime(Member); _confirmAfter = Time.unscaledTime + .18f; }
            }
            Refresh(); return;
        }
        if (_page == 3) { _reading = true; Refresh(); return; }
        if (_page == 1)
        {
            if (Member == null || _global == null) return;
            EquipmentChangeResult result = _row < _equipment.Count ? EquipmentLoadoutService.TryEquip(_global, Member, (EquipmentSlot)_filter, _equipment[_row]) : EquipmentLoadoutService.TryUnequip(_global, Member, (EquipmentSlot)_filter);
            _status = result.Message; if (result.Succeeded) RefreshRuntime(Member); Refresh(); return;
        }
        if (_items.Count == 0 || _global == null || !Mutable) return;
        ItemData item = _items[_row];
        if (item.Type != ItemType.Consumable || !item.UsableInOverworld) return;
        if (!_targeting) { _targeting = true; Refresh(); return; }
        bool used = InventoryManager.TryUsePartyItem(item.ItemID, Member, out string error);
        _status = used ? L("used", "사용했습니다.") : error;
        _targeting = false;
        if (used) for (int i = 0; i < _global.Party.Count; i++) RefreshRuntime(_global.Party[i]);
        GameInput.SuppressPlayerConfirmForCurrentFrame(); Refresh();
    }
    private void RequestReset()
    {
        if (_page != 2 || Member == null || !Mutable) return;
        _view.EndTextEditing();
        _modal = _tab == PowerGrowthTab.Stats ? MenuModal.Stats : MenuModal.Skills;
        _modalMember = Member; _modalChoice = false; RenderModal();
    }
    private void RenderModal()
    {
        bool discard = _modal == MenuModal.Discard;
        _view.Modal(true, discard ? L("pending_title", "미적용 배분을 취소하고 나갈까요?") : L("reset_title", "포인트를 무료로 초기화할까요?"),
            discard ? L("pending_body", "미적용 배분만 취소합니다. 포인트는 소모되지 않습니다.") : L("reset_body", "선택한 동료의 현재 탭 투자만 반환합니다. 기본 스킬과 다른 동료는 유지됩니다."),
            discard ? L("leave", "배분 취소 후 나가기") : L("reset", "초기화"), _modalChoice);
    }
    private void ResolveModal(bool accepted)
    {
        MenuModal modal = _modal; CharacterSaveData member = _modalMember;
        _modal = MenuModal.None; _view.Modal(false); _confirmAfter = Time.unscaledTime + .18f;
        if (!accepted) { _openOptionsAfterClose = false; Refresh(); return; }
        if (modal == MenuModal.Discard) { _drafts.Clear(); CloseThroughManager(); return; }
        if (member != null && _global != null)
        {
            bool success;
            if (modal == MenuModal.Stats) { success = _global.TryResetGrowthStats(member, out int refunded); if (success && _drafts.TryGetValue(member, out CharacterStatInvestments draft)) draft.Reset(); }
            else { SkillTreeActionResult result = _global.TryResetSkillTree(member); success = result.Succeeded; _status = result.Message; }
            if (success) { RefreshRuntime(member); _status = L("reset_done", "포인트를 반환했습니다."); }
        }
        Refresh();
    }
    private void RefreshRuntime(CharacterSaveData member)
    {
        for (int i = 0; i < _runtimeParty.Length; i++)
        {
            PlayerCharacter player = _runtimeParty[i];
            if (player != null && player.IsBoundToSaveData(member)) player.LoadDataFromGlobal(member);
        }
    }
    private static string SlotName(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return L("weapon", "무기");
            case EquipmentSlot.Accessory1: return L("accessory1", "장신구 1");
            case EquipmentSlot.Accessory2: return L("accessory2", "장신구 2");
            case EquipmentSlot.Head: return L("head", "머리");
            case EquipmentSlot.Body: return L("body", "몸");
            default: return L("shoes", "신발");
        }
    }
}
