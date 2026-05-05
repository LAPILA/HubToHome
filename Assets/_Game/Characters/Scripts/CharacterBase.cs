using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum StatType { MaxHP, MaxMP, ATK, DEF, SPD }
// ── 속성 정의 ──
public enum DamageElement { Physical, Fire, Ice, Electric, Dark, Light, True }

/// <summary>
/// 모든 캐릭터의 최상위 베이스 클래스.
/// 동적 스탯 계산 구조와 방어(Defend) 상태를 지원합니다.
/// </summary>
public abstract class CharacterBase : MonoBehaviour
{
    [Header("Base Stats (순수 능력치)")]
    public int BaseMaxHP = 100;
    public int BaseMaxMP = 100;
    public int BaseATK = 10;
    public int BaseDEF = 5;
    public int BaseSPD = 10;
    
    // ── 🚨 동적 스탯 프로퍼티 (기본값 + 장비 + 상태이상) ──
    public int MaxHP => Mathf.Max(1, BaseMaxHP + GetExtraStat(StatType.MaxHP));
    public int MaxMP => Mathf.Max(0, BaseMaxMP + GetExtraStat(StatType.MaxMP));
    public int ATK   => Mathf.Max(0, BaseATK + GetExtraStat(StatType.ATK));
    public int DEF   => Mathf.Max(0, BaseDEF + GetExtraStat(StatType.DEF));
    public int SPD   => Mathf.Max(0, BaseSPD + GetExtraStat(StatType.SPD));

    // 런타임 상태
    public int CurrentHP { get; protected set; }
    public int CurrentMP { get; protected set; }
    public bool IsAlive => CurrentHP > 0;
    public bool IsBound { get; set; } = false;   // 속박 (회피/점프/도망 불가, 패링만 가능)
    public bool IsStunned { get; set; } = false; // 기절 (턴 스킵, 행동 아예 불가)
    public bool IsBerserk { get; set; } = false; // 광폭화 (아군 피아식별 불가)

    // ── 액션 이벤트 (출혈 등 특정 행동 시 발동하는 효과용) ──
    public event System.Action OnActionExecuted; 
    public void NotifyActionExecuted()
    {
        OnActionExecuted?.Invoke();
    }

    // ── 상태 제약 체크 도구 ──
    public bool CanDodgeOrJump() => !IsBound && !IsStunned;
    public bool CanTakeTurn() => IsAlive && !IsStunned;
    // 델타룬 스타일 방어 (턴 시작 시 해제됨)
    public bool IsDefending { get; set; } = false; 

    // UI 갱신용 이벤트
    public event Action<CharacterBase, int, int> OnHPChanged;
    public event Action<CharacterBase, int, int> OnMPChanged;

    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();

    protected virtual void Awake()
    {
        CurrentHP = BaseMaxHP;
        CurrentMP = BaseMaxMP;
    }

    // ── 스탯 합산 ──────────────────────────────────────────────
    /// <summary>하위 클래스(Player)에서 장비 스탯 등을 더할 수 있도록 virtual 처리</summary>
    protected virtual int GetExtraStat(StatType type)
    {
        // 상태이상(버프/디버프) 합산
        return _activeEffects.Sum(e => e.GetStatModifier(type));
    }

    public Transform GetPivot(string pivotName)
    {
        Transform pivot = transform.Find($"Pivots/{pivotName}");
        return pivot != null ? pivot : transform;
    }

    // ── 데미지 및 회복 ──────────────────────────────────────────
    public virtual int TakeDamage(int rawDamage)
    {
        if (!IsAlive) return 0;

        int actualDamage = Mathf.Max(1, rawDamage - DEF);
        
        // 🚨 방어 중이면 데미지 절반으로 감소 (델타룬 시스템)
        if (IsDefending) actualDamage = Mathf.Max(1, actualDamage / 2);

        CurrentHP = Mathf.Clamp(CurrentHP - actualDamage, 0, MaxHP);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        
        OnDamageTaken(actualDamage);
        if (CurrentHP == 0) OnDie();
        
        return actualDamage;
    }

    public virtual int TakePureDamage(int damage)
    {
        if (!IsAlive) return 0;
        // 고정 데미지는 DEF와 방어를 무시함
        CurrentHP = Mathf.Clamp(CurrentHP - damage, 0, MaxHP);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        
        OnDamageTaken(damage);
        if (CurrentHP == 0) OnDie();
        return damage;
    }

    public virtual void HealHP(int amount) 
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
    }

    public virtual void HealMP(int amount) 
    {
        CurrentMP = Mathf.Min(MaxMP, CurrentMP + amount);
        OnMPChanged?.Invoke(this, CurrentMP, MaxMP);
    }

    public virtual void ConsumeMP(int amount) 
    {
        CurrentMP = Mathf.Max(0, CurrentMP - amount);
        OnMPChanged?.Invoke(this, CurrentMP, MaxMP);
    }

    // ── 상태 이상(Status) 관리 ──────────────────────────────────
    public void AddEffect(StatusEffect effect)
    {
        if (!IsAlive) return;

        var existingEffect = _activeEffects.Find(e => e.EffectID == effect.EffectID);
        if (existingEffect != null)
        {
            existingEffect.AddStack(effect.DurationTurns); 
            return;
        }

        _activeEffects.Add(effect);
        effect.OnApply(this); 
    }

    public void RemoveEffect(StatusEffect effect)
    {
        if (_activeEffects.Remove(effect)) effect.OnRemove(); 
    }

    public bool HasEffect(string effectID) => _activeEffects.Any(e => e.EffectID == effectID);

    public void ProcessEffects()
    {
        if (!IsAlive) return;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].OnTick(); 
            if (_activeEffects[i].IsExpired)
            {
                _activeEffects[i].OnRemove(); 
                _activeEffects.RemoveAt(i);
            }
        }
    }

    // ── VFX 및 이벤트 ──────────────────────────────────────────
    public void AddLoopVFX(string buffId, GameObject vfxPrefab, string pivotName = "Bottom")
    {
        if (_activeLoopVFX.ContainsKey(buffId) || vfxPrefab == null) return;
        Transform pivot = GetPivot(pivotName);
        GameObject vfx = ObjectPoolManager.Instance.Spawn(vfxPrefab, pivot.position, Quaternion.identity);
        vfx.transform.SetParent(pivot); 
        vfx.transform.localPosition = Vector3.zero;
        _activeLoopVFX[buffId] = vfx;
    }

    public void RemoveLoopVFX(string buffId)
    {
        if (_activeLoopVFX.TryGetValue(buffId, out GameObject vfx))
        {
            vfx.transform.SetParent(null); 
            ObjectPoolManager.Instance.Despawn(vfx);
            _activeLoopVFX.Remove(buffId);
        }
    }

    protected virtual void OnDamageTaken(int damage) { }
    protected abstract void OnDie();
}