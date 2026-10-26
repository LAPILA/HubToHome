using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum EnemyAssaultStageKind
{
    [InspectorName("지상 왕복")] GroundRush = 0,
    [InspectorName("공중 강하")] AerialDive = 1,
    [InspectorName("지상 · 공중 혼합")] Mixed = 2
}

[Serializable]
public sealed class EnemyAssaultStage
{
    [LabelText("이동 형태")] public EnemyAssaultStageKind Kind;
    [LabelText("타격 횟수"), Range(1, 40)] public int HitCount = 4;
    [HideInInspector] public float StartTimeWindow = .65f;
    [HideInInspector] public float EndTimeWindow = .42f;
    [LabelText("처음 관통 시간"), Range(.08f, .6f)] public float StartPassDuration = .3f;
    [LabelText("마지막 관통 시간"), Range(.08f, .6f)] public float EndPassDuration = .18f;
    [LabelText("처음 화면 끝 접근"), Range(.04f, .3f)] public float StartEntryDuration = .1f;
    [LabelText("마지막 화면 끝 접근"), Range(.04f, .3f)] public float EndEntryDuration = .05f;
    [HideInInspector] public float StartReadyDuration = .08f;
    [HideInInspector] public float EndReadyDuration = .03f;
    [LabelText("한 타 피해 배율"), MinValue(0f)] public float DamageMultiplier = .22f;
    [LabelText("공중 높이"), Range(0f, 8f)] public float AirHeight = 3f;
    [LabelText("혼합 구간 공중 확률"), Range(0f, 1f)] public float MixedAerialChance = .5f;

    public bool HasValidSettings => (Kind == EnemyAssaultStageKind.GroundRush
        || Kind == EnemyAssaultStageKind.AerialDive || Kind == EnemyAssaultStageKind.Mixed)
        && HitCount >= 1 && HitCount <= 40
        && InRange(StartTimeWindow, .18f, 3f) && InRange(EndTimeWindow, .18f, 3f)
        && InRange(StartPassDuration, .08f, .6f) && InRange(EndPassDuration, .08f, .6f)
        && InRange(StartEntryDuration, .04f, .3f) && InRange(EndEntryDuration, .04f, .3f)
        && InRange(StartReadyDuration, 0f, 1f) && InRange(EndReadyDuration, 0f, 1f)
        && InRange(DamageMultiplier, 0f, 10f) && InRange(AirHeight, 0f, 8f)
        && InRange(MixedAerialChance, 0f, 1f);

    public float Progress(int hitIndex) => HitCount <= 1 ? 0f : Mathf.Clamp01((float)hitIndex / (HitCount - 1));

    public bool IsAerialHit(float randomSample) => Kind == EnemyAssaultStageKind.AerialDive
        || (Kind == EnemyAssaultStageKind.Mixed && InRange(randomSample, 0f, 1f)
            && (MixedAerialChance >= 1f || randomSample < MixedAerialChance));

    internal static bool InRange(float value, float min, float max)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}

[Serializable]
[TypeInfoBox("적 전용. 단계별 가속 왕복과 공중 강하를 조합합니다. 매 타격은 기존 방어 대응과 피해 적용을 사용하며, 최종 특수 반격은 뒤의 별도 블록으로 제작합니다.")]
public sealed class Action_EnemyAssaultCombo : SkillActionBlock
{
    [LabelText("연속 공격 단계"), ListDrawerSettings(ShowIndexLabels = true)]
    public List<EnemyAssaultStage> Stages = new List<EnemyAssaultStage>();
    [AssetsOnly, LabelText("타격 직전 전조")] public GameObject ImpactCuePrefab;
    [LabelText("공격 트리거")] public string AttackAnimTriggerName = "Attack";
    [LabelText("공격 시작 → 타격 (초)"), MinValue(0f)] public float AttackAnimationLeadTime = .12f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;
    [LabelText("화면 끝 여백 (픽셀)"), Range(0f, 64f)] public float EdgeMarginPixels = 12f;
    // 저장 호환용. 모든 적 공격이 연속 대응이므로 더 이상 정지 모드로 전환하지 않습니다.
    [HideInInspector] public bool PassThroughOnGuard = true;
    [HideInInspector] public bool ContinuousMotion = true;

    [ShowInInspector, ReadOnly, LabelText("전체 타격 수")]
    public int TotalHitCount
    {
        get
        {
            int count = 0;
            for (int i = 0; Stages != null && i < Stages.Count; i++)
                if (Stages[i] != null) count += Mathf.Clamp(Stages[i].HitCount, 0, 40);
            return count;
        }
    }

    [ShowInInspector, ReadOnly, LabelText("설정 유효")]
    public bool HasValidSettings
    {
        get
        {
            if (Stages == null || Stages.Count == 0 || Stages.Count > 12 || TotalHitCount > 80
                || !EnemyAssaultStage.InRange(EdgeMarginPixels, 0f, 64f)
                || !EnemyAssaultStage.InRange(AttackAnimationLeadTime, 0f, 1f)
                || !Enum.IsDefined(typeof(DamageElement), Element)) return false;
            for (int i = 0; i < Stages.Count; i++)
                if (Stages[i] == null || !Stages[i].HasValidSettings) return false;
            return true;
        }
    }

