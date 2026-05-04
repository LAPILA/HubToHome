using UnityEngine;

/// <summary>장비 슬롯 종류</summary>
public enum EquipmentSlot
{
    Weapon,
    Accessory1,
    Accessory2,
    Head,
    Body,
    Shoes,
}

/// <summary>
/// 장비 아이템 데이터 ScriptableObject.
/// 에디터에서 Create > HubToHome > EquipmentData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "HubToHome/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    [Header("Identity")]
    public string         ItemName    = "Equipment";
    public string         ItemID      = "equip_001";
    public Sprite         Icon;
    [TextArea] public string Description = "";

    [Header("Slot")]
    public EquipmentSlot  Slot;

    [Header("Stat Bonuses")]
    public int BonusATK    = 0;
    public int BonusDEF    = 0;
    public int BonusSPD    = 0;
    public int BonusMaxHP  = 0;
    public int BonusMaxMP = 0;

    [Header("Special Reaction")]
    [Tooltip("특정 캐릭터가 이 장비를 장착할 때 트리거할 대화 ID")]
    public string EquipReactionDialogueID = "";
}
