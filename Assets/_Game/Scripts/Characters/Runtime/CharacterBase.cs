using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StatType { MaxHP = 0, MaxAP = 1, ATK = 2, DEF = 3, SPD = 4 }
public enum DamageElement
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Electric = 3,
    Corrosion = 4,
}

public readonly struct DamageResult
{
    public DamageResult(
        int rawDamage,
        DamageElement element,
        int finalDamage,
        bool applied,
        bool wasInvincible,
        bool targetDefeated)
    {
        RawDamage = rawDamage;
        Element = element;
        FinalDamage = finalDamage;
        Applied = applied;
        WasInvincible = wasInvincible;
        TargetDefeated = targetDefeated;
    }

    public int RawDamage { get; }
    public DamageElement Element { get; }
    public int FinalDamage { get; }
    public bool Applied { get; }
    public bool WasInvincible { get; }
    public bool TargetDefeated { get; }
}

public enum StatusApplicationStatus
{
    Applied,
    BlockedByResistance,
    InvalidEffect,
    TargetUnavailable,
}

public readonly struct StatusApplicationResult
{
    public StatusApplicationResult(
        StatusApplicationStatus status,
        string effectId,
        float resistance)
    {
        Status = status;
        EffectId = effectId ?? string.Empty;
        Resistance = resistance;
    }

    public StatusApplicationStatus Status { get; }
    public string EffectId { get; }
    public float Resistance { get; }
    public bool Applied => Status == StatusApplicationStatus.Applied;
}

public abstract class CharacterBase : MonoBehaviour
{
    private readonly CharacterStats _characterStats = new CharacterStats();
    private readonly List<StatModifier> _battleStatModifiers = new List<StatModifier>();
    private bool _characterStatsDirty = true;
    // CurrentHP/AP는 레이어 계산값이 아니라 전투 대상 인스턴스의 런타임 자원이다.
    private int _currentHP;
    private int _currentAP;

    public CharacterStats Stats
    {
        get
        {
            EnsureCharacterStats();
            return _characterStats;
        }
    }

    // UI·서비스는 CharacterStats 구현체 대신 이 읽기 전용 계약을 사용한다.
    public ICharacterStatsReader StatsReader => _characterStats;
    public StatBlock ResolvedStats
    {
        get
        {
            EnsureCharacterStats();
            return _characterStats.ResolvedStats;
        }
    }
    
    // 기존 public API는 유지하되 최종값의 소유자는 CharacterStats로 통합한다.
    public int MaxHP => GetResolvedStat(StatType.MaxHP);
    public int MaxAP => GetResolvedStat(StatType.MaxAP);
    public int ATK   => GetResolvedStat(StatType.ATK);
    public int DEF   => GetResolvedStat(StatType.DEF);
    public int SPD   => GetResolvedStat(StatType.SPD);

    public int CurrentHP => _currentHP;
    public int CurrentAP => _currentAP;
    public bool IsAlive => CurrentHP > 0;
    
    [Header("Runtime Status")]
    public bool IsBound { get; set; } = false;   
    public bool IsStunned { get; set; } = false; 
    public bool IsBerserk { get; set; } = false; 
    public bool IsDefending { get; set; } = false; 
    public bool IsInvincible { get; set; } = false; 

    public event Action OnActionExecuted; 
    public event Action<CharacterBase, int, int> OnHPChanged;
    public event Action<CharacterBase, int, int> OnAPChanged;

    protected readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();
    private readonly Dictionary<string, GameObject> _activeLoopVFX = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Transform> _pivotCache =
        new Dictionary<string, Transform>(StringComparer.Ordinal);

    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();
    private IScreenShakeScaleProvider _screenShakeScaleProvider =
        new GameConfigScreenShakeScaleProvider();

    protected virtual void Awake()
    {
        // PlayerCharacter/EnemyCharacter가 각자의 Data.BaseStats를 주입한 뒤 자원을 초기화한다.
    }

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    public void SetScreenShakeScaleProvider(IScreenShakeScaleProvider provider)
    {
        _screenShakeScaleProvider = provider ?? new GameConfigScreenShakeScaleProvider();
    }

    protected Color ResolveFlashColor(Color authoredColor)
    {
        float scale = VisualAccessibilityPolicy.NormalizeScale(
            _screenFlashScaleProvider?.Scale
            ?? GameConfigManager.DefaultFlashIntensity);
        return VisualAccessibilityPolicy.ScaleFlashColor(
            Color.white,
            authoredColor,
            scale);
    }

