using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 캐릭터의 최상위 베이스 클래스.
/// 전투 스탯, 데미지 처리, 상태이상(StatusEffect)을 독립적으로 관리합니다.
/// </summary>
public abstract class CharacterBase : MonoBehaviour
{
    [Header("Base Stats")]
    public int MaxHP = 100;
    public int MaxMP = 100;
    public int ATK = 10;
    public int DEF = 5;
    public int SPD = 10;

    // 프로퍼티를 통한 안전한 데이터 접근
    public int CurrentHP { get; protected set; }
    public int CurrentMP { get; protected set; }
    public bool IsBound { get; set; } = false;
    public bool IsAlive => CurrentHP > 0;

    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();

    protected virtual void Awake()
    {
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }
    // ── 피벗(Pivot) 관리 ──────────────────────────────────────────
    /// <summary>
    /// 하이라키의 "Pivots/이름" 경로에서 오브젝트를 찾습니다. 
    /// </summary>
    public Transform GetPivot(string pivotName)
    {
        Transform pivot = transform.Find($"Pivots/{pivotName}");
        return pivot != null ? pivot : transform;
    }

    // ── 데미지 및 회복 (캡슐화) ──────────────────────────────────────────
    public virtual int TakeDamage(int rawDamage)
    {
        if (!IsAlive) return 0;

        int actualDamage = Mathf.Max(1, rawDamage - DEF);
        CurrentHP = Mathf.Clamp(CurrentHP - actualDamage, 0, MaxHP);
        
        OnDamageTaken(actualDamage);
        if (CurrentHP == 0) OnDie();
        
        return actualDamage;
    }

    public virtual int TakePureDamage(int damage)
    {
        if (!IsAlive) return 0;

        CurrentHP = Mathf.Clamp(CurrentHP - damage, 0, MaxHP);
        OnDamageTaken(damage);
        
        if (CurrentHP == 0) OnDie();
        return damage;
    }

    public virtual void HealHP(int amount) => CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    public virtual void HealMP(int amount) => CurrentMP = Mathf.Min(MaxMP, CurrentMP + amount);
    public virtual void ConsumeMP(int amount) => CurrentMP = Mathf.Max(0, CurrentMP - amount);

    // ── 상태 이상(Status) 관리 ────────────────────────────────────────
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
        
        // 🚨 OnApply는 최초 적용 시 대상(Target)을 기억해야 하므로 'this'를 넘깁니다.
        effect.OnApply(this); 
    }

    public void RemoveEffect(StatusEffect effect)
    {
        if (_activeEffects.Remove(effect))
        {
            // 🚨 수정됨: StatusEffect가 이미 대상을 알고 있으므로 인자 없이 스스로 해제합니다.
            effect.OnRemove(); 
        }
    }

    public void ProcessEffects()
    {
        if (!IsAlive) return;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            // 🚨 수정됨: 스스로 틱(Tick)을 처리합니다.
            _activeEffects[i].OnTick(); 
            
            if (_activeEffects[i].IsExpired)
            {
                // 🚨 수정됨: 스스로 해제 로직을 수행합니다.
                _activeEffects[i].OnRemove(); 
                _activeEffects.RemoveAt(i);
            }
        }
    }

    // ── VFX 관리 ──────────────────────────────────────────
    public void AddLoopVFX(string buffId, GameObject vfxPrefab, string pivotName = "Pivots/Bottom")
    {
        if (_activeLoopVFX.ContainsKey(buffId) || vfxPrefab == null) return;

        Transform pivot = transform.Find(pivotName) ?? transform;
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

    // ── 추상/가상 이벤트 ──────────────────────────────────────
    protected virtual void OnDamageTaken(int damage) { }
    protected abstract void OnDie();
    public bool HasSpeedAdvantageOver(CharacterBase other) => (SPD - other.SPD) >= 20;
}