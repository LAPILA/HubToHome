using UnityEngine;

/// <summary>아이템 종류</summary>
public enum ItemType
{
    Consumable,     // 소모품 (HP 회복 등)
    KeyItem,        // 중요 아이템 (퀘스트 관련)
    Equipment,      // 장비 (EquipmentData 참조)
}

/// <summary>
/// 아이템 데이터 ScriptableObject.
/// 에디터에서 Create > HubToHome > ItemData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "HubToHome/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string   ItemID      = "item_001";
    public string   ItemName    = "Item";
    public Sprite   Icon;
    [TextArea]
    public string   Description = "";

    [Header("Type")]
    public ItemType Type = ItemType.Consumable;

    [Header("Consumable Effect")]
    [Tooltip("HP 회복량 (Consumable 타입일 때)")]
    public int HealAmount = 0;

    [Header("Equipment Reference")]
    [Tooltip("Equipment 타입일 때 참조할 EquipmentData")]
    public EquipmentData EquipmentRef;

    [Header("Stack")]
    public bool  IsStackable  = true;
    public int   MaxStackSize = 99;
}
