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

    [BoxGroup("Equip Rules")]
    [Tooltip("비워 두면 모든 캐릭터가 장착할 수 있습니다. CharacterData.CharacterID를 사용합니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<string> AllowedCharacterIDs = new List<string>();

    [BoxGroup("Stat Bonuses")]
    [InfoBox("장비가 제공하는 모든 전투 수치는 이 StatBlock에 입력합니다. 기본값은 0인 보정 블록입니다.")]
    public StatBlock StatBonuses = StatBlock.CreateZeroModifier();

    public void AppendStatModifiers(List<StatModifier> destination)
    {
        if (destination == null)
            return;

        StatModifier.AppendStatBlock(
            destination,
            StatLayer.Equipment,
            StatBonuses,
            string.IsNullOrWhiteSpace(ItemID) ? name : ItemID);
    }

    // ── 🚨 추가됨: 특수 패시브 스킬 ──
    [BoxGroup("Special Effects")]
    [Tooltip("장착 시 패시브로 적용될 특수 효과 ID (예: 'AutoHeal_5', 'DoubleAttack')")]
    public string PassiveEffectID = "";

    [BoxGroup("Special Effects")]
    [Tooltip("특정 캐릭터가 이 장비를 장착할 때 트리거할 대화 ID")]
    public string EquipReactionDialogueID = "";

    public bool CanEquip(string characterDataId)
    {
        if (AllowedCharacterIDs == null || AllowedCharacterIDs.Count == 0)
            return true;

        string normalized = string.IsNullOrWhiteSpace(characterDataId)
            ? string.Empty
            : characterDataId.Trim();
        for (int i = 0; i < AllowedCharacterIDs.Count; i++)
        {
            if (string.Equals(AllowedCharacterIDs[i]?.Trim(), normalized, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
