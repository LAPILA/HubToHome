using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // 연출을 위해 추가

/// <summary>
/// 플레이어 캐릭터. 
/// CharacterBase를 상속하며 레벨링, 장비, 스킬, 애니메이션, VFX를 총괄합니다.
/// </summary>
public class PlayerCharacter : CharacterBase
{
    // ── 🚨 애니메이션 해시 (스킬 시퀀서가 직접 접근할 수 있도록 public static 선언) ──
    public static readonly int HashBattleIdle  = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove  = Animator.StringToHash("BattleMove");
    public static readonly int HashBattleReady = Animator.StringToHash("BattleReady"); // 새로 추가된 준비 자세!
    public static readonly int HashAttack      = Animator.StringToHash("Attack");
    public static readonly int HashHurt        = Animator.StringToHash("Hurt");
    public static readonly int HashDie         = Animator.StringToHash("Die");

    [Header("Level & EXP")]
    public int Level = 1;
    public int EXP = 0;
    public int EXPToNextLevel = 100;
    public const int MaxLevel = 99;

    [Header("Equipment Slots")]
    public EquipmentData WeaponSlot;
    public EquipmentData Accessory1Slot;
    public EquipmentData Accessory2Slot;
    public EquipmentData HeadSlot;
    public EquipmentData BodySlot;
    public EquipmentData ShoesSlot;

    [Header("Skills")]
    public List<SkillData> Skills = new List<SkillData>();

    [Header("Identity")]
    public string CharacterID = "Player";

    // ── 컴포넌트 캐싱 ──
    private Animator _animator;
    private CharacterVFX _vfx;
    private SpriteRenderer _spriteRenderer;

    protected override void Awake()
    {
        base.Awake();

        // 🚨 EnemyCharacter처럼 스스로 연출 컴포넌트를 가집니다.
        _animator = GetComponent<Animator>();
        _vfx = GetComponent<CharacterVFX>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        RecalculateStats();
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;

        // 전투 시작 시 대기 상태로 돌입
        PlayBattleAnim(HashBattleIdle);
    }

    // ── 🚨 애니메이션 제어 (시퀀서 블록들이 호출함) ───────────────────────────
    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator != null && HasParameter(triggerHash))
        {
            _animator.SetTrigger(triggerHash);
        }
    }

    private bool HasParameter(int paramHash)
    {
        if (_animator == null) return false;
        foreach (AnimatorControllerParameter param in _animator.parameters)
            if (param.nameHash == paramHash) return true;
        return false;
    }

    // ── 스탯 아키텍처 (안전 보장) ───────────────────────────────────────────
    public void RecalculateStats()
    {
        int bATK = GetEquipBonus(e => e?.BonusATK ?? 0);
        int bDEF = GetEquipBonus(e => e?.BonusDEF ?? 0);
        int bSPD = GetEquipBonus(e => e?.BonusSPD ?? 0);
        int bHP  = GetEquipBonus(e => e?.BonusMaxHP ?? 0);
        int bMP  = GetEquipBonus(e => e?.BonusMaxMP ?? 0);

        ATK   = 10 + bATK;
        DEF   = 5  + bDEF;
        SPD   = 10 + bSPD;
        MaxHP = 100 + bHP;
        MaxMP = 100 + bMP;

        foreach (var effect in _activeEffects)
        {
            if (effect is FreezeEffect freeze) SPD -= (10 * freeze.Stacks);
            if (effect is SpeedUpEffect speed) SPD += (50 * speed.Stacks);
        }

        CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
        CurrentMP = Mathf.Clamp(CurrentMP, 0, MaxMP);
    }

    private int GetEquipBonus(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot)      +
               selector(Accessory1Slot)  +
               selector(Accessory2Slot)  +
               selector(HeadSlot)        +
               selector(BodySlot)        +
               selector(ShoesSlot);
    }

    public void Equip(EquipmentData equipment)
    {
        if (equipment == null) return;

        switch (equipment.Slot)
        {
            case EquipmentSlot.Weapon:     WeaponSlot     = equipment; break;
            case EquipmentSlot.Accessory1: Accessory1Slot = equipment; break;
            case EquipmentSlot.Accessory2: Accessory2Slot = equipment; break;
            case EquipmentSlot.Head:       HeadSlot       = equipment; break;
            case EquipmentSlot.Body:       BodySlot       = equipment; break;
            case EquipmentSlot.Shoes:      ShoesSlot      = equipment; break;
        }

        RecalculateStats();

        if (!string.IsNullOrEmpty(equipment.EquipReactionDialogueID))
            Debug.Log($"[Equip] {CharacterID}: {equipment.EquipReactionDialogueID}");
    }

    public void GainEXP(int amount)
    {
        if (Level >= MaxLevel) return;

        EXP += amount;
        while (EXP >= EXPToNextLevel && Level < MaxLevel)
        {
            EXP -= EXPToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level++;
        EXPToNextLevel = Mathf.RoundToInt(EXPToNextLevel * 1.2f);
        RecalculateStats();
        
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }

    // ── 🚨 피격 및 사망 연출 통합 ───────────────────────────────────────────
    protected override void OnDamageTaken(int damage)
    {
        base.OnDamageTaken(damage); 

        // 1. 빨간색 플래시 깜빡임 연출
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(Color.red, 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => _spriteRenderer.color = Color.white);
        }

        // 2. 피격 이펙트(VFX) 자동 재생
        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        // 3. 상태에 따른 애니메이션 트리거
        if (IsAlive)
        {
            PlayBattleAnim(HashHurt);
            transform.DOKill(false);
            transform.DOShakePosition(0.2f, 0.15f, 30, 90f); // 약간의 물리적 흔들림
        }
        else
        {
            OnDie();
        }
    }

    protected override void OnDie()
    {
        PlayBattleAnim(HashDie);
        Debug.Log($"<color=red>[Player] {CharacterID} 사망.</color>");
    }
}