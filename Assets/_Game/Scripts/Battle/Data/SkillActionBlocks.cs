using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public enum DefenseRequirement
{
    [InspectorName("가드 / 회피")]
    ParryOrDodge = 0,
    [InspectorName("구 점프 전용 (새 모드: 회피)")]
    JumpOnly = 1,
    [InspectorName("일반 공격 (가드 / 회피)")]
    Any = 2,
    [InspectorName("구 패링 전용 (새 모드: 가드 / 회피)")]
    ParryOnly = 3,
    [InspectorName("회피만")]
    DodgeOnly = 4,
    [InspectorName("구 회피 / 점프 (새 모드: 회피)")]
    DodgeOrJump = 5,
    [InspectorName("반격 가능 특수공격 (회피 / 연계 반격)")]
    Counterable = 6
}
public enum TelegraphVisualMode
{
    [InspectorName("Sprite")]
    Sprite,
    [InspectorName("Animator Trigger")]
    AnimatorTrigger,
    [InspectorName("Prefab VFX")]
    PrefabVFX
}

public enum EnemyDefensePatternMode
{
    [InspectorName("즉시 판정 (기존 호환)")]
    ImmediateReaction = 0,
    [InspectorName("전조 후 판정")]
    TelegraphThenWindow = 1,
    [InspectorName("이번 턴 전조만")]
    TelegraphThenNextTurnWindow = 2
}

// ═══════════════════════════════════════════════════════════════
// ── 1. 공통 컨텍스트 및 베이스 클래스 ──
// ═══════════════════════════════════════════════════════════════

public class SkillContext
{
    public CharacterBase Actor;
    public List<CharacterBase> Targets;
    
    public float CurrentDamageMultiplier = 1.0f;
    public bool IsPerfectQTE = false;
    public bool StopTimelineExecution = false;
    public System.Func<bool> IsExecutionActive;
    public BattleLinkCounterService LinkCounterService;
    // 적 타임라인에서 현재 타격의 준비/충돌 트리거가 이미 시작됐는지 표시합니다.
    // 방어창·애니메이션·투사체 블록이 같은 공격에 준비 자세를 중복 재생하지 않게 합니다.
    public bool EnemyAttackReadyPresented;
    public bool EnemyAttackImpactStarted;
    // 방어 판정의 정리 시간은 충돌 전에 적용하면 공격 애니메이션과 피해가 벌어집니다.
    // 다음 실제 충돌 소비자가 피해/상태를 적용한 뒤 이 값을 소비합니다.
    public float PendingDefensePostImpactDelay;
    // 방어 결과 모션은 피해 적용과 같은 프레임에 시작하지만, 전투 턴 정리에서
    // Idle로 되돌리기 전에 모션이 끝날 때까지 한 번 소비해야 합니다.
    public PlayerController PendingDefenseReactionController;
    public DefenseInput PendingDefenseReactionInput = DefenseInput.None;
    public bool PendingDefenseReactionTookDamage;
    /// <summary>
    /// 플레이어 스킬에서 실행 중인 실시간 입력 QTE입니다. QTE 자체는 애니메이션과
    /// 병행하고, 피해/상태 적용 지점에서만 결과를 기다립니다.
    /// </summary>
    public QteExecution ActiveSkillQte;
    public QteExecution ActiveDefenseWindow;
    // 성공한 반격은 실행 오류/취소와 구분해 적의 턴을 정상 종료합니다.
    public bool AttackInterruptedByCounter;
    // 비행/연쇄 공격은 방어창을 먼저 끝내지 않고 실제 충돌 소비자가 함께 실행합니다.
    private Action_DefenseWindow _deferredDefenseWindow;
    private BattleDefenderPresentationScope _defenderPresentation;
    private bool? _hasEnemyImpact;

    public IEnumerator StageDefender(PlayerCharacter target)
    {
        if (!(Actor is EnemyCharacter) || target == null || !target.IsAlive) yield break;
        if (_defenderPresentation != null && _defenderPresentation.Player == target) yield break;
        yield return ReturnDefender();
        if (!CanContinueExecution || target == null || !target.IsAlive) yield break;
        _defenderPresentation = new BattleDefenderPresentationScope(target, IsExecutionActive);
        yield return _defenderPresentation.Enter();
        if (_defenderPresentation == null || !_defenderPresentation.IsStaged)
            StopTimelineExecution = true;
    }

    public IEnumerator ReturnDefender()
    {
        BattleDefenderPresentationScope scope = _defenderPresentation;
        if (scope == null) yield break;
        try { yield return scope.Return(); }
        finally
        {
            scope.Dispose();
            if (ReferenceEquals(_defenderPresentation, scope)) _defenderPresentation = null;
        }
    }

    public Vector3? DefenderReturnPosition => _defenderPresentation?.HomePosition;

    public void ReleaseDefenderPresentation()
    {
        _defenderPresentation?.Dispose();
        _defenderPresentation = null;
    }

    public IEnumerator ExecuteBlock(SkillActionBlock block, IReadOnlyList<SkillActionBlock> timeline)
    {
        if (Actor is EnemyCharacter)
        {
            if (!_hasEnemyImpact.HasValue)
            {
                _hasEnemyImpact = false;
                for (int i = 0; timeline != null && i < timeline.Count; i++)
                {
                    SkillActionBlock candidate = timeline[i];
                    if (candidate != null && !candidate.Disabled
                        && (candidate is Action_Damage || candidate is Action_Projectile))
                    { _hasEnemyImpact = true; break; }
                }
            }
            // 이동/투사체가 목표 좌표를 잡기 전에 전진을 완료합니다. 연쇄 공격은 각 대상마다 처리합니다.
            if (_hasEnemyImpact == true && !(block is Action_SequentialMelee))
                yield return StageDefender(MainTarget as PlayerCharacter);
            if (!CanContinueExecution) yield break;
        }
        if (Actor is EnemyCharacter && block is Action_DefenseWindow defense
            && HasSynchronizedImpact(defense, timeline))
        {
            _deferredDefenseWindow = defense;
            yield break;
        }
        yield return block.Execute(this);
    }

    public static bool HasSynchronizedImpact(Action_DefenseWindow defense, IReadOnlyList<SkillActionBlock> timeline)
    {
        if (defense.PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow) return false;
        bool found = false;
        for (int i = 0; timeline != null && i < timeline.Count; i++)
        {
            SkillActionBlock candidate = timeline[i];
            if (!found) { found = ReferenceEquals(candidate, defense); continue; }
            if (candidate == null || candidate.Disabled) continue;
            return candidate is Action_Projectile || candidate is Action_SequentialMelee;
        }
        return false;
    }

    public Action_DefenseWindow TakeImpactDefense(float duration, string attackTrigger = null, bool flight = false)
    {
        Action_DefenseWindow source = _deferredDefenseWindow;
        _deferredDefenseWindow = null;
        source ??= new Action_DefenseWindow
        {
            UseTelegraph = false, DelayAfter = 0f,
            TimeWindow = Mathf.Max(0.3f, duration), AttackAnimationLeadTime = 0.12f
        };
        Action_DefenseWindow copy = source.CopyForImpact(flight ? duration : source.TimeWindow, attackTrigger);
        if (!flight) copy.AttackAnimationLeadTime = source.AttackAnimationLeadTime;
        return copy;
    }

    public bool CanContinueExecution => !StopTimelineExecution && !AttackInterruptedByCounter
        && Actor != null
        && Actor.IsAlive
        && (IsExecutionActive == null || IsExecutionActive());

    public CharacterBase MainTarget => Targets != null && Targets.Count > 0 ? Targets[0] : null;

    public bool TryPresentEnemyAttackReady(EnemyCharacter enemy)
    {
        if (enemy == null || EnemyAttackReadyPresented || EnemyAttackImpactStarted)
            return false;

        enemy.PlayAttackReady();
        EnemyAttackReadyPresented = true;
        return true;
    }