    public override SkillActionAuthoringTiming GetAuthoringTiming()
    {
        float minimum = 0f;
        for (int i = 0; Stages != null && i < Stages.Count; i++)
        {
            EnemyAssaultStage stage = Stages[i];
            if (stage == null || !stage.HasValidSettings) continue;
            minimum += stage.HitCount * (stage.StartPassDuration + stage.EndPassDuration
                + stage.StartEntryDuration + stage.EndEntryDuration) * .5f;
        }
        return SkillActionAuthoringTiming.Variable("적 연속 돌파 · 공중 강하", minimum);
    }

    public void ConfigureDefenseForHit(Action_DefenseWindow defense, EnemyAssaultStage stage, int hitIndex, float randomSample)
    {
        float t = stage.Progress(hitIndex);
        defense.PatternMode = EnemyDefensePatternMode.ImmediateReaction;
        defense.Requirement = DefenseRequirement.Any;
        defense.UseTelegraph = false;
        defense.TimeWindow = Interpolate(stage.StartTimeWindow, stage.EndTimeWindow, t);
        defense.AttackAnimTriggerName = AttackAnimTriggerName;
        defense.AttackAnimationLeadTime = Mathf.Min(AttackAnimationLeadTime, defense.TimeWindow);
        defense.AttackReadyDuration = Interpolate(stage.StartReadyDuration, stage.EndReadyDuration, t);
        defense.ImpactCuePrefab = ImpactCuePrefab;
        defense.ImpactCuePivotName = CharacterPivotId.Center;
        defense.UseMeleePassThrough = true;
        defense.MeleePassExtent = EnemyMeleePassExtent.ScreenWide;
        float passDuration = Interpolate(stage.StartPassDuration, stage.EndPassDuration, t);
        defense.ScreenPassDuration = passDuration;
        // 화면 구도 확보에 실패한 Local 경로에도 같은 가속을 전달합니다.
        // 일반 방어 블록의 기본값이나 기존 스킬 데이터는 변경하지 않습니다.
        defense.MeleeDashDuration = Mathf.Clamp(passDuration * .5f, .04f, .3f);
        defense.MeleeExitDuration = Mathf.Clamp(passDuration * .5f, .04f, .25f);
        defense.ScreenEdgeApproachDuration = Interpolate(stage.StartEntryDuration, stage.EndEntryDuration, t);
        defense.ScreenEdgeMarginPixels = EdgeMarginPixels;
        defense.MeleeApproachHeight = stage.IsAerialHit(randomSample) ? stage.AirHeight : 0f;
        defense.MeleePassThroughOnGuard = true;
        defense.ContinuousMeleeMotion = true;
        defense.DelayAfter = 0f;
        defense.ShakeOnFail = false;
    }

    private static float Interpolate(float start, float end, float progress)
        // Lerp의 끝점 반올림으로 .08이 .07999998이 되면 유효한 화면 관통이 Local로 바뀝니다.
        => progress <= 0f ? start : progress >= 1f ? end : Mathf.Lerp(start, end, progress);

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context?.Actor is EnemyCharacter) || !CanRun(context)) yield break;
        if (!HasValidSettings)
        {
            context.StopTimelineExecution = true;
            yield break;
        }
        yield return context.StageCombatants(context.MainTarget);
        if (!CanRun(context)) yield break;

        var defense = new Action_DefenseWindow();
        var damage = new Action_Damage { Element = Element, ShakeCamera = false };
        bool previousDefer = context.DeferDefenseReactionCompletion;
        context.DeferDefenseReactionCompletion = true;
        try
        {
            for (int stageIndex = 0; stageIndex < Stages.Count && CanRun(context); stageIndex++)
            {
                EnemyAssaultStage stage = Stages[stageIndex];
                damage.SkillMultiplier = stage.DamageMultiplier;
                for (int hit = 0; hit < stage.HitCount && CanRun(context); hit++)
                {
                    while (BattleDelay.IsPaused && CanRun(context)) yield return null;
                    if (!CanRun(context)) yield break;
                    ConfigureDefenseForHit(defense, stage, hit,
                        stage.Kind == EnemyAssaultStageKind.Mixed ? UnityEngine.Random.value : 0f);
                    context.ResetEnemyAttackPresentation();
                    yield return defense.Execute(context);
                    if (!CanRun(context)) yield break;
                    // 기존 피해 소비자 한 번만 실행합니다. 판정/상태/피격 반응은 기존 경로 그대로입니다.
                    yield return damage.Execute(context);
                }
            }
            context.DeferDefenseReactionCompletion = previousDefer;
            if (CanRun(context)) yield return context.WaitForPendingDefenseReaction();
        }
        finally
        {
            context.DeferDefenseReactionCompletion = previousDefer;
            if (!CanRun(context)) context.CancelPendingDefenseReaction();
            // 각 타격의 정상 scope는 끝 위치를 유지합니다. 전체 취소/원위치 복원은 기존 실행 루트가 소유합니다.
            context.CancelEnemyMeleePresentation();
            context.ResetEnemyAttackPresentation();
        }
    }

    private static bool CanRun(SkillContext context) => context != null && context.CanContinueExecution
        && context.Actor is EnemyCharacter enemy && enemy.isActiveAndEnabled
        && context.Targets != null && context.Targets.Count == 1
        && context.MainTarget is PlayerCharacter target && target.IsAlive && target.isActiveAndEnabled;
}
