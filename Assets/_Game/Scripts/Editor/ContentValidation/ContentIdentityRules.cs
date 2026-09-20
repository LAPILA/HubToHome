#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class ContentIdentityRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        ValidateIds(snapshot.Characters, data => data != null ? data.CharacterID : null, "character", "Character", context);
        ValidateIds(snapshot.Enemies, data => data != null ? data.EnemyId : null, "enemy", "Enemy", context);
        ValidateSkills(context);
        ValidateIds(snapshot.Items, data => data != null ? data.ItemID : null, "item", "Item", context);
        ValidateIds(snapshot.Scenarios, data => data != null ? data.ScenarioId : null, "scenario", "Scenario", context);
    }

    public static void ValidateSkills(ContentValidationRuleContext context)
    {
        ValidateIds(context.Snapshot.Skills, data => data != null ? data.SkillID : null, "skill", "Skill", context);
    }

    private static void ValidateIds<T>(
        IReadOnlyList<T> assets,
        Func<T, string> getId,
        string codePrefix,
        string displayName,
        ContentValidationRuleContext context) where T : UnityEngine.Object
    {
        var owners = new Dictionary<string, T>(StringComparer.Ordinal);
        var reportedFirstOwners = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < assets.Count; i++)
        {
            T asset = assets[i];
            if (asset == null)
                continue;

            string id = getId(asset);
            if (string.IsNullOrWhiteSpace(id))
            {
                context.Add(asset, codePrefix + ".id.missing", displayName + " ID is missing.");
                continue;
            }

            if (!ContentIdPolicy.IsValid(id))
            {
                context.Add(
                    asset,
                    codePrefix + ".id.invalid",
                    displayName + " ID '" + id + "' does not follow the content ID rule.");
                continue;
            }

            if (owners.TryGetValue(id, out T previous))
            {
                string previousPath = context.Snapshot.GetAssetPath(previous);
                context.Add(
                    asset,
                    codePrefix + ".id.duplicate",
                    "Duplicate " + displayName + " ID '" + id + "' (also '" + previousPath + "').");
                // Each conflicting asset must expose the issue when inspected on its own.
                if (reportedFirstOwners.Add(id))
                    context.Add(previous, codePrefix + ".id.duplicate",
                        "Duplicate " + displayName + " ID '" + id + "' (also '" + context.Snapshot.GetAssetPath(asset) + "').");
                continue;
            }

            owners.Add(id, asset);
        }
    }
}
#endif
