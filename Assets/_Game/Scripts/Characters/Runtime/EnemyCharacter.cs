using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using DG.Tweening;

public class EnemyCharacter : CharacterBase
{
    public static readonly int HashMoveX      = Animator.StringToHash("Horizontal");
    public static readonly int HashMoveY      = Animator.StringToHash("Vertical");
    public static readonly int HashIsMoving   = Animator.StringToHash("IsMoving");
    public static readonly int HashAttack     = Animator.StringToHash("Attack");
    public static readonly int HashHurt       = Animator.StringToHash("Hurt");
    public static readonly int HashDie        = Animator.StringToHash("Die");
    public static readonly int HashBattleIdle = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove = Animator.StringToHash("BattleMove");
    public static readonly int HashBattleMoveBack = Animator.StringToHash("BattleMoveBack");
    public static readonly int HashBattleReady = Animator.StringToHash("BattleReady");
    public static readonly int HashSkill      = Animator.StringToHash("Skill");
    public static readonly int HashCrossCut   = Animator.StringToHash("CrossCut");
    // ZEV 등 레거시 Animator가 사용하는 공격 준비 트리거입니다.
    public static readonly int HashCrossCutReady = Animator.StringToHash("CrossCutReady");
    public static readonly int HashTelegraph  = Animator.StringToHash("Telegraph");

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private CharacterVFX _vfx; 
    private Tween _returnToIdleTween;
    private int _lastBattleTrigger;

    public Sprite BattlePortrait => Data != null && Data.Portrait != null
        ? Data.Portrait
        : (_spriteRenderer != null ? _spriteRenderer.sprite : null);
    public Sprite TurnOrderPortrait => Data != null && Data.TurnOrderPortrait != null
        ? Data.TurnOrderPortrait
        : BattlePortrait;

    [Header("Enemy Data")]
    public EnemyData Data;

    [SerializeField, Min(0f), Tooltip("기본 Attack 모션 시작부터 타격까지의 초. 타임라인 스킬은 방어 블록의 시간을 사용합니다.")]
    private float _basicAttackImpactLeadTime = 0.12f;
    public float BasicAttackImpactLeadTime => Mathf.Max(0f, _basicAttackImpactLeadTime);

    [Header("Animation Mode")]
    [SerializeField] private bool _isBattleMode = true;

    [Header("VFX Settings")]
    [SerializeField] private Color _hurtFlashColor = Color.white;
    [SerializeField] private float _flashDuration = 0.08f;
    [SerializeField] private float _shakeStrength = 0.15f;
    [SerializeField, Min(0f)] private float _hitPopHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float _hitPopUpDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _hitPopReturnDuration = 0.16f;
    private Vector3 _hitReactionOrigin;
    private bool _hitReactionActive;

    private void OnDisable()
    {
        KillVisualTweens();
    }

    private void OnDestroy()
    {
        KillVisualTweens();
    }

    protected override void Awake()
    {
        base.Awake(); 
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _vfx = GetComponent<CharacterVFX>(); 
        if (_isBattleMode) PlayBattleAnim(HashBattleIdle);
    }

    public void SetBattleMode(bool active)
    {
        _isBattleMode = active;
        if (_animator == null) _animator = GetComponent<Animator>();

        if (active)
        {
            SetOverworldMoving(Vector2.zero, false);
            ResetTriggerIfExists(HashBattleMove);
            ResetTriggerIfExists(HashBattleReady);
            ResetTriggerIfExists(HashAttack);
            ResetTriggerIfExists(HashSkill);
            ResetTriggerIfExists(HashCrossCutReady);
            ResetTriggerIfExists(HashTelegraph);
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
            PlayBattleAnim(HashBattleIdle);
        }
        else
        {
            ResetTriggerIfExists(HashBattleIdle);
            ResetTriggerIfExists(HashBattleMove);
            ResetTriggerIfExists(HashBattleReady);
            ResetTriggerIfExists(HashCrossCutReady);
            ResetTriggerIfExists(HashTelegraph);
        }
    }

    public IEnumerator ForceEnterBattleIdleRoutine()
    {
        if (!IsAlive) yield break;
        SetBattleMode(true);
        yield return null;
        if (!IsAlive) yield break;
        PlayBattleAnim(HashBattleIdle);
        yield return null;
        if (!IsAlive) yield break;
        PlayBattleAnim(HashBattleIdle);
    }

