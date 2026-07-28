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
    [SerializeField] private CharacterData _characterData;
    [SerializeField] private string _fallbackCharacterID = "Player";
    public string CharacterID { get; private set; } = "Player";
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

    public CharacterData CharacterData => _characterData;
    public string DisplayName => _characterData != null
        ? _characterData.ResolveDisplayName(GlobalDataManager.Instance != null ? GlobalDataManager.Instance.PlayerName : null)
        : CharacterID;
    public Sprite BattlePortrait => _characterData != null && _characterData.Portrait != null
        ? _characterData.Portrait
        : (_spriteRenderer != null ? _spriteRenderer.sprite : null);
    public Sprite TurnOrderPortrait => _characterData != null && _characterData.TurnOrderPortrait != null
        ? _characterData.TurnOrderPortrait
        : BattlePortrait;

    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        _vfx = GetComponent<CharacterVFX>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyCharacterData();
    }

    private void Start()
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null) return;

        CharacterSaveData saveData = global.InitializePartyFromScene(this);
        if (saveData != null)
            LoadDataFromGlobal(saveData);
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null || !HasParameter(triggerHash)) return;
        if (!IsAlive && triggerHash != HashDie) return;
        _animator.SetTrigger(triggerHash);
    }

    public void PlayBasicAttackEffect()
    {
        _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
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

    public void SetCharacterData(CharacterData data)
    {
        _characterData = data;
        ApplyCharacterData();
    }

    private void ApplyCharacterData()
    {
        CharacterID = _characterData != null && !string.IsNullOrWhiteSpace(_characterData.CharacterID)
            ? _characterData.CharacterID
            : _fallbackCharacterID;

        if (_characterData == null) return;

        BaseMaxHP = _characterData.BaseMaxHP;
        BaseMaxMP = _characterData.BaseMaxMP;
        BaseATK = _characterData.BaseATK;
        BaseDEF = _characterData.BaseDEF;
        BaseSPD = _characterData.BaseSPD;

        if (_characterData.DefaultSkills != null && _characterData.DefaultSkills.Count > 0)
            Skills = new List<SkillData>(_characterData.DefaultSkills);
    }

    // ── 글로벌 동기화 ──
    public void LoadDataFromGlobal(CharacterSaveData saveData)
    {
        if (saveData == null) return;
        _mySaveDataRef = saveData;

        if (!string.IsNullOrWhiteSpace(saveData.CharacterDataID))
        {
            CharacterData resolvedData = CharacterDatabase.FindById(saveData.CharacterDataID);
            if (resolvedData != null)
                _characterData = resolvedData;
        }

        ApplyCharacterData();

        if (saveData.EquippedSkillIDs != null && saveData.EquippedSkillIDs.Count > 0)
        {
            var resolvedSkills = new List<SkillData>();
            for (int i = 0; i < saveData.EquippedSkillIDs.Count; i++)
            {
                string skillId = saveData.EquippedSkillIDs[i];
                SkillData skill = SkillDatabase.FindById(skillId);
                if (skill != null)
                    resolvedSkills.Add(skill);
                else
                    Debug.LogWarning($"[PlayerCharacter] Saved skill ID could not be resolved: {skillId}", this);
            }

            if (resolvedSkills.Count > 0)
            {
                Skills.Clear();
                Skills.AddRange(resolvedSkills);
            }
        }

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

    public bool SynchronizePersistentVitals(CharacterSaveData saveData)
    {
        if (saveData == null)
            return false;

        string sceneCharacterId = _characterData != null && !string.IsNullOrWhiteSpace(_characterData.CharacterID)
            ? _characterData.CharacterID.Trim()
            : string.Empty;
        string savedCharacterId = string.IsNullOrWhiteSpace(saveData.CharacterDataID)
            ? string.Empty
            : saveData.CharacterDataID.Trim();
        if (_mySaveDataRef != saveData
            && !string.IsNullOrEmpty(sceneCharacterId)
            && !string.IsNullOrEmpty(savedCharacterId)
            && !string.Equals(sceneCharacterId, savedCharacterId, System.StringComparison.Ordinal))
        {
            return false;
        }

        _mySaveDataRef = saveData;
        BaseMaxHP = Mathf.Max(1, saveData.MaxHP);
        BaseMaxMP = Mathf.Max(0, saveData.MaxMP);
        SetCurrentHPValue(Mathf.Clamp(saveData.HP, 1, MaxHP));
        SetCurrentMPValue(Mathf.Clamp(saveData.MP, 0, MaxMP));
        return true;
    }

    public void SaveDataToGlobal()
    {
        if (_mySaveDataRef == null)
        {
            if (GlobalDataManager.Instance != null)
                _mySaveDataRef = GlobalDataManager.Instance.InitializePartyFromScene(this);
            if (_mySaveDataRef == null)
                return;
        }
        
        _mySaveDataRef.CharacterDataID = _characterData != null ? _characterData.CharacterID : string.Empty;
        _mySaveDataRef.CharacterID = DisplayName;
        _mySaveDataRef.HP    = CurrentHP;
        _mySaveDataRef.MP    = CurrentMP;
        _mySaveDataRef.MaxHP = BaseMaxHP;
        _mySaveDataRef.MaxMP = BaseMaxMP;
        _mySaveDataRef.ATK   = BaseATK;
        _mySaveDataRef.DEF   = BaseDEF;
        _mySaveDataRef.SPD   = BaseSPD;
        _mySaveDataRef.Level = Level;
        _mySaveDataRef.EXP   = EXP;
        if (_mySaveDataRef.EquippedSkillIDs == null)
            _mySaveDataRef.EquippedSkillIDs = new List<string>();
        else
            _mySaveDataRef.EquippedSkillIDs.Clear();
        for (int i = 0; i < Skills.Count; i++)
        {
            SkillData skill = Skills[i];
            if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillID))
                _mySaveDataRef.EquippedSkillIDs.Add(skill.SkillID);
        }
    }

    // ── 피격 & 연출 ──
    protected override void OnDamageTaken(int damage)
    {
        // 무적이면 이펙트/모션 완전 스킵
        if (IsInvincible) return; 

        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(ResolveFlashColor(Color.red), 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    if (_spriteRenderer != null)
                        _spriteRenderer.color = Color.white;
                })
                .OnKill(() =>
                {
                    if (_spriteRenderer != null)
                        _spriteRenderer.color = Color.white;
                });
        }

        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive)
        {
            PlayBattleAnim(HashHurt);
            transform.DOKill(false);
            transform.DOShakePosition(0.2f, 0.15f * ResolveShakeScale(), 30, 90f);
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