    public void MarkEnemyAttackImpactStarted()
    {
        EnemyAttackImpactStarted = true;
    }

    public void ResetEnemyAttackPresentation()
    {
        EnemyAttackReadyPresented = false;
        EnemyAttackImpactStarted = false;
    }

    /// <summary>
    /// 피해/상태 블록만 있는 레거시 적 스킬도 동일한 준비 → 공격 계약을 지킵니다.
    /// 방어창이나 명시적인 애니메이션 블록이 먼저 충돌을 시작한 경우에는
    /// 중복 트리거를 보내지 않습니다.
    /// </summary>
    public IEnumerator EnsureEnemyAttackImpact(string triggerName = null)
    {
        if (!(Actor is EnemyCharacter enemy) || EnemyAttackImpactStarted)
            yield break;

        if (TryPresentEnemyAttackReady(enemy))
            yield return new WaitForSecondsRealtime(0.08f);

        if (!string.IsNullOrWhiteSpace(triggerName))
            enemy.PlaySkillAnim(triggerName, EnemyCharacter.HashAttack);
        else
        {
            enemy.PlayBasicAttackEffect();
            enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
        }

        MarkEnemyAttackImpactStarted();
    }

    /// <summary>
    /// 타임라인이 중단될 때 입력창과 방어 리액션을 동기적으로 복구합니다.
    /// 정상 완료 경로에서는 WaitForPendingDefenseReaction이 먼저 필드를 소비하므로
    /// 이 메서드는 취소/씬 전환 안전망으로만 동작합니다.
    /// </summary>
    public void CancelPendingDefenseReaction()
    {
        QteExecution defense = ActiveDefenseWindow;
        ActiveDefenseWindow = null;
        if (defense != null && !defense.IsDone) QTEManager.Instance?.Cancel(defense);
        PlayerController controller = PendingDefenseReactionController;
        PendingDefenseReactionController = null;
        PendingDefenseReactionInput = DefenseInput.None;
        PendingDefenseReactionTookDamage = false;
        PendingDefensePostImpactDelay = 0f;
        if (controller != null)
            controller.ResetDefenseReactionLock();
        ReleaseDefenderPresentation();
    }

    public void QueueDefensePostImpactDelay(float delay)
    {
        PendingDefensePostImpactDelay += Mathf.Max(0f, delay);
    }

    public void QueueDefenseReaction(
        PlayerController controller,
        DefenseInput input,
        bool tookDamage)
    {
        if (controller == null)
            return;

        PendingDefenseReactionController = controller;
        PendingDefenseReactionInput = input;
        PendingDefenseReactionTookDamage = tookDamage;
    }

    public IEnumerator WaitForPendingDefenseReaction()
    {
        PlayerController controller = PendingDefenseReactionController;
        DefenseInput input = PendingDefenseReactionInput;
        bool tookDamage = PendingDefenseReactionTookDamage;

        PendingDefenseReactionController = null;
        PendingDefenseReactionInput = DefenseInput.None;
        PendingDefenseReactionTookDamage = false;

        if (controller == null)
            yield break;

        yield return controller.WaitForDefenseReactionComplete(input, tookDamage);
        if (controller != null)
            controller.ResetDefenseReactionLock();
    }

    public IEnumerator WaitForPendingDefensePostImpactDelay()
    {
        float delay = PendingDefensePostImpactDelay;
        PendingDefensePostImpactDelay = 0f;
        if (delay <= 0f || !CanContinueExecution)
            yield break;

        yield return new WaitForSecondsRealtime(delay);
    }

    public IEnumerator WaitForActiveSkillQte()
    {
        QteExecution execution = ActiveSkillQte;
        if (execution == null)
            yield break;

        while (!execution.IsDone && CanContinueExecution)
            yield return null;

        if (!execution.IsDone)
        {
            QTEManager.Instance?.Cancel(execution);
            StopTimelineExecution = true;
            yield break;
        }

        if (execution.Termination != QteTermination.Completed)
            StopTimelineExecution = true;
    }
}

[System.Serializable]
public abstract class SkillActionBlock
{
    [HideInInspector] public string BlockName => GetReadableBlockName();

    [PropertyOrder(-50)]
    [LabelText("사용")]
    [ShowInInspector]
    public bool Enabled
    {
        get { return !Disabled; }
        set { Disabled = !value; }
    }

    [HideInInspector]
    public bool Disabled;

    [PropertyOrder(-49)]
    [LabelText("디자이너 라벨")]
    public string DesignerLabel = string.Empty;

    [PropertyOrder(-48)]
    [TextArea(1, 3)]
    [LabelText("메모")]
    public string Note = string.Empty;

    [PropertyOrder(-60)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("블록 요약")]
    public string BlockHeader
    {
        get
        {
            string label = string.IsNullOrWhiteSpace(DesignerLabel) ? GetReadableBlockName() : DesignerLabel.Trim();
            return (Enabled ? string.Empty : "[비활성] ") + "[" + GetBlockCategoryKo() + "] " + label;
        }
    }

    public abstract IEnumerator Execute(SkillContext context);

    public virtual SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Unsupported(GetBlockCategoryKo());
    }

    private string GetReadableBlockName()
    {
        if (this is Action_Wait) return "대기";
        if (this is Action_Move) return "이동";
        if (this is Action_PlayAnim) return "애니메이션";
        if (this is Action_Damage) return "데미지";
        if (this is Action_ApplyStatus) return "상태이상";
        if (this is Action_QTE) return "QTE";
        if (this is Action_VFX) return "VFX";
        if (this is Action_DefenseWindow) return "방어 대응";
        if (this is Action_Projectile) return "투사체";
        if (this is Action_SequentialMelee) return "연쇄 근접";
        return GetType().Name.Replace("Action_", string.Empty);
    }

    private string GetBlockCategoryKo()
    {
        if (this is Action_Wait) return "흐름";
        if (this is Action_Move) return "이동";
        if (this is Action_PlayAnim) return "애니메이션";
        if (this is Action_Damage || this is Action_Projectile || this is Action_SequentialMelee) return "데미지";
        if (this is Action_VFX) return "VFX";
        if (this is Action_QTE) return "QTE";
        if (this is Action_DefenseWindow) return "방어";
        if (this is Action_ApplyStatus) return "상태이상";
        return "기타";
    }

    protected static void PlayActorBattleAnim(CharacterBase actor, int triggerHash)
    {
        switch (actor)
        {
            case PlayerCharacter player:
                player.PlayBattleAnim(triggerHash);
                break;
            case EnemyCharacter enemy:
                enemy.PlayBattleAnim(triggerHash);
                break;
        }
    }

    protected static Vector3 GetActorDefaultBattlePos(CharacterBase actor)
    {
        var pm = PositionManager.Instance;
        if (pm == null || actor == null) return actor != null ? actor.transform.position : Vector3.zero;

        if (actor is PlayerCharacter player)
        {
            int idx = BattleManager.Instance != null ? BattleManager.Instance._playerParty.IndexOf(player) : -1;
            return idx >= 0 ? pm.GetPlayerDefaultPos(idx) : actor.transform.position;
        }

        if (actor is EnemyCharacter enemy)
        {
            int idx = BattleManager.Instance != null ? BattleManager.Instance._enemies.IndexOf(enemy) : -1;
            return idx >= 0 ? pm.GetEnemyDefaultPos(idx) : actor.transform.position;
        }

        return actor.transform.position;
    }

    protected static PlayerController GetPlayerController(CharacterBase actor)
    {
        return actor != null ? actor.GetComponent<PlayerController>() : null;
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 2. 실제 조립할 액션 블록들 ──
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
[TypeInfoBox("지정된 시간 동안 대기합니다.")]
public class Action_Wait : SkillActionBlock
{
    [LabelText("대기 시간 (초)")] public float WaitTime = 0.5f;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("흐름", WaitTime);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        yield return new WaitForSeconds(WaitTime);
    }
}

