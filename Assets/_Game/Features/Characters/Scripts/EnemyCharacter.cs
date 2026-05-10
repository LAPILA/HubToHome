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
    public static readonly int HashSkill      = Animator.StringToHash("Skill");

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private CharacterVFX _vfx; 

    [Header("Enemy Data")]
    public EnemyData Data;

    [Header("Animation Mode")]
    [SerializeField] private bool _isBattleMode = true;

    [Header("VFX Settings")]
    [SerializeField] private Color _hurtFlashColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float _flashDuration = 0.08f;
    [SerializeField] private float _shakeStrength = 0.15f;

    public float MercyPercentage { get; private set; } = 0f;

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
        SetBattleMode(true);
        yield return null;
        PlayBattleAnim(HashBattleIdle);
        yield return null;
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
            MercyPercentage = 0f;
        }
    }

    public void AddMercy(float amount)
    {
        MercyPercentage = Mathf.Clamp01(MercyPercentage + amount);
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null || !HasParameter(triggerHash)) return;

        if (triggerHash == HashBattleIdle || triggerHash == HashBattleMove || triggerHash == HashAttack || triggerHash == HashSkill)
            _isBattleMode = true;

        if (_isBattleMode)
            _animator.SetTrigger(triggerHash);
    }

    public void PlayBasicAttackEffect()
    {
        if (_isBattleMode)
            _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
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
            _spriteRenderer.DOColor(_hurtFlashColor, _flashDuration)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    if (_spriteRenderer != null)
                        _spriteRenderer.color = Color.white;
                });
        }

        transform.DOKill(false); 
        transform.DOShakePosition(0.2f, _shakeStrength, 30, 90f);

        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive) PlayBattleAnim(HashHurt);
        else         OnDie();
    }

    protected override void OnDie()
    {
        KillVisualTweens();
        PlayBattleAnim(HashDie);
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
            return EnemyAction.EnragedAttack;

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
        if (_spriteRenderer != null) _spriteRenderer.DOKill();
        transform.DOKill(false);
    }
}

public enum EnemyAction { BasicAttack, UseSkill, EnragedAttack, Defend }