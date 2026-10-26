using System;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ElementalItemOperation { None = 0, AddBuildup = 1, ConsumeForDamage = 2 }

/// <summary>기존 아이템 효과에 선택적으로 붙이는 전투 전용 누적 효과입니다.</summary>
[Serializable]
public sealed class ElementalItemPayload
{
    [LabelText("누적 효과")] public ElementalItemOperation Operation;
    [LabelText("상태 종류"), HideIf("@Operation == ElementalItemOperation.None")]
    public ElementalStatusKind Kind;
    [LabelText("부여 / 최대 소비량"), Min(1), HideIf("@Operation == ElementalItemOperation.None")]
    public int Amount = 1;
    [LabelText("소비량당 피해"), Min(1), ShowIf("@Operation == ElementalItemOperation.ConsumeForDamage")]
    [Tooltip("실제로 소비한 누적량 × 이 수치의 상태 피해입니다. 일반 공격 명중이나 추가 상태 발동으로 취급하지 않습니다.")]
    public int DamagePerStack = 1;

    public bool IsConfigured => Operation != ElementalItemOperation.None;
    public bool IsValid => Enum.IsDefined(typeof(ElementalItemOperation), Operation)
        && Enum.IsDefined(typeof(ElementalStatusKind), Kind) && Amount > 0
        && (Operation != ElementalItemOperation.ConsumeForDamage || DamagePerStack > 0);

    public bool CanApply(CharacterBase target)
    {
        if (!IsConfigured || !IsValid || target == null || !target.IsAlive) return false;
        return Operation == ElementalItemOperation.AddBuildup
            ? target.Elements.CanAdd(Kind)
            : target.Elements.GetValue(Kind) > 0;
    }

    public bool TryApply(CharacterBase target)
    {
        if (!CanApply(target)) return false;
        if (Operation == ElementalItemOperation.AddBuildup)
        {
            int previous = target.Elements.GetValue(Kind);
            double previousRemainder = target.Elements.GetBuildupRemainder(Kind);
            bool wasFrozen = target.Elements.IsFrozen;
            target.Elements.Add(Kind, Amount);
            // 저항으로 정수 1 미만만 쌓여도 유효한 적용입니다. 이를 실패/환불하면 무료 누적이 됩니다.
            return previous != target.Elements.GetValue(Kind)
                || previousRemainder != target.Elements.GetBuildupRemainder(Kind)
                || wasFrozen != target.Elements.IsFrozen;
        }
        int consumed = target.Elements.Consume(Kind, Amount);
        if (consumed <= 0) return false;
        int damage = (int)Math.Min(int.MaxValue, (long)consumed * DamagePerStack);
        return target.ApplyStatusDamage(damage, Kind) > 0;
    }
}
