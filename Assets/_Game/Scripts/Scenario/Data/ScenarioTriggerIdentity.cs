using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public static class ScenarioTriggerIdentity
{
    public static string Create()
    {
        return Guid.NewGuid().ToString("N");
    }

    public static void EnsureUnique(
        ScenarioTriggerConditionNodeData root,
        string deterministicSeed = "")
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        EnsureUnique(root, seen, Normalize(deterministicSeed), "root");
    }

    public static ScenarioTriggerConditionNodeData ClonePreservingIds(
        ScenarioTriggerConditionNodeData source)
    {
        return Clone(source, preserveIds: true);
    }

    public static ScenarioTriggerConditionNodeData CloneWithNewIds(
        ScenarioTriggerConditionNodeData source)
    {
        return Clone(source, preserveIds: false);
    }

    public static ScenarioTriggerRuleData CloneRule(ScenarioTriggerRuleData source)
    {
        if (source == null)
        {
            return null;
        }

        return new ScenarioTriggerRuleData
        {
            RuleId = source.RuleId ?? string.Empty,
            DisplayNameKo = source.DisplayNameKo ?? string.Empty,
            EventId = source.EventId ?? string.Empty,
            Timing = source.Timing,
            CheckpointId = source.CheckpointId ?? string.Empty,
            Once = source.Once,
            Disabled = source.Disabled,
            Conditions = ClonePreservingIds(source.Conditions),
            SequenceId = source.SequenceId ?? string.Empty,
            TargetInputsJson = source.TargetInputsJson ?? "{}"
        };
    }

    private static void EnsureUnique(
        ScenarioTriggerConditionNodeData node,
        HashSet<string> seen,
        string seed,
        string path)
    {
        if (node == null)
        {
            return;
        }

        string nodeId = Normalize(node.NodeId);
        if (string.IsNullOrEmpty(nodeId) || !seen.Add(nodeId))
        {
            nodeId = CreateUnique(seen, seed, path, node.ConditionId);
        }

        node.NodeId = nodeId;
        if (node.Children == null)
        {
            node.Children = new List<ScenarioTriggerConditionNodeData>();
            return;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            EnsureUnique(node.Children[i], seen, seed, path + "/" + i);
        }
    }

    private static ScenarioTriggerConditionNodeData Clone(
        ScenarioTriggerConditionNodeData source,
        bool preserveIds)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new ScenarioTriggerConditionNodeData
        {
            NodeId = preserveIds ? Normalize(source.NodeId) : Create(),
            Kind = source.Kind,
            GroupMode = source.GroupMode,
            ConditionId = source.ConditionId ?? string.Empty,
            ParametersJson = source.ParametersJson ?? "{}",
            Negate = source.Negate,
            Children = new List<ScenarioTriggerConditionNodeData>()
        };
        if (source.Children != null)
        {
            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(Clone(source.Children[i], preserveIds));
            }
        }

        return clone;
    }

    private static string CreateUnique(
        HashSet<string> seen,
        string seed,
        string path,
        string conditionId)
    {
        if (!string.IsNullOrEmpty(seed))
        {
            int attempt = 0;
            while (true)
            {
                string candidate = Deterministic(seed + "|" + path + "|" + Normalize(conditionId) + "|" + attempt);
                if (seen.Add(candidate))
                {
                    return candidate;
                }

                attempt++;
            }
        }

        string id;
        do
        {
            id = Create();
        }
        while (!seen.Add(id));
        return id;
    }

    private static string Deterministic(string value)
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
