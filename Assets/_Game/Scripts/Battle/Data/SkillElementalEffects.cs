using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ElementalApplicationMode
{
    [InspectorName("스킬당 대상별 1회")] OncePerSkill = 0,
    [InspectorName("명중할 때마다")] PerHit = 1
}

[Serializable]
public struct ElementalStatusApplication
{
    [LabelText("누적 상태")] public ElementalStatusKind Kind;
    [LabelText("부여량"), MinValue(0f)] public float Amount;
    [LabelText("부여 주기")] public ElementalApplicationMode Mode;
}

public partial class SkillContext
{
    private SkillData _elementalSkill;
    private HashSet<(CharacterBase target, int application)> _appliedElementalOnce;
    public float CurrentStatusBuildupMultiplier = 1f;

    private void BeginElementalSkill(SkillData skill) => _elementalSkill = skill;

    /// <summary>One confirmed direct-hit path for every skill block, including rapid/projectile hits.</summary>
    public DamageResult ApplySkillHit(CharacterBase target, int rawDamage, DamageElement element)
    {
        if (!CanContinueExecution || target == null || !target.IsAlive || rawDamage <= 0)
            return default;
        float proc = _elementalSkill != null ? _elementalSkill.StatusProcCoefficient : 1f;
        // 감전 반응은 TakeDamage 안에서 동기적으로 통지됩니다. 병렬 스킬도 자신의 묶음에 기록합니다.
        using var damageContext = BattleManager.Instance != null
            ? BattleManager.Instance.DamageBatches.Activate(DamageBatchId) : default;
        DamageResult result = target.TakeDamage(rawDamage, element, Actor, proc);
        if (!result.Applied || result.FinalDamage <= 0 || !target.IsAlive || CurrentStatusBuildupMultiplier <= 0f)
            return result;

        ElementalStatusApplication[] applications = _elementalSkill != null ? _elementalSkill.ElementalApplications : null;
        for (int i = 0; applications != null && i < applications.Length; i++)
        {
            ElementalStatusApplication application = applications[i];
            if (application.Amount <= 0f || (uint)application.Kind >= 5u) continue;
            if (application.Mode == ElementalApplicationMode.OncePerSkill)
            {
                _appliedElementalOnce ??= new HashSet<(CharacterBase, int)>();
                if (!_appliedElementalOnce.Add((target, i))) continue;
            }
            target.Elements.Add(application.Kind, application.Amount, CurrentStatusBuildupMultiplier);
        }
        return result;
    }
}
