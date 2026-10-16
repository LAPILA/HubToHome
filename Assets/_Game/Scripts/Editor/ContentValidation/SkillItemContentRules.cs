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

            if (item.IsStackable && (item.MaxStackSize < 1 || item.MaxStackSize > 99))
            {
                context.Add(
                    item,
                    "item.stack.max.invalid",
                    "아이템 최대 보유 수량은 1~99로 지정하세요.");
            }

            if (item.Type == ItemType.Equipment)
            {
                if (item.Equipment == null)
                    context.Add(item, "item.equipment.missing", "장비 상품의 EquipmentData를 연결하세요.");
                else
                {
                    for (int previous = 0; previous < i; previous++)
                    {
                        ItemData other = snapshot.Items[previous];
                        if (other != null && other.Type == ItemType.Equipment && other.Equipment == item.Equipment)
                        {
                            context.Add(item, "item.equipment.product.duplicate", "같은 장비를 여러 상품 ItemData로 중복 등록하지 마세요. 상점별 가격은 ShopEntry에서 지정합니다.");
                            break;
                        }
                    }
                    if (snapshot.Catalog == null || snapshot.Catalog.Equipment == null || !snapshot.Catalog.Equipment.Contains(item.Equipment))
                        context.Add(item, "item.equipment.not_registered", "장비를 런타임 카탈로그에 등록하세요.");
                    if (!item.Equipment.HasValidWeaponOwner())
                        context.Add(item, "equipment.weapon.owner.missing", "무기는 전용 캐릭터 ID 하나가 필요합니다.");
                    if (item.Equipment.TriggeredEffects != null)
                        for (int effectIndex = 0; effectIndex < item.Equipment.TriggeredEffects.Count; effectIndex++)
                            if (item.Equipment.TriggeredEffects[effectIndex] == null || !item.Equipment.TriggeredEffects[effectIndex].IsValid)
                                context.Add(item, "equipment.triggered_effect.invalid", "장비 조건부 효과의 조건/효과/수치를 확인하세요.");
                }
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

            if ((item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage || item.ActionType == EffectActionType.Revive)
                && item.TargetStat != TargetStatType.HP
                && item.TargetStat != TargetStatType.AP)
            {
                context.Add(
                    item,
                    "item.consumable.target_stat.invalid",
                    "Heal or damage item must target HP or AP.");
            }

            if (item.ActionType == EffectActionType.Revive && item.TargetStat != TargetStatType.HP)
                context.Add(item, "item.revive.target_stat.invalid", "부활 아이템은 HP를 회복해야 합니다.");
            if (item.ActionType == EffectActionType.Cleanse && !item.CleanseAllHarmful
                && !System.Enum.IsDefined(typeof(ElementalStatusKind), item.CleanseElement))
                context.Add(item, "item.cleanse.element.invalid", "정화할 누적 상태가 올바르지 않습니다.");
            if (!System.Enum.IsDefined(typeof(EffectActionType), item.ActionType))
                context.Add(item, "item.consumable.effect.unknown", "지원하지 않는 아이템 효과입니다.");

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
