using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 캐릭터(플레이어/적)의 공통 베이스 추상 클래스.
/// HP, ATK, DEF, SPD 스탯과 데미지 계산, 상태 이상 처리를 담당합니다.
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

    // ── 상태 이상 ─────────────────────────────────────────────
    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();

    // ── 초기화 ────────────────────────────────────────────────
    protected virtual void Awake()
    {
        CurrentHP = MaxHP;
    }

    // ── 데미지 수신 ───────────────────────────────────────────
    /// <summary>
    /// 데미지를 받습니다. DEF 감소 후 HP에 반영합니다.
    /// </summary>
    /// <returns>실제 적용된 데미지</returns>
    public virtual int TakeDamage(int rawDamage)
    {
        int actualDamage = Mathf.Max(1, rawDamage - DEF);
        CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
        OnDamageTaken(actualDamage);

        if (CurrentHP <= 0)
            OnDie();

        return actualDamage;
    }

    /// <summary>DEF를 무시하는 순수 데미지 (독, 화상 등)</summary>
    public virtual int TakePureDamage(int damage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
        OnDamageTaken(damage);

        if (CurrentHP <= 0)
            OnDie();

        return damage;
    }

    // ── 회복 ──────────────────────────────────────────────────
    public virtual void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    // ── 상태 이상 ─────────────────────────────────────────────
    public void AddEffect(StatusEffect effect)
    {
        _activeEffects.Add(effect);
    }

    public void RemoveEffect(StatusEffect effect)
    {
        _activeEffects.Remove(effect);
    }

    /// <summary>턴 시작 시 상태 이상 틱 처리</summary>
    public void ProcessEffects()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].OnTick(this);
            if (_activeEffects[i].IsExpired)
                _activeEffects.RemoveAt(i);
        }
    }

    // ── 추상/가상 이벤트 ──────────────────────────────────────
    protected virtual void OnDamageTaken(int damage) { }
    protected abstract void OnDie();

    // ── 유틸리티 ──────────────────────────────────────────────
    public bool IsAlive => CurrentHP > 0;

    /// <summary>
    /// Speed Gap Logic: 대상과 SPD 차이가 20 이상이면 추가 행동권 여부 반환.
    /// </summary>
    public bool HasSpeedAdvantageOver(CharacterBase other)
        => (SPD - other.SPD) >= 20;
}
