using System.Collections.Generic;
using UnityEngine;
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

[CreateAssetMenu(fileName = "NewSequenceSkill", menuName = "HubToHome/SkillData_Sequence")]
public class SkillData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50)]
    public Sprite Icon;
    
    [BoxGroup("Identity")] public string SkillName = "New Skill";
    [BoxGroup("Identity")] public string SkillID   = "skill_000";
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    [BoxGroup("Identity"), LabelText("사용 범위")]
    public SkillUsageProfile UsageProfile = SkillUsageProfile.Shared;

    [BoxGroup("Player Runtime")]
    public int MPCost = 10;
    
    [BoxGroup("Targeting")]
    public TargetAreaType TargetType = TargetAreaType.EnemyOnly;
    [BoxGroup("Targeting")]
    public bool IsAoE = false;

    [InfoBox("플레이어/적 구분 없이 Action Timeline 블록만 쌓아서 스킬을 제작합니다. 적의 방어 방식, 이동, 애니메이션, VFX도 모두 타임라인 블록으로 구성하세요.")]
    [Title("Action Timeline")]
    [SerializeReference, HideReferenceObjectPicker] 
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "BlockName")]
    public List<SkillActionBlock> ActionTimeline = new List<SkillActionBlock>();
}