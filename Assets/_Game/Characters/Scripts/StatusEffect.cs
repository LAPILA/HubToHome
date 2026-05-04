using UnityEngine;

public enum StatusEffectType { None, Burn, Poison, Freeze, Bind, Sleep, SpeedUp }

/// <summary>
/// 상태 이상 베이스 클래스.
/// 객체지향 원칙에 따라 자신이 누구에게(Target) 적용되었는지 스스로 기억(Caching)합니다.
/// </summary>
public abstract class StatusEffect
{
    public string EffectID { get; protected set; }
    public int DurationTurns { get; protected set; }
    public int Stacks { get; protected set; } = 1;
    public bool IsExpired => DurationTurns <= 0;
    
    protected CharacterBase Target; // 🚨 핵심: 자신이 걸려있는 대상을 기억합니다.
    protected GameObject LoopVFXPrefab;
    protected string PivotName;

    public StatusEffect(string id, int duration, GameObject vfxPrefab = null, string pivot = "Pivots/Bottom")
    {
        EffectID = id;
        DurationTurns = duration;
        LoopVFXPrefab = vfxPrefab;
        PivotName = pivot;
    }

    public virtual void OnApply(CharacterBase target)
    {
        Target = target; // 대상 캐싱
        if (LoopVFXPrefab != null) Target.AddLoopVFX(EffectID, LoopVFXPrefab, PivotName);
    }

    public virtual void AddStack(int turns)
    {
        Stacks++;
        DurationTurns = Mathf.Max(DurationTurns, turns);
        Debug.Log($"<color=orange>[Status]</color> {EffectID} 중첩됨! (현재 {Stacks}스택)");
    }

    public virtual void OnTick() 
    { 
        DurationTurns--; 
    }

    public virtual void OnRemove()
    {
        if (LoopVFXPrefab != null && Target != null) Target.RemoveLoopVFX(EffectID);
    }
}

// ── 1. 화상 (Burn) ──
public class BurnEffect : StatusEffect
{
    public BurnEffect(int duration) : base("Burn", duration) {}
    public override void OnTick()
    {
        base.OnTick();
        int damage = 5 * Stacks; 
        Target.TakePureDamage(damage);
        Debug.Log($"<color=red>화상 데미지 {damage}!</color> 남은 턴: {DurationTurns}");
    }
}

// ── 2. 중독 (Poison) ──
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration) : base("Poison", duration) {}
    public override void OnTick()
    {
        base.OnTick();
        int damage = 3 * Stacks;
        int mpDrain = 2 * Stacks;
        Target.TakePureDamage(damage);
        Target.ConsumeMP(mpDrain);
    }
}

// ── 3. 빙결 (Freeze): 안전한 스탯 롤백 아키텍처 ──
public class FreezeEffect : StatusEffect
{
    private int _spdReduction;
    public FreezeEffect(int duration) : base("Freeze", duration) {}

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        _spdReduction = 10 * Stacks;
        Target.SPD -= _spdReduction; 
    }

    public override void AddStack(int turns)
    {
        // 🚨 롤백 -> 계산 -> 재적용 패턴 (버그 원천 차단)
        if (Target != null) Target.SPD += _spdReduction; 
        
        base.AddStack(turns);
        
        _spdReduction = 10 * Stacks;
        if (Target != null) Target.SPD -= _spdReduction;
    }

    public override void OnRemove()
    {
        if (Target != null) Target.SPD += _spdReduction; // 원상복구
        base.OnRemove();
    }
}

// ── 4. 속박 (Bind) ──
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

// ── 5. 수면 (Sleep) ──
public class SleepEffect : StatusEffect
{
    public SleepEffect(int duration) : base("Sleep", duration) {}
    // (BattleManager의 AdvanceTurn 등에서 target.HasEffect("Sleep") 검사 필요)
}

// ── 6. 가속 (SpeedUp) ──
public class SpeedUpEffect : StatusEffect
{
    private int _spdBonus;
    public SpeedUpEffect(int duration) : base("SpeedUp", duration) {}

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        _spdBonus = 50 * Stacks;
        Target.SPD += _spdBonus;
    }

    public override void AddStack(int turns)
    {
        if (Target != null) Target.SPD -= _spdBonus;
        base.AddStack(turns);
        _spdBonus = 50 * Stacks;
        if (Target != null) Target.SPD += _spdBonus;
    }

    public override void OnRemove()
    {
        if (Target != null) Target.SPD -= _spdBonus;
        base.OnRemove();
    }
}