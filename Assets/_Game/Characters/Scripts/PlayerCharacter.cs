using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerCharacter : CharacterBase
{
    #region [ Animation Hashes ]
    public static readonly int HashBattleIdle  = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove  = Animator.StringToHash("BattleMove");
    public static readonly int HashBattleReady = Animator.StringToHash("BattleReady"); 
    public static readonly int HashAttack      = Animator.StringToHash("Attack");
    public static readonly int HashHurt        = Animator.StringToHash("Hurt");
    public static readonly int HashDie         = Animator.StringToHash("Die");
    #endregion

    [Header("Identity & Progression")]
    public string CharacterID = "Player";
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

    private void Start()
    {
        if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.Party.Count == 0)
        {
            GlobalDataManager.Instance.InitializePartyFromScene(this);
        }
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

    // ── 🚨 장비 스탯 적용 (Flat 고정값) ──
    protected override int GetFlatStatBonus(StatType type)
    {
        return type switch
        {
            StatType.ATK   => GetEquipSum(e => e?.BonusATK ?? 0),
            StatType.DEF   => GetEquipSum(e => e?.BonusDEF ?? 0),
            StatType.SPD   => GetEquipSum(e => e?.BonusSPD ?? 0),
            StatType.MaxHP => GetEquipSum(e => e?.BonusMaxHP ?? 0),
            StatType.MaxMP => GetEquipSum(e => e?.BonusMaxMP ?? 0),
            _ => 0
        };
    }

    // ── 🚨 장비 스탯 적용 (Percent 비율 증가값 - 훗날 대비) ──
    // 추후 EquipmentData에 BonusPercentATK 같은 값이 생기면 여기에 연결하시면 됩니다.
    protected override float GetPercentStatBonus(StatType type)
    {
        return 0f; 
    }

    // Null 조건부 연산자(?.) 덕분에 장비 슬롯이 비어있어도 안전하게 0을 반환합니다.
    private int GetEquipSum(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot) + selector(Accessory1Slot) + selector(Accessory2Slot) +
               selector(HeadSlot) + selector(BodySlot) + selector(ShoesSlot);
    }

    // ── 글로벌 동기화 ──
    public void LoadDataFromGlobal(CharacterSaveData saveData)
    {
        if (saveData == null) return;
        _mySaveDataRef = saveData;

        CharacterID = saveData.CharacterID;
        Level       = saveData.Level;
        EXP         = saveData.EXP;
        
        BaseMaxHP   = saveData.MaxHP;
        CurrentHP   = saveData.HP;
        BaseMaxMP   = saveData.MaxMP;
        CurrentMP   = saveData.MP;

        if (CurrentHP <= 0) 
        {
            CurrentHP = 1;
            saveData.HP = 1;
        }

        BaseATK     = saveData.ATK; 
        BaseDEF     = saveData.DEF;
        BaseSPD     = saveData.SPD;
    }

    public void SaveDataToGlobal()
    {
        if (_mySaveDataRef == null) 
        {
            if (GlobalDataManager.Instance != null)
                GlobalDataManager.Instance.InitializePartyFromScene(this);
            return;
        }
        
        _mySaveDataRef.HP    = CurrentHP;
        _mySaveDataRef.MP    = CurrentMP;
        _mySaveDataRef.Level = Level;
        _mySaveDataRef.EXP   = EXP;
    }

    // ── 피격 & 연출 ──
    protected override void OnDamageTaken(int damage)
    {
        // 무적이면 이펙트/모션 완전 스킵
        if (IsInvincible) return; 

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
    }

    // PlayerController에서 회피/점프 시 호출 (무적 판정)
    public void SetEvasive(bool state)
    {
        IsInvincible = state; 
    }
}