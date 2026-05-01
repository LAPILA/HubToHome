using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 캐릭터(플레이어/적)의 공통 베이스 추상 클래스.
/// HP, ATK, DEF, SPD 스탯과 데미지 계산, 상태 이상 및 루핑 VFX 처리를 담당합니다.
/// </summary>
public abstract class CharacterBase : MonoBehaviour
{
    // ── 스탯 ──────────────────────────────────────────────────
    [Header("Base Stats")]
    public int MaxHP  = 100;
    public int ATK    = 10;
    public int DEF    = 5;
    public int SPD    = 10;

    public int CurrentHP { get; protected set; }

    // ── 상태 이상 및 VFX ─────────────────────────────────────
    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    
    // 버프/디버프용 루핑 이펙트 저장소 (Key: Effect ID)
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();

    // ── 초기화 ────────────────────────────────────────────────
    protected virtual void Awake()
    {
        CurrentHP = MaxHP;
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

    public virtual void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    // ── 상태 이상 시스템 ───────────────────────────────────────
    
    /// <summary>상태 이상을 부여하고 효과(VFX, 스탯 변경)를 적용합니다.</summary>
    public void AddEffect(StatusEffect effect)
    {
        // 이미 같은 종류의 효과가 있다면 지속시간 갱신 (선택 사항)
        var existingEffect = _activeEffects.Find(e => e.EffectID == effect.EffectID);
        if (existingEffect != null)
        {
            existingEffect.RefreshDuration(effect.DurationTurns);
            return;
        }

        _activeEffects.Add(effect);
        effect.OnApply(this); // 🚨 적용될 때 스탯 변동 및 VFX 켜기
    }

    /// <summary>상태 이상을 강제로 해제하고 효과를 원상복구합니다.</summary>
    public void RemoveEffect(StatusEffect effect)
    {
        if (_activeEffects.Contains(effect))
        {
            effect.OnRemove(this); // 🚨 해제될 때 스탯 복구 및 VFX 끄기
            _activeEffects.Remove(effect);
        }
    }

    /// <summary>턴 시작 시 상태 이상 틱 처리</summary>
    public void ProcessEffects()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].OnTick(this); // 독 데미지 등 틱 효과 발생
            
            if (_activeEffects[i].IsExpired)
            {
                _activeEffects[i].OnRemove(this);
                _activeEffects.RemoveAt(i);
            }
        }
    }

    // ── 루핑 VFX 관리 ─────────────────────────────────────────
    
    public void AddLoopVFX(string buffId, GameObject vfxPrefab, string pivotName = "Pivots/Bottom")
    {
        if (_activeLoopVFX.ContainsKey(buffId) || vfxPrefab == null) return;

        Transform pivot = transform.Find(pivotName) ?? transform;
        
        // 풀에서 꺼내어 피벗 자식으로 붙이기 (캐릭터를 따라다니게 됨)
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

    public bool IsAlive => CurrentHP > 0;
    public bool HasSpeedAdvantageOver(CharacterBase other) => (SPD - other.SPD) >= 20;
}