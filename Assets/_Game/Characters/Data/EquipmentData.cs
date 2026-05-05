using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public enum EquipmentSlot
{
    Weapon, Accessory1, Accessory2, Head, Body, Shoes,
}

[CreateAssetMenu(fileName = "NewEquipment", menuName = "HubToHome/EquipmentData")]
public class EquipmentData : SerializedScriptableObject 
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50)]
    public Sprite Icon;

    [BoxGroup("Identity")] public string ItemName = "Equipment";
    [BoxGroup("Identity")] public string ItemID   = "equip_001";
    [BoxGroup("Identity")] public EquipmentSlot Slot;
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    [BoxGroup("Stat Bonuses")] 
    [HorizontalGroup("Stat Bonuses/Row1", LabelWidth = 60)] public int BonusMaxHP = 0;
    [HorizontalGroup("Stat Bonuses/Row1", LabelWidth = 60)] public int BonusMaxMP = 0;
    
    [BoxGroup("Stat Bonuses")] 
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusATK   = 0;
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusDEF   = 0;
    [HorizontalGroup("Stat Bonuses/Row2", LabelWidth = 60)] public int BonusSPD   = 0;

    // ── 🚨 추가됨: 상태이상 방어(내성) 보너스 ──
    [BoxGroup("Resistances (상태이상 방어력)")]
    [InfoBox("음수(-)를 넣으면 해당 상태이상에 걸릴 확률이나 데미지가 감소합니다. (예: Burn -50 = 화상 확률 50% 감소)")]
    [DictionaryDrawerSettings(KeyLabel = "상태이상", ValueLabel = "저항 수치")]
    public Dictionary<string, int> StatusResistanceBonus = new Dictionary<string, int>();

    // ── 🚨 추가됨: 특수 패시브 스킬 ──
    [BoxGroup("Special Effects")]
    [Tooltip("장착 시 패시브로 적용될 특수 효과 ID (예: 'AutoHeal_5', 'DoubleAttack')")]
    public string PassiveEffectID = "";

    [BoxGroup("Special Effects")]
    [Tooltip("특정 캐릭터가 이 장비를 장착할 때 트리거할 대화 ID")]
    public string EquipReactionDialogueID = "";
}