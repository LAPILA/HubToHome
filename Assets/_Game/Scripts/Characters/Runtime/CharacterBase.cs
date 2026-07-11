using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StatType { MaxHP, MaxMP, ATK, DEF, SPD }
public enum DamageElement { Physical, Fire, Ice, Electric, Dark, Light, True }

public abstract class CharacterBase : MonoBehaviour
{
    [Header("Base Stats (순수 능력치)")]
    public int BaseMaxHP = 100;
    public int BaseMaxMP = 100;
    public int BaseATK = 10;
    public int BaseDEF = 5;
    public int BaseSPD = 10;
    
    // ── 🚨 1단계: 최종 스탯 계산 ──
    public int MaxHP => GetCalculatedStat(StatType.MaxHP, BaseMaxHP);
    public int MaxMP => GetCalculatedStat(StatType.MaxMP, BaseMaxMP);
    public int ATK   => GetCalculatedStat(StatType.ATK, BaseATK);
    public int DEF   => GetCalculatedStat(StatType.DEF, BaseDEF);
    public int SPD   => GetCalculatedStat(StatType.SPD, BaseSPD);

    public int CurrentHP { get; protected set; }
    public int CurrentMP { get; protected set; }
    public bool IsAlive => CurrentHP > 0;
    
    [Header("Runtime Status")]
    public bool IsBound { get; set; } = false;   
    public bool IsStunned { get; set; } = false; 
    public bool IsBerserk { get; set; } = false; 
    public bool IsDefending { get; set; } = false; 
    public bool IsInvincible { get; set; } = false; 

    public event Action OnActionExecuted; 
    public event Action<CharacterBase, int, int> OnHPChanged;
    public event Action<CharacterBase, int, int> OnMPChanged;

    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();

    protected virtual void Awake()
    {
        CurrentHP = BaseMaxHP;
        CurrentMP = BaseMaxMP;
    }

    public void NotifyActionExecuted() => OnActionExecuted?.Invoke();
    public bool CanDodgeOrJump() => !IsBound && !IsStunned;
    public bool CanTakeTurn() => IsAlive && !IsStunned;

    public bool TryShowBattleSpeech(
        BattleSpeechTrigger trigger,
        SkillData skill = null,
        CharacterBase target = null,
        int battleTurn = 0,
        float holdOverride = -1f,
        BattleSpeechBubbleDirection? directionOverride = null)
    {
        BattleSpeechBubble bubble = GetBattleSpeechBubble();
        return bubble != null && bubble.TryShow(trigger, this, skill, target, battleTurn, holdOverride, directionOverride);
    }

    public bool IsBattleSpeechShowing()
    {
        BattleSpeechBubble bubble = GetBattleSpeechBubble();
        return bubble != null && bubble.IsShowing;
    }

    public IEnumerator WaitForBattleSpeech()
    {
        BattleSpeechBubble bubble = GetBattleSpeechBubble();
        if (bubble != null)
            yield return bubble.WaitUntilHidden();
    }

    private BattleSpeechBubble GetBattleSpeechBubble()
    {
        return GetComponentInChildren<BattleSpeechBubble>(true);
    }

    public Transform GetPivot(string pivotName)
    {
        Transform pivot = transform.Find($"Pivots/{pivotName}");
        return pivot != null ? pivot : transform;
    }

    // ── 1. 스탯 계산 (LINQ 제거, for문 최적화) ──────────────────────────────
    private int GetCalculatedStat(StatType type, int baseValue)
    {
        int flatBonus = GetFlatStatBonus(type);
        float percentBonus = GetPercentStatBonus(type);

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            flatBonus += _activeEffects[i].GetFlatModifier(type);
            percentBonus += _activeEffects[i].GetPercentModifier(type);
        }

