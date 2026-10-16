using System;
using Sirenix.OdinInspector;
using UnityEngine;

public enum EquipmentEffectTrigger { BattleStart, TurnStart, JustGuard, Dodge, Counter }
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
