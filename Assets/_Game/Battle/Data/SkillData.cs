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
    [Header("Identity")]
    public string SkillName = "Skill";
    public string SkillID   = "skill_001";
    public Sprite Icon;
    [TextArea] public string Description = "";

    [Header("Cost & Damage")]
    public int MPCost = 10;
    public float DamageMultiplier = 1.5f;

    [Header("Target & Effect (New)")]
    public TargetAreaType TargetType = TargetAreaType.EnemyOnly;
    public bool IsAoE = false;
    
    [Tooltip("스킬이 데미지를 줄지, 상태이상을 걸지, 힐을 할지 결정")]
    public EffectActionType ActionType = EffectActionType.Damage;
    public StatusEffectType StatusEffect = StatusEffectType.None;
    public int StatusDurationTurns = 0;

    [Header("QTE ")]
    public QTEType QTEType = QTEType.Sequence;
    public float QTETimeLimit = 1.0f;
    public float QTESuccessMultiplier = 1.5f;
    public float QTEFailMultiplier    = 0.5f;

    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillQTENode> QTENodes = new List<SkillQTENode>();

    [Header("Animation & Timing")]
    public SkillCastType CastType = SkillCastType.MeleeDash;
    public float VFXSpawnDelay = 0.2f;
    public float DamageDelay = 0.25f;

    [Header("Visual")]
    public GameObject EffectPrefab;
    public bool SpawnVFXOnTarget = true;
}