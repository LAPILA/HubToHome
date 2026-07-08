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

    [InfoBox("SkillData는 전투 스킬 전용입니다. 스토리 대화, 지역 이동, 시나리오 플래그, 컷신 분기, Timeline 전체 컷신 호출은 넣지 마세요.")]
    [Title("전투 스킬 블록")]
    [LabelText("Combat Skill Blocks")]
    [SerializeReference, HideReferenceObjectPicker]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true, ListElementLabelName = "BlockHeader")]
    public List<SkillActionBlock> ActionTimeline = new List<SkillActionBlock>();

    [Button("SkillData Validate")]
    public void ValidateSkillData()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(SkillID))
        {
            errors.Add("SkillID가 비어 있습니다.");
        }

        if (TargetType == TargetAreaType.Both)
        {
            warnings.Add("TargetType.Both는 현재 전투 스킬 타임라인에서 모호할 수 있습니다. AllyOnly / EnemyOnly / AoEAll 사용을 권장합니다.");
        }

        if (ActionTimeline == null || ActionTimeline.Count == 0)
        {
            warnings.Add("전투 스킬 블록이 비어 있습니다.");
        }
        else
        {
            for (int i = 0; i < ActionTimeline.Count; i++)
            {
                ValidateBlock(ActionTimeline[i], i, errors, warnings);
            }
        }

        if (errors.Count == 0 && warnings.Count == 0)
        {
            Debug.Log("[SkillData] Validation passed: " + SafeId(SkillID), this);
            return;
        }

        string message = "[SkillData] Validation result for '" + SafeId(SkillID) + "'";
        if (errors.Count > 0)
        {
            message += "\n오류:\n- " + string.Join("\n- ", errors.ToArray());
        }

        if (warnings.Count > 0)
        {
            message += "\n경고:\n- " + string.Join("\n- ", warnings.ToArray());
        }

        if (errors.Count > 0)
        {
            Debug.LogError(message, this);
        }
        else
        {
            Debug.LogWarning(message, this);
        }
    }

    private void ValidateBlock(SkillActionBlock block, int index, List<string> errors, List<string> warnings)
    {
        if (block == null)
        {
            warnings.Add("Block[" + index + "]가 비어 있습니다.");
            return;
        }

        string label = string.IsNullOrWhiteSpace(block.DesignerLabel) ? block.BlockName : block.DesignerLabel.Trim();
        string prefix = "Block[" + index + "] " + label + ": ";

        Action_PlayAnim playAnim = block as Action_PlayAnim;
        if (playAnim != null && string.IsNullOrWhiteSpace(playAnim.AnimTriggerName))
        {
            errors.Add(prefix + "Animation Trigger가 비어 있습니다.");
        }

        Action_VFX vfx = block as Action_VFX;
        if (vfx != null && vfx.VfxPrefab == null)
        {
            errors.Add(prefix + "VFX Prefab이 비어 있습니다.");
        }

        Action_QTE qte = block as Action_QTE;
        if (qte != null)
        {
            if (qte.Nodes == null || qte.Nodes.Count == 0)
            {
                warnings.Add(prefix + "QTE 노드가 비어 있습니다.");
            }

            if (qte.TimeLimit <= 0f)
            {
                warnings.Add(prefix + "QTE 제한 시간이 0 이하입니다.");
            }
        }

        Action_DefenseWindow defense = block as Action_DefenseWindow;
        if (defense != null)
        {
            if (defense.TimeWindow <= 0f)
            {
                warnings.Add(prefix + "방어 판정 시간이 0 이하입니다.");
            }

            if (defense.UseTelegraph)
            {
                if (defense.TelegraphVisualMode == TelegraphVisualMode.PrefabVFX && defense.WarningVfxPrefab == null)
                {
                    warnings.Add(prefix + "전조 VFX 프리팹이 비어 있습니다.");
                }

                if (defense.TelegraphVisualMode == TelegraphVisualMode.Sprite && defense.WarningSprite == null)
                {
                    warnings.Add(prefix + "전조 Sprite가 비어 있습니다.");
                }

                if (defense.TelegraphVisualMode == TelegraphVisualMode.AnimatorTrigger
                    && string.IsNullOrWhiteSpace(defense.TelegraphAnimatorTriggerName))
                {
                    warnings.Add(prefix + "전조 Animator Trigger가 비어 있습니다.");
                }
            }

            if (string.IsNullOrWhiteSpace(defense.AttackAnimTriggerName))
            {
                warnings.Add(prefix + "패링/방어 블록의 공격 애니메이션 트리거가 비어 있습니다.");
            }
        }

        Action_Projectile projectile = block as Action_Projectile;
        if (projectile != null)
        {
            if (projectile.ProjectilePrefab == null)
            {
                errors.Add(prefix + "Projectile Prefab이 비어 있습니다.");
            }

            if (projectile.ImpactVFXPrefab == null)
            {
                warnings.Add(prefix + "Impact VFX Prefab이 비어 있습니다.");
            }
        }

        Action_SequentialMelee sequentialMelee = block as Action_SequentialMelee;
        if (sequentialMelee != null)
        {
            if (string.IsNullOrWhiteSpace(sequentialMelee.AttackAnimTrigger))
            {
                errors.Add(prefix + "연쇄 근접 공격의 Animation Trigger가 비어 있습니다.");
            }

            if (sequentialMelee.HitVfxPrefab == null)
            {
                warnings.Add(prefix + "연쇄 근접 공격의 Hit VFX Prefab이 비어 있습니다.");
            }
        }
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }
}