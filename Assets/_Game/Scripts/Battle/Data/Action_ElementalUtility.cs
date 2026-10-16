using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ElementalUtilityOperation
{
    [InspectorName("상태 부여")] Apply = 0,
    [InspectorName("상태 소비 피해")] ConsumeBurst = 1,
    [InspectorName("5종 상태 정화")] Cleanse = 2,
    [InspectorName("HP 회복")] Heal = 3,
    [InspectorName("즉시 상태 피해")] Detonate = 4,
    [InspectorName("상태 소비 기절")] Stun = 5
}

[System.Serializable]
[TypeInfoBox("누적 상태를 소비하거나 정화/회복합니다. 공격에 동반되는 상태는 스킬의 ‘명중 시 상태 부여’에 지정하세요. 상태 피해는 감전/파쇄를 재발동하지 않습니다.")]
public sealed class Action_ElementalUtility : SkillActionBlock
{
    [LabelText("처리")] public ElementalUtilityOperation Operation;
    [LabelText("누적 상태")] public ElementalStatusKind Kind;
    [LabelText("부여 / 소비량 (소비 0 = 전부)")] public float Amount = 4f;
    [LabelText("소비 1당 피해"), MinValue(0f)] public float DamagePerValue = 3f;
    [LabelText("HP 회복량"), MinValue(0)] public int HealAmount = 60;
    [LabelText("기절 필요 / 소비량"), MinValue(1)] public int MinimumValue = 150;
    [LabelText("선택 대상 대신 시전자")] public bool TargetActor;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("누적 상태 / 회복", 0f);

    public override IEnumerator Execute(SkillContext context)
    {
        if (context == null || !context.CanContinueExecution) yield break;
        // 이 블록은 한 MoveNext에서 끝나므로 다른 시퀀스에 표시 문맥이 새지 않습니다.
        using var damageContext = BattleManager.Instance != null
            ? BattleManager.Instance.DamageBatches.Activate(context.DamageBatchId) : default;
        if (TargetActor) ApplyTo(context.Actor, context.CurrentStatusBuildupMultiplier);
        else
            for (int i = 0; context.Targets != null && i < context.Targets.Count; i++)
                ApplyTo(context.Targets[i], context.CurrentStatusBuildupMultiplier);
        yield break;
    }

    private void ApplyTo(CharacterBase target, float buildupMultiplier)
    {
        if (target == null || !target.IsAlive) return;
        ElementalStatusState state = target.Elements;
        switch (Operation)
        {
            case ElementalUtilityOperation.Apply:
                state.Add(Kind, Amount, buildupMultiplier);
                break;
            case ElementalUtilityOperation.ConsumeBurst:
                int consumed = state.Consume(Kind, Amount <= 0f ? state.GetValue(Kind) : Mathf.FloorToInt(Amount));
                if (consumed > 0) target.ApplyStatusDamage(Mathf.Max(0, Mathf.FloorToInt(consumed * DamagePerValue)), Kind);
                break;
            case ElementalUtilityOperation.Cleanse:
                state.Cleanse();
                break;
            case ElementalUtilityOperation.Heal:
                target.HealHP(Mathf.Max(0, HealAmount));
                break;
            case ElementalUtilityOperation.Detonate:
                state.Trigger(Kind);
                break;
            case ElementalUtilityOperation.Stun:
                int required = Mathf.Max(1, MinimumValue);
                if (state.GetValue(Kind) >= required && state.CanApplyCrowdControl
                    && target.TryApplyStatusEffect(new StunEffect(1)).Applied)
                    state.Consume(Kind, required);
                break;
        }
    }
}
