using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public enum EquipmentSlot
{
    Weapon, Accessory1, Accessory2, Head, Body, Shoes,
}

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Hub To Home/캐릭터/장비 데이터")]
public class EquipmentData : SerializedScriptableObject 
{
    [BoxGroup("기본 정보"), HideLabel, PreviewField(50)]
    public Sprite Icon;

    [BoxGroup("기본 정보"), LabelText("장비 이름")] public string ItemName = "Equipment";
    [BoxGroup("기본 정보"), LabelText("고유 ID"), Required("저장에 사용할 고유 ID가 필요합니다.")]
    [ValidateInput(nameof(IsValidId), "ID 앞뒤 공백을 제거하세요. 출시 후 ID를 바꾸면 기존 저장 연결이 끊깁니다.")]
    public string ItemID   = "equip_001";
    [BoxGroup("기본 정보"), LabelText("장착 슬롯")] public EquipmentSlot Slot;
    [BoxGroup("기본 정보"), LabelText("설명"), TextArea(2, 4)] public string Description = "";

    [BoxGroup("기본 정보"), LabelText("판매 가능")]
    [Tooltip("특수/이야기 장비는 해제하세요. 상품 ItemData의 판매 설정과 함께 검사합니다.")]
    public bool IsSellable = true;

    [BoxGroup("장착 제한"), LabelText("허용 캐릭터 ID")]
    [Tooltip("무기는 전용 캐릭터 ID가 필요합니다. 다른 슬롯은 비우면 공용입니다. 기존 빈 무기는 저장 호환을 위해 작동하지만 제작 검사에서 오류를 표시합니다.")]
    [ValidateInput(nameof(HasValidWeaponOwner), "무기는 전용 캐릭터 ID를 하나 지정하세요. 기존 데이터는 자동 수정하지 않습니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<string> AllowedCharacterIDs = new List<string>();

    [BoxGroup("능력치 보정"), LabelText("추가 수치")]
    [InfoBox("모든 값은 기본 수치에 더하는 보정입니다. 미입력은 0. ATK +5 = 공격력 5 증가. 화염 피해 배율 -0.25 = 기본 1에서 0.75배. 상태 배율 -1 = 해당 상태 면역. 피해 배율에 1을 넣으면 기본 1 + 1 = 2배가 되므로 주의하세요.")]
    public StatBlock StatBonuses = StatBlock.CreateZeroModifier();

    [BoxGroup("조건부 효과"), LabelText("전투 효과"), ListDrawerSettings(ShowIndexLabels = true)]
    [Tooltip("조건을 만족한 실제 전투 이벤트에서만 실행됩니다. 빈 목록은 추가 효과 없음입니다.")]
    public List<EquipmentTriggeredEffect> TriggeredEffects = new List<EquipmentTriggeredEffect>();

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

    [FoldoutGroup("예약 필드 · 아직 실행 안 됨", false), LabelText("패시브 효과 ID")]
    [Tooltip("현재는 저장용 예약 필드입니다. ID만 입력해도 효과가 실행되는 기능은 아직 연결되지 않았습니다.")]
    public string PassiveEffectID = "";

    [FoldoutGroup("예약 필드 · 아직 실행 안 됨"), LabelText("장착 반응 대사 ID")]
    [Tooltip("현재는 저장용 예약 필드입니다. 장착 시 대사 실행은 아직 연결되지 않았습니다.")]
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

    private static bool IsValidId(string id) => !string.IsNullOrWhiteSpace(id) && id == id.Trim();

    public bool HasValidWeaponOwner()
        => Slot != EquipmentSlot.Weapon || (AllowedCharacterIDs != null && AllowedCharacterIDs.Count == 1
            && !string.IsNullOrWhiteSpace(AllowedCharacterIDs[0]));
}