    protected float ResolveShakeScale()
    {
        return VisualAccessibilityPolicy.NormalizeScale(
            _screenShakeScaleProvider?.Scale
            ?? GameConfigManager.DefaultScreenShake);
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

    public void HideBattleSpeechImmediate()
    {
        BattleSpeechBubble bubble = GetBattleSpeechBubble();
        bubble?.HideImmediate();
    }

    private BattleSpeechBubble GetBattleSpeechBubble()
    {
        return GetComponentInChildren<BattleSpeechBubble>(true);
    }

    public bool TryGetPivot(string pivotName, out Transform pivot)
    {
        pivot = null;
        if (string.IsNullOrWhiteSpace(pivotName))
        {
            return false;
        }

        if (_pivotCache.TryGetValue(pivotName, out pivot))
        {
            return pivot != null;
        }

        pivot = transform.Find(CharacterPivotId.GetPath(pivotName));
        _pivotCache[pivotName] = pivot;
        return pivot != null;
    }

    public Transform GetPivot(string pivotName)
    {
        return TryGetPivot(pivotName, out Transform pivot) ? pivot : transform;
    }

    protected void MarkCharacterStatsDirty()
    {
        _characterStatsDirty = true;
    }

    protected virtual void PopulateEquipmentStatModifiers(List<StatModifier> modifiers)
    {
        // 장비가 없는 전투 대상은 빈 Equipment 레이어를 사용한다.
    }

    private void EnsureCharacterStats()
    {
        if (!_characterStats.IsInitialized)
            throw new InvalidOperationException(
                $"{GetType().Name}에 CharacterStats.BaseStats가 주입되지 않았습니다.");
        if (!_characterStatsDirty)
            return;

        _battleStatModifiers.Clear();
        for (int i = 0; i < _activeEffects.Count; i++)
            _activeEffects[i].AppendStatModifiers(_battleStatModifiers);

        var equipmentModifiers = new List<StatModifier>();
        PopulateEquipmentStatModifiers(equipmentModifiers);
        _characterStats.SetEquipmentModifiers(equipmentModifiers);
        _characterStats.SetBattleModifiers(_battleStatModifiers);
        _characterStatsDirty = false;
        ClampCurrentResources();
    }

    private int GetResolvedStat(StatType type)
    {
        EnsureCharacterStats();
        return _characterStats.ResolvedStats.GetPrimaryStat(type);
    }

    protected void SetBaseStats(StatBlock baseStats)
    {
        _characterStats.SetBaseStats(baseStats);
        MarkCharacterStatsDirty();
        EnsureCharacterStats();
    }

    protected void SetProgressedBaseStats(StatBlock progressedBaseStats)
    {
        _characterStats.SetProgressedBaseStats(progressedBaseStats);
        MarkCharacterStatsDirty();
        EnsureCharacterStats();
    }

    // ── 3. 속성 상성 및 4. 최종 데미지 배율 가져오기 (LINQ 제거) ────────────
    public virtual float GetElementAffinity(DamageElement element)
    {
        EnsureCharacterStats();
        return _characterStats.ResolvedStats.GetElementResistance(element);
    }

    public float GetIncomingDamageMultiplier()
    {
        EnsureCharacterStats();
        return _characterStats.ResolvedStats.IncomingDamageMultiplier;
    }

    public float GetOutgoingDamageMultiplier()
    {
        EnsureCharacterStats();
        return _characterStats.ResolvedStats.OutgoingDamageMultiplier;
    }

    // ── 🚨 궁극의 4단계 데미지 파이프라인 ──────────────────────────────────
    public virtual DamageResult TakeDamage(
        int rawDamage,
        DamageElement element,
        CharacterBase attacker = null)
    {
        if (!IsAlive)
            return new DamageResult(rawDamage, element, 0, false, false, true);
        if (IsInvincible) 
        {
            Debug.Log($"<color=cyan>[무적/회피]</color> {gameObject.name} 데미지 무시!");
            return new DamageResult(rawDamage, element, 0, false, true, false);
        }

        float outgoingMult = attacker != null ? attacker.GetOutgoingDamageMultiplier() : 1.0f;
        float step1Damage = rawDamage * outgoingMult;

        // 일반 DEF는 물리 피해에만 적용하고, 속성 피해는 속성 저항으로 방어한다.
        float defMultiplier = element == DamageElement.Physical
            ? 100f / (100f + Mathf.Max(0, DEF))
            : 1f;
        float step2Damage = step1Damage * defMultiplier;

        float elementMult = GetElementAffinity(element);
        float step3Damage = step2Damage * elementMult;

        float incomingMult = GetIncomingDamageMultiplier();
        float step4Damage = step3Damage * incomingMult;

        if (IsDefending) step4Damage *= 0.5f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(step4Damage));

        SetCurrentHPInternal(CurrentHP - finalDamage);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        
        string elemLog = elementMult > 1f ? "<color=red>약점!</color>" : (elementMult < 1f ? "<color=grey>저항</color>" : "");
        Debug.Log($"[Damage] 원본:{rawDamage} -> 방어감소:{step2Damage:F1} -> 속성({elemLog}):{step3Damage:F1} -> <b>최종: {finalDamage}</b>");

        OnDamageTaken(finalDamage);
        if (CurrentHP == 0) OnDie();
        
        return new DamageResult(
            rawDamage,
            element,
            finalDamage,
            true,
            false,
            CurrentHP == 0);
    }

