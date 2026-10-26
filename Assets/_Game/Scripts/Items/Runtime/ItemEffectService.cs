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

        if (!IsValidTarget(item, target))
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

        if (item.HasElementalPayload && (!inBattle || !item.ElementalPayload.IsValid))
        {
            error = !inBattle ? "누적 효과 아이템은 전투에서만 사용할 수 있습니다." : "누적 효과의 종류와 수치를 확인하세요.";
            return false;
        }

        if (!System.Enum.IsDefined(typeof(EffectActionType), item.ActionType)
            || item.ActionType == EffectActionType.None && !(item.HasElementalPayload && item.ElementalPayload.CanApply(target)))
        {
            error = "The item has no effect.";
            return false;
        }

        if (item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage || item.ActionType == EffectActionType.Revive)
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
            if (item.ActionType == EffectActionType.Revive && item.TargetStat != TargetStatType.HP)
            {
                error = "부활은 HP 효과만 지원합니다.";
                return false;
            }
            if (item.ActionType == EffectActionType.Heal
                && (item.TargetStat == TargetStatType.HP
                    ? target.CurrentHP >= target.MaxHP
                    : target.CurrentAP >= target.MaxAP))
            {
                error = "이미 최대치여서 회복할 수 없습니다.";
                return false;
            }
            if (item.ActionType == EffectActionType.Damage && item.TargetStat == TargetStatType.AP && target.CurrentAP <= 0)
            {
                error = "감소시킬 AP가 없습니다.";
                return false;
            }
        }

        if (item.ActionType == EffectActionType.Cleanse && !ItemCleanseService.CanCleanse(item, target))
        {
            error = "정화할 상태가 없습니다.";
            return false;
        }

        if (item.ActionType == EffectActionType.ApplyStatus
            && (!StatusEffectFactory.IsKnown(item.StatusEffectID) || item.StatusDurationTurns <= 0))
        {
            error = $"Unknown status effect: {item.StatusEffectID}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryApply(ItemData item, CharacterBase target, bool inBattle, out string error)
        => TryApply(item, target, inBattle, out error, out _);

    /// <summary>일반 HP 변화만 별도로 반환합니다. 누적 소비 피해는 자체 상태 피해 이벤트를 사용합니다.</summary>
    public static bool TryApply(ItemData item, CharacterBase target, bool inBattle, out string error, out int mainHpDelta)
    {
        mainHpDelta = 0;
        if (!CanApply(item, target, inBattle, out error))
            return false;

        int previousHp = target.CurrentHP;
        int previousAp = target.CurrentAP;
        bool mainApplied = item.ActionType != EffectActionType.None;
        int value = CalculateValue(item, target);
        switch (item.ActionType)
        {
            case EffectActionType.Heal:
                if (item.TargetStat == TargetStatType.HP) target.HealHP(Mathf.Min(value, Mathf.Max(0, target.MaxHP - target.CurrentHP)));
                else if (item.TargetStat == TargetStatType.AP) target.RestoreAP(Mathf.Min(value, Mathf.Max(0, target.MaxAP - target.CurrentAP)));
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
                mainApplied = target.TryApplyStatusEffect(effect).Applied;
                break;

            case EffectActionType.Cleanse:
                ItemCleanseService.Apply(item, target);
                break;

            case EffectActionType.Revive:
                target.HealHP(Mathf.Min(Mathf.Max(1, value), target.MaxHP));
                if (inBattle && target is PlayerCharacter player)
                    player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                break;
        }

        mainHpDelta = previousHp - target.CurrentHP;
        if (item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage || item.ActionType == EffectActionType.Revive)
            mainApplied = mainHpDelta != 0 || previousAp != target.CurrentAP;
        bool payloadApplied = item.HasElementalPayload && item.ElementalPayload.TryApply(target);
        if (!mainApplied && !payloadApplied)
        {
            error = "적용할 수 있는 효과가 없습니다.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static int CalculateValue(ItemData item, CharacterBase target)
    {
        int maxValue = item.TargetStat == TargetStatType.AP ? target.MaxAP : target.MaxHP;
        return CalculateValue(item, maxValue);
    }

    internal static int CalculateValue(ItemData item, int maxValue)
    {
        return item.CalcType switch
        {
            ValueCalcType.Flat => Mathf.Max(0, item.EffectValue),
            ValueCalcType.Percentage => (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, System.Math.Round((double)maxValue * item.EffectValue * .01d))),
            ValueCalcType.Full => maxValue,
            _ => 0
        };
    }

    public static bool IsValidTarget(ItemData item, CharacterBase target)
        => item != null && target != null
            && (item.ActionType == EffectActionType.Revive ? !target.IsAlive : target.IsAlive);

    public static bool CanApplyToSavedMember(ItemData item, CharacterSaveData target, out string error)
        => SavedPartyItemEffectService.CanApply(item, target, out error);

    public static bool TryApplyToSavedMember(ItemData item, CharacterSaveData target, out string error)
        => SavedPartyItemEffectService.TryApply(item, target, out error);
}