[System.Serializable]
[TypeInfoBox("캐릭터를 특정 위치로 부드럽게 이동시킵니다.")]
public class Action_Move : SkillActionBlock
{
    public enum MoveDest
    {
        TargetFront = 0,
        TargetBack = 1,
        TargetTop = 2,
        Center = 3,
        OriginalPos = 4,
        [InspectorName("타겟 앞 자동 공격 위치")]
        AttackStaging = 5
    }
    
    [LabelText("목적지")] public MoveDest Destination;
    [LabelText("이동 시간")] public float Duration = 0.2f;
    [LabelText("이동 방식")] public Ease MoveEase = Ease.OutQuad;
    [LabelText("적 이동 높이 (0 = 지상)"), MinValue(0)] public float EnemyHopHeight;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("이동", Duration);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }
        var pm = PositionManager.Instance;
        Vector3 targetPos = context.Actor.transform.position; 
        var mainTarget = context.MainTarget;

        if (mainTarget != null && Destination == MoveDest.AttackStaging)
        {
            targetPos = pm != null
                ? pm.GetAttackStagingPos(context.Actor, mainTarget)
                : mainTarget.GetPivot(CharacterPivotId.Front).position;
        }
        else if (mainTarget != null && (Destination == MoveDest.TargetFront || Destination == MoveDest.TargetBack || Destination == MoveDest.TargetTop))
        {
            switch (Destination)
            {
                case MoveDest.TargetFront: targetPos = mainTarget.GetPivot(CharacterPivotId.Front).position; break;
                case MoveDest.TargetBack:  targetPos = mainTarget.GetPivot(CharacterPivotId.Back).position; break;
                case MoveDest.TargetTop:   targetPos = mainTarget.GetPivot(CharacterPivotId.Top).position; break;
            }
        }
        else if (Destination == MoveDest.Center && pm != null)
            targetPos = context.Actor is EnemyCharacter
                ? PositionManager.HorizontalApproach(context.Actor, pm.GetCenterPos()) : pm.GetCenterPos();
        else if (Destination == MoveDest.OriginalPos)
            targetPos = GetActorDefaultBattlePos(context.Actor);

        PlayActorBattleAnim(context.Actor, context.Actor is EnemyCharacter ? EnemyCharacter.HashBattleMove : PlayerCharacter.HashBattleMove);

        var ghostTrail = context.Actor.GetComponentInChildren<CharacterGhostTrail>();
        Tween movement = null;
        Vector3 startPosition = context.Actor.transform.position;
        bool completed = false;
        try
        {
            if (ghostTrail != null) ghostTrail.SetTrailActive(true);
            movement = context.Actor is EnemyCharacter && EnemyHopHeight > 0f
                ? context.Actor.transform.DOJump(targetPos, EnemyHopHeight, 1, Duration)
                : context.Actor.transform.DOMove(targetPos, Duration);
            movement.SetEase(MoveEase).SetRecyclable(false).SetAutoKill(false);
            while (context.CanContinueExecution && movement.IsActive() && !movement.IsComplete())
                yield return null;

            if (!context.CanContinueExecution)
            {
                context.StopTimelineExecution = true;
                yield break;
            }
            completed = movement.IsActive() && movement.IsComplete();
            PlayActorBattleAnim(context.Actor, context.Actor is EnemyCharacter ? EnemyCharacter.HashBattleIdle : PlayerCharacter.HashBattleIdle);
        }
        finally
        {
            movement?.Kill();
            if (!completed && context.Actor != null)
                context.Actor.transform.position = startPosition;
            if (ghostTrail != null) ghostTrail.SetTrailActive(false);
        }
    }
}

[System.Serializable]
[TypeInfoBox("특정 애니메이션을 재생합니다.")]
public class Action_PlayAnim : SkillActionBlock
{
    private const float EnemyAttackReadyDuration = 0.08f;

    [LabelText("애니메이션 이름 (Trigger)")] public string AnimTriggerName = "Attack";
    [LabelText("애니메이션 후 대기시간")]
    [InfoBox("애니메이션과 VFX를 같은 박자에 맞추고 싶으면 0으로 두고, 필요한 경우에만 Wait 블록 또는 DelayAfter를 사용하세요.")]
    public float DelayAfter = 0f;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("애니메이션", DelayAfter);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        bool isEnemyAttack = context?.Actor is EnemyCharacter
            && IsEnemyAttackAnimation(AnimTriggerName);
        // DefenseWindow의 결과 콜백이 이미 공격 트리거를 시작한 뒤 이어지는
        // 레거시 PlayAnim Attack은 같은 타격을 두 번 재생하지 않습니다.
        if (isEnemyAttack && context.EnemyAttackImpactStarted)
        {
            float duplicateDelay = Mathf.Max(0f, DelayAfter);
            if (duplicateDelay > 0f)
                yield return new WaitForSecondsRealtime(duplicateDelay);
            yield break;
        }

        // 방어 대응 블록이 없는 레거시 적 스킬도 공격 전에 같은 준비 자세를
        // 거칩니다. 이미 BattleReady/Telegraph 자체를 재생하는 블록은 제외합니다.
        if (isEnemyAttack && context.Actor is EnemyCharacter enemy)
        {
            if (!context.EnemyAttackImpactStarted)
            {
                if (context.TryPresentEnemyAttackReady(enemy))
                    yield return new WaitForSecondsRealtime(EnemyAttackReadyDuration);
            }
        }

        int hash = Animator.StringToHash(AnimTriggerName);
        PlayActorBattleAnim(context.Actor, hash);
        float authoredDelay = Mathf.Max(0f, DelayAfter);
        if (context?.Actor is EnemyCharacter enemyActor)
        {
            if (IsEnemyReadyAnimation(AnimTriggerName))
            {
                context.EnemyAttackReadyPresented = true;
                // 명시적으로 준비 트리거를 작성한 타임라인도 최소 준비 시간이
                // 보장되어야 다음 Attack 트리거가 같은 프레임에 덮어쓰지 않습니다.
                float remainingReadyHold = Mathf.Max(0f, EnemyAttackReadyDuration - authoredDelay);
                if (remainingReadyHold > 0f)
                    yield return new WaitForSecondsRealtime(remainingReadyHold);
            }
            else if (IsEnemyAttackAnimation(AnimTriggerName))
                context.MarkEnemyAttackImpactStarted();
        }
        if (authoredDelay > 0f) yield return new WaitForSecondsRealtime(authoredDelay);
    }

    private static bool IsEnemyAttackAnimation(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return false;

        string normalized = triggerName.Trim().ToLowerInvariant();
        return normalized != "battleidle"
            && normalized != "battlemove"
            && normalized != "battlemoveback"
            && normalized != "hurt"
            && normalized != "die"
            && normalized != "victory"
            && !normalized.Contains("ready")
            && !normalized.Contains("telegraph");
    }

    private static bool IsEnemyReadyAnimation(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return false;

        string normalized = triggerName.Trim().ToLowerInvariant();
        return normalized.Contains("ready") || normalized.Contains("telegraph");
    }
}

[System.Serializable]
[TypeInfoBox("데미지를 입힙니다. 이전 QTE 블록의 배율이 적용됩니다.")]
public class Action_Damage : SkillActionBlock
{
    [LabelText("기본 스킬 배율")] public float SkillMultiplier = 1.0f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;
    [LabelText("카메라 흔들림")] public bool ShakeCamera = true;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("피해", 0f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        // 애니메이션/VFX는 QTE와 병행하고, 실제 피해 적용 직전에만 결과를 확정합니다.
        yield return context.WaitForActiveSkillQte();
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }

        // 피해 블록이 공격 애니메이션 없이 단독으로 남은 레거시 데이터도
        // 준비 자세 → 공격 트리거 → 피해 순서를 보장합니다.
        yield return context.EnsureEnemyAttackImpact();
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }

        float finalMultiplier = SkillMultiplier * context.CurrentDamageMultiplier;
        if (finalMultiplier <= 0f)
        {
            context.CurrentDamageMultiplier = 1.0f;
            context.IsPerfectQTE = false;
            context.ResetEnemyAttackPresentation();
            yield return context.WaitForPendingDefenseReaction();
            yield return context.WaitForPendingDefensePostImpactDelay();
            yield break;
        }

        int finalDamage = Mathf.RoundToInt(context.Actor.ATK * finalMultiplier);

        foreach (var target in context.Targets)
        {
            if (!context.CanContinueExecution)
            {
                context.StopTimelineExecution = true;
                yield break;
            }
            if (target == null || !target.IsAlive) continue;
            
            int previousHp = target.CurrentHP;
            DamageResult damageResult = target.TakeDamage(finalDamage, Element, context.Actor);
            int dmg = damageResult.FinalDamage;
            BattleManager.Instance.InvokeDamageEvent(context.Actor, target, dmg, context.IsPerfectQTE, previousHp);
            
            if (ShakeCamera) 
                CameraController.Instance?.PlayHeavySlam(Vector3.right, context.IsPerfectQTE ? 1.2f : 0.6f, true);
        }
        
        context.CurrentDamageMultiplier = 1.0f; 
        context.IsPerfectQTE = false;
        context.ResetEnemyAttackPresentation();
        yield return context.WaitForPendingDefenseReaction();
        yield return context.WaitForPendingDefensePostImpactDelay();
        
        yield break;
    }
}

// 🚨 [새로 추가됨] 상태이상 부여 액션 (수면 마법 등)
[System.Serializable]
[TypeInfoBox("지정된 대상에게 상태이상을 부여합니다.")]
public class Action_ApplyStatus : SkillActionBlock
{
    [LabelText("부여할 상태이상 ID")] public string StatusID = "Sleep";
    [LabelText("지속 턴 수")] public int DurationTurns = 2;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("상태이상", 0f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        yield return context.WaitForActiveSkillQte();
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }

        // 상태만 부여하는 적 스킬도 애니메이션 없이 피해/효과가 튀지 않도록
        // 동일한 공격 준비 계약을 적용합니다. 일반적으로는 앞선 PlayAnim이
        // 이미 시작했으므로 이 호출은 아무 작업도 하지 않습니다.
        yield return context.EnsureEnemyAttackImpact();
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }

        foreach (var target in context.Targets)
        {
            if (!target.IsAlive) continue;

            if (StatusEffectFactory.TryCreate(StatusID, DurationTurns, out StatusEffect effect))
            {
                target.TryApplyStatusEffect(effect);
            }
            else
            {
                Debug.LogWarning($"[Action_ApplyStatus] 등록되지 않은 상태이상 ID입니다: {StatusID}");
            }
        }
        yield return context.WaitForPendingDefenseReaction();
        context.ResetEnemyAttackPresentation();
        yield return context.WaitForPendingDefensePostImpactDelay();
        yield break;
    }
}

[System.Serializable]
[TypeInfoBox("플레이어 스킬 전용 실시간 QTE입니다. 스킬 애니메이션/이동과 동시에 진행되고, 적 스킬에서는 무시됩니다.")]
public class Action_QTE : SkillActionBlock
{
    [LabelText("제한 시간")]
    public float TimeLimit = 1.0f;
    [LabelText("성공 배율")]
    public float SuccessMultiplier = 1.5f;
    [LabelText("실패 배율")]
    public float FailMultiplier = 0.5f;
    
    [ListDrawerSettings(ShowIndexLabels = true)]
    [LabelText("QTE 노드")]
    public List<SkillQTENode> Nodes = new List<SkillQTENode>();

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("QTE", Mathf.Max(0f, TimeLimit) + 0.2f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        // QTE는 플레이어가 직접 조작하는 스킬에만 존재합니다. 적 타임라인에
        // 잘못 들어간 블록은 UI를 열지 않고 즉시 건너뛰어 전투 흐름을 보호합니다.
        if (!(context?.Actor is PlayerCharacter))
            yield break;

        if (Nodes == null || Nodes.Count == 0 || QTEManager.Instance == null)
            yield break;

        // 하나의 스킬에서 QTE를 중첩 실행하면 이전 입력 세션이 취소됩니다.
        // 앞선 QTE가 있으면 결과를 먼저 소비한 뒤 새 QTE를 시작합니다.
        yield return context.WaitForActiveSkillQte();
        if (!context.CanContinueExecution)
            yield break;

        int successCount = 0;
        QteExecution execution = null;
        execution = QTEManager.Instance.StartSequenceQTEWithResult(
            Nodes,
            TimeLimit,
            (success, total) =>
            {
                successCount = success;
                int safeTotal = Mathf.Max(1, total);
                float ratio = Mathf.Clamp01((float)success / safeTotal);
                context.CurrentDamageMultiplier = Mathf.Lerp(FailMultiplier, SuccessMultiplier, ratio);
                context.IsPerfectQTE = success >= safeTotal;
            });

        context.ActiveSkillQte = execution;
        // 다음 블록(대개 이동/공격)이 같은 프레임에 시작되도록 합니다.
        // 결과 배율은 피해 블록 직전에 WaitForActiveSkillQte()가 소비합니다.
        yield return null;

        if (execution == null || execution.Termination == QteTermination.Failed)
            context.StopTimelineExecution = true;
    }
}



[System.Serializable]
[TypeInfoBox("이펙트(VFX)를 재생합니다. ObjectPoolManager를 지원합니다.")]
public class Action_VFX : SkillActionBlock
{
    public enum VfxPivot { ActorCenter, ActorFront, TargetCenter, TargetBottom, TargetTop }
    
    [AssetsOnly, Required, LabelText("VFX 프리팹")] public GameObject VfxPrefab;
    [LabelText("소환 위치")] public VfxPivot Pivot;
    [LabelText("Actor 회전 사용")] public bool UseActorRotation = false;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("VFX", 0f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (VfxPrefab == null) yield break;

        Vector3 spawnPos = context.Actor.transform.position;
        var target = context.MainTarget;

        switch (Pivot)
        {
            case VfxPivot.ActorCenter:  spawnPos = context.Actor.GetPivot(CharacterPivotId.Center).position; break;
            case VfxPivot.ActorFront:   spawnPos = context.Actor.GetPivot(CharacterPivotId.Front).position; break;
            case VfxPivot.TargetCenter: if (target != null) spawnPos = target.GetPivot(CharacterPivotId.Center).position; break;
            case VfxPivot.TargetBottom: if (target != null) spawnPos = target.GetPivot(CharacterPivotId.Bottom).position; break;
            case VfxPivot.TargetTop:    if (target != null) spawnPos = target.GetPivot(CharacterPivotId.Top).position; break;
        }

        Quaternion rotation = UseActorRotation ? context.Actor.transform.rotation : Quaternion.identity;

        // 🚨 GameObject.Instantiate 에러 수정 및 ObjectPool 적용
        GameObject spawnedVfx;
        if (ObjectPoolManager.Instance != null)
        {
            spawnedVfx = ObjectPoolManager.Instance.Spawn(VfxPrefab, spawnPos, rotation);
        }
        else
        {
            spawnedVfx = GameObject.Instantiate(VfxPrefab, spawnPos, rotation);
        }
        CharacterVFX.ApplyRuntimeAudioNormalization(spawnedVfx);
        yield break; 
    }
}

[System.Serializable]
[TypeInfoBox("적 타격 직전 배치합니다. Z 가드/저스트 가드, X 회피를 판정합니다. 반격 가능 특수공격은 X/C만 허용하며 C 성공 시 남은 스킬을 중단하고 공격받은 한 명만 반격합니다. 실제 적 피해는 뒤의 피해 블록에서 처리합니다.")]
public class Action_DefenseWindow : SkillActionBlock
{
    private const float LegacyDefaultAttackReadyDuration = 0.08f;

    [LabelText("방어 패턴 모드")]
    public EnemyDefensePatternMode PatternMode = EnemyDefensePatternMode.TelegraphThenWindow;

