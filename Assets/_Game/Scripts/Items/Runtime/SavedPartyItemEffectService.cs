using System;

/// <summary>필드에 생성되지 않은 동료도 저장 파티의 HP/AP를 대상으로 아이템을 사용합니다.</summary>
internal static class SavedPartyItemEffectService
{
    public static bool CanApply(ItemData item, CharacterSaveData target, out string error)
    {
        if (item == null || target == null || item.Type != ItemType.Consumable || !item.UsableInOverworld)
            return Fail("필드에서 사용할 수 없는 아이템입니다.", out error);
        if (item.TargetType == TargetAreaType.EnemyOnly)
            return Fail("아군에게 사용할 수 없습니다.", out error);
        if (item.ActionType != EffectActionType.Heal && item.ActionType != EffectActionType.Damage && item.ActionType != EffectActionType.Revive)
            return Fail("전투 상태는 필드에 남지 않습니다.", out error);
        bool dead = target.HP <= 0;
        if (item.ActionType == EffectActionType.Revive ? !dead : dead)
            return Fail(item.ActionType == EffectActionType.Revive ? "전투 불능 동료에게 사용하세요." : "전투 불능 동료에게 사용할 수 없습니다.", out error);
        if (item.TargetStat != TargetStatType.HP && item.TargetStat != TargetStatType.AP
            || item.ActionType == EffectActionType.Revive && item.TargetStat != TargetStatType.HP)
            return Fail("효과 대상 수치를 확인하세요. 부활은 HP만 지원합니다.", out error);

        CharacterStatsProjectionService.ResolveResourceCaps(target, CharacterDatabase.FindById(target.CharacterDataID), out int maxHp, out int maxAp);
        int maximum = item.TargetStat == TargetStatType.AP ? maxAp : maxHp;
        int current = item.TargetStat == TargetStatType.AP ? target.AP : target.HP;
        if (ItemEffectService.CalculateValue(item, maximum) <= 0)
            return Fail("효과 수치는 0보다 커야 합니다.", out error);
        if (item.ActionType == EffectActionType.Heal && current >= maximum)
            return Fail("이미 모두 회복되어 있습니다.", out error);
        if (item.ActionType == EffectActionType.Damage && current <= 0)
            return Fail("감소시킬 수치가 없습니다.", out error);
        error = string.Empty;
        return true;
    }

    public static bool TryApply(ItemData item, CharacterSaveData target, out string error)
    {
        if (!CanApply(item, target, out error)) return false;
        CharacterStatsProjectionService.ResolveResourceCaps(target, CharacterDatabase.FindById(target.CharacterDataID), out int maxHp, out int maxAp);
        int maximum = item.TargetStat == TargetStatType.AP ? maxAp : maxHp;
        int current = item.TargetStat == TargetStatType.AP ? target.AP : target.HP;
        int value = ItemEffectService.CalculateValue(item, maximum);
        int next = item.ActionType == EffectActionType.Damage
            ? Math.Max(0, current - value) : (int)Math.Min(maximum, (long)current + value);
        if (item.ActionType == EffectActionType.Revive) next = Math.Max(1, Math.Min(maxHp, value));
        if (item.TargetStat == TargetStatType.AP) target.AP = next;
        else target.HP = next;
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error) { error = message; return false; }
}
