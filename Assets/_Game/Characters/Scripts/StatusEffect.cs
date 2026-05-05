using UnityEngine;

public abstract class StatusEffect
{
    public string EffectID { get; protected set; }
    public int DurationTurns { get; protected set; }
    public int Stacks { get; protected set; } = 1;
    public const int MaxStacks = 100; // 🚨 최대 100스택 제한
    
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
        DurationTurns = Mathf.Max(DurationTurns, turns); // 지속시간 갱신
    }

    public virtual int GetStatModifier(StatType type) => 0;

    public virtual void OnTick() { DurationTurns--; }

    public virtual void OnRemove()
    {
        if (LoopVFXPrefab != null && Target != null) Target.RemoveLoopVFX(EffectID);
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 1. 화상 (Burn) : 턴 종료 시 (스택 * 최대체력의 1/100) 데미지
// ═══════════════════════════════════════════════════════════════
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

// ═══════════════════════════════════════════════════════════════
// ── 2. 빙결 (Freeze) : SPD 감소, 10스택 도달 시 '속박' 부여
// ═══════════════════════════════════════════════════════════════
public class FreezeEffect : StatusEffect
{
    public FreezeEffect(int duration, int stacks = 1) : base("Freeze", duration, stacks) {}
    
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        CheckBindTrigger();
    }

    public override void AddStack(int turns, int stackAmount = 1)
    {
        base.AddStack(turns, stackAmount);
        CheckBindTrigger();
    }

    private void CheckBindTrigger()
    {
        // 🚨 10스택 이상 쌓이면 빙결 지속시간만큼 속박(Bind) 부여!
        if (Stacks >= 10 && !Target.HasEffect("Bind"))
        {
            Target.AddEffect(new BindEffect(DurationTurns));
            Debug.Log($"<color=cyan>[Freeze]</color> {Target.name}의 빙결이 10스택에 도달하여 속박되었습니다!");
        }
    }

    public override int GetStatModifier(StatType type)
    {
        if (type == StatType.SPD) return -2 * Stacks; // 스택당 SPD 2씩 감소
        return 0;
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 3. 출혈 (Bleed) : 공격/스킬 사용 시 최대체력 기반 데미지
// ═══════════════════════════════════════════════════════════════
public class BleedEffect : StatusEffect
{
    public BleedEffect(int duration, int stacks = 1) : base("Bleed", duration, stacks) {}

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Target.OnActionExecuted += TakeBleedDamage; // 행동 이벤트 구독
    }

    private void TakeBleedDamage()
    {
        int damage = Mathf.Max(1, (Target.MaxHP / 100) * Stacks);
        Target.TakePureDamage(damage);
        Debug.Log($"<color=red>[Bleed]</color> {Target.name}이(가) 움직여서 출혈 데미지 {damage} 발생!");
    }

    public override void OnRemove()
    {
        if (Target != null) Target.OnActionExecuted -= TakeBleedDamage; // 구독 해제
        base.OnRemove();
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 4. 독 (Poison) : 고정 수치 데미지 (스택당 5)
// ═══════════════════════════════════════════════════════════════
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration, int stacks = 1) : base("Poison", duration, stacks) {}
    public override void OnTick()
    {
        base.OnTick();
        Target.TakePureDamage(5 * Stacks); 
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 5. 속박 (Bind) : 회피/점프/도망 불가
// ═══════════════════════════════════════════════════════════════
public class BindEffect : StatusEffect
{
    public BindEffect(int duration) : base("Bind", duration) {}
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Target.IsBound = true;
    }
    public override void OnRemove()
    {
        if (Target != null) Target.IsBound = false;
        base.OnRemove();
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 6. 기절 (Stun) : 턴 스킵, 행동 불가
// ═══════════════════════════════════════════════════════════════
public class StunEffect : StatusEffect
{
    public StunEffect(int duration) : base("Stun", duration) {}
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Target.IsStunned = true;
    }
    public override void OnRemove()
    {
        if (Target != null) Target.IsStunned = false;
        base.OnRemove();
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 7. 광폭화 (Berserk) : 공격력 2배, 방어력 하락, 피아식별 불가
// ═══════════════════════════════════════════════════════════════
public class BerserkEffect : StatusEffect
{
    public BerserkEffect(int duration) : base("Berserk", duration) {}
    
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Target.IsBerserk = true;
    }

    public override int GetStatModifier(StatType type)
    {
        // ATK 100% 상승(2배), DEF 50% 하락 효과
        if (type == StatType.ATK) return Target.BaseATK; 
        if (type == StatType.DEF) return -(Target.BaseDEF / 2);
        return 0;
    }

    public override void OnRemove()
    {
        if (Target != null) Target.IsBerserk = false;
        base.OnRemove();
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 8. 스탯 단순 증감 버프/디버프 (전체 약화, 공격력 감소 등)
// ═══════════════════════════════════════════════════════════════
public class StatModifierEffect : StatusEffect
{
    private StatType _statType;
    private int _modifierPerStack;

    public StatModifierEffect(string id, int duration, StatType statType, int modifierPerStack, int stacks = 1) 
        : base(id, duration, stacks)
    {
        _statType = statType;
        _modifierPerStack = modifierPerStack;
    }

    public override int GetStatModifier(StatType type)
    {
        if (type == _statType) return _modifierPerStack * Stacks;
        return 0;
    }
}