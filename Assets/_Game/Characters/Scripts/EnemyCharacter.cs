using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 적 캐릭터. CharacterBase를 상속하며 AI 패턴 및 DOTween 기반 액션 연출을 포함합니다.
/// </summary>
public class EnemyCharacter : CharacterBase
{
    // ── 애니메이터 해시 ───────────────────────────────────────
    public static readonly int HashAttack     = Animator.StringToHash("Attack");
    public static readonly int HashHurt       = Animator.StringToHash("Hurt");
    public static readonly int HashDie        = Animator.StringToHash("Die");
    public static readonly int HashBattleIdle = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove = Animator.StringToHash("BattleMove");

    // ── 컴포넌트 ─────────────────────────────────────────────
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Vector3 _originalLocalPos;

    [Header("Enemy Data")]
    public EnemyData Data;

    [Header("VFX Settings")]
    [SerializeField] private Color _hurtFlashColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float _flashDuration = 0.08f;
    [SerializeField] private float _shakeStrength = 0.15f;

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalLocalPos = transform.localPosition;

        if (Data != null)
        {
            MaxHP     = Data.MaxHP;
            ATK       = Data.ATK;
            DEF       = Data.DEF;
            SPD       = Data.SPD;
            CurrentHP = MaxHP;
        }

        // 전투 시작 시 기본 대기 상태
        PlayBattleAnim(HashBattleIdle);
    }

    // ── 애니메이션 제어 ───────────────────────────────────────
    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null) return;

        // 해당 파라미터가 애니메이터에 존재하는지 확인 후 트리거 (없으면 무시)
        if (HasParameter(triggerHash))
        {
            _animator.SetTrigger(triggerHash);
        }
    }

    private bool HasParameter(int paramHash)
    {
        if (_animator == null) return false;
        foreach (AnimatorControllerParameter param in _animator.parameters)
        {
            if (param.nameHash == paramHash) return true;
        }
        return false;
    }

    // ── 피격 및 사망 연출 (DOTween) ───────────────────────────
    protected override void OnDamageTaken(int damage)
    {
        // 1. 빨간색 플래시 효과
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOColor(_hurtFlashColor, _flashDuration)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => _spriteRenderer.color = Color.white);
        }

        // 2. 피격 흔들림 (Hurt 애니메이션 유무와 상관없이 물리적 움찔거림 추가)
        transform.DOKill();
        transform.DOShakePosition(0.2f, _shakeStrength, 30, 90f);

        // 3. 상태에 따른 애니메이션 트리거
        if (IsAlive)
            PlayBattleAnim(HashHurt);
        else
            OnDie();
    }

    protected override void OnDie()
    {
        PlayBattleAnim(HashDie);
        
        // 사망 시 서서히 투명해지며 사라지는 연출
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0f, 0.8f).SetDelay(0.2f).OnComplete(() => {
                // TODO: BattleManager에 사망 보고 후 처리
            });
        }
    }

    // ── 전투 액션 연출 (BattleManager에서 호출) ──────────────────

    /// <summary>근거리 돌격: 타겟 앞까지 빠르게 이동</summary>
    public void DoMoveToTarget(Vector3 targetPos, float duration)
    {
        PlayBattleAnim(HashBattleMove);
        transform.DOMove(targetPos, duration).SetEase(Ease.OutQuart);
    }

    /// <summary>원래 위치로 복귀</summary>
    public void DoReturnToStart(Vector3 startPos, float duration)
    {
        PlayBattleAnim(HashBattleMove);
        transform.DOMove(startPos, duration).SetEase(Ease.InQuad)
            .OnComplete(() => PlayBattleAnim(HashBattleIdle));
    }

    // ── AI 행동 선택 ──────────────────────────────────────────
    public EnemyAction DecideAction()
    {
        if (Data == null) return EnemyAction.BasicAttack;

        float hpRatio = (float)CurrentHP / MaxHP;
        if (hpRatio <= 0.5f && Data.HasEnragedPattern)
            return EnemyAction.EnragedAttack;

        if (Data.SkillList != null && Data.SkillList.Count > 0)
        {
            if (Random.value < Data.SkillUseChance)
                return EnemyAction.UseSkill;
        }

        return EnemyAction.BasicAttack;
    }

    public string[] GetDrops()
    {
        if (Data == null || Data.DropItemIDs == null) return System.Array.Empty<string>();
        return Data.DropItemIDs;
    }

    
}

public enum EnemyAction
{
    BasicAttack,
    UseSkill,
    EnragedAttack,
    Defend,
}
