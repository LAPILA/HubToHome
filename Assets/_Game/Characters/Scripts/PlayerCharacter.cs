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

    #region [ Identity & Progression ]
    [Header("Identity & Progression")]
    public string CharacterID = "Player";
    public int Level = 1;
    public int EXP = 0;
    public int EXPToNextLevel = 100;
    public const int MaxLevel = 99;
    #endregion

    #region [ Equipment Slots & Skills ]
    [Header("Equipment Slots")]
    public EquipmentData WeaponSlot;
    public EquipmentData Accessory1Slot;
    public EquipmentData Accessory2Slot;
    public EquipmentData HeadSlot;
    public EquipmentData BodySlot;
    public EquipmentData ShoesSlot;

    public List<SkillData> Skills = new List<SkillData>();
    #endregion

    #region [ Internal State ]
    // 내가 누군지 기억하는 글로벌 데이터 참조 (전투 종료 시 저장용)
    private CharacterSaveData _mySaveDataRef;

    private Animator _animator;
    private CharacterVFX _vfx;
    private SpriteRenderer _spriteRenderer;
    #endregion

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
            Debug.Log($"<color=cyan>[PlayerCharacter] {CharacterID}가 최초 글로벌 데이터에 스스로를 등록했습니다!</color>");
        }
    }

    #region [ Animation Controller ]
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
    #endregion

    #region [ Stats & Equipment ]
    // ── 🚨 장비 스탯 안전 합산 (버그 원천 차단) ──
    protected override int GetExtraStat(StatType type)
    {
        int equipBonus = type switch
        {
            StatType.ATK   => GetEquipBonus(e => e?.BonusATK ?? 0),
            StatType.DEF   => GetEquipBonus(e => e?.BonusDEF ?? 0),
            StatType.SPD   => GetEquipBonus(e => e?.BonusSPD ?? 0),
            StatType.MaxHP => GetEquipBonus(e => e?.BonusMaxHP ?? 0),
            StatType.MaxMP => GetEquipBonus(e => e?.BonusMaxMP ?? 0),
            _ => 0
        };
        
        // 장비 보너스 + 상태이상 보너스(base)
        return equipBonus + base.GetExtraStat(type);
    }

    private int GetEquipBonus(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot) + selector(Accessory1Slot) + selector(Accessory2Slot) +
               selector(HeadSlot) + selector(BodySlot) + selector(ShoesSlot);
    }
    #endregion

    #region [ Global Data Sync (SSOT) ]
    /// <summary>전투 시작 시 할당받은 파티원 데이터를 로드합니다.</summary>
    public void LoadDataFromGlobal(CharacterSaveData saveData)
    {
        if (saveData == null) 
        {
            Debug.LogWarning($"<color=orange>[{gameObject.name}] 세이브 데이터가 없습니다. 인스펙터 스탯을 기준으로 진행합니다.</color>");
            return;
        }

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
            Debug.LogWarning($"<color=red>[{CharacterID}] 체력이 0인 상태로 로드되었습니다. 강제 부활(HP 1) 처리합니다.</color>");
            CurrentHP = 1;
            saveData.HP = 1;
        }

        BaseATK     = saveData.ATK; 
        BaseDEF     = saveData.DEF;
        BaseSPD     = saveData.SPD;
    }

    /// <summary>전투 종료/맵 이동 시 현재 HP/MP를 내 글로벌 데이터에 덮어씁니다.</summary>
    public void SaveDataToGlobal()
    {
        if (_mySaveDataRef == null) 
        {
            // 참조가 없다면 씬에서 바로 시작한 경우이므로 GlobalManager에 나를 강제로 등록함
            if (GlobalDataManager.Instance != null)
                GlobalDataManager.Instance.InitializePartyFromScene(this);
            return;
        }
        
        _mySaveDataRef.HP    = CurrentHP;
        _mySaveDataRef.MP    = CurrentMP;
        _mySaveDataRef.Level = Level;
        _mySaveDataRef.EXP   = EXP;
    }
    #endregion

    #region [ Battle Visuals & Feedback ]
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
    #endregion
}