using UnityEngine;

public enum StatusEffectType { None, Burn, Poison, Freeze, Bind, Sleep, SpeedUp }

public abstract class StatusEffect
{
    public string EffectID { get; protected set; }
    public int DurationTurns { get; protected set; }
    public int Stacks { get; protected set; } = 1;
    public bool IsExpired => DurationTurns <= 0;
    
    protected GameObject LoopVFXPrefab;
    protected string PivotName;

    public StatusEffect(string id, int duration, GameObject vfxPrefab = null, string pivot = "Pivots/Bottom")
    {
        EffectID = id;
        DurationTurns = duration;
        LoopVFXPrefab = vfxPrefab;
        PivotName = pivot;
    }

    public virtual void AddStack(int turns)
    {
        Stacks++;
        DurationTurns = Mathf.Max(DurationTurns, turns);
        Debug.Log($"{EffectID} 중첩됨! (현재 {Stacks}스택)");
    }

    public virtual void OnApply(CharacterBase target)
    {
        if (LoopVFXPrefab != null) target.AddLoopVFX(EffectID, LoopVFXPrefab, PivotName);
    }

    public virtual void OnTick(CharacterBase target) { DurationTurns--; }

    public virtual void OnRemove(CharacterBase target)
    {
        if (LoopVFXPrefab != null) target.RemoveLoopVFX(EffectID);
    }
}

// ── 1. 화상 (Burn): 턴마다 HP 지속 데미지 ──
public class BurnEffect : StatusEffect
{
    public BurnEffect(int duration) : base("Burn", duration) {}
    public override void OnTick(CharacterBase target)
    {
        base.OnTick(target);
        int damage = 5 * Stacks; // 1스택당 5데미지
        target.TakePureDamage(damage);
        Debug.Log($"화상 데미지 {damage}! 남은 턴: {DurationTurns}");
    }
}

// ── 2. 중독 (Poison): 턴마다 HP, MP 동시 감소 ──
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration) : base("Poison", duration) {}
    public override void OnTick(CharacterBase target)
    {
        base.OnTick(target);
        int damage = 3 * Stacks;
        int mpDrain = 2 * Stacks;
        target.TakePureDamage(damage);
        target.ConsumeMP(mpDrain);
        Debug.Log($"중독! HP -{damage}, MP -{mpDrain}");
    }
}

// ── 3. 빙결 (Freeze): 걸려있는 동안 SPD 감소 ──
public class FreezeEffect : StatusEffect
{
    private int _spdReduction;
    public FreezeEffect(int duration) : base("Freeze", duration) {}

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        _spdReduction = 10 * Stacks;
        target.SPD -= _spdReduction; // 스피드 강제 감소
        Debug.Log($"빙결! 속도 {_spdReduction} 감소 (현재속도: {target.SPD})");
    }

    public override void AddStack(int turns)
    {
        // 중첩 시 이전 스피드 감소량을 롤백하고 새로 적용해야 버그가 안 생김
        CharacterBase target = null; // TODO: target 참조 필요 (보통 OnApply에서 캐싱)
        base.AddStack(turns);
        // 복잡도를 줄이려면 빙결은 스택 당 속도 감소율을 고정하는게 좋습니다.
    }

    public override void OnRemove(CharacterBase target)
    {
        base.OnRemove(target);
        target.SPD += _spdReduction; // 스피드 원상복구
        Debug.Log("빙결 해제! 속도 원상복구");
    }
}

// ── 4. 속박 (Bind): 회피/점프/Run 불가능, 패링만 가능 ──
public class BindEffect : StatusEffect
{
    public BindEffect(int duration) : base("Bind", duration) {}
    
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        target.IsBound = true; // 🚨 CharacterBase의 플래그 켜기
        Debug.Log("속박됨! 패링 외 행동 불가!");
    }

    public override void OnRemove(CharacterBase target)
    {
        base.OnRemove(target);
        target.IsBound = false; // 플래그 끄기
        Debug.Log("속박 해제!");
    }
}

// ── 5. 수면 (Sleep): 행동 불능 ──
public class SleepEffect : StatusEffect
{
    public SleepEffect(int duration) : base("Sleep", duration) {}
    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        Debug.Log("수면 상태! 행동 불가!");
        // TODO: BattleManager에서 턴 스킵 로직에 target.HasStatus("Sleep") 체크 추가
    }
}

// ── 6. 가속 (SpeedUp): 턴 속도 증가 (버프) ──
public class SpeedUpEffect : StatusEffect
{
    private int _spdBonus = 50;
    public SpeedUpEffect(int duration) : base("SpeedUp", duration) {}

    public override void OnApply(CharacterBase target)
    {
        base.OnApply(target);
        target.SPD += _spdBonus;
        Debug.Log($"아드레날린 폭발! 속도 {_spdBonus} 증가 (현재속도: {target.SPD})");
    }

    public override void OnRemove(CharacterBase target)
    {
        base.OnRemove(target);
        target.SPD -= _spdBonus;
    }
}