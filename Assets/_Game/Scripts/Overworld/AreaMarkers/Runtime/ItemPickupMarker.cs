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
    [SerializeField, ShowIf(nameof(UseFallbackPickupMessage)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;
    [TitleGroup("Item 설정/메시지")]
    [SerializeField, ShowIf(nameof(UseFallbackPickupMessage)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    private bool UseFallbackPickupMessage => pickupDialogueData == null;

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
        AreaMarkerRuntimeService.GrantItem(this, itemId, amount);

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

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(itemId))
            issues.Add("itemId가 비어 있습니다.");
        if (amount <= 0)
            issues.Add("amount는 1 이상이어야 합니다.");
    }
}