    public virtual int TakePureDamage(int damage)
    {
        if (!IsAlive || IsInvincible) return 0;
        SetCurrentHPInternal(CurrentHP - damage);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
        OnDamageTaken(damage);
        if (CurrentHP == 0) OnDie();
        return damage;
    }

    protected void SetCurrentHPValue(int value)
    {
        SetCurrentHPInternal(value);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
    }

    protected void SetCurrentAPValue(int value)
    {
        SetCurrentAPInternal(value);
        OnAPChanged?.Invoke(this, CurrentAP, MaxAP);
    }

    private void SetCurrentHPInternal(int value)
    {
        EnsureCharacterStats();
        _currentHP = Mathf.Clamp(value, 0, Mathf.Max(0, MaxHP));
    }

    private void SetCurrentAPInternal(int value)
    {
        EnsureCharacterStats();
        _currentAP = Mathf.Clamp(value, 0, Mathf.Max(0, MaxAP));
    }

    private void ClampCurrentResources()
    {
        if (!_characterStats.IsInitialized)
            return;

        _currentHP = Mathf.Clamp(_currentHP, 0, Mathf.Max(0, MaxHP));
        _currentAP = Mathf.Clamp(_currentAP, 0, Mathf.Max(0, MaxAP));
    }

    // ── 회복, 상태이상 관리 ──
    public virtual void HealHP(int amount)
    {
        SetCurrentHPInternal(CurrentHP + amount);
        OnHPChanged?.Invoke(this, CurrentHP, MaxHP);
    }

    public virtual void RestoreAP(int amount)
    {
        SetCurrentAPInternal(CurrentAP + amount);
        OnAPChanged?.Invoke(this, CurrentAP, MaxAP);
    }

    public virtual void ConsumeAP(int amount)
    {
        SetCurrentAPInternal(CurrentAP - amount);
        OnAPChanged?.Invoke(this, CurrentAP, MaxAP);
    }

    [Obsolete("Use RestoreAP.")]
    public void HealMP(int amount) => RestoreAP(amount);

    [Obsolete("Use ConsumeAP.")]
    public void ConsumeMP(int amount) => ConsumeAP(amount);

    public StatusApplicationResult TryApplyStatusEffect(StatusEffect effect)
    {
        if (effect == null)
            return new StatusApplicationResult(
                StatusApplicationStatus.InvalidEffect,
                null,
                0f);
        if (!IsAlive)
            return new StatusApplicationResult(
                StatusApplicationStatus.TargetUnavailable,
                effect.EffectID,
                0f);

        EnsureCharacterStats();
        float resistance = _characterStats.ResolvedStats.GetStatusResistance(effect.EffectID);
        if (resistance <= 0f)
        {
            return new StatusApplicationResult(
                StatusApplicationStatus.BlockedByResistance,
                effect.EffectID,
                resistance);
        }
        
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (_activeEffects[i].EffectID == effect.EffectID)
            {
                _activeEffects[i].AddStack(effect.DurationTurns);
                MarkCharacterStatsDirty();
                return new StatusApplicationResult(
                    StatusApplicationStatus.Applied,
                    effect.EffectID,
                    resistance);
            }
        }
        
        _activeEffects.Add(effect);
        effect.OnApply(this); 
        MarkCharacterStatsDirty();
        return new StatusApplicationResult(
            StatusApplicationStatus.Applied,
            effect.EffectID,
            resistance);
    }

    public void RemoveEffect(StatusEffect effect)
    {
        if (_activeEffects.Remove(effect))
        {
            effect.OnRemove();
            MarkCharacterStatsDirty();
        }
    }
    
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

        MarkCharacterStatsDirty();
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
