#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class BattleContentRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        ValidateCharacterReferences(context);
        ValidateEnemyReferences(context);
    }

    private static void ValidateCharacterReferences(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        var projectSkills = new HashSet<SkillData>(snapshot.Skills);
        for (int i = 0; i < snapshot.Characters.Count; i++)
        {
            CharacterData data = snapshot.Characters[i];
            if (data == null)
                continue;

            ValidateBattlePrefab<PlayerCharacter>(data, data.BattlePrefab, "character", "Character", context);
            ValidateSkillReferences(
                data.DefaultSkills,
                projectSkills,
                data,
                "character.default_skill",
                "DefaultSkills",
                context);
            AddOptionalVisualWarning(
                data.Portrait,
                data,
                "character.visual.portrait.missing",
                "Character portrait is missing.",
                context);
            AddOptionalVisualWarning(
                data.TurnOrderPortrait,
                data,
                "character.visual.turn_order_portrait.missing",
                "Character turn order portrait is missing.",
                context);
        }
    }

    private static void ValidateEnemyReferences(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        var projectSkills = new HashSet<SkillData>(snapshot.Skills);
        HashSet<string> projectItemIds = BuildItemIds(snapshot.Items);
        for (int i = 0; i < snapshot.Enemies.Count; i++)
        {
            EnemyData data = snapshot.Enemies[i];
            if (data == null)
                continue;

            ValidateBattlePrefab<EnemyCharacter>(data, data.BattlePrefab, "enemy", "Enemy", context);
            ValidateSkillReferences(
                data.SkillList,
                projectSkills,
                data,
                "enemy.skill",
                "SkillList",
                context);
            ValidateSkillReferences(
                data.StrongSkillList,
                projectSkills,
                data,
                "enemy.strong_skill",
                "StrongSkillList",
                context);
            ValidateEnemyDrops(data, projectItemIds, context);
            AddOptionalVisualWarning(
                data.Portrait,
                data,
                "enemy.visual.portrait.missing",
                "Enemy portrait is missing.",
                context);
            AddOptionalVisualWarning(
                data.TurnOrderPortrait,
                data,
                "enemy.visual.turn_order_portrait.missing",
                "Enemy turn order portrait is missing.",
                context);
        }
    }

    private static HashSet<string> BuildItemIds(IReadOnlyList<ItemData> items)
    {
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item != null && !string.IsNullOrWhiteSpace(item.ItemID))
                itemIds.Add(item.ItemID.Trim());
        }

        return itemIds;
    }

    private static void ValidateEnemyDrops(
        EnemyData enemy,
        HashSet<string> projectItemIds,
        ContentValidationRuleContext context)
    {
        if (enemy.Drops != null)
        {
            for (int i = 0; i < enemy.Drops.Count; i++)
            {
                EnemyDropEntry drop = enemy.Drops[i];
                if (drop == null || string.IsNullOrWhiteSpace(drop.ItemId))
                {
                    context.Add(enemy, "enemy.drop.item.missing", "Drops[" + i + "] item ID is missing.");
                }
                else if (!projectItemIds.Contains(drop.ItemId.Trim()))
                {
                    context.Add(
                        enemy,
                        "enemy.drop.item.unknown",
                        "Drops[" + i + "] references unknown item '" + drop.ItemId + "'.");
                }

                if (drop == null)
                    continue;

                if (drop.MinAmount < 1 || drop.MaxAmount < drop.MinAmount)
                {
                    context.Add(
                        enemy,
                        "enemy.drop.amount.invalid",
                        "Drops[" + i + "] has an invalid amount range.");
                }

                if (drop.DropChance < 0f || drop.DropChance > 1f)
                {
                    context.Add(
                        enemy,
                        "enemy.drop.chance.invalid",
                        "Drops[" + i + "] chance must be between 0 and 1.");
                }
            }
        }

        if (enemy.DropItemIDs == null)
            return;

        for (int i = 0; i < enemy.DropItemIDs.Count; i++)
        {
            string itemId = enemy.DropItemIDs[i];
            if (string.IsNullOrWhiteSpace(itemId))
            {
                context.Add(enemy, "enemy.legacy_drop.item.missing", "DropItemIDs[" + i + "] is missing.");
            }
            else if (!projectItemIds.Contains(itemId.Trim()))
            {
                context.Add(
                    enemy,
                    "enemy.legacy_drop.item.unknown",
                    "DropItemIDs[" + i + "] references unknown item '" + itemId + "'.");
            }
        }
    }

    private static void ValidateBattlePrefab<TComponent>(
        UnityEngine.Object owner,
        GameObject prefab,
        string codePrefix,
        string displayName,
        ContentValidationRuleContext context) where TComponent : Component
    {
        if (prefab == null)
        {
            context.Add(
                owner,
                codePrefix + ".battle_prefab.missing",
                displayName + " battle prefab is missing.");
            return;
        }

        if (prefab.GetComponent<TComponent>() == null)
        {
            context.Add(
                owner,
                codePrefix + ".battle_prefab.component_missing",
                displayName + " battle prefab has no " + typeof(TComponent).Name + ".");
        }
    }

    private static void ValidateSkillReferences(
        IReadOnlyList<SkillData> references,
        HashSet<SkillData> projectSkills,
        UnityEngine.Object owner,
        string codePrefix,
        string fieldName,
        ContentValidationRuleContext context)
    {
        if (references == null)
            return;

        for (int i = 0; i < references.Count; i++)
        {
            SkillData skill = references[i];
            if (skill == null)
            {
                context.Add(owner, codePrefix + ".missing", fieldName + "[" + i + "] is missing.");
            }
            else if (!projectSkills.Contains(skill))
            {
                context.Add(
                    owner,
                    codePrefix + ".unknown",
                    fieldName + "[" + i + "] references a Skill outside project content.");
            }
        }
    }

    private static void AddOptionalVisualWarning(
        UnityEngine.Object visual,
        UnityEngine.Object owner,
        string code,
        string message,
        ContentValidationRuleContext context)
    {
        if (visual == null)
            context.Add(owner, code, message, ContentValidationSeverity.Warning);
    }
}
#endif
