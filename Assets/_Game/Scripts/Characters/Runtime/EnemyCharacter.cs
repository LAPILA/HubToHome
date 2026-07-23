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
    public static readonly int HashSkill      = Animator.StringToHash("Skill");
    public static readonly int HashCrossCut   = Animator.StringToHash("CrossCut");

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private CharacterVFX _vfx; 
    private Tween _returnToIdleTween;
    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();
    private IScreenShakeScaleProvider _screenShakeScaleProvider =
        new GameConfigScreenShakeScaleProvider();

    public Sprite BattlePortrait => Data != null && Data.Portrait != null
        ? Data.Portrait
        : (_spriteRenderer != null ? _spriteRenderer.sprite : null);
    public Sprite TurnOrderPortrait => Data != null && Data.TurnOrderPortrait != null
        ? Data.TurnOrderPortrait
        : BattlePortrait;

    [Header("Enemy Data")]
    public EnemyData Data;

    [Header("Animation Mode")]
    [SerializeField] private bool _isBattleMode = true;

    [Header("VFX Settings")]
    [SerializeField] private Color _hurtFlashColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float _flashDuration = 0.08f;
    [SerializeField] private float _shakeStrength = 0.15f;

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

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    public void SetScreenShakeScaleProvider(IScreenShakeScaleProvider provider)
    {
        _screenShakeScaleProvider = provider ?? new GameConfigScreenShakeScaleProvider();
    }

    private Color ResolveFlashColor(Color authoredColor)
    {
        float scale = VisualAccessibilityPolicy.NormalizeScale(
            _screenFlashScaleProvider?.Scale
            ?? GameConfigManager.DefaultFlashIntensity);
        return VisualAccessibilityPolicy.ScaleFlashColor(
            Color.white,
            authoredColor,
            scale);
    }

    private float ResolveShakeScale()
    {
        return VisualAccessibilityPolicy.NormalizeScale(
            _screenShakeScaleProvider?.Scale
            ?? GameConfigManager.DefaultScreenShake);
    }

    public void SetBattleMode(bool active)
    {
        _isBattleMode = active;
        if (_animator == null) _animator = GetComponent<Animator>();

        if (active)
        {
            SetOverworldMoving(Vector2.zero, false);
            ResetTriggerIfExists(HashBattleMove);
            ResetTriggerIfExists(HashAttack);
            ResetTriggerIfExists(HashSkill);
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
            BaseMaxHP = Data.MaxHP;
            BaseATK   = Data.ATK;
            BaseDEF   = Data.DEF;
            BaseSPD   = Data.SPD;
            
            CurrentHP = MaxHP; 
            CurrentMP = MaxMP;
        }
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null || !HasParameter(triggerHash)) return;

        if (!IsAlive && triggerHash != HashDie)
            return;

        if (triggerHash == HashBattleIdle || triggerHash == HashBattleMove || triggerHash == HashBattleMoveBack || triggerHash == HashAttack || triggerHash == HashSkill)
            _isBattleMode = true;

        if (_isBattleMode)
            _animator.SetTrigger(triggerHash);
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

        _isBattleMode = true;

        if (_animator.HasState(0, HashBattleIdle))
        {
            _animator.CrossFade(HashBattleIdle, 0.05f, 0);
        }

        _animator.SetTrigger(HashBattleIdle);
    }

    public void PlayBasicAttackEffect()
    {
        if (_isBattleMode)
            _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
    }

    public void PlaySkillAnim(string triggerName, int fallbackTriggerHash)
    {
        if (_animator == null) return;

        int preferredHash = Animator.StringToHash(triggerName);
        if (HasParameter(preferredHash))
        {
            _isBattleMode = true;
            _animator.SetTrigger(preferredHash);
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
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(ResolveFlashColor(_hurtFlashColor), _flashDuration)
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

        transform.DOKill(false); 
        transform.DOShakePosition(0.2f, _shakeStrength * ResolveShakeScale(), 30, 90f);

        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive)
        {
            PlayBattleAnim(HashHurt);
            _returnToIdleTween?.Kill();
            _returnToIdleTween = DOVirtual.DelayedCall(0.35f, () =>
            {
                if (this != null && isActiveAndEnabled && IsAlive)
                    ForceBattleIdle();
            }).SetId(this);
        }
        else         OnDie();
    }

    protected override void OnDie()
    {
        KillVisualTweens();
        _isBattleMode = true;
        ResetTriggerIfExists(HashBattleIdle);
        ResetTriggerIfExists(HashBattleMove);
        ResetTriggerIfExists(HashBattleMoveBack);
        ResetTriggerIfExists(HashAttack);
        ResetTriggerIfExists(HashSkill);
        ResetTriggerIfExists(HashCrossCut);
        ResetTriggerIfExists(HashHurt);
        if (_animator != null && HasParameter(HashDie))
            _animator.SetTrigger(HashDie);
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0f, 0.8f).SetDelay(0.2f).OnComplete(() => {
                gameObject.SetActive(false); 
            });
        }
    }

    public EnemyAction DecideAction()
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

    // ── 🚨 LINQ 제거 및 최적화된 속성 상성 체크 ──
    public override float GetElementAffinity(DamageElement element)
    {
        float baseAffinity = 1.0f;

        if (Data != null)
        {
            if (Data.EnemyName == "얼음 골렘" && element == DamageElement.Fire)
                baseAffinity = 1.5f; 
            else if (Data.EnemyName == "얼음 골렘" && element == DamageElement.Ice)
                baseAffinity = 0.5f; 
        }

        float effectModifier = 0f;
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            effectModifier += _activeEffects[i].GetElementResistanceModifier(element);
        }
        
        return Mathf.Max(0f, baseAffinity + effectModifier);
    }

    private void KillVisualTweens()
    {
        _returnToIdleTween?.Kill();
        _returnToIdleTween = null;
        if (_spriteRenderer != null) _spriteRenderer.DOKill();
        transform.DOKill(false);
    }
}

public enum EnemyAction { BasicAttack, UseSkill, UseStrongSkill, EnragedAttack, Defend }