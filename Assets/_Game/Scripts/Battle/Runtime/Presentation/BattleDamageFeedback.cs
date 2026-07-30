using UnityEngine;

public enum BattleDamageFeedbackKind
{
    Damage,
    Miss
}

public readonly struct BattleDamageFeedback
{
    public BattleDamageFeedback(
        CharacterBase source,
        CharacterBase target,
        int amount,
        bool isCritical,
        BattleDamageFeedbackKind kind)
    {
        Source = source;
        Target = target;
        Amount = amount;
        IsCritical = isCritical;
        Kind = kind;
    }

    public CharacterBase Source { get; }
    public CharacterBase Target { get; }
    public int Amount { get; }
    public bool IsCritical { get; }
    public BattleDamageFeedbackKind Kind { get; }

    public Color ResolveColor()
    {
        if (Kind == BattleDamageFeedbackKind.Miss)
            return Color.white;

        if (Source is PlayerCharacter sourcePlayer)
            return sourcePlayer.BattleSymbolColor;

        return Target is PlayerCharacter targetPlayer
            ? targetPlayer.BattleSymbolColor
            : Color.white;
    }
}
