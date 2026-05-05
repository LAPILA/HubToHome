using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public enum QTEType { None, Timing, Sequence }
public enum SkillCastType { MeleeDash, RangedStatic }

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

    [BoxGroup("Cost")] public int MPCost = 10;
    
    [BoxGroup("Target")] public TargetAreaType TargetType = TargetAreaType.EnemyOnly;
    [BoxGroup("Target")] public bool IsAoE = false;

    // 🚨 다형성 직렬화 리스트: 인스펙터에서 아래의 ActionBlock들을 마음대로 조립하게 해줍니다.
    [Title("스킬 타임라인 (시퀀스)")]
    [SerializeReference] 
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "BlockName")]
    public List<SkillActionBlock> ActionTimeline = new List<SkillActionBlock>();
}