using UnityEngine;
using Sirenix.OdinInspector;

public enum EquipmentSlot
{
    Weapon, Accessory1, Accessory2, Head, Body, Shoes,
}

[CreateAssetMenu(fileName = "NewEquipment", menuName = "HubToHome/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50)]
    public Sprite Icon;

    [BoxGroup("Identity")] public string ItemName = "Equipment";
    [BoxGroup("Identity")] public string ItemID   = "equip_001";
    [BoxGroup("Identity")] public EquipmentSlot Slot;
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    // 스탯을 가로로 배치하여 보기 좋게 만듦
    [BoxGroup("Stat Bonuses")] 
    [HorizontalGroup("Stat Bonuses/Row1", LabelWidth = 60)] public int BonusMaxHP = 0;
    [HorizontalGroup("Stat Bonuses/Row1", LabelWidth = 60)] public int BonusMaxMP = 0;
    
    [BoxGroup("Stat Bonuses")] 
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusATK   = 0;
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusDEF   = 0;
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusSPD   = 0;

    [BoxGroup("Special Reaction")]
    [Tooltip("특정 캐릭터가 이 장비를 장착할 때 트리거할 대화 ID")]
    public string EquipReactionDialogueID = "";
}