using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public enum DefenseRequirement
{
    [InspectorName("패링 또는 회피")]
    ParryOrDodge = 0,
    [InspectorName("점프만")]
    JumpOnly = 1,
    [InspectorName("패링 / 회피 / 점프")]
    Any = 2,
    [InspectorName("패링만")]
    ParryOnly = 3,
    [InspectorName("회피만")]
    DodgeOnly = 4,
    [InspectorName("회피 또는 점프")]
    DodgeOrJump = 5
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

    public CharacterBase MainTarget => Targets != null && Targets.Count > 0 ? Targets[0] : null;
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
        if (this is Action_DefenseWindow) return "방어/패링";
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

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("이동", Duration);
    }

    public override IEnumerator Execute(SkillContext context)
    {
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
        else if (Destination == MoveDest.Center && pm != null) targetPos = pm.GetCenterPos();
        else if (Destination == MoveDest.OriginalPos)
            targetPos = GetActorDefaultBattlePos(context.Actor);

        PlayActorBattleAnim(context.Actor, context.Actor is EnemyCharacter ? EnemyCharacter.HashBattleMove : PlayerCharacter.HashBattleMove);

        var ghostTrail = context.Actor.GetComponentInChildren<CharacterGhostTrail>();
        if (ghostTrail != null) ghostTrail.SetTrailActive(true);

        yield return context.Actor.transform.DOMove(targetPos, Duration).SetEase(MoveEase).WaitForCompletion();
        if (ghostTrail != null) ghostTrail.SetTrailActive(false);

        PlayActorBattleAnim(context.Actor, context.Actor is EnemyCharacter ? EnemyCharacter.HashBattleIdle : PlayerCharacter.HashBattleIdle);
    }
}

[System.Serializable]
[TypeInfoBox("특정 애니메이션을 재생합니다.")]
public class Action_PlayAnim : SkillActionBlock
{
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
        int hash = Animator.StringToHash(AnimTriggerName);
        PlayActorBattleAnim(context.Actor, hash);
        if (DelayAfter > 0) yield return new WaitForSeconds(DelayAfter);
    }
}

[System.Serializable]
[TypeInfoBox("데미지를 입힙니다. 이전 QTE 블록의 배율이 적용됩니다.")]
public class Action_Damage : SkillActionBlock
{
    [LabelText("기본 스킬 배율")] public float SkillMultiplier = 1.0f;
    [LabelText("카메라 흔들림")] public bool ShakeCamera = true;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("피해", 0f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        float finalMultiplier = SkillMultiplier * context.CurrentDamageMultiplier;
        if (finalMultiplier <= 0f)
        {
            context.CurrentDamageMultiplier = 1.0f;
            context.IsPerfectQTE = false;
            yield break;
        }

        int finalDamage = Mathf.RoundToInt(context.Actor.ATK * finalMultiplier);

        foreach (var target in context.Targets)
        {
            if (!target.IsAlive) continue;
            
            int previousHp = target.CurrentHP;
            int dmg = target.TakeDamage(finalDamage);
            BattleManager.Instance.InvokeDamageEvent(target, dmg, context.IsPerfectQTE, previousHp);
            
            if (ShakeCamera) 
                CameraController.Instance?.PlayHeavySlam(Vector3.right, context.IsPerfectQTE ? 1.2f : 0.6f, true);
        }
        
        context.CurrentDamageMultiplier = 1.0f; 
        context.IsPerfectQTE = false;
        
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
        foreach (var target in context.Targets)
        {
            if (!target.IsAlive) continue;

            if (StatusEffectFactory.TryCreate(StatusID, DurationTurns, out StatusEffect effect))
            {
                target.AddEffect(effect);
            }
            else
            {
                Debug.LogWarning($"[Action_ApplyStatus] 등록되지 않은 상태이상 ID입니다: {StatusID}");
            }
        }
        yield break;
    }
}

[System.Serializable]
[TypeInfoBox("QTE를 실행하고 성공 여부에 따라 다음 데미지/회복 배율을 결정합니다.")]
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
        if (Nodes == null || Nodes.Count == 0 || QTEManager.Instance == null)
            yield break;

        int successCount = 0;
        QteExecution execution = QTEManager.Instance.StartSequenceQTEWithResult(
            Nodes,
            TimeLimit,
            (success, _) => successCount = success);

        try
        {
            yield return new WaitUntil(() => execution.IsDone);
            if (execution.Termination == QteTermination.Cancelled
                || execution.Termination == QteTermination.Failed)
            {
                yield break;
            }

            float ratio = (float)successCount / Nodes.Count;
            context.CurrentDamageMultiplier = Mathf.Lerp(FailMultiplier, SuccessMultiplier, ratio);
            context.IsPerfectQTE = successCount == Nodes.Count;
            yield return new WaitForSeconds(0.2f);
        }
        finally
        {
            if (!execution.IsDone)
                QTEManager.Instance?.Cancel(execution);
        }
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
[TypeInfoBox("적 스킬용 방어 대응 윈도우입니다. 올바른 입력이면 회피/패링, 실패하면 지정 배율 데미지를 줍니다.")]
public class Action_DefenseWindow : SkillActionBlock
{
    [LabelText("방어 패턴 모드")]
    public EnemyDefensePatternMode PatternMode = EnemyDefensePatternMode.TelegraphThenWindow;

