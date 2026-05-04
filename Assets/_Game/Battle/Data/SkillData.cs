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

[CreateAssetMenu(fileName = "NewSkill", menuName = "HubToHome/SkillData")]
public class SkillData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50)]
    public Sprite Icon;
    
    [BoxGroup("Identity")] public string SkillName = "Skill";
    [BoxGroup("Identity")] public string SkillID   = "skill_001";
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    [BoxGroup("Cost & Power")] public int MPCost = 10;
    [BoxGroup("Cost & Power")] public float DamageMultiplier = 1.5f;

    [BoxGroup("Target & Effect")] public TargetAreaType TargetType = TargetAreaType.EnemyOnly;
    [BoxGroup("Target & Effect")] public bool IsAoE = false;
    
    [BoxGroup("Target & Effect")] public EffectActionType ActionType = EffectActionType.Damage;
    
    // 상태이상 스킬일 때만 표시되도록 인스펙터 최적화 (Odin)
    [BoxGroup("Target & Effect"), ShowIf("ActionType", EffectActionType.ApplyStatus)] 
    public StatusEffectType StatusEffect = StatusEffectType.None;
    
    [BoxGroup("Target & Effect"), ShowIf("ActionType", EffectActionType.ApplyStatus)] 
    public int StatusDurationTurns = 0;

    [BoxGroup("QTE Settings")] public QTEType QTEType = QTEType.Sequence;
    [BoxGroup("QTE Settings"), ShowIf("QTEType", QTEType.Sequence)] public float QTETimeLimit = 1.0f;
    [BoxGroup("QTE Settings"), ShowIf("QTEType", QTEType.Sequence)] public float QTESuccessMultiplier = 1.5f;
    [BoxGroup("QTE Settings"), ShowIf("QTEType", QTEType.Sequence)] public float QTEFailMultiplier    = 0.5f;

    [BoxGroup("QTE Settings"), ShowIf("QTEType", QTEType.Sequence)]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillQTENode> QTENodes = new List<SkillQTENode>();

    [BoxGroup("Animation & Visual")] public SkillCastType CastType = SkillCastType.MeleeDash;
    [BoxGroup("Animation & Visual")] public float VFXSpawnDelay = 0.2f;
    [BoxGroup("Animation & Visual")] public float DamageDelay = 0.25f;
    [BoxGroup("Animation & Visual")] public GameObject EffectPrefab;
    [BoxGroup("Animation & Visual")] public bool SpawnVFXOnTarget = true;
}