    [LabelText("공격 대응 방식")] public DefenseRequirement Requirement = DefenseRequirement.Any;
    [ShowIf(nameof(IsCounterable)), LabelText("반격: 피격 대상 ATK 배율"), MinValue(0.01f)]
    public float CounterDamageMultiplier = 1.5f;
    private bool IsCounterable => Requirement == DefenseRequirement.Counterable;
    [LabelText("전조 사용")]
    public bool UseTelegraph = true;
    [LabelText("전조 표현 방식")]
    [ShowIf(nameof(UseTelegraph))]
    public TelegraphVisualMode TelegraphVisualMode = TelegraphVisualMode.PrefabVFX;
    [AssetsOnly]
    [ShowIf(nameof(UseTelegraph))]
    [ValidateInput(nameof(HasRequiredWarningVfx), "Prefab VFX 전조에는 VFX Prefab이 필요합니다.")]
    public GameObject WarningVfxPrefab;
    [ShowIf(nameof(UseTelegraph))]
    [ValidateInput(nameof(HasRequiredWarningSprite), "Sprite 전조에는 Sprite가 필요합니다.")]
    public Sprite WarningSprite;
    [ShowIf(nameof(UseTelegraph))]
    [ValidateInput(nameof(HasRequiredTelegraphTrigger), "Animator Trigger 전조에는 Trigger 이름이 필요합니다.")]
    public string TelegraphAnimatorTriggerName = "";
    [ShowIf(nameof(UseTelegraph))]
    public string TelegraphAttachPivotName = CharacterPivotId.Back;
    [LabelText("전조 지속 시간")]
    [MinValue(0f)]
    [ValidateInput(nameof(HasValidTelegraphDuration), "전조 후 판정 모드는 0보다 긴 전조 시간이 필요합니다.")]
    public float TelegraphDuration = 0.8f;
    [LabelText("전조 후 준비 시간")]
    [MinValue(0f)] public float DefenseOpenDelay = 0f;
    [LabelText("판정 시간")]
    [ValidateInput(nameof(HasValidTimeWindow), "방어 판정 시간은 0보다 커야 합니다.")]
    public float TimeWindow = 0.8f;
    [LabelText("구형 모드: BAD도 피해 방지")]
    public bool AllowNearSuccess = true;
    [LabelText("개별 판정 구간 사용")]
    public bool OverrideTimingProfile;
    [ShowIf(nameof(OverrideTimingProfile))]
    [LabelText("저스트 / 구 Great / 구 Good (초)")]
    [Tooltip("Z/X/C 모드에서는 첫 값이 저스트 가드 구간입니다. 회피/연계 반격 구간은 QTEManager의 공통 설정을 사용합니다. Great/Good는 구형 방어 모드에만 사용됩니다.")]
    [ValidateInput(nameof(HasValidTimingProfile), "판정 구간은 0 이상, Perfect ≤ Great ≤ Good ≤ 판정 시간 순서여야 합니다.")]
    public DefenseTimingProfile TimingProfile = new DefenseTimingProfile(0.12f, 0.22f, 0.40f);
    [LabelText("실패 데미지 배율")] public float FailDamageMultiplier = 1f;
    [LabelText("실패 시 카메라 흔들기")] public bool ShakeOnFail = true;
    [ShowIf(nameof(ShakeOnFail)), LabelText("실패 흔들림 강도"), MinValue(0f)]
    [ValidateInput(nameof(HasValidFailShakeIntensity), "실패 흔들림 강도는 0보다 커야 합니다.")]
    public float FailShakeIntensity = 0.35f;
    [ShowIf(nameof(ShakeOnFail)), LabelText("실패 흔들림 시간"), MinValue(0f)]
    [ValidateInput(nameof(HasValidFailShakeDuration), "실패 흔들림 시간은 0보다 커야 합니다.")]
    public float FailShakeDuration = 0.2f;
    [ShowIf(nameof(ShakeOnFail)), LabelText("카메라 안전 등급")]
    public CameraShakeSafety FailShakeSafety = CameraShakeSafety.GameplaySafe;
    [LabelText("판정 후 딜레이")] public float DelayAfter = 0.1f;
    [LabelText("전조 후 공격 애니메이션 트리거")]
    public string AttackAnimTriggerName = "";
    [LabelText("공격 모션 시작 → 타격 시간"), MinValue(0f)]
    [Tooltip("클립 시작부터 실제 타격 프레임까지의 초입니다. 방어 판정 시간 이하여야 합니다. 0은 기존처럼 판정 시점에 모션을 시작합니다.")]
    public float AttackAnimationLeadTime;
    [AssetsOnly, LabelText("타격 직전 전조 프리팹")]
    [Tooltip("BattleTelegraphCue가 붙은 프리팹. 실제 대응 성공 구간이 열릴 때 표시합니다. 일반 공격은 패링, 회피 전용은 회피, 특수공격은 반격 구간을 사용합니다.")]
    public GameObject ImpactCuePrefab;
    // 기존 자산 호환용. 전조 시작은 이제 판정 정책에서 계산하므로 사용하지 않습니다.
    [HideInInspector]
    public float ImpactCueLeadTime = 0.3f;
    [ShowIf(nameof(ImpactCuePrefab)), LabelText("전조 표시 피벗")]
    public string ImpactCuePivotName = CharacterPivotId.Top;
    [LabelText("공격 준비 자세 시간")]
    [MinValue(0f)]
    [Tooltip("적이 실제 공격을 시작하기 전에 준비 자세를 유지하는 시간입니다. 이 시간에도 Z/X/C 입력을 버퍼링합니다.")]
    public float AttackReadyDuration = 0.08f;
    [LabelText("구형 공격 애니메이션 후 대기")]
    [MinValue(0f)]
    [Tooltip("레거시 데이터 호환용입니다. 별도 대기는 적용하지 않습니다. 클립의 타격 시점은 '공격 모션 시작 → 타격 시간'으로 맞춥니다.")]
    public float AttackAnimDelay = 0f;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        float afterDelay = Mathf.Max(0f, DelayAfter);
        if (PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow)
        {
            float telegraphOnly = UseTelegraph ? Mathf.Max(0f, TelegraphDuration) : 0f;
            return SkillActionAuthoringTiming.Fixed("전조", telegraphOnly + afterDelay);
        }

