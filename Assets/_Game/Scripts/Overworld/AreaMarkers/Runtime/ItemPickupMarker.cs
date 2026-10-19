using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ItemPickupMarker : AreaMarkerBase
{
    [TitleGroup("Item 설정/지급")]
    [SerializeField, LabelText("아이템 ID")] private string itemId;
    [TitleGroup("Item 설정/지급")]
    [SerializeField, Min(1), LabelText("수량")] private int amount = 1;

    [TitleGroup("Item 설정/메시지")]
    [SerializeField, Tooltip("획득 후 표시할 DialogueData입니다. 비어 있으면 pickupMessage를 1노드 대사로 표시합니다."), LabelText("DialogueData")]
    private DialogueData pickupDialogueData;
    [TitleGroup("Item 설정/메시지")]
    [TextArea(2, 6)] [SerializeField, ShowIf(nameof(UseFallbackPickupMessage)), LabelText("획득 메시지")]
    private string pickupMessage;
    [TitleGroup("Item 설정/메시지")]
    [SerializeField, ShowIf(nameof(UseFallbackPickupMessage)), LabelText("화자")]
    private SpeakerData fallbackSpeaker;
    [TitleGroup("Item 설정/메시지")]
    [SerializeField, ShowIf(nameof(UseFallbackPickupMessage)), LabelText("표정")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    [TitleGroup("상자 외형")]
    [SerializeField, LabelText("획득 후 열린 상자 유지")]
    [Tooltip("끄면 기존 아이템처럼 획득 후 사라집니다. 켜면 1회성 상자가 되어 열린 외형으로 남습니다.")]
    private bool keepVisibleAfterPickup;
    [TitleGroup("상자 외형")]
    [SerializeField, ShowIf(nameof(keepVisibleAfterPickup)), LabelText("상자 렌더러")]
    private SpriteRenderer pickupRenderer;
    [TitleGroup("상자 외형")]
    [SerializeField, ShowIf(nameof(keepVisibleAfterPickup)), LabelText("닫힌 그림")]
    private Sprite closedSprite;
    [TitleGroup("상자 외형")]
    [SerializeField, ShowIf(nameof(keepVisibleAfterPickup)), LabelText("열린 그림")]
    private Sprite openedSprite;

    private bool _pickupInProgress;
    private GlobalDataManager _completionSource;
    private string _completionFlag;
    private string _explicitCompletionFlag;

    private bool UseFallbackPickupMessage => pickupDialogueData == null;

    private void OnEnable()
    {
        if (!keepVisibleAfterPickup) return;
        isOneShot = true;
        CacheChestRenderer();
        BindCompletionSource();
        RefreshChestState();
    }

    private void OnDisable() => UnbindCompletionSource();
    private void OnDestroy() => UnbindCompletionSource();

    protected override void Start()
    {
        if (keepVisibleAfterPickup)
        {
            isOneShot = true;
            CacheChestRenderer();
            BindCompletionSource();
        }
        base.Start();
        RefreshChestState();
    }

    protected override void Reset()
    {
        markerType = AreaMarkerType.Item;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = true;
        base.Reset();
    }

    protected override void EnsureDefaults()
    {
        if (keepVisibleAfterPickup) isOneShot = true;
        base.EnsureDefaults();
    }

    public override bool CanInteract(PlayerController player)
    {
        if (_pickupInProgress) return false;
        if (keepVisibleAfterPickup)
        {
            isOneShot = true;
            if (BindCompletionSource()) RefreshChestState();
        }
        return base.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        _pickupInProgress = true;
        try
        {
            if (isOneShot && string.IsNullOrWhiteSpace(markerId))
            {
                Debug.LogWarning("[ItemPickupMarker] 일회성 아이템의 마커 ID가 비어 있습니다.", this);
                return;
            }
            if (keepVisibleAfterPickup)
            {
                CacheChestRenderer();
                if (pickupRenderer == null || closedSprite == null || openedSprite == null)
                {
                    Debug.LogWarning("[ItemPickupMarker] 상자 렌더러와 닫힌/열린 그림을 연결해야 합니다.", this);
                    return;
                }
            }
            if (!AreaMarkerRuntimeService.TryGrantItem(this, ResolveGlobalData(), itemId, amount,
                    out ItemData item, out string error))
            {
                TryStartDialogue(null, error, fallbackSpeaker, fallbackEmotion);
                return;
            }
            // Commit before presentation. Cancel/skip/missing dialogue cannot grant this reward twice.
            if (isOneShot) CompleteMarker();
            AudioManager.Instance?.PlaySelectionSfx();
            TryStartDialogue(pickupDialogueData, BuildPickupMessage(item), fallbackSpeaker, fallbackEmotion);
        }
        finally
        {
            _pickupInProgress = false;
        }
    }

    protected override void ApplyCompletedState()
    {
        if (!keepVisibleAfterPickup)
        {
            base.ApplyCompletedState();
            return;
        }
        CacheChestRenderer();
        if (pickupRenderer != null && openedSprite != null) pickupRenderer.sprite = openedSprite;
        ShowHighlight(false);
        // Keep solid collision and the opened graphic; CanInteract alone rejects further input.
    }

    private void CacheChestRenderer()
    {
        if (pickupRenderer == null) pickupRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (closedSprite == null && pickupRenderer != null && pickupRenderer.sprite != openedSprite)
            closedSprite = pickupRenderer.sprite;
    }

    private bool BindCompletionSource()
    {
        GlobalDataManager source = ResolveGlobalData();
        if (_completionSource == source) return false;
        UnbindCompletionSource();
        _completionSource = source;
        if (_completionSource != null)
        {
            // Marker identity is immutable during play. Do not rebuild keys for every unrelated flag event.
            _completionFlag = AreaMarkerStateService.BuildCompletionFlag(gameObject.scene.name, areaId, markerId);
            _explicitCompletionFlag = string.IsNullOrWhiteSpace(setFlagOnComplete) ? null : setFlagOnComplete.Trim();
            _completionSource.FlagChanged += HandleCompletionFlagChanged;
        }
        return true;
    }

    private void UnbindCompletionSource()
    {
        if (_completionSource != null) _completionSource.FlagChanged -= HandleCompletionFlagChanged;
        _completionSource = null;
    }

    private void HandleCompletionFlagChanged(string key, int oldValue, int newValue)
    {
        if (key == _completionFlag || key == _explicitCompletionFlag)
            RefreshChestState();
    }

    private void RefreshChestState()
    {
        if (!keepVisibleAfterPickup) return;
        CacheChestRenderer();
        RefreshCompletionState();
        if (!IsCompleted && pickupRenderer != null && closedSprite != null) pickupRenderer.sprite = closedSprite;
    }

    private string BuildPickupMessage(ItemData item)
    {
        if (!string.IsNullOrWhiteSpace(pickupMessage))
            return pickupMessage;

        string itemName = item != null && !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : "아이템";
        return $"* {itemName} ×{amount}을(를) 얻었다.";
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null) return;
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(itemId))
            issues.Add("itemId가 비어 있습니다.");
        if (amount <= 0)
            issues.Add("amount는 1 이상이어야 합니다.");
        if (keepVisibleAfterPickup)
        {
            if (pickupRenderer == null && GetComponentInChildren<SpriteRenderer>(true) == null)
                issues.Add("상자 SpriteRenderer가 없습니다.");
            if (closedSprite == null) issues.Add("닫힌 상자 그림이 없습니다.");
            if (openedSprite == null) issues.Add("열린 상자 그림이 없습니다.");
        }
    }
}
