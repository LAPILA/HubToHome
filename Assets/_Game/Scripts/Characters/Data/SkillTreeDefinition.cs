using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class SkillTreeNodeDefinition
{
    [HorizontalGroup("Identity", 0.45f), LabelWidth(55f)]
    public string NodeId = "node.skill";

    [HorizontalGroup("Identity"), Required, LabelWidth(38f)]
    public SkillData Skill;

    [HorizontalGroup("Rules"), MinValue(0), LabelWidth(48f)]
    public int Cost = 1;

    [HorizontalGroup("Rules"), MinValue(1), LabelWidth(62f)]
    public int RequiredLevel = 1;

    [HorizontalGroup("Rules"), LabelWidth(66f)]
    public bool AutoEquip = true;

    [LabelText("UI Position")]
    public Vector2 Position;

    [ListDrawerSettings(ShowIndexLabels = true)]
    [LabelText("Prerequisite Node IDs")]
    public List<string> PrerequisiteNodeIds = new List<string>();

    public string ResolveId()
    {
        return string.IsNullOrWhiteSpace(NodeId) ? string.Empty : NodeId.Trim();
    }
}

[CreateAssetMenu(
    fileName = "SkillTreeDefinition",
    menuName = "HubToHome/Growth/Skill Tree")]
public sealed class SkillTreeDefinition : SerializedScriptableObject
{
    [MinValue(1)]
    [LabelText("Maximum Equipped Skills")]
    public int MaximumEquippedSkills = 6;

    [ListDrawerSettings(
        ShowIndexLabels = true,
        ShowFoldout = true,
        DefaultExpandedState = true,
        ListElementLabelName = "NodeId")]
    public List<SkillTreeNodeDefinition> Nodes = new List<SkillTreeNodeDefinition>();

    public SkillTreeNodeDefinition FindNode(string nodeId)
    {
        string normalized = NormalizeId(nodeId);
        if (string.IsNullOrEmpty(normalized) || Nodes == null)
            return null;

        for (int i = 0; i < Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = Nodes[i];
            if (node != null
                && string.Equals(
                    node.ResolveId(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public int FindNodeIndex(string nodeId)
    {
        string normalized = NormalizeId(nodeId);
        if (string.IsNullOrEmpty(normalized) || Nodes == null)
            return -1;

        for (int i = 0; i < Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = Nodes[i];
            if (node != null
                && string.Equals(
                    node.ResolveId(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    [Button("Validate Skill Tree", ButtonSizes.Medium)]
    public void ValidateSkillTree()
    {
        List<string> issues = CollectValidationIssues();
        if (issues.Count == 0)
        {
            Debug.Log("[SkillTreeDefinition] Validation passed: " + name, this);
            return;
        }

        Debug.LogError(
            "[SkillTreeDefinition] " + name + "\n- " + string.Join("\n- ", issues),
            this);
    }

    public List<string> CollectValidationIssues()
    {
        var issues = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (Nodes == null || Nodes.Count == 0)
        {
            issues.Add("노드가 없습니다.");
            return issues;
        }

        for (int i = 0; i < Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = Nodes[i];
            if (node == null)
            {
                issues.Add($"Nodes[{i}]가 비어 있습니다.");
                continue;
            }

            string id = node.ResolveId();
            if (string.IsNullOrEmpty(id))
                issues.Add($"Nodes[{i}]의 NodeId가 비어 있습니다.");
            else if (!ids.Add(id))
                issues.Add("중복 NodeId: " + id);
            if (node.Skill == null)
                issues.Add((string.IsNullOrEmpty(id) ? $"Nodes[{i}]" : id) + "에 Skill이 없습니다.");
        }

        for (int i = 0; i < Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = Nodes[i];
            if (node?.PrerequisiteNodeIds == null)
                continue;

            string nodeId = node.ResolveId();
            for (int prerequisiteIndex = 0;
                 prerequisiteIndex < node.PrerequisiteNodeIds.Count;
                 prerequisiteIndex++)
            {
                string prerequisite = NormalizeId(
                    node.PrerequisiteNodeIds[prerequisiteIndex]);
                if (string.IsNullOrEmpty(prerequisite))
                    issues.Add(nodeId + "에 빈 선행 노드 ID가 있습니다.");
                else if (string.Equals(nodeId, prerequisite, StringComparison.Ordinal))
                    issues.Add(nodeId + "가 자신을 선행 노드로 참조합니다.");
                else if (!ids.Contains(prerequisite))
                    issues.Add(nodeId + "의 선행 노드를 찾을 수 없습니다: " + prerequisite);
            }
        }

        DetectCycles(ids, issues);
        return issues;
    }

    private void DetectCycles(HashSet<string> knownIds, ICollection<string> issues)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in knownIds)
            Visit(id, visiting, visited, issues);
    }

    private void Visit(
        string nodeId,
        ISet<string> visiting,
        ISet<string> visited,
        ICollection<string> issues)
    {
        if (visited.Contains(nodeId))
            return;
        if (!visiting.Add(nodeId))
        {
            issues.Add("순환 선행 조건이 있습니다: " + nodeId);
            return;
        }

        SkillTreeNodeDefinition node = FindNode(nodeId);
        if (node?.PrerequisiteNodeIds != null)
        {
            for (int i = 0; i < node.PrerequisiteNodeIds.Count; i++)
            {
                string prerequisite = NormalizeId(node.PrerequisiteNodeIds[i]);
                if (!string.IsNullOrEmpty(prerequisite))
                    Visit(prerequisite, visiting, visited, issues);
            }
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}