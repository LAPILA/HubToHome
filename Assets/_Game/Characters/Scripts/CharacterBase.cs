using System.Collections.Generic;
using UnityEngine;

public abstract class CharacterBase : MonoBehaviour
{
    [Header("Base Stats")]
    public int MaxHP  = 100;
    public int MaxMP  = 100;
    public int ATK    = 10;
    public int DEF    = 5;
    public int SPD    = 10;

    public int CurrentHP { get; protected set; }
    public int CurrentMP { get; protected set; }
    public bool IsBound { get; set; } = false;

    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();

    protected virtual void Awake()
    {
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }

    // ── 데미지 & 회복 ──────────────────────────────────────────
    public virtual int TakeDamage(int rawDamage)
    {
        int actualDamage = Mathf.Max(1, rawDamage - DEF);
        CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
        OnDamageTaken(actualDamage);
        if (CurrentHP <= 0) OnDie();
        return actualDamage;
    }

    public virtual int TakePureDamage(int damage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
        OnDamageTaken(damage);
        if (CurrentHP <= 0) OnDie();
        return damage;
    }

    public virtual void HealHP(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    public virtual void HealMP(int amount)
    {
        CurrentMP = Mathf.Min(MaxMP, CurrentMP + amount);
    }

    public virtual void ConsumeMP(int amount)
    {
        CurrentMP = Mathf.Max(0, CurrentMP - amount);
    }

    // ── 상태 이상 시스템 ───────────────────────────────────────
    public void AddEffect(StatusEffect effect)
    {
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
        if (_activeEffects.Contains(effect))
        {
            effect.OnRemove(this); 
            _activeEffects.Remove(effect);
        }
    }

    public void ProcessEffects()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].OnTick(this); 
            
            if (_activeEffects[i].IsExpired)
            {
                _activeEffects[i].OnRemove(this);
                _activeEffects.RemoveAt(i);
            }
        }
    }

    // ── 루핑 VFX 관리 (기존과 동일) ──────────────────────────
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


    protected virtual void OnDamageTaken(int damage) { }
    protected abstract void OnDie();

    public bool IsAlive => CurrentHP > 0;
    public bool HasSpeedAdvantageOver(CharacterBase other) => (SPD - other.SPD) >= 20;
}