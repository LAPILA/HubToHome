using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerCharacter : CharacterBase
{
    public static readonly int HashBattleIdle  = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove  = Animator.StringToHash("BattleMove");
    public static readonly int HashBattleReady = Animator.StringToHash("BattleReady"); 
    public static readonly int HashAttack      = Animator.StringToHash("Attack");
    public static readonly int HashHurt        = Animator.StringToHash("Hurt");
    public static readonly int HashDie         = Animator.StringToHash("Die");

    [Header("Identity & Progression")]
    public string CharacterID = "Hero";
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

    public List<SkillData> Skills = new List<SkillData>();

    // 내가 누군지 기억하는 글로벌 데이터 참조 (전투 종료 시 저장용)
    private CharacterSaveData _mySaveDataRef;

    private Animator _animator;
    private CharacterVFX _vfx;
    private SpriteRenderer _spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        _vfx = GetComponent<CharacterVFX>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator != null && HasParameter(triggerHash))
            _animator.SetTrigger(triggerHash);
    }

    private bool HasParameter(int paramHash)
    {
        if (_animator == null) return false;
        foreach (AnimatorControllerParameter param in _animator.parameters)
            if (param.nameHash == paramHash) return true;
        return false;
    }

    // ── 🚨 장비 스탯 안전 합산 (버그 원천 차단) ──
    protected override int GetExtraStat(StatType type)
    {
        int equipBonus = 0;
        switch (type)
        {
            case StatType.ATK: equipBonus = GetEquipBonus(e => e?.BonusATK ?? 0); break;
            case StatType.DEF: equipBonus = GetEquipBonus(e => e?.BonusDEF ?? 0); break;
            case StatType.SPD: equipBonus = GetEquipBonus(e => e?.BonusSPD ?? 0); break;
            case StatType.MaxHP: equipBonus = GetEquipBonus(e => e?.BonusMaxHP ?? 0); break;
            case StatType.MaxMP: equipBonus = GetEquipBonus(e => e?.BonusMaxMP ?? 0); break;
        }
        
        // 장비 보너스 + 상태이상 보너스(base)
        return equipBonus + base.GetExtraStat(type);
    }

    private int GetEquipBonus(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot) + selector(Accessory1Slot) + selector(Accessory2Slot) +
               selector(HeadSlot) + selector(BodySlot) + selector(ShoesSlot);
    }

    // ── 글로벌 데이터 동기화 (다중 파티원) ──
    /// <summary>전투 시작 시 할당받은 파티원 데이터를 로드합니다.</summary>
    public void LoadDataFromGlobal(CharacterSaveData saveData)
    {
        _mySaveDataRef = saveData;

        CharacterID = saveData.CharacterID;
        Level = saveData.Level;
        EXP = saveData.EXP;
        
        BaseMaxHP = saveData.MaxHP;
        CurrentHP = saveData.HP;
        BaseMaxMP = saveData.MaxMP;
        CurrentMP = saveData.MP;

        BaseATK = saveData.ATK; 
        BaseDEF = saveData.DEF;
        BaseSPD = saveData.SPD;
    }

    /// <summary>전투 종료 시 현재 HP/MP를 내 글로벌 데이터에 덮어씁니다.</summary>
    public void SaveDataToGlobal()
    {
        if (_mySaveDataRef == null) return;
        
        _mySaveDataRef.HP = CurrentHP;
        _mySaveDataRef.MP = CurrentMP;
        _mySaveDataRef.Level = Level;
        _mySaveDataRef.EXP = EXP;
    }

    // ── 연출 ──
    protected override void OnDamageTaken(int damage)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(Color.red, 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => _spriteRenderer.color = Color.white);
        }

        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive)
        {
            PlayBattleAnim(HashHurt);
            transform.DOKill(false);
            transform.DOShakePosition(0.2f, 0.15f, 30, 90f); 
        }
    }

    protected override void OnDie()
    {
        PlayBattleAnim(HashDie);
        Debug.Log($"<color=red>[Player] {CharacterID} 쓰러짐!</color>");
    }
}