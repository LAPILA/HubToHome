internal static class ItemCleanseService
{
    public static bool CanCleanse(ItemData item, CharacterBase target)
    {
        if (item == null || target == null) return false;
        for (int i = 0; i < 5; i++)
        {
            var kind = (ElementalStatusKind)i;
            if ((item.CleanseAllHarmful || item.CleanseElement == kind)
                && (target.Elements.GetValue(kind) > 0 || kind == ElementalStatusKind.Chill && target.Elements.IsFrozen)) return true;
        }
        for (int i = 0; i < target.ActiveStatusEffects.Count; i++)
            if (Matches(item, target.ActiveStatusEffects[i].EffectID)) return true;
        return false;
    }

    public static void Apply(ItemData item, CharacterBase target)
    {
        for (int i = target.ActiveStatusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = target.ActiveStatusEffects[i];
            if (Matches(item, effect.EffectID)) target.RemoveEffect(effect);
        }
        if (item.CleanseAllHarmful) target.Elements.Cleanse();
        else target.Elements.Cleanse(item.CleanseElement);
    }

    private static bool Matches(ItemData item, string id)
    {
        if (!item.CleanseAllHarmful)
        {
            switch (item.CleanseElement)
            {
                case ElementalStatusKind.Burn: return id == StatusEffectIds.Burn;
                case ElementalStatusKind.Chill: return id == StatusEffectIds.Freeze || id == StatusEffectIds.Bind;
                case ElementalStatusKind.Bleed: return id == StatusEffectIds.Bleed;
                case ElementalStatusKind.Poison: return id == StatusEffectIds.Poison;
                default: return false;
            }
        }
        // 유익한 Haste / IceShield 및 종류를 모르는 확장 효과는 제거하지 않습니다.
        return id == StatusEffectIds.Burn || id == StatusEffectIds.Freeze || id == StatusEffectIds.Bleed
            || id == StatusEffectIds.Poison || id == StatusEffectIds.Bind || id == StatusEffectIds.Stun
            || id == StatusEffectIds.Berserk || id == StatusEffectIds.Wet;
    }
}