        float finalValue = (baseValue + flatBonus) * (1f + percentBonus);
        return Mathf.Max(type == StatType.MaxMP ? 0 : 1, Mathf.RoundToInt(finalValue));
    }

    protected virtual int GetFlatStatBonus(StatType type) => 0;
    protected virtual float GetPercentStatBonus(StatType type) => 0f;

    // ── 3. 속성 상성 및 4. 최종 데미지 배율 가져오기 (LINQ 제거) ────────────
    public virtual float GetElementAffinity(DamageElement element)
    {
        float modifier = 0f;
        for (int i = 0; i < _activeEffects.Count; i++)
            modifier += _activeEffects[i].GetElementResistanceModifier(element);
            
        return Mathf.Max(0f, 1.0f + modifier); 
    }

    public float GetIncomingDamageMultiplier()
    {
        float modifier = 0f;
        for (int i = 0; i < _activeEffects.Count; i++)
            modifier += _activeEffects[i].GetIncomingDamageModifier();
            
        return Mathf.Max(0f, 1.0f + modifier);
    }

    public float GetOutgoingDamageMultiplier()
    {
        float modifier = 0f;
        for (int i = 0; i < _activeEffects.Count; i++)
            modifier += _activeEffects[i].GetOutgoingDamageModifier();
            
        return Mathf.Max(0f, 1.0f + modifier);
    }

    // ── 🚨 궁극의 4단계 데미지 파이프라인 ──────────────────────────────────
    public virtual int TakeDamage(int rawDamage, DamageElement element = DamageElement.Physical, CharacterBase attacker = null)
    {
        if (!IsAlive) return 0;
        if (IsInvincible) 
        {
            Debug.Log($"<color=cyan>[무적/회피]</color> {gameObject.name} 데미지 무시!");
            return 0; 
        }

        float outgoingMult = attacker != null ? attacker.GetOutgoingDamageMultiplier() : 1.0f;
        float step1Damage = rawDamage * outgoingMult;

        float defMultiplier = 100f / (100f + Mathf.Max(0, DEF)); 
        float step2Damage = step1Damage * defMultiplier;

        float elementMult = GetElementAffinity(element);
        float step3Damage = step2Damage * elementMult;

        float incomingMult = GetIncomingDamageMultiplier();
        float step4Damage = step3Damage * incomingMult;

        if (IsDefending) step4Damage *= 0.5f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(step4Damage));

        CurrentHP = Mathf.Clamp(CurrentHP - finalDamage, 0, MaxHP);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        
        string elemLog = elementMult > 1f ? "<color=red>약점!</color>" : (elementMult < 1f ? "<color=grey>저항</color>" : "");
        Debug.Log($"[Damage] 원본:{rawDamage} -> 방어감소:{step2Damage:F1} -> 속성({elemLog}):{step3Damage:F1} -> <b>최종: {finalDamage}</b>");

        OnDamageTaken(finalDamage);
        if (CurrentHP == 0) OnDie();
        
        return finalDamage;
    }

    public virtual int TakePureDamage(int damage)
    {
        if (!IsAlive || IsInvincible) return 0;
        CurrentHP = Mathf.Clamp(CurrentHP - damage, 0, MaxHP);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        OnDamageTaken(damage);
        if (CurrentHP == 0) OnDie();
        return damage;
    }

    // ── 회복, 상태이상 관리 ──
    public virtual void HealHP(int amount) { CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount); OnHPChanged?.Invoke(this, CurrentHP, MaxHP); }
    public virtual void HealMP(int amount) { CurrentMP = Mathf.Min(MaxMP, CurrentMP + amount); OnMPChanged?.Invoke(this, CurrentMP, MaxMP); }
    public virtual void ConsumeMP(int amount) { CurrentMP = Mathf.Max(0, CurrentMP - amount); OnMPChanged?.Invoke(this, CurrentMP, MaxMP); }

    public void AddEffect(StatusEffect effect)
    {
        if (!IsAlive) return;
        
        // Any(), Find() 대신 for문 사용
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (_activeEffects[i].EffectID == effect.EffectID)
            {
                _activeEffects[i].AddStack(effect.DurationTurns);
                return;
            }
        }
        
        _activeEffects.Add(effect);
        effect.OnApply(this); 
    }

    public void RemoveEffect(StatusEffect effect) { if (_activeEffects.Remove(effect)) effect.OnRemove(); }
    
    // LINQ Any 제거
    public bool HasEffect(string effectID) 
    {
        for (int i = 0; i < _activeEffects.Count; i++)
            if (_activeEffects[i].EffectID == effectID) return true;
        return false;
    }
    
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

    public void AddLoopVFX(string buffId, GameObject vfxPrefab, string pivotName = "Bottom")
    {
        if (_activeLoopVFX.ContainsKey(buffId) || vfxPrefab == null) return;
        Transform pivot = GetPivot(pivotName);
        GameObject vfx = ObjectPoolManager.Instance.Spawn(vfxPrefab, pivot.position, Quaternion.identity);
        CharacterVFX.ApplyRuntimeAudioNormalization(vfx);
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
