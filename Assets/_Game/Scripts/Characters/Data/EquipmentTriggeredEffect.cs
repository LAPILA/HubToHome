using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 저장된 기존 조건의 숫자는 유지하며 새 조건은 뒤에 추가합니다.
public enum EquipmentEffectTrigger { BattleStart = 0, TurnStart = 1, JustGuard = 2, Dodge = 3, Counter = 4, ItemUsed = 5 }
public enum EquipmentEffectKind { HealHP, RestoreAP }

[Serializable]
public sealed class EquipmentTriggeredEffect
{
    [LabelText("발동 조건")] public EquipmentEffectTrigger Trigger;
    [LabelText("효과")] public EquipmentEffectKind Effect;
    [LabelText("계산 방식")] public ValueCalcType Calculation;
    [LabelText("수치"), Min(0), HideIf("@Calculation == ValueCalcType.Full")]
    public int Amount;

    public bool IsValid => Enum.IsDefined(typeof(EquipmentEffectTrigger), Trigger)
        && Enum.IsDefined(typeof(EquipmentEffectKind), Effect)
        && Enum.IsDefined(typeof(ValueCalcType), Calculation) && Amount >= 0;
}
