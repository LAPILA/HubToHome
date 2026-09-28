using System.Collections.Generic;
using UnityEngine;

public abstract class StatusEffect
{
    public string EffectID { get; protected set; }
    public int DurationTurns { get; protected set; }
    public int Stacks { get; protected set; } = 1;
    public const int MaxStacks = 100; 
    
    public bool IsExpired => DurationTurns <= 0;
    
    protected CharacterBase Target; 
    protected GameObject LoopVFXPrefab;
    protected string PivotName;

    public StatusEffect(string id, int duration, int initialStacks = 1, GameObject vfxPrefab = null, string pivot = CharacterPivotId.Bottom)
    {
        EffectID = id;
        DurationTurns = duration;
        Stacks = Mathf.Clamp(initialStacks, 1, MaxStacks);
        LoopVFXPrefab = vfxPrefab;
        PivotName = pivot;
    }

    public virtual void OnApply(CharacterBase target)
    {
        Target = target; 
        if (LoopVFXPrefab != null) Target.AddLoopVFX(EffectID, LoopVFXPrefab, PivotName);
    }

    public virtual void AddStack(int turns, int stackAmount = 1)
    {
        Stacks = Mathf.Clamp(Stacks + stackAmount, 1, MaxStacks);
        DurationTurns = Mathf.Max(DurationTurns, turns); 
    }

    public virtual void AppendStatModifiers(List<StatModifier> modifiers) { }

    public virtual void RefreshFrom(StatusEffect effect) => AddStack(effect.DurationTurns);

    public virtual bool TickAtTurnEnd => false;

    /// <summary>Read-only lifetime projection. Never call OnTick/OnRemove to draw a turn forecast.</summary>
    public virtual bool IsActiveAfterTurnBoundaries(int turnStarts, int turnEnds)
    {
        int ticks = Mathf.Max(0, TickAtTurnEnd ? turnEnds : turnStarts);
        return ticks == 0 || DurationTurns > ticks;
    }

    public virtual void OnTick() { DurationTurns--; }

    public virtual void OnRemove()
    {
        if (LoopVFXPrefab != null && Target != null) Target.RemoveLoopVFX(EffectID);
    }
}

// ── 1. 화상 (Burn) ──
public class BurnEffect : StatusEffect
{
    public BurnEffect(int duration, int stacks = 1) : base(StatusEffectIds.Burn, duration, stacks) {}
    public override void OnTick()
    {
        base.OnTick();
        int damage = Mathf.Max(1, (Target.MaxHP / 100) * Stacks);
        Target.TakePureDamage(damage); 
    }
}

// ── 2. 빙결 (Freeze) ──
public class FreezeEffect : StatusEffect
{
    public FreezeEffect(int duration, int stacks = 1) : base(StatusEffectIds.Freeze, duration, stacks) {}
    
    public override void OnApply(CharacterBase target) { base.OnApply(target); CheckBindTrigger(); }
    public override void AddStack(int turns, int stackAmount = 1) { base.AddStack(turns, stackAmount); CheckBindTrigger(); }

    private void CheckBindTrigger()
    {
        if (Stacks >= 10 && !Target.HasEffect(StatusEffectIds.Bind))
            Target.TryApplyStatusEffect(new BindEffect(DurationTurns));
    }

    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null) return;
        modifiers.Add(StatModifier.ForPrimary(
            StatLayer.Battle,
            StatType.SPD,
            additivePercent: -0.1f * Stacks,
            sourceId: EffectID));
    }
}

// ── 3. 출혈 (Bleed) ──
public class BleedEffect : StatusEffect
{
    public BleedEffect(int duration, int stacks = 1) : base(StatusEffectIds.Bleed, duration, stacks) {}
    public override bool TickAtTurnEnd => true;

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Target.OnActionExecuted += TakeBleedDamage; 
    }

    private void TakeBleedDamage()
    {
        int damage = Mathf.Max(1, (Target.MaxHP / 100) * Stacks);
        Target.TakePureDamage(damage);
    }

    public override void OnRemove()
    {
        if (Target != null) Target.OnActionExecuted -= TakeBleedDamage; 
        base.OnRemove();
    }
}

// ── 4. 독 (Poison) ──
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration, int stacks = 1) : base(StatusEffectIds.Poison, duration, stacks) {}
    public override void OnTick()
    {
        base.OnTick();
        Target.TakePureDamage(5 * Stacks); 
    }
}

// ── 5. 속박 (Bind) ──
public class BindEffect : StatusEffect
{
    public BindEffect(int duration) : base(StatusEffectIds.Bind, duration) {}
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsBound = true; }
    public override void OnRemove() { if (Target != null) Target.IsBound = false; base.OnRemove(); }
}

