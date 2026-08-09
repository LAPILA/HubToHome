using UnityEngine;

/// <summary>
/// 아이템 효과 검증과 적용을 한곳에서 처리합니다. 소비 여부는 호출자가 결정합니다.
/// </summary>
public static class ItemEffectService
{
    public static bool CanApply(ItemData item, CharacterBase target, bool inBattle, out string error)
    {
        if (item == null)
        {
            error = "ItemData is missing.";
            return false;
        }

        if (target == null || !target.IsAlive)
        {
            error = "The target is not available.";
            return false;
        }

        if (item.Type != ItemType.Consumable)
        {
            error = "The item is not consumable.";
            return false;
        }

        if (inBattle ? !item.UsableInBattle : !item.UsableInOverworld)
        {
            error = inBattle ? "The item cannot be used in battle." : "The item cannot be used in the overworld.";
            return false;
        }

        if (item.ActionType == EffectActionType.None)
        {
            error = "The item has no effect.";
            return false;
        }

        if (item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage)
        {
            if (item.TargetStat != TargetStatType.HP && item.TargetStat != TargetStatType.AP)
            {
                error = "A heal or damage item must target HP or AP.";
                return false;
            }

            if (CalculateValue(item, target) <= 0)
            {
                error = "The item effect value must be greater than zero.";
                return false;
            }
        }

        if (item.ActionType == EffectActionType.ApplyStatus
            && !StatusEffectFactory.IsKnown(item.StatusEffectID))
        {
            error = $"Unknown status effect: {item.StatusEffectID}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryApply(ItemData item, CharacterBase target, bool inBattle, out string error)
    {
        if (!CanApply(item, target, inBattle, out error))
            return false;

        int value = CalculateValue(item, target);
        switch (item.ActionType)
        {
            case EffectActionType.Heal:
                if (item.TargetStat == TargetStatType.HP) target.HealHP(value);
                else if (item.TargetStat == TargetStatType.AP) target.RestoreAP(value);
                else
                {
                    error = "A heal item must target HP or AP.";
                    return false;
                }
                break;

            case EffectActionType.Damage:
                if (item.TargetStat == TargetStatType.HP) target.TakePureDamage(value);
                else target.ConsumeAP(value);
                break;

            case EffectActionType.ApplyStatus:
                if (!StatusEffectFactory.TryCreate(item.StatusEffectID, item.StatusDurationTurns, out StatusEffect effect))
                {
                    error = $"Unknown status effect: {item.StatusEffectID}";
                    return false;
                }
                target.TryApplyStatusEffect(effect);
                break;
        }

        error = string.Empty;
        return true;
    }

    private static int CalculateValue(ItemData item, CharacterBase target)
    {
        int maxValue = item.TargetStat == TargetStatType.AP ? target.MaxAP : target.MaxHP;
        return item.CalcType switch
        {
            ValueCalcType.Flat => Mathf.Max(0, item.EffectValue),
            ValueCalcType.Percentage => Mathf.Max(0, Mathf.RoundToInt(maxValue * item.EffectValue * 0.01f)),
            ValueCalcType.Full => maxValue,
            _ => 0
        };
    }
}
