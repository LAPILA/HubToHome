using UnityEngine;

public class ItemPickupMarker : AreaMarkerBase
{
    [Header("Item Pickup")]
    [SerializeField] private string itemId;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField, Tooltip("획득 후 표시할 DialogueData입니다. 비어 있으면 pickupMessage를 1노드 대사로 표시합니다.")]
    private DialogueData pickupDialogueData;
    [TextArea(2, 6)] [SerializeField]
    private string pickupMessage;
    [SerializeField] private SpeakerData fallbackSpeaker;
    [SerializeField] private EmotionType fallbackEmotion = EmotionType.Normal;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Item;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = true;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        if (!string.IsNullOrWhiteSpace(itemId) && GlobalDataManager.Instance != null)
            GlobalDataManager.Instance.AddItem(itemId, amount);
        Debug.Log($"[ItemPickupMarker] 아이템 획득: itemId={itemId}, amount={amount}", this);

        bool started = TryStartDialogue(
            pickupDialogueData,
            BuildPickupMessage(),
            fallbackSpeaker,
            fallbackEmotion,
            isOneShot ? CompleteMarker : null);

        if (!started && isOneShot)
            CompleteMarker();
    }

    private string BuildPickupMessage()
    {
        if (!string.IsNullOrWhiteSpace(pickupMessage))
            return pickupMessage;

        string itemName = string.IsNullOrWhiteSpace(itemId) ? "아이템" : itemId;
        return amount > 1
            ? $"* {itemName}을(를) {amount}개 얻었다."
            : $"* {itemName}을(를) 얻었다.";
    }
}