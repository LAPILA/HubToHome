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

    public StatusEffect(string id, int duration, int initialStacks = 1, GameObject vfxPrefab = null, string pivot = "Bottom")
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

    // [Step 1] 수치 직접 증가 & 비율 증가
    public virtual int GetFlatModifier(StatType type) => 0;
    public virtual float GetPercentModifier(StatType type) => 0f;

    // [Step 3] 속성 저항력 증감 (예: +0.5f 이면 해당 속성 피해를 50% 더 받음)
    public virtual float GetElementResistanceModifier(DamageElement element) => 0f;

    // [Step 4] 받는/주는 최종 피해 증감 (곱연산)
    public virtual float GetIncomingDamageModifier() => 0f;
    public virtual float GetOutgoingDamageModifier() => 0f;

    public virtual void OnTick() { DurationTurns--; }

    public virtual void OnRemove()
    {
        if (LoopVFXPrefab != null && Target != null) Target.RemoveLoopVFX(EffectID);
    }
}

// ── 1. 화상 (Burn) ──
public class BurnEffect : StatusEffect
{
    public BurnEffect(int duration, int stacks = 1) : base("Burn", duration, stacks) {}
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
    public FreezeEffect(int duration, int stacks = 1) : base("Freeze", duration, stacks) {}
    
    public override void OnApply(CharacterBase target) { base.OnApply(target); CheckBindTrigger(); }
    public override void AddStack(int turns, int stackAmount = 1) { base.AddStack(turns, stackAmount); CheckBindTrigger(); }

    private void CheckBindTrigger()
    {
        if (Stacks >= 10 && !Target.HasEffect("Bind")) Target.AddEffect(new BindEffect(DurationTurns));
    }

    public override float GetPercentModifier(StatType type)
    {
        if (type == StatType.SPD) return -0.1f * Stacks; // 1스택당 속도 10% 깎임
        return 0f;
    }
}

// ── 3. 출혈 (Bleed) ──
public class BleedEffect : StatusEffect
{
    public BleedEffect(int duration, int stacks = 1) : base("Bleed", duration, stacks) {}

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
    public PoisonEffect(int duration, int stacks = 1) : base("Poison", duration, stacks) {}
    public override void OnTick()
    {
        base.OnTick();
        Target.TakePureDamage(5 * Stacks); 
    }
}

// ── 5. 속박 (Bind) ──
public class BindEffect : StatusEffect
{
    public BindEffect(int duration) : base("Bind", duration) {}
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsBound = true; }
    public override void OnRemove() { if (Target != null) Target.IsBound = false; base.OnRemove(); }
}

// ── 6. 기절 (Stun) ──
public class StunEffect : StatusEffect
{
    public StunEffect(int duration) : base("Stun", duration) {}
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsStunned = true; }
    public override void OnRemove() { if (Target != null) Target.IsStunned = false; base.OnRemove(); }
}

// ── 7. 광폭화 (Berserk) ──
public class BerserkEffect : StatusEffect
{
    public BerserkEffect(int duration) : base("Berserk", duration) {}
    
    public override void OnApply(CharacterBase target) { base.OnApply(target); Target.IsBerserk = true; }

    public override float GetPercentModifier(StatType type)
    {
        if (type == StatType.ATK) return 1.0f;  // 공격력 +100%
        if (type == StatType.DEF) return -0.5f; // 방어력 -50%
        return 0f;
    }

    public override void OnRemove() { if (Target != null) Target.IsBerserk = false; base.OnRemove(); }
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

    public override int GetFlatModifier(StatType type) => type == _statType ? _flatModifier * Stacks : 0;
    public override float GetPercentModifier(StatType type) => type == _statType ? _percentModifier * Stacks : 0f;
}

// ── 🛡️ 보호막 버프 예시 ──
public class IceShieldEffect : StatusEffect
{
    public IceShieldEffect(int duration) : base("IceShield", duration) {}
    
    // 받는 최종 피해 20% 깎음 (-0.2f)
    public override float GetIncomingDamageModifier() => -0.2f; 
}

// ── 💦 디버프 예시: 흠뻑 젖음 ──
public class WetEffect : StatusEffect
{
    public WetEffect(int duration) : base("Wet", duration) {}
    
    public override float GetElementResistanceModifier(DamageElement element)
    {
        // 번개 속성 피해를 맞으면 데미지 50% 추가로 더 받음! (+0.5f)
        if (element == DamageElement.Electric) return +0.5f; 
        return 0f;
    }
}