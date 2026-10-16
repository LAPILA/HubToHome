using UnityEngine;

public enum BattleDamageFeedbackKind
{
    Damage,
    Miss
}

// 피해 속성과 누적 상태를 동일한 표시 줄로 정규화합니다. 기존 직렬화 enum은 유지합니다.
public enum BattleDamageType { Physical, Fire, Ice, Electric, Poison, Bleed }

public static class BattleDamageTypeUtility
{
    public static BattleDamageType FromElement(DamageElement element) => element switch
    {
        DamageElement.Fire => BattleDamageType.Fire,
        DamageElement.Ice => BattleDamageType.Ice,
        DamageElement.Electric => BattleDamageType.Electric,
        DamageElement.Corrosion => BattleDamageType.Poison,
        _ => BattleDamageType.Physical
    };

    public static BattleDamageType FromStatus(ElementalStatusKind kind) => kind switch
    {
        ElementalStatusKind.Burn => BattleDamageType.Fire,
        ElementalStatusKind.Chill => BattleDamageType.Ice,
        ElementalStatusKind.Electric => BattleDamageType.Electric,
        ElementalStatusKind.Poison => BattleDamageType.Poison,
        ElementalStatusKind.Bleed => BattleDamageType.Bleed,
        _ => BattleDamageType.Physical
    };

    public static Color ColorFor(BattleDamageType type) => type switch
    {
        BattleDamageType.Fire => ElementalStatusPresentation.GetColor(ElementalStatusKind.Burn),
        BattleDamageType.Ice => ElementalStatusPresentation.GetColor(ElementalStatusKind.Chill),
        BattleDamageType.Electric => ElementalStatusPresentation.GetColor(ElementalStatusKind.Electric),
        BattleDamageType.Poison => ElementalStatusPresentation.GetColor(ElementalStatusKind.Poison),
        BattleDamageType.Bleed => ElementalStatusPresentation.GetColor(ElementalStatusKind.Bleed),
        _ => Color.white
    };
}

public readonly struct BattleDamageFeedback
{
    public BattleDamageFeedback(
        CharacterBase source,
        CharacterBase target,
        int amount,
        bool isCritical,
        BattleDamageFeedbackKind kind,
        DamageElement element = DamageElement.Physical,
        long batchId = 0)
    {
        Source = source;
        Target = target;
        Amount = amount;
        IsCritical = isCritical;
        Kind = kind;
        Element = element;
        BatchId = batchId;
    }

    public CharacterBase Source { get; }
    public CharacterBase Target { get; }
    public int Amount { get; }
    public bool IsCritical { get; }
    public BattleDamageFeedbackKind Kind { get; }
    public DamageElement Element { get; }
    public long BatchId { get; }

    public Color ResolveColor()
    {
        if (Kind == BattleDamageFeedbackKind.Miss)
            return Color.white;

        return BattleDamageTypeUtility.ColorFor(BattleDamageTypeUtility.FromElement(Element));
    }
}
