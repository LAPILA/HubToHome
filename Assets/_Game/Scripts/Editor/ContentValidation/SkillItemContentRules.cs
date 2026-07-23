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
            if (skill == null)
                continue;

            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
            for (int issueIndex = 0; issueIndex < report.Issues.Count; issueIndex++)
            {
                EnemyAttackAuthoringIssue issue = report.Issues[issueIndex];
                string prefix = issue.BlockIndex >= 0
                    ? "ActionTimeline[" + issue.BlockIndex + "] "
                    : string.Empty;
                ContentValidationSeverity severity =
                    issue.Severity == EnemyAttackAuthoringSeverity.Error
                        ? ContentValidationSeverity.Error
                        : ContentValidationSeverity.Warning;
                context.Add(skill, issue.Code, prefix + issue.Message, severity);
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
