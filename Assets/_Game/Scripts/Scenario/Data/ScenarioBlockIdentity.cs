using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public static class ScenarioBlockIdentity
{
    public static string Create()
    {
        return Guid.NewGuid().ToString("N");
    }

    public static void EnsureUnique(List<ScenarioActionData> actions)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        EnsureUnique(actions, seenIds, string.Empty, string.Empty);
    }

    public static void EnsureUnique(
        List<ScenarioActionData> actions,
        string deterministicSeed)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        EnsureUnique(actions, seenIds, Normalize(deterministicSeed), string.Empty);
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
        HashSet<string> seenIds,
        string deterministicSeed,
        string parentPath)
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

            string path = string.IsNullOrEmpty(parentPath)
                ? i.ToString()
                : parentPath + "/" + i;
            string blockId = Normalize(action.BlockId);
            if (string.IsNullOrEmpty(blockId) || !seenIds.Add(blockId))
            {
                blockId = CreateUnique(seenIds, deterministicSeed, path, action.ActionId);
            }

            action.BlockId = blockId;
            EnsureUnique(action.Children, seenIds, deterministicSeed, path);
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

    private static string CreateUnique(
        HashSet<string> seenIds,
        string deterministicSeed,
        string path,
        string actionId)
    {
        if (!string.IsNullOrEmpty(deterministicSeed))
        {
            int attempt = 0;
            while (true)
            {
                string deterministicId = CreateDeterministic(
                    deterministicSeed + "|" + path + "|" + Normalize(actionId) + "|" + attempt);
                if (seenIds.Add(deterministicId))
                {
                    return deterministicId;
                }

                attempt++;
            }
        }

        string blockId;
        do
        {
            blockId = Create();
        }
        while (!seenIds.Add(blockId));

        return blockId;
    }

    private static string CreateDeterministic(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
