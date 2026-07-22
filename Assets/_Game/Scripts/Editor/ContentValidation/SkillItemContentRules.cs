#if UNITY_EDITOR
using UnityEngine;

internal static class SkillItemContentRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        ValidateSkillRules(context);
        ValidateItemRules(context);
        ValidateVisuals(context);
    }

    private static void ValidateSkillRules(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        for (int skillIndex = 0; skillIndex < snapshot.Skills.Count; skillIndex++)
        {
            SkillData skill = snapshot.Skills[skillIndex];
            if (skill == null || skill.ActionTimeline == null)
                continue;

            for (int blockIndex = 0; blockIndex < skill.ActionTimeline.Count; blockIndex++)
            {
                SkillActionBlock block = skill.ActionTimeline[blockIndex];
                if (block == null)
                {
                    context.Add(
                        skill,
                        "skill.timeline.block.missing",
                        "ActionTimeline[" + blockIndex + "] is missing.");
                    continue;
                }

                if (block is Action_VFX vfx && vfx.VfxPrefab == null)
                {
                    context.Add(
                        skill,
                        "skill.timeline.vfx_prefab.missing",
                        "ActionTimeline[" + blockIndex + "] VFX prefab is missing.");
                }

                if (block is Action_Projectile projectile && projectile.ProjectilePrefab == null)
                {
                    context.Add(
                        skill,
                        "skill.timeline.projectile_prefab.missing",
                        "ActionTimeline[" + blockIndex + "] projectile prefab is missing.");
                }
            }
        }
    }

    private static void ValidateItemRules(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        for (int i = 0; i < snapshot.Items.Count; i++)
        {
            ItemData item = snapshot.Items[i];
            if (item == null)
                continue;

            if (item.IsStackable && item.MaxStackSize < 1)
            {
                context.Add(
                    item,
                    "item.stack.max.invalid",
                    "Stackable item MaxStackSize must be at least 1.");
            }

            if (item.Type != ItemType.Consumable)
                continue;

            if (item.ActionType == EffectActionType.None)
            {
                context.Add(
                    item,
                    "item.consumable.effect.missing",
                    "Consumable item has no effect.");
            }

            if ((item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage)
                && item.TargetStat != TargetStatType.HP
                && item.TargetStat != TargetStatType.MP)
            {
                context.Add(
                    item,
                    "item.consumable.target_stat.invalid",
                    "Heal or damage item must target HP or MP.");
            }

            if (item.ActionType != EffectActionType.ApplyStatus)
                continue;

            if (!StatusEffectFactory.IsKnown(item.StatusEffectID))
            {
                context.Add(
                    item,
                    "item.consumable.status.unknown",
                    "Consumable item references unknown status '" + item.StatusEffectID + "'.");
            }

            if (item.StatusDurationTurns < 1)
            {
                context.Add(
                    item,
                    "item.consumable.status_duration.invalid",
                    "Status effect duration must be at least 1 turn.");
            }
        }
    }

    private static void ValidateVisuals(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        for (int i = 0; i < snapshot.Skills.Count; i++)
        {
            SkillData skill = snapshot.Skills[i];
            if (skill != null && skill.Icon == null)
            {
                context.Add(
                    skill,
                    "skill.visual.icon.missing",
                    "Skill icon is missing.",
                    ContentValidationSeverity.Warning);
            }
        }

        for (int i = 0; i < snapshot.Items.Count; i++)
        {
            ItemData item = snapshot.Items[i];
            if (item != null && item.Icon == null)
            {
                context.Add(
                    item,
                    "item.visual.icon.missing",
                    "Item icon is missing.",
                    ContentValidationSeverity.Warning);
            }
        }
    }
}
#endif