        float telegraphLead = PatternMode == EnemyDefensePatternMode.TelegraphThenWindow && UseTelegraph
            ? Mathf.Max(0f, TelegraphDuration)
            : 0f;
        // 준비 자세를 먼저 보인 뒤, 하나의 방어 시계로 모션 시작과 충돌을 예약합니다.
        float activeWindow = new BattleImpactTiming(TimeWindow, 0.2f).Duration;
        float duration = telegraphLead + Mathf.Max(0f, DefenseOpenDelay)
            + ResolveAttackReadyDuration() + activeWindow + afterDelay;
        return SkillActionAuthoringTiming.Variable("방어", duration);
    }

    private bool HasRequiredWarningVfx(GameObject value)
    {
        return !UseTelegraph
            || TelegraphVisualMode != TelegraphVisualMode.PrefabVFX
            || value != null;
    }

    private bool HasRequiredWarningSprite(Sprite value)
    {
        return !UseTelegraph
            || TelegraphVisualMode != TelegraphVisualMode.Sprite
            || value != null;
    }

    private bool HasRequiredTelegraphTrigger(string value)
    {
        return !UseTelegraph
            || TelegraphVisualMode != TelegraphVisualMode.AnimatorTrigger
            || !string.IsNullOrWhiteSpace(value);
    }

    private bool HasValidTelegraphDuration(float value)
    {
        return !UseTelegraph
            || PatternMode == EnemyDefensePatternMode.ImmediateReaction
            || value > 0f;
    }

    private bool HasValidTimeWindow(float value)
    {
        return PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow || value > 0f;
    }

    private bool HasValidTimingProfile(DefenseTimingProfile value)
    {
        return !OverrideTimingProfile
            || PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow
            || (value.PerfectWindow >= 0f
                && value.PerfectWindow <= value.GreatWindow
                && value.GreatWindow <= value.GoodWindow
                && value.GoodWindow <= TimeWindow);
    }

    private bool HasValidFailShakeIntensity(float value)
    {
        return !ShakeOnFail || value > 0f;
    }

    private bool HasValidFailShakeDuration(float value)
    {
        return !ShakeOnFail || value > 0f;
    }

    private GameObject SpawnTelegraph(CharacterBase actor)
    {
        if (!UseTelegraph || actor == null) return null;

        GameObject spawnedVFX = null;
        Transform attachPivot = actor.GetPivot(TelegraphAttachPivotName);
        if (attachPivot == null) attachPivot = actor.transform;

        if (actor is EnemyCharacter enemy && !string.IsNullOrWhiteSpace(TelegraphAnimatorTriggerName))
        {
            enemy.PlaySkillAnim(TelegraphAnimatorTriggerName, EnemyCharacter.HashSkill);
        }

        if (TelegraphVisualMode == TelegraphVisualMode.Sprite && WarningSprite != null)
        {
            spawnedVFX = new GameObject($"TelegraphSprite_{Requirement}");
            var sr = spawnedVFX.AddComponent<SpriteRenderer>();
            sr.sprite = WarningSprite;
            sr.sortingOrder = 50;
            spawnedVFX.transform.SetParent(attachPivot, false);
            spawnedVFX.transform.localPosition = Vector3.zero;
        }
        else if (TelegraphVisualMode == TelegraphVisualMode.PrefabVFX && WarningVfxPrefab != null)
        {
            if (ObjectPoolManager.Instance != null)
                spawnedVFX = ObjectPoolManager.Instance.Spawn(WarningVfxPrefab, attachPivot.position, Quaternion.identity);
            else
                spawnedVFX = GameObject.Instantiate(WarningVfxPrefab, attachPivot.position, Quaternion.identity);

            spawnedVFX.transform.SetParent(attachPivot, true);
        }

        return spawnedVFX;
    }

    private void DespawnTelegraph(GameObject spawnedVFX)
    {
        if (spawnedVFX != null)
        {
            if (TelegraphVisualMode == TelegraphVisualMode.PrefabVFX && ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Despawn(spawnedVFX);
            else
                GameObject.Destroy(spawnedVFX);
        }
    }

    public Action_DefenseWindow CopyForImpact(float duration, string attackTrigger = null)
    {
        var copy = (Action_DefenseWindow)MemberwiseClone();
        copy.TimeWindow = Mathf.Max(0.01f, duration);
        copy.AttackAnimationLeadTime = copy.TimeWindow;
        if (!string.IsNullOrWhiteSpace(attackTrigger)) copy.AttackAnimTriggerName = attackTrigger;
        return copy;
    }

    public override IEnumerator Execute(SkillContext context) => ExecuteImpact(context);

    public IEnumerator ExecuteImpact(SkillContext context, System.Action<float> advanceAttack = null,
        System.Action onImpactResolved = null)
    {
        if (!(context.Actor is EnemyCharacter enemy)
            || context.Targets == null
            || context.Targets.Count == 0)
        {
            yield break;
        }

        GameObject telegraph = null;
        BattleUIController.Instance?.ShowEnemyTarget(context.MainTarget, context.Targets.Count > 1);
        PlayerController targetController = GetPlayerController(context.Targets[0]);
        QteExecution execution = null;
        bool resultResolved = false;

        try
        {
            // 전조/접근 단계부터 입력을 받을 수 있게 합니다. Z를 미리 유지하거나
            // X/C를 선입력해도 실제 충돌 시점에 한 번만 소비됩니다.
            if (PatternMode != EnemyDefensePatternMode.TelegraphThenNextTurnWindow && targetController != null)
                targetController.PrepareDefenseWindow();

            if (PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow)
            {
                telegraph = SpawnTelegraph(context.Actor);
                if (UseTelegraph && TelegraphDuration > 0f)
                    yield return new WaitForSecondsRealtime(TelegraphDuration);
                if (DelayAfter > 0f)
                    yield return new WaitForSecondsRealtime(DelayAfter);
                yield break;
            }

            if (PatternMode == EnemyDefensePatternMode.TelegraphThenWindow)
            {
                telegraph = SpawnTelegraph(context.Actor);
                if (UseTelegraph && TelegraphDuration > 0f)
                    yield return new WaitForSecondsRealtime(TelegraphDuration);
                if (DefenseOpenDelay > 0f)
                    yield return new WaitForSecondsRealtime(DefenseOpenDelay);
            }
            else
            {
                if (DefenseOpenDelay > 0f)
                    yield return new WaitForSecondsRealtime(DefenseOpenDelay);
                telegraph = SpawnTelegraph(context.Actor);
            }
            if (!context.CanContinueExecution)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            CharacterBase target = context.Targets[0];
            if (target == null || !target.IsAlive)
            {
                context.StopTimelineExecution = true;
                yield break;
            }
            DefenseQteResult finalResult = default;
            bool resultReceived = false;

            QTEManager qteManager = QTEManager.Instance;
            if (qteManager == null)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            // Ready가 같은 프레임의 공격 모션을 덮지 않도록 예약보다 먼저 완료합니다.
            // 준비 중에도 PlayerController의 방어 입력 버퍼는 유지됩니다.
            if (context.TryPresentEnemyAttackReady(enemy))
                yield return new WaitForSecondsRealtime(ResolveAttackReadyDuration());
            if (!context.CanContinueExecution || target == null || !target.IsAlive || qteManager == null)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            DefenseQteRequest request = OverrideTimingProfile
                ? new DefenseQteRequest(
                    TimeWindow,
                    1f,
                    Requirement,
                    TimingProfile,
                    AllowNearSuccess)
                : qteManager.CreateDefenseRequest(
                    TimeWindow,
                    1f,
                    Requirement,
                    AllowNearSuccess);
            // 적 공격은 방어 판정만 공유하고, 일반 방어 QTE 패널/이벤트는 사용하지 않습니다.
            execution = qteManager.StartBattleDefenseWindow(
                request,
                targetController,
                result =>
                {
                    finalResult = result;
                    resultReceived = true;
                    resultResolved = true;
                    onImpactResolved?.Invoke();
                    // 판정 시점은 이미 진행 중인 모션의 실제 타격 프레임입니다.
                    if (!result.IsCounterSuccess && enemy != null && enemy.IsAlive)
                    {
                        if (result.PreventsDamage && targetController != null)
                        {
                            // 결과가 피해를 막은 경우에만 즉시 회피/저스트가드 모션을 시작합니다.
                            // 일반 가드는 피격 모션과 겹치므로 별도 패링 모션을 재생하지 않습니다.
                            targetController.ConfirmDefenseSuccess(
                                result.IsPerfectParry ? DefenseInput.Parry : result.Input);
                        }

                    }
                },
                () =>
                {
                    if (!context.CanContinueExecution || context.EnemyAttackImpactStarted)
                        return;
                    if (!string.IsNullOrWhiteSpace(AttackAnimTriggerName))
                        enemy.PlaySkillAnim(AttackAnimTriggerName, EnemyCharacter.HashAttack);
                    else
                        enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
                    context.MarkEnemyAttackImpactStarted();
                },
                AttackAnimationLeadTime,
                attacker: enemy, cuePrefab: ImpactCuePrefab, cuePivot: ImpactCuePivotName,
                onAttackProgress: advanceAttack);
            context.ActiveDefenseWindow = execution;

            yield return new WaitUntil(() => execution.IsDone);
            if (execution.Termination == QteTermination.Cancelled
                || execution.Termination == QteTermination.Failed
                || !context.CanContinueExecution)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            if (resultReceived && finalResult.IsCounterSuccess)
            {
                context.CurrentDamageMultiplier = 0f;
                context.AttackInterruptedByCounter = true;
                context.PendingDefensePostImpactDelay = 0f;
                if (context.LinkCounterService == null || !(target is PlayerCharacter defender))
                {
                    context.StopTimelineExecution = true;
                    Debug.LogError("[Action_DefenseWindow] 연계 반격 실행 서비스 또는 전열 대상이 없습니다.", enemy);
                    yield break;
                }

                // 전조/방어 포즈를 반격에 넘기기 전에 정리합니다. 반격은 턴/AP를 소비하지 않습니다.
                DespawnTelegraph(telegraph);
                telegraph = null;
                if (targetController != null)
                    targetController.ResetDefenseReactionLock();
                IEnumerator counter = context.LinkCounterService.Execute(
                    enemy, defender, CounterDamageMultiplier, context.IsExecutionActive,
                    GetActorDefaultBattlePos(enemy), context.DefenderReturnPosition);
                try
                {
                    while (counter.MoveNext())
                        yield return counter.Current;
                }
                finally
                {
                    (counter as System.IDisposable)?.Dispose();
                    context.ReleaseDefenderPresentation();
                }
                if (context.IsExecutionActive != null && !context.IsExecutionActive())
                    context.StopTimelineExecution = true;
                yield break;
            }
            else if (resultReceived && finalResult.IsPerfectParry)
            {
                context.CurrentDamageMultiplier = 0f;

                if (target is PlayerCharacter playerTarget
                    && BattleManager.Instance != null)
                {
                    playerTarget.RestoreAP(BattleManager.Instance._apOnParryPerfect);
                    BattleManager.Instance.InvokeAPChangedEvent(playerTarget, playerTarget.CurrentAP);
                }
            }
            else if (resultReceived && finalResult.IsGuard)
            {
                context.CurrentDamageMultiplier *= finalResult.DamageMultiplier;
                if (targetController != null)
                    targetController.ConfirmGuardSuccess();
            }
            else if (resultReceived && finalResult.PreventsDamage)
            {
                // 회피에는 AP/반격 보상을 주지 않습니다. 구 점프의 무피해도 보존합니다.
                context.CurrentDamageMultiplier = 0f;
            }
            else
            {
                context.CurrentDamageMultiplier *= FailDamageMultiplier;
                if (ShakeOnFail)
                    PlayFailCameraFeedback();
            }

            // 결과 모션은 위 콜백에서 이미 시작했습니다. 실제 피해 블록이 이어지면
            // 그 블록 직후에 모션을 소비해, 턴 종료의 Idle 리셋이 저스트가드/피격을
            // 중간에 끊지 않도록 합니다. 일반 가드·실패는 TakeDamage가 Hurt를
            // 시작하므로 별도 방어 모션 대신 Hurt 상태를 기다립니다.
            if (resultReceived)
            {
                DefenseInput reactionInput = finalResult.PreventsDamage
                    ? finalResult.IsPerfectParry ? DefenseInput.Parry : finalResult.Input
                    : DefenseInput.None;
                context.QueueDefenseReaction(
                    targetController,
                    reactionInput,
                    !finalResult.PreventsDamage);
            }

            // 이 대기를 충돌 전에 두면 공격 트리거와 피해가 벌어집니다.
            // 실제 피해/상태 소비자가 끝난 뒤에만 정리 시간으로 적용합니다.
            context.QueueDefensePostImpactDelay(DelayAfter);
        }
        finally
        {
            if (ReferenceEquals(context.ActiveDefenseWindow, execution)) context.ActiveDefenseWindow = null;
            if (execution != null && !execution.IsDone)
            {
                context.StopTimelineExecution = true;
                QTEManager.Instance?.Cancel(execution);
            }

            DespawnTelegraph(telegraph);
            if (targetController != null)
            {
                if (!resultResolved)
                    targetController.ResetDefenseReactionLock();
                else
                    targetController.CloseDefenseInputWindow();
            }
        }
    }

    private void PlayFailCameraFeedback()
    {
        CameraController cameraController = CameraController.Instance;
        if (cameraController == null || cameraController.IsStaticBattlePresentation)
            return;

        if (!cameraController.TryImpulse(
                Vector3.right,
                Mathf.Max(0.001f, FailShakeIntensity),
                Mathf.Max(0.01f, FailShakeDuration),
                FailShakeSafety,
                out string error))
        {
            Debug.LogWarning("[Action_DefenseWindow] Camera feedback skipped: " + error);
        }
    }

    private float ResolveAttackReadyDuration()
    {
        // 새 필드가 없는 기존 SerializeReference 에셋도 공격 준비 단계를 보장합니다.
        return AttackReadyDuration > 0f
            ? AttackReadyDuration
            : LegacyDefaultAttackReadyDuration;
    }

}

