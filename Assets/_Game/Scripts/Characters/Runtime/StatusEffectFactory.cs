using System;
using System.Collections.Generic;

public static class StatusEffectIds
{
    public const string Burn = "Burn";
    public const string Freeze = "Freeze";
    public const string Bleed = "Bleed";
    public const string Poison = "Poison";
    public const string Bind = "Bind";
    public const string Stun = "Stun";
    public const string Berserk = "Berserk";
    public const string IceShield = "IceShield";
    public const string Wet = "Wet";
}

public static class StatusEffectFactory
{
    private static readonly string[] SupportedIds =
    {
        StatusEffectIds.Burn,
        StatusEffectIds.Freeze,
        StatusEffectIds.Bleed,
        StatusEffectIds.Poison,
        StatusEffectIds.Bind,
        StatusEffectIds.Stun,
        StatusEffectIds.Berserk,
        StatusEffectIds.IceShield,
        StatusEffectIds.Wet
    };

    public static IReadOnlyList<string> KnownIds => SupportedIds;

    public static bool IsKnown(string effectId)
    {
        if (string.IsNullOrEmpty(effectId))
        {
            return false;
        }

        for (int i = 0; i < SupportedIds.Length; i++)
        {
            if (string.Equals(SupportedIds[i], effectId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryCreate(string effectId, int durationTurns, out StatusEffect effect)
    {
        int duration = Math.Max(0, durationTurns);
        effect = effectId switch
        {
            StatusEffectIds.Burn => new BurnEffect(duration),
            StatusEffectIds.Freeze => new FreezeEffect(duration),
            StatusEffectIds.Bleed => new BleedEffect(duration),
            StatusEffectIds.Poison => new PoisonEffect(duration),
            StatusEffectIds.Bind => new BindEffect(duration),
            StatusEffectIds.Stun => new StunEffect(duration),
            StatusEffectIds.Berserk => new BerserkEffect(duration),
            StatusEffectIds.IceShield => new IceShieldEffect(duration),
            StatusEffectIds.Wet => new WetEffect(duration),
            _ => null
        };

        return effect != null;
    }
}