    public void SetOverworldMoving(Vector2 direction, bool isMoving)
    {
        if (_animator == null) return;
        if (_isBattleMode) return;
        if (HasParameter(HashMoveX)) _animator.SetFloat(HashMoveX, direction.x);
        if (HasParameter(HashMoveY)) _animator.SetFloat(HashMoveY, direction.y);
        if (HasParameter(HashIsMoving)) _animator.SetBool(HashIsMoving, isMoving);
    }

    public void Setup(EnemyData data)
    {
        Data = data;
        if (Data != null)
        {
            // EnemyData도 Player와 동일하게 BaseStats를 CharacterStats에 주입한다.
            SetBaseStats(Data.BaseStats);
            SetCurrentHPValue(MaxHP);
            SetCurrentAPValue(MaxAP);
        }
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null || !HasParameter(triggerHash)) return;

        if (!IsAlive && triggerHash != HashDie)
            return;

        if (triggerHash == HashBattleIdle || triggerHash == HashBattleMove || triggerHash == HashBattleMoveBack || triggerHash == HashBattleReady || triggerHash == HashCrossCutReady || triggerHash == HashAttack || triggerHash == HashSkill)
            _isBattleMode = true;

        if (_isBattleMode)
            SetBattleTrigger(triggerHash);
    }

    private void SetBattleTrigger(int triggerHash)
    {
        // 이전 피격의 지연 Idle이 새 공격을 끊지 않도록 명령을 교체합니다.
        _returnToIdleTween?.Kill();
        _returnToIdleTween = null;
        if (_lastBattleTrigger != 0)
            _animator.ResetTrigger(_lastBattleTrigger);
        _animator.SetTrigger(triggerHash);
        _lastBattleTrigger = triggerHash;
    }

    public void ForceBattleIdle()
    {
        if (!IsAlive) return;
        if (_animator == null || !HasParameter(HashBattleIdle)) return;

        ResetTriggerIfExists(HashHurt);
        ResetTriggerIfExists(HashAttack);
        ResetTriggerIfExists(HashSkill);
        ResetTriggerIfExists(HashCrossCut);
        ResetTriggerIfExists(HashBattleMove);
        ResetTriggerIfExists(HashBattleMoveBack);
        ResetTriggerIfExists(HashBattleReady);
        ResetTriggerIfExists(HashCrossCutReady);
        ResetTriggerIfExists(HashTelegraph);

        _isBattleMode = true;

        if (_animator.HasState(0, HashBattleIdle))
        {
            _animator.CrossFade(HashBattleIdle, 0.05f, 0);
        }

        SetBattleTrigger(HashBattleIdle);
    }

    public void PlayBasicAttackEffect()
    {
        if (_isBattleMode)
            _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
    }

    /// <summary>
    /// 공격 직전 준비 자세를 한 번 재생합니다. BattleReady가 없는 기존 Animator는
    /// CrossCutReady/Telegraph를 우선 사용하고, 둘 다 없으면 BattleIdle을 유지합니다.
    /// Skill은 실제 공격 애니메이션인 경우가 많아 준비 자세의 대체 트리거로 사용하지 않습니다.
    /// </summary>
    public void PlayAttackReady()
    {
        if (_animator == null)
            return;

        if (HasParameter(HashBattleReady))
            PlayBattleAnim(HashBattleReady);
        else if (HasParameter(HashCrossCutReady))
            PlayBattleAnim(HashCrossCutReady);
        else if (HasParameter(HashTelegraph))
            PlayBattleAnim(HashTelegraph);
        else
            PlayBattleAnim(HashBattleIdle);
    }

    public void PlaySkillAnim(string triggerName, int fallbackTriggerHash)
    {
        if (_animator == null) return;

        int preferredHash = Animator.StringToHash(triggerName);
        if (HasParameter(preferredHash))
        {
            _isBattleMode = true;
            SetBattleTrigger(preferredHash);
            return;
        }

        PlayBattleAnim(fallbackTriggerHash);
    }

    public bool IsEnemyNamed(string enemyName)
    {
        return Data != null && string.Equals(Data.EnemyName, enemyName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ResetTriggerIfExists(int triggerHash)
    {
        if (_animator != null && HasParameter(triggerHash))
            _animator.ResetTrigger(triggerHash);
    }

    private bool HasParameter(int paramHash)
    {
        if (_animator == null) return false;
        foreach (AnimatorControllerParameter param in _animator.parameters)
            if (param.nameHash == paramHash) return true;
        return false;
    }

    protected override void OnDamageTaken(int damage)
    {
        _returnToIdleTween?.Kill();
        _returnToIdleTween = null;

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
            _spriteRenderer.DOColor(ResolveFlashColor(_hurtFlashColor), Mathf.Max(0.01f, _flashDuration))
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
            float popHeight = ResolveHitPopHeight() * ResolveShakeScale();
            Sequence pop = DOTween.Sequence().SetRecyclable(false).SetUpdate(true);
            pop.SetTarget(transform);
            pop.Append(transform.DOMoveY(origin.y + popHeight, Mathf.Max(0.01f, _hitPopUpDuration))
                .SetEase(Ease.OutQuad));
            pop.Append(transform.DOMoveY(origin.y, Mathf.Max(0.01f, _hitPopReturnDuration))
                .SetEase(Ease.InQuad));
            pop.OnComplete(() => CompleteHitReaction(origin));
            pop.OnKill(() => CompleteHitReaction(origin));

            _returnToIdleTween = DOVirtual.DelayedCall(0.35f, () =>
            {
                if (this != null && isActiveAndEnabled && IsAlive)
                    ForceBattleIdle();
            }).SetId(this);
        }
        else
        {
            transform.position = origin;
            _hitReactionActive = false;
            OnDie();
        }
    }

    private void CompleteHitReaction(Vector3 origin)
    {
        if (this == null)
            return;

        transform.position = origin;
        _hitReactionActive = false;
    }

    private float ResolveHitPopHeight()
    {
        return _hitPopHeight > 0f ? _hitPopHeight : Mathf.Max(0f, _shakeStrength);
    }

    protected override void OnDie()
    {
        KillVisualTweens();
        _isBattleMode = true;
        ResetTriggerIfExists(HashBattleIdle);
        ResetTriggerIfExists(HashBattleMove);
        ResetTriggerIfExists(HashBattleMoveBack);
        ResetTriggerIfExists(HashBattleReady);
        ResetTriggerIfExists(HashCrossCutReady);
        ResetTriggerIfExists(HashAttack);
        ResetTriggerIfExists(HashSkill);
        ResetTriggerIfExists(HashCrossCut);
        ResetTriggerIfExists(HashHurt);
        ResetTriggerIfExists(HashTelegraph);
        if (_animator != null && HasParameter(HashDie))
            _animator.SetTrigger(HashDie);
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0f, 0.8f).SetDelay(0.2f).OnComplete(() => {
                gameObject.SetActive(false); 
            });
        }
    }

    public virtual EnemyAction DecideAction()
    {
        if (Data == null) return EnemyAction.BasicAttack;

        float hpRatio = (float)CurrentHP / MaxHP;
        if (hpRatio <= 0.5f && Data.HasEnragedPattern)
        {
            if (Data.StrongSkillList != null && Data.StrongSkillList.Count > 0)
                return EnemyAction.UseStrongSkill;

            if (Data.SkillList != null && Data.SkillList.Count > 0)
                return EnemyAction.UseSkill;

            return EnemyAction.BasicAttack;
        }

        if (Data.StrongSkillList != null && Data.StrongSkillList.Count > 0)
        {
            if (Random.value < Data.StrongSkillUseChance) return EnemyAction.UseStrongSkill;
        }

        if (Data.SkillList != null && Data.SkillList.Count > 0)
        {
            if (Random.value < Data.SkillUseChance) return EnemyAction.UseSkill;
        }

        return EnemyAction.BasicAttack;
    }

    /// <summary>AI가 선택한 행동에 사용할 스킬입니다. 일반 적의 기존 무작위 선택을 유지합니다.</summary>
    public virtual SkillData SelectSkill(EnemyAction action)
    {
        if (Data == null) return null;

        List<SkillData> skills = action == EnemyAction.UseSkill
            ? Data.SkillList
            : action == EnemyAction.UseStrongSkill ? Data.StrongSkillList : null;
        return skills != null && skills.Count > 0
            ? skills[Random.Range(0, skills.Count)]
            : null;
    }

    private void KillVisualTweens()
    {
        _returnToIdleTween?.Kill();
        _returnToIdleTween = null;
        if (_spriteRenderer != null) _spriteRenderer.DOKill();
        transform.DOKill(false);
        _hitReactionActive = false;
    }
}

public enum EnemyAction { BasicAttack, UseSkill, UseStrongSkill, EnragedAttack, Defend, Wait }
