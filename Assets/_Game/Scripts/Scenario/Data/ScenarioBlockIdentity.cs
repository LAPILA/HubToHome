using System;
using System.Collections.Generic;

public static class ScenarioBlockIdentity
{
    public static string Create()
    {
        return Guid.NewGuid().ToString("N");
    }

    public static void EnsureUnique(List<ScenarioActionData> actions)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        EnsureUnique(actions, seenIds);
    }

    public static ScenarioActionData ClonePreservingIds(ScenarioActionData source)
    {
        return Clone(source, preserveIds: true);
    }

    public static ScenarioActionData CloneWithNewIds(ScenarioActionData source)
    {
        return Clone(source, preserveIds: false);
    }

    private static void EnsureUnique(
        List<ScenarioActionData> actions,
        HashSet<string> seenIds)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            string blockId = Normalize(action.BlockId);
            if (string.IsNullOrEmpty(blockId) || !seenIds.Add(blockId))
            {
                blockId = CreateUnique(seenIds);
            }

            action.BlockId = blockId;
            EnsureUnique(action.Children, seenIds);
        }
    }

    private static ScenarioActionData Clone(
        ScenarioActionData source,
        bool preserveIds)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new ScenarioActionData
        {
            BlockId = preserveIds ? Normalize(source.BlockId) : Create(),
            DesignerLabel = source.DesignerLabel ?? string.Empty,
            ActionId = source.ActionId ?? string.Empty,
            ParametersJson = source.ParametersJson ?? "{}",
            Note = source.Note ?? string.Empty,
            Disabled = source.Disabled,
            Children = new List<ScenarioActionData>()
        };

        if (source.Children == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Children.Count; i++)
        {
            clone.Children.Add(Clone(source.Children[i], preserveIds));
        }

        return clone;
    }

    private static string CreateUnique(HashSet<string> seenIds)
    {
        string blockId;
        do
        {
            blockId = Create();
        }
        while (!seenIds.Add(blockId));

        return blockId;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
