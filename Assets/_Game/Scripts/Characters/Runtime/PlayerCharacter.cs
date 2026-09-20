using System.Collections;
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
    private static readonly int HashAttackState = Animator.StringToHash("attack");
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
    [Header("Hit Reaction")]
    [SerializeField, Min(0f)] private float _hitPopHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float _hitPopUpDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _hitPopReturnDuration = 0.16f;
    private Vector3 _hitReactionOrigin;
    private bool _hitReactionActive;

    /// <summary>
    /// PlayerCharacter가 TakeDamage에서 시작한 피격 트윈의 생명주기입니다.
    /// PlayerController의 방어 결과 정리가 이 상태를 기준으로 대기합니다.
    /// </summary>
    public bool IsHitReactionActive => _hitReactionActive;

    private void OnDisable()
    {
        _hitReactionActive = false;
        if (_spriteRenderer != null)
            _spriteRenderer.DOKill();
        transform.DOKill(false);
    }

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
        if (_lastBattleTrigger != 0)
            _animator.ResetTrigger(_lastBattleTrigger);
        _animator.SetTrigger(triggerHash);
        _lastBattleTrigger = triggerHash;
        BattleAnimationVersion++;
    }

    private int _lastBattleTrigger;
    public uint BattleAnimationVersion { get; private set; }

    /// <summary>공격 상태가 끝나거나 다른 상태로 중단될 때까지 기다립니다.</summary>
    public IEnumerator WaitForAttackAnimationComplete(float maxWait = 2f)
    {
        if (_animator == null)
            yield break;

        float startedAt = Time.unscaledTime;
        float deadline = startedAt + Mathf.Max(0.25f, maxWait);
        bool attackStateSeen = false;

        while (Time.unscaledTime < deadline)
        {
            if (this == null || _animator == null || !_animator.isActiveAndEnabled || !IsAlive)
                yield break;
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            bool isTransitioning = _animator.IsInTransition(0);
            AnimatorStateInfo next = isTransitioning
                ? _animator.GetNextAnimatorStateInfo(0)
                : default;
            bool currentIsAttack = current.shortNameHash == HashAttackState || current.shortNameHash == HashAttack;
            bool nextIsAttack = isTransitioning
                && (next.shortNameHash == HashAttackState || next.shortNameHash == HashAttack);

            if (currentIsAttack || nextIsAttack)
                attackStateSeen = true;

            if (attackStateSeen)
            {
                if (!isTransitioning && !currentIsAttack)
                    yield break;

                if (currentIsAttack && !isTransitioning && current.normalizedTime >= 1f)
                    yield break;
            }
            // 이미 끝난 짧은 클립이나 중단된 공격 때문에 최대 2초를 기다리지 않습니다.
            else if (Time.unscaledTime - startedAt >= 0.1f)
                yield break;

            yield return null;
        }
    }

    public void PlayBasicAttackEffect()
    {
        _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
    }

    /// <summary>
    /// 기본 공격 직전 준비 자세를 재생합니다. 기존 캐릭터 Animator에 새 Trigger가
    /// 없더라도 BattleIdle로 안전하게 대체해 타임라인을 중단하지 않습니다.
    /// </summary>
    public void PlayAttackReady()
    {
        if (_animator == null)
            return;

        if (HasParameter(HashBattleReady))
            PlayBattleAnim(HashBattleReady);
        else if (HasParameter(HashBattleIdle))
            PlayBattleAnim(HashBattleIdle);
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
        int savedHp = saveData.HP;
        int savedAp = saveData.AP;
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
        // 성장 초기화는 기본 최대치로 제한하므로, 장비 포함 실제 최대치로 다시 적용합니다.
        SetCurrentHPValue(Mathf.Clamp(savedHp, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(savedAp, 0, MaxAP));
        saveData.HP = CurrentHP;
        saveData.AP = CurrentAP;

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
        int savedHp = saveData.HP;
        int savedAp = saveData.AP;
        CharacterGrowthService.EnsureInitialized(saveData, _characterData);
        if (saveData.HasInitializedEquipment)
            ApplyEquipmentFromSave(saveData);
        SetProgressedBaseStats(CreateProgressedBaseStats(saveData));
        SetCurrentHPValue(Mathf.Clamp(savedHp, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(savedAp, 0, MaxAP));
        // GlobalData의 확정값을 씬에 반영하는 단방향 동기화입니다.
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
        int savedHp = CurrentHP;
        int savedAp = CurrentAP;
        _mySaveDataRef.HP = savedHp;
        _mySaveDataRef.AP = savedAp;
        SaveEquipmentToGlobal(_mySaveDataRef);

        CharacterGrowthService.EnsureInitialized(_mySaveDataRef, _characterData);
        SetProgressedBaseStats(CreateProgressedBaseStats(_mySaveDataRef));
        SetCurrentHPValue(Mathf.Clamp(savedHp, 0, MaxHP));
        SetCurrentAPValue(Mathf.Clamp(savedAp, 0, MaxAP));

        PowerProgressionService.SynchronizeUnlockedSkills(
            _mySaveDataRef,
            _characterData);
        SkillTreeProgressionService.Synchronize(
            _mySaveDataRef,
            _characterData);
        _mySaveDataRef.HP = CurrentHP;
        _mySaveDataRef.AP = CurrentAP;
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

        bool hadHitReaction = _hitReactionActive;
        Vector3 origin = hadHitReaction ? _hitReactionOrigin : transform.position;
        transform.DOKill(false);
        if (!hadHitReaction)
            origin = transform.position;

        _hitReactionOrigin = origin;
        _hitReactionActive = IsAlive;
        transform.position = origin;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            Color restoreColor = _spriteRenderer.color;
            _spriteRenderer.DOColor(ResolveFlashColor(Color.white), 0.05f)
                .SetUpdate(true)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    if (_spriteRenderer != null)
                        _spriteRenderer.color = restoreColor;
                })
                .OnKill(() =>
                {
                    if (_spriteRenderer != null)
                        _spriteRenderer.color = restoreColor;
                });
        }

        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive)
        {
            PlayBattleAnim(HashHurt);

            float popHeight = Mathf.Max(0f, _hitPopHeight) * ResolveShakeScale();
            Sequence pop = DOTween.Sequence().SetRecyclable(false).SetUpdate(true);
            pop.SetTarget(transform);
            pop.Append(transform.DOMoveX(origin.x - Mathf.Min(0.1875f, popHeight), Mathf.Max(0.01f, _hitPopUpDuration))
                .SetEase(Ease.OutQuad));
            pop.Append(transform.DOMoveX(origin.x, Mathf.Max(0.01f, _hitPopReturnDuration))
                .SetEase(Ease.InQuad));
            pop.OnComplete(() => CompleteHitReaction(origin));
            pop.OnKill(() => CompleteHitReaction(origin));
        }
        else
        {
            transform.position = origin;
            _hitReactionActive = false;
        }
    }

    private void CompleteHitReaction(Vector3 origin)
    {
        if (this == null)
            return;

        transform.position = origin;
        _hitReactionActive = false;
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
