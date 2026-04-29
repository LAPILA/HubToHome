using UnityEngine;

/// <summary>
/// 적 캐릭터. CharacterBase를 상속하며 AI 패턴과 드롭 아이템 데이터를 포함합니다.
/// </summary>
public class EnemyCharacter : CharacterBase
{
    // ── 애니메이터 해시 (PlayerController와 동일한 파라미터 이름 사용) ──
    private static readonly int HashAttack     = Animator.StringToHash("Attack");
    private static readonly int HashHurt       = Animator.StringToHash("Hurt");
    private static readonly int HashDie        = Animator.StringToHash("Die");
    private static readonly int HashBattleIdle = Animator.StringToHash("BattleIdle");

    private Animator _animator;

    [Header("Enemy Data")]
    public EnemyData Data;

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();

        if (Data != null)
        {
            MaxHP     = Data.MaxHP;
            ATK       = Data.ATK;
            DEF       = Data.DEF;
            SPD       = Data.SPD;
            CurrentHP = MaxHP;
        }

        // 전투 시작 시 BattleIdle 상태로 전환
        if (_animator != null) _animator.SetTrigger(HashBattleIdle);
    }

    // ── 애니메이션 ────────────────────────────────────────────
    /// <summary>적 애니메이터 트리거를 재생합니다.</summary>
    public void PlayBattleAnim(int triggerHash)
    {
        if (_animator == null) return;
        _animator.SetTrigger(triggerHash);
    }

    // ── AI 행동 선택 ──────────────────────────────────────────
    /// <summary>
    /// 현재 상태에 따라 다음 행동을 결정합니다.
    /// BattleManager의 EnemyTurn 단계에서 호출됩니다.
    /// </summary>
    public EnemyAction DecideAction()
    {
        if (Data == null) return EnemyAction.BasicAttack;

        // HP 비율에 따른 패턴 전환 (예: 50% 이하 → 강화 패턴)
        float hpRatio = (float)CurrentHP / MaxHP;
        if (hpRatio <= 0.5f && Data.HasEnragedPattern)
            return EnemyAction.EnragedAttack;

        // 스킬 사용 확률 체크
        if (Data.SkillList != null && Data.SkillList.Count > 0)
        {
            if (Random.value < Data.SkillUseChance)
                return EnemyAction.UseSkill;
        }

        return EnemyAction.BasicAttack;
    }

    // ── 드롭 처리 ─────────────────────────────────────────────
    /// <summary>사망 시 드롭 아이템 ID 목록을 반환합니다.</summary>
    public string[] GetDrops()
    {
        if (Data == null || Data.DropItemIDs == null) return System.Array.Empty<string>();
        return Data.DropItemIDs;
    }

    // ── 사망 처리 ─────────────────────────────────────────────
    protected override void OnDie()
    {
        Debug.Log($"[EnemyCharacter] {(Data != null ? Data.EnemyName : name)} defeated.");
        // TODO: BattleManager에 사망 통보, 드롭 처리
    }

    protected override void OnDamageTaken(int damage)
    {
        Debug.Log($"[EnemyCharacter] {(Data != null ? Data.EnemyName : name)} took {damage} damage. HP: {CurrentHP}/{MaxHP}");
        if (IsAlive)
            PlayBattleAnim(HashHurt);
        else
            PlayBattleAnim(HashDie);
    }
}

/// <summary>적의 행동 유형 열거형</summary>
public enum EnemyAction
{
    BasicAttack,
    UseSkill,
    EnragedAttack,
    Defend,
}