// ═══════════════════════════════════════════════════════════════
// ── 3. 원거리 투사체 블록 ──
// ═══════════════════════════════════════════════════════════════
[System.Serializable]
[TypeInfoBox("내 위치에서 타겟을 향해 투사체를 날립니다.")]
public class Action_Projectile : SkillActionBlock
{
    [AssetsOnly, Required, LabelText("투사체 프리팹")] public GameObject ProjectilePrefab;
    [AssetsOnly, LabelText("충돌 VFX 프리팹")] public GameObject ImpactVFXPrefab;
    [LabelText("비행 시간")] public float FlightDuration = 0.3f;
    [LabelText("데미지 배율")] public float DamageMultiplier = 1.0f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("투사체", FlightDuration);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }
        if (ProjectilePrefab == null || context.MainTarget == null) yield break;

        Vector3 startPos = context.Actor.GetPivot(CharacterPivotId.Center).position;
        Vector3 endPos = context.MainTarget.GetPivot(CharacterPivotId.Center).position;

        ObjectPoolManager pool = ObjectPoolManager.Instance;
        GameObject proj = null;
        Tween flight = null;
        try
        {
            if (context.Actor is EnemyCharacter)
            {
                bool launched = false;
                Action_DefenseWindow defense = context.TakeImpactDefense(FlightDuration, flight: true);
                yield return defense.ExecuteImpact(context, attackTime =>
                {
                    if (!launched)
                    {
                        launched = true;
                        proj = pool != null ? pool.Spawn(ProjectilePrefab, startPos, Quaternion.identity)
                            : GameObject.Instantiate(ProjectilePrefab, startPos, Quaternion.identity);
                        CharacterVFX.ApplyRuntimeAudioNormalization(proj);
                        flight = proj.transform.DOMove(endPos, Mathf.Max(0.01f, FlightDuration))
                            .SetEase(Ease.Linear).SetRecyclable(false).SetAutoKill(false).Pause();
                    }
                    if (proj == null || !flight.IsActive())
                        throw new System.InvalidOperationException("공격 중 투사체가 제거되었습니다.");
                    // 전조·입력·투사체가 같은 시계를 사용합니다. 메뉴 정지와 감속도 함께 적용됩니다.
                    flight.Goto(Mathf.Min(attackTime, Mathf.Max(0.01f, FlightDuration)), false);
                }, () =>
                {
                    flight?.Kill();
                    flight = null;
                    if (proj != null)
                    {
                        if (pool != null) pool.Despawn(proj);
                        else GameObject.Destroy(proj);
                        proj = null;
                    }
                });
            }
            else
            {
                proj = pool != null ? pool.Spawn(ProjectilePrefab, startPos, Quaternion.identity)
                    : GameObject.Instantiate(ProjectilePrefab, startPos, Quaternion.identity);
                CharacterVFX.ApplyRuntimeAudioNormalization(proj);
                flight = proj.transform.DOMove(endPos, FlightDuration).SetEase(Ease.Linear).SetRecyclable(false);
                while (context.CanContinueExecution && proj != null && flight.IsActive() && !flight.IsComplete())
                    yield return null;
                if (proj == null) context.StopTimelineExecution = true;
            }
        }
        finally
        {
            flight?.Kill();
            if (proj != null)
            {
                if (pool != null) pool.Despawn(proj);
                else GameObject.Destroy(proj);
            }
        }

        if (context.AttackInterruptedByCounter) yield break;
        if (!context.CanContinueExecution || context.MainTarget == null || !context.MainTarget.IsAlive)
        {
            context.StopTimelineExecution = true;
            yield break;
        }

        // QTE는 투사체 비행과 병행하고, 실제 충돌 프레임에서만 결과를 확정합니다.
        yield return context.WaitForActiveSkillQte();
        if (!context.CanContinueExecution)
            yield break;

        if (ImpactVFXPrefab != null)
        {
            GameObject impactVfx;
            if (ObjectPoolManager.Instance != null) impactVfx = ObjectPoolManager.Instance.Spawn(ImpactVFXPrefab, endPos, Quaternion.identity);
            else impactVfx = GameObject.Instantiate(ImpactVFXPrefab, endPos, Quaternion.identity);
            CharacterVFX.ApplyRuntimeAudioNormalization(impactVfx);
        }
        
        float effectiveDamageMultiplier = DamageMultiplier * context.CurrentDamageMultiplier;
        if (effectiveDamageMultiplier <= 0f)
        {
            context.CurrentDamageMultiplier = 1.0f;
            context.IsPerfectQTE = false;
            context.ResetEnemyAttackPresentation();
            yield return context.WaitForPendingDefenseReaction();
            yield return context.WaitForPendingDefensePostImpactDelay();
            yield break;
        }

        int dmg = Mathf.RoundToInt(context.Actor.ATK * effectiveDamageMultiplier);
        int previousHp = context.MainTarget.CurrentHP;
        DamageResult damageResult = context.MainTarget.TakeDamage(dmg, Element, context.Actor);
        int dealt = damageResult.FinalDamage;
        BattleManager.Instance.InvokeDamageEvent(context.Actor, context.MainTarget, dealt, context.IsPerfectQTE, previousHp);

        context.CurrentDamageMultiplier = 1.0f;
        context.IsPerfectQTE = false;
        context.ResetEnemyAttackPresentation();
        yield return context.WaitForPendingDefenseReaction();
        yield return context.WaitForPendingDefensePostImpactDelay();
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 4. 연쇄 다중 공격 (수정됨) ──
// ═══════════════════════════════════════════════════════════════
[System.Serializable]
[TypeInfoBox("광역기(AoE) 스킬일 경우, 모든 타겟을 순서대로 돌아가며 타격합니다.")]
public class Action_SequentialMelee : SkillActionBlock
{
    [LabelText("공격 애니메이션 트리거"), Required] public string AttackAnimTrigger = "Attack";
    [LabelText("데미지 배율")] public float DamageMultiplier = 0.8f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;
    [LabelText("대시 속도")] public float DashSpeed = 0.15f;
    [AssetsOnly, LabelText("히트 VFX 프리팹")] public GameObject HitVfxPrefab;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Variable("연쇄 피해", Mathf.Max(0f, DashSpeed) + 0.3f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (!context.CanContinueExecution)
        {
            context.StopTimelineExecution = true;
            yield break;
        }
        if (context.Targets.Count == 0) yield break;
        bool enemyAttack = context.Actor is EnemyCharacter;
        Action_DefenseWindow impactDefense = enemyAttack
            ? context.TakeImpactDefense(0.6f, AttackAnimTrigger) : null;
        float baseDamageMultiplier = context.CurrentDamageMultiplier;
        if (context.CurrentDamageMultiplier <= 0f)
        {
            context.CurrentDamageMultiplier = 1.0f;
            context.IsPerfectQTE = false;
            context.ResetEnemyAttackPresentation();
            yield return context.WaitForPendingDefenseReaction();
            yield return context.WaitForPendingDefensePostImpactDelay();
            yield break;
        }

        // 잔상 컴포넌트 찾기
        var ghostTrail = context.Actor.GetComponentInChildren<CharacterGhostTrail>();

        List<CharacterBase> shuffledTargets = new List<CharacterBase>(context.Targets);
        for (int i = 0; i < shuffledTargets.Count; i++) {
            CharacterBase temp = shuffledTargets[i];
            int randomIndex = UnityEngine.Random.Range(i, shuffledTargets.Count); 
            shuffledTargets[i] = shuffledTargets[randomIndex];
            shuffledTargets[randomIndex] = temp;
        }

        foreach (var target in shuffledTargets)
        {
            if (!context.CanContinueExecution)
            {
                context.StopTimelineExecution = true;
                yield break;
            }
            if (target == null || !target.IsAlive) continue;

            if (enemyAttack)
                yield return context.StageDefender(target as PlayerCharacter);
            if (!context.CanContinueExecution || target == null || !target.IsAlive) yield break;

            Vector3 targetPos = PositionManager.HorizontalApproach(context.Actor,
                target.GetPivot(CharacterPivotId.Front).position);

            Tween movement = null;
            try
            {
                if (ghostTrail != null) ghostTrail.SetTrailActive(true);
                movement = context.Actor.transform.DOMove(targetPos, DashSpeed).SetEase(Ease.OutExpo).SetRecyclable(false);
                while (context.CanContinueExecution && movement.IsActive() && !movement.IsComplete())
                    yield return null;
            }
            finally
            {
                movement?.Kill();
                if (ghostTrail != null) ghostTrail.SetTrailActive(false);
            }

            if (!context.CanContinueExecution || target == null || !target.IsAlive)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            // 이동/돌진은 QTE와 병행하고, 타격 직전에만 결과를 기다립니다.
            yield return context.WaitForActiveSkillQte();
            if (!context.CanContinueExecution || target == null || !target.IsAlive)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            if (enemyAttack)
            {
                List<CharacterBase> originalTargets = context.Targets;
                context.Targets = new List<CharacterBase> { target };
                context.CurrentDamageMultiplier = baseDamageMultiplier;
                context.ResetEnemyAttackPresentation();
                try
                {
                    yield return impactDefense.Execute(context);
                }
                finally { context.Targets = originalTargets; }
                if (context.AttackInterruptedByCounter) yield break;
            }
            else
            {
                PlayActorBattleAnim(context.Actor, Animator.StringToHash(AttackAnimTrigger));
                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (!context.CanContinueExecution || target == null || !target.IsAlive)
            {
                context.StopTimelineExecution = true;
                yield break;
            }

            if (HitVfxPrefab != null)
            {
                GameObject hitVfx;
                if (ObjectPoolManager.Instance != null) hitVfx = ObjectPoolManager.Instance.Spawn(HitVfxPrefab, target.GetPivot(CharacterPivotId.Center).position, Quaternion.identity);
                else hitVfx = GameObject.Instantiate(HitVfxPrefab, target.GetPivot(CharacterPivotId.Center).position, Quaternion.identity);
                CharacterVFX.ApplyRuntimeAudioNormalization(hitVfx);
            }
            
            if (context.CurrentDamageMultiplier > 0f)
            {
                int dmg = Mathf.RoundToInt(context.Actor.ATK * DamageMultiplier * context.CurrentDamageMultiplier);
                int previousHp = target.CurrentHP;
                DamageResult damageResult = target.TakeDamage(dmg, Element, context.Actor);
                int dealt = damageResult.FinalDamage;
                BattleManager.Instance.InvokeDamageEvent(context.Actor, target, dealt, context.IsPerfectQTE, previousHp);
                CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.4f, true);
            }

            context.ResetEnemyAttackPresentation();
            yield return context.WaitForPendingDefenseReaction();
            yield return context.WaitForPendingDefensePostImpactDelay();

            yield return new WaitForSecondsRealtime(0.2f);
        }

        context.CurrentDamageMultiplier = 1.0f;
        context.IsPerfectQTE = false;
        context.ResetEnemyAttackPresentation();
        yield return context.WaitForPendingDefenseReaction();
        yield return context.WaitForPendingDefensePostImpactDelay();
    }
}
