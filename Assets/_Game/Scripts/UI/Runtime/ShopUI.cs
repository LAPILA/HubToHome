using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShopUI : MonoBehaviour, IShopSessionLauncher
{
    private enum ShopMode
    {
        Buy,
        Sell
    }

    private static ShopUI s_instance;

    private readonly List<ItemData> _sellItems = new List<ItemData>();
    private CanvasGroup _canvasGroup;
    private TMP_Text _title;
    private TMP_Text _tabs;
    private TMP_Text _money;
    private TMP_Text _list;
    private TMP_Text _description;
    private TMP_Text _status;
    private ShopSession _session;
    private Action<ShopSessionResult> _onClosed;
    private ShopMode _mode;
    private int _sellIndex;
    private int _submitFrame = -1;
    private bool _visible;
    private GameState _previousGameState = GameState.Exploration;

    public static ShopUI Instance => s_instance;
    public bool IsVisible => _visible;

    public static ShopUI EnsureGlobal()
    {
        if (s_instance != null)
            return s_instance;

        ShopUI existing = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var root = new GameObject("ShopUI");
        return root.AddComponent<ShopUI>();
    }

    public bool TryOpen(
        ShopDefinition shop,
        string vendorId,
        Action<ShopSessionResult> onClosed)
    {
        if (_visible || shop == null || GlobalDataManager.Instance == null)
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

        _session.Changed += Refresh;
        _session.Closed += HandleSessionClosed;
        _onClosed = onClosed;
        _mode = ShopMode.Buy;
        _sellIndex = 0;
        _status.text = string.Empty;
        _previousGameState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        _visible = true;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        Refresh();
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
        AreaMarkerRuntimeService.UnregisterShopSessionLauncher(this);
        ForceClose();
        if (s_instance == this)
            s_instance = null;
    }

    private void Update()
    {
        if (!_visible || _session == null)
            return;

        if (GameInput.UILeftPressed || GameInput.UIRightPressed)
        {
            _mode = _mode == ShopMode.Buy ? ShopMode.Sell : ShopMode.Buy;
            _sellIndex = 0;
            _status.text = string.Empty;
            Refresh();
        }
        else if (GameInput.UIUpPressed)
        {
            MoveSelection(-1);
        }
        else if (GameInput.UIDownPressed)
        {
            MoveSelection(1);
        }
        else if (GameInput.UISubmitPressed && _submitFrame != Time.frameCount)
        {
            _submitFrame = Time.frameCount;
            ConfirmSelection();
        }
        else if (GameInput.UICancelPressed || GameInput.CancelPressed)
        {
            Close(ShopSessionEndReason.Canceled);
        }
    }

    private void MoveSelection(int delta)
    {
        if (_mode == ShopMode.Buy)
        {
            _session.MoveSelection(delta);
            return;
        }

        RebuildSellItems();
        if (_sellItems.Count == 0)
            return;
        _sellIndex = ((_sellIndex + delta) % _sellItems.Count + _sellItems.Count) % _sellItems.Count;
        Refresh();
    }

    private void ConfirmSelection()
    {
        if (_mode == ShopMode.Buy)
        {
            ShopPurchaseResult purchaseResult = _session.PurchaseSelected();
            _status.text = purchaseResult.Succeeded
                ? $"구매 완료: {purchaseResult.ItemAmount}개 / {purchaseResult.TotalPrice}G"
                : purchaseResult.Message;
            Refresh();
            return;
        }

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

    private void Refresh()
    {
        if (_session == null)
            return;

        _title.text = _session.Shop.DisplayName;
        _tabs.text = _mode == ShopMode.Buy
            ? "<color=#FFE05C>[ 구매 ]</color>   판매"
            : "구매   <color=#FFE05C>[ 판매 ]</color>";
        _money.text = "G " + _session.Store.Money;

        if (_mode == ShopMode.Buy)
            RefreshBuyList();
        else
            RefreshSellList();
    }

    private void RefreshBuyList()
    {
        var builder = new StringBuilder();
        IReadOnlyList<ShopEntry> entries = _session.Shop.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            ShopEntry entry = entries[i];
            bool selected = i == _session.SelectedIndex;
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(entry.Item != null ? entry.Item.ItemName : "(누락)");
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
    }

    private void RefreshSellList()
    {
        RebuildSellItems();
        if (_sellItems.Count == 0)
        {
            _list.text = "판매할 수 있는 아이템이 없습니다.";
            _description.text = string.Empty;
            return;
        }

        _sellIndex = Mathf.Clamp(_sellIndex, 0, _sellItems.Count - 1);
        var builder = new StringBuilder();
        for (int i = 0; i < _sellItems.Count; i++)
        {
            ItemData item = _sellItems[i];
            bool selected = i == _sellIndex;
            int count = _session.Store.GetItemCount(item.ItemID);
            int price = item.Price <= 0 ? 0 : Mathf.Max(1, item.Price / 2);
            builder.Append(selected ? "<color=#FFE05C>▶ " : "  ");
            builder.Append(item.ItemName).Append("  x").Append(count);
            builder.Append("   ").Append(price).Append('G');
            if (selected)
                builder.Append("</color>");
            builder.AppendLine();
        }

        _list.text = builder.ToString();
        _description.text = _sellItems[_sellIndex].Description;
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
        _session?.TryClose(reason, out _);
    }

    private void ForceClose()
    {
        if (_session != null && !_session.IsClosed)
            _session.TryClose(ShopSessionEndReason.ForcedClosed, out _);
    }

    private void HandleSessionClosed(ShopSessionResult result)
    {
        ShopSession closedSession = _session;
        _session = null;
        if (closedSession != null)
        {
            closedSession.Changed -= Refresh;
            closedSession.Closed -= HandleSessionClosed;
        }

        Action<ShopSessionResult> callback = _onClosed;
        _onClosed = null;
        HideImmediate();
        GameStateManager.Instance?.ChangeState(_previousGameState);
        callback?.Invoke(result);
    }

    private void HideImmediate()
    {
        _visible = false;
        if (_canvasGroup == null)
            return;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void BuildView()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        TMP_FontAsset font = GameContentCatalog.Instance != null
            ? GameContentCatalog.Instance.DefaultUiFont
            : TMP_Settings.defaultFontAsset;
        Image backdrop = CreateImage("Backdrop", transform, new Color32(8, 8, 12, 245));
        Stretch(backdrop.rectTransform);

        _title = CreateText("Title", transform, font, 30f, FontStyles.Bold);
        _title.alignment = TextAlignmentOptions.Left;
        SetRect(_title.rectTransform, new Vector2(-205f, 188f), new Vector2(330f, 42f));

        _tabs = CreateText("Tabs", transform, font, 20f, FontStyles.Normal);
        _tabs.alignment = TextAlignmentOptions.Center;
        SetRect(_tabs.rectTransform, new Vector2(0f, 142f), new Vector2(360f, 34f));

        _money = CreateText("Money", transform, font, 22f, FontStyles.Bold);
        _money.alignment = TextAlignmentOptions.Right;
        SetRect(_money.rectTransform, new Vector2(220f, 188f), new Vector2(150f, 36f));

        _list = CreateText("List", transform, font, 19f, FontStyles.Normal);
        _list.alignment = TextAlignmentOptions.TopLeft;
        _list.textWrappingMode = TextWrappingModes.NoWrap;
        SetRect(_list.rectTransform, new Vector2(-125f, 5f), new Vector2(340f, 250f));

        _description = CreateText("Description", transform, font, 17f, FontStyles.Normal);
        _description.alignment = TextAlignmentOptions.TopLeft;
        _description.textWrappingMode = TextWrappingModes.Normal;
        SetRect(_description.rectTransform, new Vector2(205f, 12f), new Vector2(220f, 210f));

        _status = CreateText("Status", transform, font, 17f, FontStyles.Normal);
        _status.alignment = TextAlignmentOptions.Center;
        _status.color = new Color32(255, 224, 92, 255);
        SetRect(_status.rectTransform, new Vector2(0f, -160f), new Vector2(560f, 42f));

        TMP_Text help = CreateText("Help", transform, font, 15f, FontStyles.Normal);
        help.text = "← → 구매/판매   ↑ ↓ 선택   확인 거래   취소 닫기";
        help.alignment = TextAlignmentOptions.Center;
        help.color = new Color32(170, 170, 180, 255);
        SetRect(help.rectTransform, new Vector2(0f, -208f), new Vector2(580f, 28f));
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float size,
        FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
