using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class EnemyCharacter : CharacterBase
{
    public static readonly int HashAttack     = Animator.StringToHash("Attack");
    public static readonly int HashHurt       = Animator.StringToHash("Hurt");
    public static readonly int HashDie        = Animator.StringToHash("Die");
    public static readonly int HashBattleIdle = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove = Animator.StringToHash("BattleMove");

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private CharacterVFX _vfx; // 🚨 VFX 매니저 캐싱
    private Vector3 _originalLocalPos;

    [Header("Enemy Data")]
    public EnemyData Data;

    [Header("VFX Settings")]
    [SerializeField] private Color _hurtFlashColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float _flashDuration = 0.08f;
    [SerializeField] private float _shakeStrength = 0.15f;

    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _vfx = GetComponent<CharacterVFX>(); // 🚨 컴포넌트 가져오기
        _originalLocalPos = transform.localPosition;

        if (Data != null)
        {
            MaxHP = Data.MaxHP;
            ATK   = Data.ATK;
            DEF   = Data.DEF;
            SPD   = Data.SPD;
            CurrentHP = MaxHP; 
            CurrentMP = MaxMP;
        }

        PlayBattleAnim(HashBattleIdle);
    }

    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator != null && HasParameter(triggerHash))
        {
            _animator.SetTrigger(triggerHash);
        }
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
        // 1. 빨간색 플래시
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(_hurtFlashColor, _flashDuration)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => _spriteRenderer.color = Color.white);
        }

        // 2. 물리적 흔들림
        transform.DOKill(false); 
        transform.DOShakePosition(0.2f, _shakeStrength, 30, 90f);

        // 🚨 3. 피격 이펙트 (Hit_Effect) 재생
        _vfx?.Play(CharacterVFX.VFXAction.Hit_Effect);

        if (IsAlive) PlayBattleAnim(HashHurt);
        else         OnDie();
    }

    protected override void OnDie()
    {
        PlayBattleAnim(HashDie);
        
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0f, 0.8f).SetDelay(0.2f).OnComplete(() => {
                gameObject.SetActive(false); 
            });
        }
    }

    // ── 전투 액션 연출 ──
    public void DoMoveToTarget(Vector3 targetPos, float duration)
    {
        PlayBattleAnim(HashBattleMove);
        transform.DOMove(targetPos, duration).SetEase(Ease.OutQuart);
    }

    public void DoReturnToStart(Vector3 startPos, float duration)
    {
        PlayBattleAnim(HashBattleMove);
        transform.DOMove(startPos, duration).SetEase(Ease.InQuad)
            .OnComplete(() => PlayBattleAnim(HashBattleIdle));
    }

    // 🚨 적 공격 이펙트 재생 함수 추가
    public void ExecuteAttack()
    {
        _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
    }

    // ── AI 행동 및 데이터 ──
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

    public IReadOnlyList<string> GetDrops()
    {
        return Data?.DropItemIDs ?? (IReadOnlyList<string>)System.Array.Empty<string>();
    }
}

public enum EnemyAction { BasicAttack, UseSkill, EnragedAttack, Defend }