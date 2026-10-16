using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;

public enum QTEType { None, Timing, Sequence }
public enum SkillCastType { MeleeDash, RangedStatic }
public enum SkillUsageProfile { Shared, PlayerOnly, EnemyOnly }

[System.Serializable]
public struct SkillQTENode
{
    [Range(0.1f, 0.9f)] public float PosX;
    [Range(0.1f, 0.9f)] public float PosY;
    public string TargetKey;
}

[CreateAssetMenu(fileName = "NewSequenceSkill", menuName = "Hub To Home/전투/스킬 데이터")]
public class SkillData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50)]
    public Sprite Icon;

    [BoxGroup("Identity")] public string SkillName = "New Skill";
    [BoxGroup("Identity")] public string SkillID = "skill_000";
    [BoxGroup("Identity"), TextArea(2, 4)]
    [Tooltip("플레이어용 효과 설명을 짧게 작성합니다. 타수·연타·입력 키·QTE·테스트 안내는 넣지 않고, 제작 메모는 블록 라벨에 작성합니다.")]
    public string Description = "";

    [BoxGroup("Identity"), LabelText("사용 범위")]
    public SkillUsageProfile UsageProfile = SkillUsageProfile.Shared;

    [BoxGroup("Player Runtime")]
    [FormerlySerializedAs("MPCost")]
    public int APCost = 10;

    [System.Obsolete("Use APCost.")]
    public int MPCost
    {
        get => APCost;
        set => APCost = value;
    }
    [BoxGroup("Targeting")]
    public TargetAreaType TargetType = TargetAreaType.EnemyOnly;
    [BoxGroup("Targeting")]
    public bool IsAoE = false;

    [BoxGroup("누적 상태"), LabelText("명중 시 상태 부여")]
    [Tooltip("피해를 받은 대상에만 적용합니다. 피해 속성과 별개이며, 완전 방어/회피 시에는 부여하지 않습니다.")]
    public ElementalStatusApplication[] ElementalApplications = System.Array.Empty<ElementalStatusApplication>();

    [BoxGroup("누적 상태"), LabelText("감전 타격 발동 계수"), Range(0f, 1f)]
    [Tooltip("일반 공격 1, 50타 연격 예시 0.1. 상태 부여량이나 직접 피해 배율과는 별개입니다.")]
    public float StatusProcCoefficient = 1f;

    [BoxGroup("고유 자원"), LabelText("조건부 강화 / 획득")]
    [Tooltip("자원 부족은 사용을 막지 않습니다. 시작 시 충족하면 지정량만 소모하고 스킬 전체 피해를 강화합니다.")]
    [ValidateInput(nameof(HasValidResourceEffect), "자원 필요량/소모량/배율 설정을 확인하세요.")]
    public SkillResourceEffect ResourceEffect = new SkillResourceEffect();

    private bool HasValidResourceEffect(SkillResourceEffect effect) => effect == null || effect.IsValid;

    [InfoBox("SkillData는 전투 스킬 전용입니다. 스토리 대화, 지역 이동, 시나리오 플래그, 컷신 분기, Timeline 전체 컷신 호출은 넣지 마세요.")]
    [Title("전투 스킬 블록")]
    [LabelText("Combat Skill Blocks")]
    [SerializeReference, HideReferenceObjectPicker]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true, ListElementLabelName = "BlockHeader")]
    [ValidateInput(nameof(HasValidActionTimeline), "적 공격 타임라인에 오류가 있습니다. 아래 검사 상태를 확인하세요.")]
    public List<SkillActionBlock> ActionTimeline = new List<SkillActionBlock>();

    [BoxGroup("Enemy Attack Authoring")]
    [ShowIf(nameof(IsEnemyAttackPattern))]
    [ShowInInspector, ReadOnly, MultiLineProperty(12)]
    [LabelText("실시간 시간축")]
    [PropertyOrder(100)]
    public string EnemyAttackTimelinePreview =>
        EnemyAttackAuthoringAnalyzer.Analyze(this).BuildTimelinePreview();

    [BoxGroup("Enemy Attack Authoring")]
    [ShowIf(nameof(IsEnemyAttackPattern))]
    [ShowInInspector, ReadOnly, MultiLineProperty(10)]
    [LabelText("검사 상태")]
    [PropertyOrder(101)]
    public string EnemyAttackValidationPreview =>
        EnemyAttackAuthoringAnalyzer.Analyze(this).BuildValidationSummary();

    [BoxGroup("Enemy Attack Authoring")]
    [ShowIf(nameof(CanApplyEnemyAttackTemplate))]
    [Button("샘플 전조 공격 블록 구성", ButtonSizes.Medium)]
    [PropertyOrder(102)]
    public void ApplyEnemyAttackTemplate()
    {
        if (!CanApplyEnemyAttackTemplate())
            return;

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Apply Enemy Attack Template");
#endif
        ActionTimeline = EnemyAttackTemplateFactory.CreateTelegraphedStrike();
        APCost = 0;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [Button("SkillData Validate")]
    public void ValidateSkillData()
    {
        EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(this);
        bool missingId = string.IsNullOrWhiteSpace(SkillID);
        bool ambiguousTarget = TargetType == TargetAreaType.Both;

        if (!missingId && !ambiguousTarget && report.Issues.Count == 0)
        {
            Debug.Log("[SkillData] Validation passed: " + SafeId(SkillID), this);
            return;
        }

        string message = "[SkillData] Validation result for '" + SafeId(SkillID) + "'";
        if (missingId)
            message += "\n[오류] SkillID가 비어 있습니다.";
        if (ambiguousTarget)
            message += "\n[경고] TargetType.Both는 모호합니다. AllyOnly / EnemyOnly / AoEAll을 권장합니다.";
        if (report.Issues.Count > 0)
            message += "\n" + report.BuildValidationSummary();

        if (missingId || report.HasErrors)
            Debug.LogError(message, this);
        else
            Debug.LogWarning(message, this);
    }

    private bool IsEnemyAttackPattern()
    {
        return UsageProfile == SkillUsageProfile.EnemyOnly;
    }

    private bool CanApplyEnemyAttackTemplate()
    {
        return IsEnemyAttackPattern()
            && (ActionTimeline == null || ActionTimeline.Count == 0);
    }

    private bool HasValidActionTimeline(List<SkillActionBlock> _)
    {
        return !EnemyAttackAuthoringAnalyzer.Analyze(this).HasErrors;
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }
}
