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
        CancelHitFeedback();
        EndRapidAttack();
        _hitReactionActive = false;
        if (_spriteRenderer != null)
            _spriteRenderer.DOKill();
        transform.DOKill(false);
    }

    public CharacterData CharacterData => _characterData;
    public CharacterBattleResource BattleResource { get; } = new CharacterBattleResource();
    public Color BattleSpriteTint => _characterData != null && _characterData.BattleSpriteTint.a > 0f
        ? _characterData.BattleSpriteTint : Color.white;
    protected override Color BaseFlashColor => BattleSpriteTint;

    public void RestoreBattleSpriteTint()
    {
        if (_spriteRenderer != null) _spriteRenderer.color = BattleSpriteTint;
    }
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
        if (triggerHash != HashHurt) CancelHitFeedback();
        EndRapidAttack();
        if (_lastBattleTrigger != 0)
            _animator.ResetTrigger(_lastBattleTrigger);
        _animator.SetTrigger(triggerHash);
        if (triggerHash == HashAttack)
            CameraController.Instance?.RequestBattleImpactPreparation();
        _lastBattleTrigger = triggerHash;
        BattleAnimationVersion++;
    }

    private int _lastBattleTrigger;
    public uint BattleAnimationVersion { get; private set; }

    private bool _rapidAttackActive;
    private float _rapidAttackOriginalSpeed;
    private float _rapidAttackCycleSpeed;
    private float _rapidAttackOwnedSpeed;

    // 고속 연격은 Trigger를 쌓지 않고 기존 attack 상태를 재생합니다.
    // 다른 전투 애니메이션(피격/사망 포함)이 요청되면 즉시 원래 속도를 돌려줍니다.
    public void BeginRapidAttack(float cycleDuration)
    {
        CancelHitFeedback();
        EndRapidAttack();
        if (_animator == null || !_animator.isActiveAndEnabled || !IsAlive
            || !_animator.HasState(0, HashAttackState)) return;
        _rapidAttackOriginalSpeed = _animator.speed;
        if (_lastBattleTrigger != 0) _animator.ResetTrigger(_lastBattleTrigger);
        _lastBattleTrigger = 0;
        _animator.Play(HashAttackState, 0, 0f);
        _animator.Update(0f);
        _rapidAttackCycleSpeed = Mathf.Clamp(_animator.GetCurrentAnimatorStateInfo(0).length
            / Mathf.Max(0.05f, cycleDuration), 0.1f, 30f);
        _animator.speed = _rapidAttackOwnedSpeed = _rapidAttackCycleSpeed;
        _rapidAttackActive = true;
        CameraController.Instance?.RequestBattleImpactPreparation();
        BattleAnimationVersion++;
    }

    public void RestartRapidAttack()
    {
        if (!_rapidAttackActive || _animator == null || !IsAlive) return;
        _animator.Play(HashAttackState, 0, 0f);
    }

    public void SetRapidAttackPaused(bool paused)
    {
        if (!_rapidAttackActive || _animator == null) return;
        // 외부 시스템이 속도를 소유한 경우 덮어쓰지 않습니다.
        if (!Mathf.Approximately(_animator.speed, _rapidAttackOwnedSpeed)) return;
        _animator.speed = _rapidAttackOwnedSpeed = paused ? 0f : _rapidAttackCycleSpeed;
    }

    public void EndRapidAttack()
    {
        if (!_rapidAttackActive) return;
        if (_animator != null && Mathf.Approximately(_animator.speed, _rapidAttackOwnedSpeed))
            _animator.speed = _rapidAttackOriginalSpeed;
        _rapidAttackActive = false;
    }

    /// <summary>공격 상태가 끝나거나 다른 상태로 중단될 때까지 기다립니다.</summary>
    public IEnumerator WaitForAttackAnimationComplete(float maxWait = 2f)
    {
        if (_animator == null)
            yield break;

        float startedAt = Time.unscaledTime;
        float deadline = startedAt + Mathf.Max(0.25f, maxWait);
        bool attackStateSeen = false;

        while (Time.unscaledTime < deadline || BattleDelay.IsPaused)
        {
            if (this == null || _animator == null || !_animator.isActiveAndEnabled || !IsAlive)
                yield break;
            if (BattleDelay.IsPaused)
            {
                startedAt += Time.unscaledDeltaTime;
                deadline += Time.unscaledDeltaTime;
                yield return null;
                continue;
            }
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
        AppendEquipmentModifier(modifiers, WeaponSlot, EquipmentSlot.Weapon);
        AppendEquipmentModifier(modifiers, Accessory1Slot, EquipmentSlot.Accessory1);
        AppendEquipmentModifier(modifiers, Accessory2Slot, EquipmentSlot.Accessory2);
        AppendEquipmentModifier(modifiers, HeadSlot, EquipmentSlot.Head);
        AppendEquipmentModifier(modifiers, BodySlot, EquipmentSlot.Body);
        AppendEquipmentModifier(modifiers, ShoesSlot, EquipmentSlot.Shoes);
    }

    private void AppendEquipmentModifier(
        List<StatModifier> modifiers,
        EquipmentData equipment, EquipmentSlot slot)
    {
        if (EquipmentLoadoutService.CanApply(equipment, slot, _characterData != null ? _characterData.CharacterID : null))
            equipment.AppendStatModifiers(modifiers);
    }

    public void SetCharacterData(CharacterData data)
    {
        _characterData = data;
        ApplyCharacterData();
    }

    private void ApplyCharacterData()
    {
        BattleResource.Configure(_characterData != null ? _characterData.BattleResource : null);
        ConfigureElementalStatus(_characterData != null ? _characterData.StatusProfile : null);
        if (_spriteRenderer != null) _spriteRenderer.DOKill(false);
        RestoreBattleSpriteTint();
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
    // 같은 CharacterData를 쓰는 테스트 동료도 저장 인스턴스는 서로 다릅니다.
    public bool IsBoundToSaveData(CharacterSaveData saveData)
        => saveData != null && ReferenceEquals(_mySaveDataRef, saveData);

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

        // 사망자는 회복/부활 경로에서만 되살립니다. 로드와 보상 갱신은 회복이 아닙니다.
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
        // 전투 종료 직전의 오래된 컴포넌트 값으로 공용 레벨업을 덮어쓰지 않습니다.
        if (GlobalDataManager.Instance != null)
        {
            Level = _mySaveDataRef.Level = GlobalDataManager.Instance.PartyLevel;
            EXP = _mySaveDataRef.EXP = GlobalDataManager.Instance.PartyExperience;
        }
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

        EquipmentData equipment = EquipmentLoadoutService.GetEquipped(saveData, slot);
        if (equipment == null)
            Debug.LogWarning($"[PlayerCharacter] 저장 장비를 적용할 수 없습니다 (ID·슬롯·캐릭터 제한 확인): {id}", this);

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

        EndRapidAttack();

        bool hadHitReaction = _hitReactionActive;
        float hitDirection = ResolveHitReactionDirection(-1f);
        Vector3 origin = hadHitReaction ? _hitReactionOrigin : transform.position;
        transform.DOKill(false);
        if (!hadHitReaction)
            origin = transform.position;

        if (_spriteRenderer != null) _spriteRenderer.DOKill();
        float hitPause = BeginHitFeedback(_spriteRenderer, _animator, 0.10f, PlayDamageImpact,
            damage, PlayDamageContact);
        // Begin replaces the old owned reaction, whose OnKill may restore its origin.
        _hitReactionOrigin = origin;
        _hitReactionActive = IsAlive;
        transform.position = origin;

        if (_spriteRenderer != null)
        {
            Color restoreColor = BattleSpriteTint;
            Tween flash = _spriteRenderer.DOColor(ResolveFlashColor(Color.white), 0.05f)
                .SetUpdate(true)
                .SetDelay(hitPause)
                .SetRecyclable(false)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
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
            TrackHitFeedbackTween(flash);
        }

        if (IsAlive)
        {
            float popHeight = Mathf.Max(0f, _hitPopHeight) * ResolveShakeScale();
            Sequence pop = DOTween.Sequence().SetRecyclable(false).SetUpdate(true)
                .SetDelay(hitPause).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            pop.SetTarget(transform);
            pop.Append(transform.DOMoveX(origin.x + hitDirection * Mathf.Min(0.1875f, popHeight),
                    Mathf.Clamp(_hitPopUpDuration, 0.01f, 0.05f)).SetEase(Ease.OutCubic));
            pop.Append(transform.DOMoveX(origin.x, Mathf.Clamp(_hitPopReturnDuration, 0.01f, 0.12f))
                .SetEase(Ease.OutQuad));
            pop.OnComplete(() => CompleteHitReaction(origin));
            pop.OnKill(() => CompleteHitReaction(origin));
            TrackHitFeedbackTween(pop);
        }
        else
        {
            transform.position = origin;
            _hitReactionActive = false;
        }
    }

    private void PlayDamageImpact()
    {
        if (this == null || !isActiveAndEnabled) return;
        if (IsAlive) PlayBattleAnim(HashHurt);
    }

    private void PlayDamageContact() => _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

    private void CompleteHitReaction(Vector3 origin)
    {
        if (this == null)
            return;

        transform.position = origin;
        _hitReactionActive = false;
    }

    protected override void OnDie()
    {
        CancelHitFeedback();
        PlayBattleAnim(HashDie);
    }

    // PlayerController에서 회피/점프 시 호출 (무적 판정)
    public void SetEvasive(bool state)
    {
        IsInvincible = state; 
    }
}
