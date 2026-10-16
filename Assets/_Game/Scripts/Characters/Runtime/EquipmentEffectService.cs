using System;
using System.Collections.Generic;

/// <summary>전투 소유자가 확정 이벤트당 한 번 호출합니다. 장비별 실제 효과는 데이터로만 정합니다.</summary>
public static class EquipmentEffectService
{
    private static readonly HashSet<PlayerCharacter> Applying = new HashSet<PlayerCharacter>();

    public static void Apply(PlayerCharacter actor, EquipmentEffectTrigger trigger)
    {
        if (actor == null || !actor.IsAlive || !actor.isActiveAndEnabled || !Applying.Add(actor)) return;
        try
        {
            ApplySlot(actor, actor.WeaponSlot, EquipmentSlot.Weapon, trigger);
            ApplySlot(actor, actor.Accessory1Slot, EquipmentSlot.Accessory1, trigger);
            ApplySlot(actor, actor.Accessory2Slot, EquipmentSlot.Accessory2, trigger);
            ApplySlot(actor, actor.HeadSlot, EquipmentSlot.Head, trigger);
            ApplySlot(actor, actor.BodySlot, EquipmentSlot.Body, trigger);
            ApplySlot(actor, actor.ShoesSlot, EquipmentSlot.Shoes, trigger);
        }
        finally { Applying.Remove(actor); }
    }

    private static void ApplySlot(PlayerCharacter actor, EquipmentData equipment, EquipmentSlot slot, EquipmentEffectTrigger trigger)
    {
        if (!EquipmentLoadoutService.CanApply(equipment, slot, actor.CharacterData != null ? actor.CharacterData.CharacterID : null)
            || equipment.TriggeredEffects == null) return;
        for (int i = 0; i < equipment.TriggeredEffects.Count; i++)
        {
            EquipmentTriggeredEffect effect = equipment.TriggeredEffects[i];
            if (effect == null || !effect.IsValid || effect.Trigger != trigger) continue;
            int maximum = effect.Effect == EquipmentEffectKind.HealHP ? actor.MaxHP : actor.MaxAP;
            int current = effect.Effect == EquipmentEffectKind.HealHP ? actor.CurrentHP : actor.CurrentAP;
            int value = effect.Calculation == ValueCalcType.Full ? maximum
                : effect.Calculation == ValueCalcType.Percentage
                    ? (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Round((double)maximum * effect.Amount * .01d)))
                    : effect.Amount;
            value = Math.Min(value, Math.Max(0, maximum - current));
            if (value <= 0) continue;
            if (effect.Effect == EquipmentEffectKind.HealHP) actor.HealHP(value);
            else actor.RestoreAP(value);
        }
    }
}
