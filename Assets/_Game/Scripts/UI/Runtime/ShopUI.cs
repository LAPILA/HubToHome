using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 오버월드 상점 화면입니다. 구매·판매뿐 아니라 상인 이미지, 대화 주제,
/// 회복 서비스를 같은 ShopDefinition에서 읽어 런타임에 가볍게 구성합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class ShopUI : MonoBehaviour, IShopSessionLauncher
{
    private enum ShopMode
    {
        Main,
        Buy,
        Sell,
        Talk,
        Service
    }

    private enum MainOption
    {
        Talk,
        Buy,
        Sell,
        Service,
        Exit
    }

    private static readonly Color32 AccentColor = new Color32(255, 224, 92, 255);
    private static readonly Color32 MutedColor = new Color32(170, 170, 180, 255);
    private static readonly Color32 PanelColor = new Color32(7, 8, 14, 255);
    private const int VisibleRows = 5;

    private static ShopUI s_instance;

    private readonly List<ItemData> _sellItems = new List<ItemData>();
    private readonly List<MainOption> _mainOptions = new List<MainOption>();

    private CanvasGroup _canvasGroup;
    private CanvasGroup _menuGroup;
    private TMP_Text _title;
    private TMP_Text _tabs;
    private TMP_Text _money;
    private TMP_Text _list;
    private TMP_Text _description;
    private TMP_Text _status;
    private TMP_Text _help;
    private Image _vendorImage;
    private ShopSession _session;
    private ShopDefinition _activeShop;
    private Action<ShopSessionResult> _onClosed;
    private ShopMode _mode;
    private int _mainIndex;
    private int _sellIndex;
    private int _talkIndex;
    private int _serviceIndex;
    private int _submitFrame = -1;
    private bool _visible;
    private bool _suspendInput;
    private bool _closing;
    private bool _restoreStateOnClose;
    private int _ownerSceneHandle;
    private float _menuResumeAt;
    private DialogueManager _dialogueOwner;
    private int _dialogueGeneration;
    private DialogueData _activeTransientDialogue;
    private SpeakerData _activeTransientSpeaker;
    private GameState _previousGameState = GameState.Exploration;

    public static ShopUI Instance => s_instance;
    public bool IsVisible => _visible;

    public static ShopUI EnsureGlobal()
    {
        ShopUI existing = s_instance != null
            ? s_instance
            : FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            return existing;
        }

        var root = new GameObject("ShopUI");
        return root.AddComponent<ShopUI>();
    }

    public bool TryOpen(
        ShopDefinition shop,
        string vendorId,
        Action<ShopSessionResult> onClosed)
    {
        if (!isActiveAndEnabled)
        {
            gameObject.SetActive(true);
            enabled = true;
        }
        if (!isActiveAndEnabled || _visible || shop == null || GlobalDataManager.Instance == null)
            return false;

        try
        {
            _session = new ShopSession(
                shop,
                new GlobalDataShopTransactionStore(GlobalDataManager.Instance));
        }
        catch (Exception exception)
        {
            Debug.LogError("[ShopUI] 상점을 열 수 없습니다: " + exception.Message, this);
            return false;
        }

        _activeShop = shop;
        _session.Changed += Refresh;
        _session.Closed += HandleSessionClosed;
        _onClosed = onClosed;
        _mode = ShopMode.Main;
        _mainIndex = 0;
        _sellIndex = 0;
        _talkIndex = 0;
        _serviceIndex = 0;
        _suspendInput = false;
        _closing = false;
        _restoreStateOnClose = true;
        _ownerSceneHandle = SceneManager.GetActiveScene().handle;
        _submitFrame = Time.frameCount;
        _menuResumeAt = 0f;
        _menuGroup.alpha = 1f;
        _status.text = string.Empty;
        _previousGameState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;

        SubscribeShopReactions();
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        _visible = true;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        Refresh();
        BeginEntrance();
        return true;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
        BuildView();
        UIRuntimeGuard.NormalizeCanvas(gameObject);
        HideImmediate();
        AreaMarkerRuntimeService.RegisterShopSessionLauncher(this);
    }

    private void OnDestroy()
    {
        ForceClose();
        StopVendorShake();
        AreaMarkerRuntimeService.UnregisterShopSessionLauncher(this);
        if (s_instance == this)
            s_instance = null;
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        ForceClose();
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        if (!_visible || current.handle == _ownerSceneHandle)
            return;
        RestoreShopMusic(restorePrevious: false);
        _restoreStateOnClose = MapTransitionService.Instance == null
            || !MapTransitionService.Instance.IsTransitioning;
        ForceClose();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (!_visible || scene.handle != _ownerSceneHandle)
            return;
        RestoreShopMusic(restorePrevious: false);
        _restoreStateOnClose = MapTransitionService.Instance == null
            || !MapTransitionService.Instance.IsTransitioning;
        ForceClose();
    }

    private void Update()
    {
        if (!_visible || _session == null)
            return;

        GameStateManager state = GameStateManager.Instance;
        if (state != null && state.CurrentState == GameState.Battle)
        {
            _restoreStateOnClose = false;
            ForceClose();
            return;
        }
        if (_isOpening || _suspendInput || _submitFrame == Time.frameCount)
            return;
        if (Time.unscaledTime < _menuResumeAt)
            return;
        _menuGroup.alpha = 1f;
        if (state != null && state.CurrentState != GameState.Cutscene)
            return;

        if (GameInput.UIUpPressed)
        {
            MoveSelection(-1);
        }
        else if (GameInput.UIDownPressed)
        {
            MoveSelection(1);
        }
        else if ((_mode == ShopMode.Buy || _mode == ShopMode.Sell)
            && (GameInput.UILeftPressed || GameInput.UIRightPressed))
        {
            _mode = _mode == ShopMode.Buy ? ShopMode.Sell : ShopMode.Buy;
            _sellIndex = 0;
            _status.text = string.Empty;
            Refresh();
            return;
        }

        if (GameInput.UISubmitPressed && _submitFrame != Time.frameCount)
        {
            _submitFrame = Time.frameCount;
            ConfirmSelection();
        }
        else if (GameInput.UICancelPressed || GameInput.CancelPressed)
        {
            if (_mode == ShopMode.Main)
                Close(ShopSessionEndReason.Canceled);
            else
            {
                _mode = ShopMode.Main;
                _status.text = string.Empty;
                Refresh();
            }
        }
    }

    private void MoveSelection(int delta)
    {
        if (delta == 0)
            return;

        switch (_mode)
        {
            case ShopMode.Main:
                if (_mainOptions.Count == 0)
                    return;
                _mainIndex = WrapIndex(_mainIndex + delta, _mainOptions.Count);
                Refresh();
                break;
            case ShopMode.Buy:
                _session.MoveSelection(delta);
                break;
            case ShopMode.Sell:
                RebuildSellItems();
                if (_sellItems.Count == 0)
                    return;
                _sellIndex = WrapIndex(_sellIndex + delta, _sellItems.Count);
                Refresh();
                break;
            case ShopMode.Talk:
                if (_activeShop.Dialogues == null || _activeShop.Dialogues.Count == 0)
                    return;
                _talkIndex = WrapIndex(_talkIndex + delta, _activeShop.Dialogues.Count);
                Refresh();
                break;
            case ShopMode.Service:
                if (_activeShop.Services == null || _activeShop.Services.Count == 0)
                    return;
                _serviceIndex = WrapIndex(_serviceIndex + delta, _activeShop.Services.Count);
                Refresh();
                break;
        }
    }

    private void ConfirmSelection()
    {
        switch (_mode)
        {
            case ShopMode.Main:
                ConfirmMainSelection();
                break;
            case ShopMode.Buy:
                ConfirmPurchase();
                break;
            case ShopMode.Sell:
                ConfirmSale();
                break;
            case ShopMode.Talk:
                StartSelectedDialogue();
                break;
            case ShopMode.Service:
                ConfirmService();
                break;
        }
    }

    private void ConfirmMainSelection()
    {
        if (_mainOptions.Count == 0)
            return;

        switch (_mainOptions[Mathf.Clamp(_mainIndex, 0, _mainOptions.Count - 1)])
        {
            case MainOption.Talk:
                _mode = ShopMode.Talk;
                _talkIndex = 0;
                break;
            case MainOption.Buy:
                _mode = ShopMode.Buy;
                break;
            case MainOption.Sell:
                _mode = ShopMode.Sell;
                _sellIndex = 0;
                break;
            case MainOption.Service:
                _mode = ShopMode.Service;
                _serviceIndex = 0;
                break;
            case MainOption.Exit:
                Close(ShopSessionEndReason.Completed);
                return;
        }

        _status.text = string.Empty;
        Refresh();
    }

    private void ConfirmPurchase()
    {
        ShopPurchaseResult purchaseResult = _session.PurchaseSelected();
        _status.text = purchaseResult.Succeeded
            ? $"구매 완료: {purchaseResult.ItemAmount}개 / {purchaseResult.TotalPrice}G"
            : purchaseResult.Message;
        Refresh();
    }

    private void ConfirmSale()
    {
        RebuildSellItems();
        if (_sellItems.Count == 0)
        {
            _status.text = "판매할 수 있는 아이템이 없습니다.";
            Refresh();
            return;
        }

        _sellIndex = Mathf.Clamp(_sellIndex, 0, _sellItems.Count - 1);
        ShopSellResult sellResult = _session.Sell(_sellItems[_sellIndex]);
        _status.text = sellResult.Succeeded
            ? $"판매 완료: {sellResult.TotalPrice}G"
            : sellResult.Message;
        Refresh();
    }

    private void ConfirmService()
    {
        IReadOnlyList<ShopServiceEntry> services = _activeShop.Services;
        if (services == null || services.Count == 0)
            return;

        ShopServiceEntry service = services[Mathf.Clamp(_serviceIndex, 0, services.Count - 1)];
        if (service == null)
            return;

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || global.Party.Count == 0)
        {
            _status.text = "회복할 파티원이 없습니다.";
            return;
        }

        if (service.Price > 0 && !_session.Store.TrySpendMoneyExact(service.Price))
        {
            _status.text = "돈이 부족합니다.";
            Refresh();
            return;
        }

        int changedMembers = global.RestorePartyVitals(service.RestoreHp, service.RestoreAp);
        if (changedMembers <= 0)
        {
            if (service.Price > 0)
                _session.Store.TryRefundMoneyExact(service.Price);
            _status.text = "파티원이 이미 모두 회복되어 있습니다.";
            Refresh();
            return;
        }

        string priceText = service.Price > 0 ? $" / {service.Price}G" : " / 무료";
        _status.text = $"{service.DisplayName} 완료: {changedMembers}명 회복{priceText}";
        Refresh();
    }

    private void StartSelectedDialogue()
    {
        IReadOnlyList<ShopDialogueEntry> dialogues = _activeShop.Dialogues;
        if (dialogues == null || dialogues.Count == 0)
            return;

        ShopDialogueEntry entry = dialogues[Mathf.Clamp(_talkIndex, 0, dialogues.Count - 1)];
        if (entry == null || DialogueManager.Instance == null)
        {
            _status.text = "대화 시스템을 준비할 수 없습니다.";
            Refresh();
            return;
        }

        DialogueData dialogue = entry.Dialogue;
        if (dialogue == null)
        {
            SpeakerData speaker = _activeShop.VendorSpeaker;
            if (speaker == null)
            {
                _activeTransientSpeaker = ScriptableObject.CreateInstance<SpeakerData>();
                _activeTransientSpeaker.name = "Runtime_ShopSpeaker_" + entry.TopicId;
                _activeTransientSpeaker.SpeakerID = _activeShop.ShopId;
                _activeTransientSpeaker.DisplayName = _activeShop.VendorDisplayName;
                speaker = _activeTransientSpeaker;
            }

            _activeTransientDialogue = ScriptableObject.CreateInstance<DialogueData>();
            _activeTransientDialogue.name = "Runtime_ShopDialogue_" + entry.TopicId;
            _activeTransientDialogue.Style = DialogueStyle.Overworld;
            _activeTransientDialogue.Nodes.Add(new DialogueNode
            {
                Speaker = speaker,
                Emotion = entry.Emotion,
                LocalizationKey = entry.LocalizationKey,
                DefaultText = entry.DefaultText,
                EventTriggerID = entry.EventTriggerId,
                IsChoiceNode = false
            });
            dialogue = _activeTransientDialogue;
        }

        _suspendInput = true;
        SetVendorPortrait(entry.PortraitOverride);
        _menuGroup.alpha = 0f;
        _dialogueOwner = DialogueManager.Instance;
        bool started = _dialogueOwner.TryStartDialogue(
            dialogue,
            HandleDialogueFinished,
            HandleDialogueCancelled,
            null,
            out _dialogueGeneration);
        if (!started)
        {
            DestroyTransientDialogue();
            _dialogueOwner = null;
            _dialogueGeneration = 0;
            _suspendInput = false;
            SetVendorPortrait(null);
            _menuGroup.alpha = 1f;
            _status.text = "대화를 시작할 수 없습니다.";
            Refresh();
        }
    }

    private void HandleDialogueFinished()
    {
        EndTransientDialogue();
    }

    private void HandleDialogueCancelled()
    {
        EndTransientDialogue();
    }

    private void EndTransientDialogue()
    {
        _dialogueGeneration = 0;
        _dialogueOwner = null;
        DestroyTransientDialogue();
        StopVendorShake();
        SetVendorPortrait(null);
        _suspendInput = false;
        _submitFrame = Time.frameCount;
        if (!_visible || _closing || _canvasGroup == null)
            return;

        // 기존 대화창의 0.2초 닫기 애니메이션 후 메뉴를 다시 표시합니다.
        _menuResumeAt = Time.unscaledTime + 0.21f;
        _status.text = string.Empty;
        Refresh();
    }

    private void DestroyTransientDialogue()
    {
        if (_activeTransientDialogue != null)
        {
            if (Application.isPlaying)
                Destroy(_activeTransientDialogue);
            else
                DestroyImmediate(_activeTransientDialogue);
            _activeTransientDialogue = null;
        }

        if (_activeTransientSpeaker != null)
        {
            if (Application.isPlaying)
                Destroy(_activeTransientSpeaker);
            else
                DestroyImmediate(_activeTransientSpeaker);
            _activeTransientSpeaker = null;
        }
    }

    private void Refresh()
    {
        if (_session == null || _activeShop == null)
            return;

        _title.text = _activeShop.DisplayName;
        _money.text = "G " + _session.Store.Money;
        if (!_suspendInput)
            SetVendorPortrait(null);

        switch (_mode)
        {
            case ShopMode.Main:
                RefreshMain();
                break;
            case ShopMode.Buy:
                _tabs.text = "<color=#FFE05C>[ 구매 ]</color>   판매";
                RefreshBuyList();
                break;
            case ShopMode.Sell:
                _tabs.text = "구매   <color=#FFE05C>[ 판매 ]</color>";
                RefreshSellList();
                break;
            case ShopMode.Talk:
                _tabs.text = "이야기";
                RefreshTalkList();
                break;
            case ShopMode.Service:
                _tabs.text = _activeShop.ServiceMenuLabel;
                RefreshServiceList();
                break;
        }
    }

    private void RefreshMain()
    {
        _tabs.text = _activeShop.VendorDisplayName;
        BuildMainOptions();
        var builder = new StringBuilder();
        for (int i = 0; i < _mainOptions.Count; i++)
        {
            bool selected = i == _mainIndex;
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(GetMainOptionLabel(_mainOptions[i]));
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        MainOption selectedOption = _mainOptions[Mathf.Clamp(_mainIndex, 0, _mainOptions.Count - 1)];
        _description.text = GetMainOptionDescription(selectedOption);
        _help.text = "↑ ↓ 선택   확인 열기   취소 닫기";
    }

    private void BuildMainOptions()
    {
        _mainOptions.Clear();
        if (_activeShop.Dialogues != null && _activeShop.Dialogues.Count > 0)
            _mainOptions.Add(MainOption.Talk);
        if (_activeShop.Entries != null && _activeShop.Entries.Count > 0)
            _mainOptions.Add(MainOption.Buy);
        _mainOptions.Add(MainOption.Sell);
        if (_activeShop.Services != null && _activeShop.Services.Count > 0)
            _mainOptions.Add(MainOption.Service);
        _mainOptions.Add(MainOption.Exit);
        _mainIndex = Mathf.Clamp(_mainIndex, 0, _mainOptions.Count - 1);
    }

    private void RefreshBuyList()
    {
        var builder = new StringBuilder();
        IReadOnlyList<ShopEntry> entries = _session.Shop.Entries;
        int start = PageStart(_session.SelectedIndex);
        for (int i = start; i < Mathf.Min(start + VisibleRows, entries.Count); i++)
        {
            ShopEntry entry = entries[i];
            bool selected = i == _session.SelectedIndex;
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(ShortLabel(entry.Item != null ? entry.Item.ItemName : "(누락)"));
            builder.Append("  x").Append(entry.Quantity);
            builder.Append("   ").Append(entry.Price * entry.Quantity).Append('G');
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        ShopEntry selectedEntry = _session.SelectedEntry;
        _description.text = selectedEntry?.Item != null
            ? selectedEntry.Item.Description
            : string.Empty;
        _help.text = "↑ ↓ 선택   확인 구매   ← → 전환   취소 뒤로" + PageLabel(_session.SelectedIndex, entries.Count);
    }

    private void RefreshSellList()
    {
        RebuildSellItems();
        if (_sellItems.Count == 0)
        {
            _list.text = "판매할 수 있는 아이템이 없습니다.";
            _description.text = string.Empty;
            _help.text = "취소 뒤로";
            return;
        }

        _sellIndex = Mathf.Clamp(_sellIndex, 0, _sellItems.Count - 1);
        var builder = new StringBuilder();
        int start = PageStart(_sellIndex);
        for (int i = start; i < Mathf.Min(start + VisibleRows, _sellItems.Count); i++)
        {
            ItemData item = _sellItems[i];
            bool selected = i == _sellIndex;
            int count = _session.Store.GetItemCount(item.ItemID);
            int price = item.Price <= 0 ? 0 : Mathf.Max(1, item.Price / 2);
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(ShortLabel(item.ItemName)).Append("  x").Append(count);
            builder.Append("   ").Append(price).Append('G');
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        _description.text = _sellItems[_sellIndex].Description;
        _help.text = "↑ ↓ 선택   확인 판매   ← → 전환   취소 뒤로" + PageLabel(_sellIndex, _sellItems.Count);
    }

    private void RefreshTalkList()
    {
        IReadOnlyList<ShopDialogueEntry> dialogues = _activeShop.Dialogues;
        _talkIndex = Mathf.Clamp(_talkIndex, 0, dialogues.Count - 1);
        var builder = new StringBuilder();
        int start = PageStart(_talkIndex);
        for (int i = start; i < Mathf.Min(start + VisibleRows, dialogues.Count); i++)
        {
            ShopDialogueEntry entry = dialogues[i];
            bool selected = i == _talkIndex;
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(entry != null ? entry.DisplayName : "(누락)");
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        ShopDialogueEntry selectedEntry = dialogues[_talkIndex];
        _description.text = selectedEntry != null
            ? $"‘{selectedEntry.DisplayName}’에 관해 물어봅니다."
            : string.Empty;
        _help.text = "↑ ↓ 주제 선택   확인 대화   취소 뒤로" + PageLabel(_talkIndex, dialogues.Count);
    }

    private void RefreshServiceList()
    {
        IReadOnlyList<ShopServiceEntry> services = _activeShop.Services;
        _serviceIndex = Mathf.Clamp(_serviceIndex, 0, services.Count - 1);
        var builder = new StringBuilder();
        int start = PageStart(_serviceIndex);
        for (int i = start; i < Mathf.Min(start + VisibleRows, services.Count); i++)
        {
            ShopServiceEntry entry = services[i];
            bool selected = i == _serviceIndex;
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(entry != null ? entry.DisplayName : "(누락)");
            if (entry != null && entry.Price > 0)
                builder.Append("  ").Append(entry.Price).Append('G');
            else
                builder.Append("  무료");
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        ShopServiceEntry selectedEntry = services[_serviceIndex];
        _description.text = selectedEntry != null ? selectedEntry.Description : string.Empty;
        _help.text = "↑ ↓ 선택   확인 이용   취소 뒤로" + PageLabel(_serviceIndex, services.Count);
    }

    private void RebuildSellItems()
    {
        _sellItems.Clear();
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
            return;

        foreach (KeyValuePair<string, int> entry in global.GetInventory())
        {
            if (entry.Value <= 0)
                continue;
            ItemData item = ItemDatabase.FindById(entry.Key);
            if (item == null || !item.IsSellable || item.Type == ItemType.KeyItem)
                continue;
            _sellItems.Add(item);
        }

        _sellItems.Sort((left, right) => string.Compare(
            left != null ? left.ItemName : string.Empty,
            right != null ? right.ItemName : string.Empty,
            StringComparison.Ordinal));
    }

    private void Close(ShopSessionEndReason reason)
    {
        if (_suspendInput)
            return;
        _session?.TryClose(reason, out _);
    }

    private void ForceClose()
    {
        if (_session != null && !_session.IsClosed)
            _session.TryClose(ShopSessionEndReason.ForcedClosed, out _);
    }

    public void CloseForTransition()
    {
        ForceClose();
    }

    private void HandleSessionClosed(ShopSessionResult result)
    {
        _closing = true;
        StopEntrance();
        RestoreShopMusic();
        if (_dialogueOwner != null && _dialogueGeneration != 0)
            _dialogueOwner.CancelDialogue(_dialogueGeneration);
        _dialogueOwner = null;
        _dialogueGeneration = 0;
        ShopSession closedSession = _session;
        _session = null;
        _activeShop = null;
        if (closedSession != null)
        {
            closedSession.Changed -= Refresh;
            closedSession.Closed -= HandleSessionClosed;
        }

        UnsubscribeShopReactions();
        DestroyTransientDialogue();
        _suspendInput = false;
        Action<ShopSessionResult> callback = _onClosed;
        _onClosed = null;
        HideImmediate();
        GameInput.SuppressPlayerConfirmForCurrentFrame();
        GameStateManager state = GameStateManager.Instance;
        if (_restoreStateOnClose && state != null && state.CurrentState == GameState.Cutscene)
            state.ChangeState(_previousGameState);
        callback?.Invoke(result);
        _closing = false;
    }

    private void HideImmediate()
    {
        _visible = false;
        _suspendInput = false;
        _ambienceSilence?.Dispose();
        _ambienceSilence = null;
        StopVendorShake();
        StopEntrance();
        if (_canvasGroup == null)
            return;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private string GetMainOptionLabel(MainOption option)
    {
        switch (option)
        {
            case MainOption.Talk: return "대화";
            case MainOption.Buy: return "아이템 구매";
            case MainOption.Sell: return "아이템 판매";
            case MainOption.Service: return _activeShop.ServiceMenuLabel;
            default: return "나가기";
        }
    }

    private string GetMainOptionDescription(MainOption option)
    {
        switch (option)
        {
            case MainOption.Talk: return $"{_activeShop.VendorDisplayName}에게 궁금한 것을 물어봅니다.";
            case MainOption.Buy: return "물건의 설명과 가격을 확인하고 구매합니다.";
            case MainOption.Sell: return "가지고 있는 아이템을 판매합니다.";
            case MainOption.Service: return "서비스를 선택해 파티를 회복합니다.";
            default: return "상점 화면을 닫고 오버월드로 돌아갑니다.";
        }
    }

    private static int PageStart(int index) => Mathf.Max(0, index) / VisibleRows * VisibleRows;

    private static string PageLabel(int index, int count) => count > VisibleRows
        ? $"   {index / VisibleRows + 1}/{(count + VisibleRows - 1) / VisibleRows}"
        : string.Empty;

    private static string ShortLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return string.Empty;
        return label.Length > 10 ? label.Substring(0, 9) + "…" : label;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;
        return ((value % count) + count) % count;
    }

}
