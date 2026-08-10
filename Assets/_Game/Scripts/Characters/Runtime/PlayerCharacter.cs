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
    public Color BattleSymbolColor
    {
        get
        {
            Color color = _characterData != null ? _characterData.BattleSymbolColor : Color.white;
            return color.a > 0f ? color : Color.white;
        }
    }
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

    protected override void PopulateEquipmentStatModifiers(List<StatModifier> modifiers)
    {
        AppendEquipmentModifier(modifiers, WeaponSlot);
        AppendEquipmentModifier(modifiers, Accessory1Slot);
        AppendEquipmentModifier(modifiers, Accessory2Slot);
        AppendEquipmentModifier(modifiers, HeadSlot);
        AppendEquipmentModifier(modifiers, BodySlot);
        AppendEquipmentModifier(modifiers, ShoesSlot);
    }

    private static void AppendEquipmentModifier(
        List<StatModifier> modifiers,
        EquipmentData equipment)
    {
        equipment?.AppendStatModifiers(modifiers);
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

        // CharacterData가 유일한 기본 스탯 원천이며, 이후 레이어는 CharacterStats가 계산한다.
        SetBaseStats(_characterData.BaseStats);
        SetCurrentHPValue(MaxHP);
        SetCurrentAPValue(MaxAP);

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
        PowerProgressionService.SynchronizeUnlockedSkills(saveData, _characterData);
        ApplyEquipmentFromSave(saveData);
        CharacterGrowthService.EnsureInitialized(saveData, _characterData);
        SkillTreeProgressionService.Synchronize(saveData, _characterData);

        bool hasExplicitSkillLoadout = saveData.EquippedSkillIDs != null
            && (_characterData?.SkillTree != null || saveData.EquippedSkillIDs.Count > 0);
        if (hasExplicitSkillLoadout)
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

            Skills.Clear();
            Skills.AddRange(resolvedSkills);
        }

        Level = Mathf.Max(1, saveData.Level);
        EXP = Mathf.Max(0, saveData.EXP);
        EXPToNextLevel = CharacterProgressionService.ExperienceRequiredForNextLevel(
            _characterData,
            Level);

        SetProgressedBaseStats(CreateProgressedBaseStats(saveData));
        SetCurrentHPValue(Mathf.Clamp(saveData.HP, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(saveData.AP, 0, MaxAP));

        if (CurrentHP <= 0) 
        {
            SetCurrentHPValue(1);
            saveData.HP = 1;
        }
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
        CharacterGrowthService.EnsureInitialized(saveData, _characterData);
        SetProgressedBaseStats(CreateProgressedBaseStats(saveData));
        SetCurrentHPValue(Mathf.Clamp(saveData.HP, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(saveData.AP, 0, MaxAP));
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

        _mySaveDataRef.CharacterDataID = _characterData != null
            ? _characterData.CharacterID
            : string.Empty;
        _mySaveDataRef.CharacterID = DisplayName;
        _mySaveDataRef.Level = Mathf.Max(1, Level);
        _mySaveDataRef.EXP = Mathf.Max(0, EXP);
        _mySaveDataRef.HP = CurrentHP;
        _mySaveDataRef.AP = CurrentAP;
        SaveEquipmentToGlobal(_mySaveDataRef);

        CharacterGrowthService.EnsureInitialized(_mySaveDataRef, _characterData);
        SetProgressedBaseStats(CreateProgressedBaseStats(_mySaveDataRef));
        SetCurrentHPValue(Mathf.Clamp(_mySaveDataRef.HP, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(_mySaveDataRef.AP, 0, MaxAP));
        _mySaveDataRef.HP = CurrentHP;
        _mySaveDataRef.AP = CurrentAP;

        PowerProgressionService.SynchronizeUnlockedSkills(
            _mySaveDataRef,
            _characterData);
        SkillTreeProgressionService.Synchronize(
            _mySaveDataRef,
            _characterData);
        _mySaveDataRef.EquippedSkillIDs ??= new List<string>();
        _mySaveDataRef.EquippedSkillIDs.Clear();
        for (int i = 0; i < Skills.Count; i++)
        {
            SkillData skill = Skills[i];
            if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillID))
                _mySaveDataRef.EquippedSkillIDs.Add(skill.SkillID);
        }
    }

    private StatBlock CreateProgressedBaseStats(CharacterSaveData saveData)
    {
        StatBlock stats = Stats.BaseStats.Clone();
        CharacterBaseStatSnapshot calculated =
            CharacterGrowthService.CalculateBaseStats(saveData, _characterData);
        stats.MaxHP = calculated.MaxHP;
        stats.MaxAP = calculated.MaxAP;
        stats.ATK = calculated.Attack;
        stats.DEF = calculated.Defense;
        stats.SPD = calculated.Speed;
        return stats;
    }

    private void ApplyEquipmentFromSave(CharacterSaveData saveData)
    {
        if (!saveData.HasInitializedEquipment)
        {
            SaveEquipmentToGlobal(saveData);
            GlobalDataManager global = GlobalDataManager.Instance;
            if (global != null)
            {
                for (int i = 0; i < EquipmentLoadoutService.SlotCount; i++)
                {
                    string id = saveData.EquippedEquipmentIDs[i];
                    if (!string.IsNullOrEmpty(id))
                        global.AddEquipmentAndGetAddedAmount(id);
                }
            }
            return;
        }

        EquipmentLoadoutService.NormalizeSlots(saveData);
        WeaponSlot = ResolveEquipment(saveData, EquipmentSlot.Weapon);
        Accessory1Slot = ResolveEquipment(saveData, EquipmentSlot.Accessory1);
        Accessory2Slot = ResolveEquipment(saveData, EquipmentSlot.Accessory2);
        HeadSlot = ResolveEquipment(saveData, EquipmentSlot.Head);
        BodySlot = ResolveEquipment(saveData, EquipmentSlot.Body);
        ShoesSlot = ResolveEquipment(saveData, EquipmentSlot.Shoes);
    }

    private void SaveEquipmentToGlobal(CharacterSaveData saveData)
    {
        if (saveData == null)
            return;

        EquipmentLoadoutService.NormalizeSlots(saveData);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Weapon] = EquipmentId(WeaponSlot);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Accessory1] = EquipmentId(Accessory1Slot);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Accessory2] = EquipmentId(Accessory2Slot);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Head] = EquipmentId(HeadSlot);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Body] = EquipmentId(BodySlot);
        saveData.EquippedEquipmentIDs[(int)EquipmentSlot.Shoes] = EquipmentId(ShoesSlot);
        saveData.HasInitializedEquipment = true;
    }

    private EquipmentData ResolveEquipment(CharacterSaveData saveData, EquipmentSlot slot)
    {
        string id = EquipmentLoadoutService.GetEquippedId(saveData, slot);
        if (string.IsNullOrEmpty(id))
            return null;

        EquipmentData equipment = EquipmentDatabase.FindById(id);
        if (equipment == null)
            Debug.LogWarning($"[PlayerCharacter] Saved equipment ID could not be resolved: {id}", this);
        else if (equipment.Slot != slot)
        {
            Debug.LogWarning($"[PlayerCharacter] Saved equipment has the wrong slot: {id}", this);
            return null;
        }

        return equipment;
    }

    private static string EquipmentId(EquipmentData equipment)
    {
        return equipment == null || string.IsNullOrWhiteSpace(equipment.ItemID)
            ? string.Empty
            : equipment.ItemID.Trim();
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