// ── 6. 기절 (Stun) ──
public class StunEffect : StatusEffect
{
    public StunEffect(int duration) : base(StatusEffectIds.Stun, duration) {}
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsStunned = true; }
    public override void OnRemove() { if (Target != null) Target.IsStunned = false; base.OnRemove(); }
}

// ── 7. 광폭화 (Berserk) ──
public class BerserkEffect : StatusEffect
{
    public BerserkEffect(int duration) : base(StatusEffectIds.Berserk, duration) {}
    
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsBerserk = true; }

    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null) return;
        modifiers.Add(StatModifier.ForPrimary(
            StatLayer.Battle,
            StatType.ATK,
            additivePercent: 1.0f,
            sourceId: EffectID));
        modifiers.Add(StatModifier.ForPrimary(
            StatLayer.Battle,
            StatType.DEF,
            additivePercent: -0.5f,
            sourceId: EffectID));
    }

    public override void OnRemove() { if (Target != null) Target.IsBerserk = false; base.OnRemove(); }
}

/// <summary>중첩하지 않는 가속. 부여 중인 현재 턴을 제외한 대상의 턴 종료에 차감합니다.</summary>
public sealed class HasteEffect : StatusEffect
{
    public const float DefaultSpeedBonus = 0.3f;
    public float SpeedBonus { get; private set; }
    private bool _skipCurrentTurnEnd;

    public HasteEffect(int duration, float speedBonus = DefaultSpeedBonus)
        : base(StatusEffectIds.Haste, duration)
    {
        SpeedBonus = Mathf.Clamp(speedBonus, 0f, 3f);
    }

    public override bool TickAtTurnEnd => true;

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        _skipCurrentTurnEnd = target.IsTakingBattleTurn;
    }

    public override void AddStack(int turns, int stackAmount = 1)
    {
        Stacks = 1;
        DurationTurns = Mathf.Max(DurationTurns, turns);
        _skipCurrentTurnEnd = Target != null && Target.IsTakingBattleTurn;
    }

    public override void RefreshFrom(StatusEffect effect)
    {
        AddStack(effect.DurationTurns);
        // 약한 가속으로 강한 가속을 덮어쓰지 않습니다.
        if (effect is HasteEffect haste) SpeedBonus = Mathf.Max(SpeedBonus, haste.SpeedBonus);
    }

    public override void OnTick()
    {
        if (_skipCurrentTurnEnd) { _skipCurrentTurnEnd = false; return; }
        base.OnTick();
    }

    public override bool IsActiveAfterTurnBoundaries(int turnStarts, int turnEnds)
    {
        int ends = Mathf.Max(0, turnEnds);
        if (ends == 0) return true;
        int ticks = Mathf.Max(0, ends - (_skipCurrentTurnEnd ? 1 : 0));
        return DurationTurns > ticks;
    }

    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        modifiers?.Add(StatModifier.ForPrimary(StatLayer.Battle, StatType.SPD,
            additivePercent: SpeedBonus, sourceId: EffectID));
    }
}

// ── 8. 스탯 커스텀 버프 ──
public class StatModifierEffect : StatusEffect
{
    private StatType _statType;
    private int _flatModifier;
    private float _percentModifier;

    public StatModifierEffect(string id, int duration, StatType statType, int flatMod = 0, float percentMod = 0f, int stacks = 1) 
        : base(id, duration, stacks)
    {
        _statType = statType;
        _flatModifier = flatMod;
        _percentModifier = percentMod;
    }

    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null) return;
        modifiers.Add(StatModifier.ForPrimary(
            StatLayer.Battle,
            _statType,
            _flatModifier * Stacks,
            _percentModifier * Stacks,
            EffectID));
    }
}

// ── 🛡️ 보호막 버프 예시 ──
public class IceShieldEffect : StatusEffect
{
    public IceShieldEffect(int duration) : base(StatusEffectIds.IceShield, duration) {}
    
    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null) return;
        modifiers.Add(StatModifier.ForIncomingDamageMultiplier(
            StatLayer.Battle,
            flatValue: -0.2f,
            sourceId: EffectID));
    }
}

// ── 💦 디버프 예시: 흠뻑 젖음 ──
public class WetEffect : StatusEffect
{
    public WetEffect(int duration) : base(StatusEffectIds.Wet, duration) {}
    
    public override void AppendStatModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null) return;
        modifiers.Add(StatModifier.ForElementResistance(
            StatLayer.Battle,
            DamageElement.Electric,
            flatValue: +0.5f,
            sourceId: EffectID));
    }
}