    [LabelText("요구 입력")] public DefenseRequirement Requirement = DefenseRequirement.JumpOnly;
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
    [LabelText("BAD 판정도 피해 방지")]
    public bool AllowNearSuccess = true;
    [LabelText("개별 판정 구간 사용")]
    public bool OverrideTimingProfile;
    [ShowIf(nameof(OverrideTimingProfile))]
    [LabelText("Perfect / Great / Good (초)")]
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
    [LabelText("공격 애니메이션 후 대기")]
    [MinValue(0f)] public float AttackAnimDelay = 0f;

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
        float activeWindow = Mathf.Max(Mathf.Max(0f, TimeWindow), Mathf.Max(0f, AttackAnimDelay));
        float duration = telegraphLead + Mathf.Max(0f, DefenseOpenDelay) + activeWindow + afterDelay;
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

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context.Actor is EnemyCharacter enemy)
            || context.Targets == null
            || context.Targets.Count == 0)
        {
            yield break;
        }

        GameObject telegraph = null;
        PlayerController targetController = null;
        QteExecution execution = null;

        try
        {
            if (PatternMode == EnemyDefensePatternMode.TelegraphThenNextTurnWindow)
            {
                telegraph = SpawnTelegraph(context.Actor);
                if (UseTelegraph && TelegraphDuration > 0f)
                    yield return new WaitForSeconds(TelegraphDuration);
                if (DelayAfter > 0f)
                    yield return new WaitForSeconds(DelayAfter);
                yield break;
            }

            if (PatternMode == EnemyDefensePatternMode.TelegraphThenWindow)
            {
                telegraph = SpawnTelegraph(context.Actor);
                if (UseTelegraph && TelegraphDuration > 0f)
                    yield return new WaitForSeconds(TelegraphDuration);
                if (DefenseOpenDelay > 0f)
                    yield return new WaitForSeconds(DefenseOpenDelay);
            }
            else
            {
                if (DefenseOpenDelay > 0f)
                    yield return new WaitForSeconds(DefenseOpenDelay);
                telegraph = SpawnTelegraph(context.Actor);
            }
            CharacterBase target = context.Targets[0];
            targetController = GetPlayerController(target);
            DefenseQteResult finalResult = default;
            bool resultReceived = false;

            targetController?.PrepareDefenseWindow();
            QTEManager qteManager = QTEManager.Instance;
            if (qteManager == null)
                yield break;

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
            execution = qteManager.StartDefenseQTEWithResult(
                request,
                targetController,
                result =>
                {
                    finalResult = result;
                    resultReceived = true;
                });

            if (!string.IsNullOrWhiteSpace(AttackAnimTriggerName))
                enemy.PlaySkillAnim(AttackAnimTriggerName, EnemyCharacter.HashSkill);

            if (AttackAnimDelay > 0f)
                yield return new WaitForSeconds(AttackAnimDelay);

            yield return new WaitUntil(() => execution.IsDone);
            if (execution.Termination == QteTermination.Cancelled
                || execution.Termination == QteTermination.Failed)
            {
                yield break;
            }

            bool success = resultReceived && finalResult.PreventsDamage;
            if (success)
            {
                targetController?.ConfirmDefenseSuccess(finalResult.Input);
                context.CurrentDamageMultiplier = 0f;

                if (finalResult.Input == DefenseInput.Dodge || finalResult.Input == DefenseInput.Jump)
                {
                    yield return targetController != null
                        ? targetController.WaitForDefenseVisualComplete(0.5f)
                        : null;
                }

                if (finalResult.Input == DefenseInput.Parry
                    && finalResult.Grade == QTEManager.QTEGrade.Perfect
                    && target is PlayerCharacter playerTarget
                    && BattleManager.Instance != null)
                {
                    playerTarget.HealMP(BattleManager.Instance._mpOnParryPerfect);
                    BattleManager.Instance.InvokeMPChangedEvent(playerTarget, playerTarget.CurrentMP);
                }
            }
            else
            {
                context.CurrentDamageMultiplier *= FailDamageMultiplier;
                if (ShakeOnFail)
                    PlayFailCameraFeedback();
            }

            if (DelayAfter > 0f)
                yield return new WaitForSeconds(DelayAfter);
        }
        finally
        {
            if (execution != null && !execution.IsDone)
                QTEManager.Instance?.Cancel(execution);

            DespawnTelegraph(telegraph);
            targetController?.ResetDefenseReactionLock();
        }
    }

    private void PlayFailCameraFeedback()
    {
        CameraController cameraController = CameraController.Instance;
        if (cameraController == null)
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

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Fixed("투사체", FlightDuration);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (ProjectilePrefab == null || context.MainTarget == null) yield break;

        Vector3 startPos = context.Actor.GetPivot(CharacterPivotId.Center).position;
        Vector3 endPos = context.MainTarget.GetPivot(CharacterPivotId.Center).position;

        // 🚨 풀링 시스템 호환 및 에러 수정
        GameObject proj;
        if (ObjectPoolManager.Instance != null)
            proj = ObjectPoolManager.Instance.Spawn(ProjectilePrefab, startPos, Quaternion.identity);
        else
            proj = GameObject.Instantiate(ProjectilePrefab, startPos, Quaternion.identity);
        CharacterVFX.ApplyRuntimeAudioNormalization(proj);
        
        yield return proj.transform.DOMove(endPos, FlightDuration).SetEase(Ease.Linear).WaitForCompletion();
        
        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Despawn(proj);
        else
            GameObject.Destroy(proj); 

        if (ImpactVFXPrefab != null)
        {
            GameObject impactVfx;
            if (ObjectPoolManager.Instance != null) impactVfx = ObjectPoolManager.Instance.Spawn(ImpactVFXPrefab, endPos, Quaternion.identity);
            else impactVfx = GameObject.Instantiate(ImpactVFXPrefab, endPos, Quaternion.identity);
            CharacterVFX.ApplyRuntimeAudioNormalization(impactVfx);
        }
        
        int dmg = Mathf.RoundToInt(context.Actor.ATK * DamageMultiplier * context.CurrentDamageMultiplier);
        int previousHp = context.MainTarget.CurrentHP;
        int dealt = context.MainTarget.TakeDamage(dmg);
        BattleManager.Instance.InvokeDamageEvent(context.MainTarget, dealt, context.IsPerfectQTE, previousHp);

        context.CurrentDamageMultiplier = 1.0f; 
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
    [LabelText("대시 속도")] public float DashSpeed = 0.15f;
    [AssetsOnly, LabelText("히트 VFX 프리팹")] public GameObject HitVfxPrefab;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        return SkillActionAuthoringTiming.Variable("연쇄 피해", Mathf.Max(0f, DashSpeed) + 0.3f);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (context.Targets.Count == 0) yield break;

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
            if (!target.IsAlive) continue;

            Vector3 targetPos = target.GetPivot(CharacterPivotId.Front).position;

            if (ghostTrail != null) ghostTrail.SetTrailActive(true);
            yield return context.Actor.transform.DOMove(targetPos, DashSpeed).SetEase(Ease.OutExpo).WaitForCompletion();
            if (ghostTrail != null) ghostTrail.SetTrailActive(false); // 🚨 도착하면 끄기

            PlayActorBattleAnim(context.Actor, Animator.StringToHash(AttackAnimTrigger));
            yield return new WaitForSeconds(0.1f); 

            if (HitVfxPrefab != null)
            {
                GameObject hitVfx;
                if (ObjectPoolManager.Instance != null) hitVfx = ObjectPoolManager.Instance.Spawn(HitVfxPrefab, target.GetPivot(CharacterPivotId.Center).position, Quaternion.identity);
                else hitVfx = GameObject.Instantiate(HitVfxPrefab, target.GetPivot(CharacterPivotId.Center).position, Quaternion.identity);
                CharacterVFX.ApplyRuntimeAudioNormalization(hitVfx);
            }
            
            int dmg = Mathf.RoundToInt(context.Actor.ATK * DamageMultiplier * context.CurrentDamageMultiplier);
            int previousHp = target.CurrentHP;
            int dealt = target.TakeDamage(dmg);
            BattleManager.Instance.InvokeDamageEvent(target, dealt, context.IsPerfectQTE, previousHp);
            CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.4f, true);

            yield return new WaitForSeconds(0.2f); 
        }

        context.CurrentDamageMultiplier = 1.0f; 
    }
}
