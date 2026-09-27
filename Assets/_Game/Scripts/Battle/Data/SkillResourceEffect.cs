using System;
using Sirenix.OdinInspector;

/// <summary>Optional skill bonus, never a skill availability restriction.</summary>
[Serializable]
public sealed class SkillResourceEffect
{
    [LabelText("사용 자원")] public BattleResourceDefinition Resource;
    [LabelText("조건부 강화 사용")] public bool EnableBoost;
    [MinValue(1), LabelText("강화에 필요한 단계")] public int RequiredStep = 1;
    [MinValue(0), LabelText("강화 시 소모 단계")] public int ConsumeStep = 1;
    [MinValue(1f), LabelText("강화 피해 배율")] public float DamageMultiplier = 1.5f;
    [MinValue(0), LabelText("정상 완료 시 획득 단계")] public int GainOnCompletion;

    public bool IsValid => Resource == null || (GainOnCompletion >= 0
        && (!EnableBoost || (RequiredStep >= 1 && RequiredStep <= Resource.MaxStep
            && ConsumeStep >= 0 && ConsumeStep <= RequiredStep
            && !float.IsNaN(DamageMultiplier) && !float.IsInfinity(DamageMultiplier) && DamageMultiplier >= 1f)));

    public bool CanBoost(CharacterBattleResource state) => IsValid && EnableBoost && Resource != null
        && state != null && state.Definition == Resource && state.Step >= RequiredStep && state.Step >= ConsumeStep;

    public string Describe(CharacterBattleResource state)
    {
        if (Resource == null || state == null || state.Definition != Resource) return "";
        if (!IsValid) return "\n자원 설정 오류";
        string text = "";
        if (EnableBoost)
        {
            string mode = CanBoost(state) ? "강화 가능" : "기본 성능";
            text = $"\n{Resource.DisplayName} {state.Step}/{RequiredStep} · {mode}"
                + $"\n충족 시 피해 ×{DamageMultiplier:0.##}, {ConsumeStep}단계 소모";
        }
        if (GainOnCompletion > 0) text += $"\n완료 시 {Resource.DisplayName} +{GainOnCompletion}";
        return text;
    }
}
