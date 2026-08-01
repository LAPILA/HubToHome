using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillTreeUnlockSaveData
{
    public string NodeId = string.Empty;
    public int PointsSpent;

    public SkillTreeUnlockSaveData Clone()
    {
        return new SkillTreeUnlockSaveData
        {
            NodeId = NodeId ?? string.Empty,
            PointsSpent = Mathf.Max(0, PointsSpent)
        };
    }
}

public enum SkillTreeNodeState
{
    Locked,
    Available,
    Unlocked,
    Equipped
}

public readonly struct SkillTreeNodeView
{
    public SkillTreeNodeView(
        SkillTreeNodeDefinition definition,
        SkillTreeNodeState state,
        string lockReason)
    {
        Definition = definition;
        State = state;
        LockReason = lockReason ?? string.Empty;
    }

    public SkillTreeNodeDefinition Definition { get; }
    public SkillTreeNodeState State { get; }
    public string LockReason { get; }
    public bool IsUnlocked =>
        State == SkillTreeNodeState.Unlocked || State == SkillTreeNodeState.Equipped;
}

public enum SkillTreeActionStatus
{
    Success,
    InvalidCharacter,
    MissingTree,
    MissingNode,
    AlreadyUnlocked,
    LevelLocked,
    PrerequisiteLocked,
    InsufficientPoints,
    SkillMissing,
    EquipLimitReached,
    NotUnlocked,
    NoChanges,
    MutationLocked
}

public readonly struct SkillTreeActionResult
{
    public SkillTreeActionResult(
        SkillTreeActionStatus status,
        string nodeId,
        int pointsChanged,
        string message)
    {
        Status = status;
        NodeId = nodeId ?? string.Empty;
        PointsChanged = pointsChanged;
        Message = message ?? string.Empty;
    }

    public SkillTreeActionStatus Status { get; }
    public string NodeId { get; }
    public int PointsChanged { get; }
    public string Message { get; }
    public bool Succeeded => Status == SkillTreeActionStatus.Success;
}

public static class SkillTreeProgressionService
{
    public static bool Synchronize(
        CharacterSaveData character,
        CharacterData data)
    {
        if (character == null)
            return false;

        CharacterGrowthService.EnsureInitialized(character, data);
        character.Growth.SkillTreeUnlocks ??=
            new List<SkillTreeUnlockSaveData>();
        character.UnlockedSkillIDs ??= new List<string>();
        character.EquippedSkillIDs ??= new List<string>();

        SkillTreeDefinition tree = data != null ? data.SkillTree : null;
        if (tree == null)
            return NormalizeUnlockRecords(character.Growth.SkillTreeUnlocks, null);

        bool changed = NormalizeSkillIds(character.UnlockedSkillIDs);
        changed |= NormalizeSkillIds(character.EquippedSkillIDs);
        changed |= NormalizeUnlockRecords(
            character.Growth.SkillTreeUnlocks,
            tree);

        var unlockedNodes = BuildUnlockMap(character.Growth.SkillTreeUnlocks);
        var legacySkills = new HashSet<string>(
            character.UnlockedSkillIDs,
            StringComparer.Ordinal);
        legacySkills.UnionWith(character.EquippedSkillIDs);

        int level = Mathf.Max(1, character.Level);
        for (int i = 0; i < tree.Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = tree.Nodes[i];
            if (node == null)
                continue;

            string nodeId = node.ResolveId();
            string skillId = ResolveSkillId(node.Skill);
            bool freeRoot = node.Cost <= 0
                && level >= Mathf.Max(1, node.RequiredLevel)
                && !HasPrerequisites(node);
            bool legacyUnlock = !string.IsNullOrEmpty(skillId)
                && legacySkills.Contains(skillId);
            bool addedUnlock = false;
            if (!unlockedNodes.ContainsKey(nodeId)
                && !string.IsNullOrEmpty(nodeId)
                && (freeRoot || legacyUnlock))
            {
                var unlock = new SkillTreeUnlockSaveData
                {
                    NodeId = nodeId,
                    PointsSpent = 0
                };
                character.Growth.SkillTreeUnlocks.Add(unlock);
                unlockedNodes[nodeId] = unlock;
                addedUnlock = true;
                changed = true;
            }

            if (addedUnlock && freeRoot && node.AutoEquip)
                changed |= TryAutoEquip(character, tree, skillId);

            if (unlockedNodes.ContainsKey(nodeId)
                && !string.IsNullOrEmpty(skillId))
            {
                changed |= AddUnique(character.UnlockedSkillIDs, skillId);
            }
        }

        int spent = CalculateSpentPoints(character.Growth.SkillTreeUnlocks);
        if (character.Growth.SkillPointsSpent != spent)
        {
            character.Growth.SkillPointsSpent = spent;
            changed = true;
        }

        return changed;
    }

    public static List<SkillTreeNodeView> BuildViews(
        CharacterSaveData character,
        CharacterData data)
    {
        var result = new List<SkillTreeNodeView>();
        if (character == null || data?.SkillTree == null)
            return result;

        Synchronize(character, data);
        SkillTreeDefinition tree = data.SkillTree;
        var unlocked = BuildUnlockMap(character.Growth.SkillTreeUnlocks);
        var equipped = new HashSet<string>(
            character.EquippedSkillIDs ?? new List<string>(),
            StringComparer.Ordinal);

        for (int i = 0; i < tree.Nodes.Count; i++)
        {
            SkillTreeNodeDefinition node = tree.Nodes[i];
            if (node == null || string.IsNullOrEmpty(node.ResolveId()))
                continue;

            string skillId = ResolveSkillId(node.Skill);
            bool isUnlocked = unlocked.ContainsKey(node.ResolveId());
            SkillTreeNodeState state;
            string lockReason = string.Empty;
            if (isUnlocked)
            {
                state = !string.IsNullOrEmpty(skillId) && equipped.Contains(skillId)
                    ? SkillTreeNodeState.Equipped
                    : SkillTreeNodeState.Unlocked;
            }
            else if (character.Level < Mathf.Max(1, node.RequiredLevel))
            {
                state = SkillTreeNodeState.Locked;
                lockReason = "LV " + Mathf.Max(1, node.RequiredLevel) + " 필요";
            }
            else if (!ArePrerequisitesUnlocked(node, unlocked))
            {
                state = SkillTreeNodeState.Locked;
                lockReason = "선행 능력 필요";
            }
            else if (character.Growth.AvailableSkillPoints < Mathf.Max(0, node.Cost))
            {
                state = SkillTreeNodeState.Locked;
                lockReason = "스킬 포인트 부족";
            }
            else
            {
                state = SkillTreeNodeState.Available;
            }

            result.Add(new SkillTreeNodeView(node, state, lockReason));
        }

        return result;
    }

    public static SkillTreeActionResult TryUnlock(
        CharacterSaveData character,
        CharacterData data,
        string nodeId)
    {
        if (character == null)
            return Failed(SkillTreeActionStatus.InvalidCharacter, nodeId, "캐릭터가 없습니다.");
        if (data?.SkillTree == null)
            return Failed(SkillTreeActionStatus.MissingTree, nodeId, "스킬 트리가 없습니다.");

        Synchronize(character, data);
        SkillTreeNodeDefinition node = data.SkillTree.FindNode(nodeId);
        if (node == null)
            return Failed(SkillTreeActionStatus.MissingNode, nodeId, "스킬 노드를 찾을 수 없습니다.");
        if (node.Skill == null)
            return Failed(SkillTreeActionStatus.SkillMissing, nodeId, "노드에 스킬이 연결되지 않았습니다.");

        var unlocked = BuildUnlockMap(character.Growth.SkillTreeUnlocks);
        string resolvedNodeId = node.ResolveId();
        if (unlocked.ContainsKey(resolvedNodeId))
            return Failed(SkillTreeActionStatus.AlreadyUnlocked, resolvedNodeId, "이미 해금한 능력입니다.");
        if (character.Level < Mathf.Max(1, node.RequiredLevel))
            return Failed(SkillTreeActionStatus.LevelLocked, resolvedNodeId, "요구 레벨에 도달하지 못했습니다.");
        if (!ArePrerequisitesUnlocked(node, unlocked))
            return Failed(SkillTreeActionStatus.PrerequisiteLocked, resolvedNodeId, "선행 능력을 먼저 해금해야 합니다.");

        int cost = Mathf.Max(0, node.Cost);
        if (character.Growth.AvailableSkillPoints < cost)
            return Failed(SkillTreeActionStatus.InsufficientPoints, resolvedNodeId, "스킬 포인트가 부족합니다.");
        if (cost > 0
            && !CharacterGrowthService.TrySpendSkillPoints(character, data, cost))
        {
            return Failed(SkillTreeActionStatus.InsufficientPoints, resolvedNodeId, "스킬 포인트가 부족합니다.");
        }

        character.Growth.SkillTreeUnlocks.Add(new SkillTreeUnlockSaveData
        {
            NodeId = resolvedNodeId,
            PointsSpent = cost
        });
        string skillId = ResolveSkillId(node.Skill);
        AddUnique(character.UnlockedSkillIDs, skillId);
        if (node.AutoEquip)
            TryAutoEquip(character, data.SkillTree, skillId);

        return new SkillTreeActionResult(
            SkillTreeActionStatus.Success,
            resolvedNodeId,
            -cost,
            node.Skill.SkillName + " 해금");
    }

    public static SkillTreeActionResult TryToggleEquipped(
        CharacterSaveData character,
        CharacterData data,
        string nodeId)
    {
        if (character == null)
            return Failed(SkillTreeActionStatus.InvalidCharacter, nodeId, "캐릭터가 없습니다.");
        if (data?.SkillTree == null)
            return Failed(SkillTreeActionStatus.MissingTree, nodeId, "스킬 트리가 없습니다.");

        Synchronize(character, data);
        SkillTreeNodeDefinition node = data.SkillTree.FindNode(nodeId);
        if (node == null)
            return Failed(SkillTreeActionStatus.MissingNode, nodeId, "스킬 노드를 찾을 수 없습니다.");
        if (node.Skill == null)
            return Failed(SkillTreeActionStatus.SkillMissing, nodeId, "노드에 스킬이 연결되지 않았습니다.");
        if (FindUnlock(character.Growth.SkillTreeUnlocks, node.ResolveId()) == null)
            return Failed(SkillTreeActionStatus.NotUnlocked, nodeId, "먼저 능력을 해금해야 합니다.");

        string skillId = ResolveSkillId(node.Skill);
        character.EquippedSkillIDs ??= new List<string>();
        int equippedIndex = character.EquippedSkillIDs.FindIndex(
            id => string.Equals(id, skillId, StringComparison.Ordinal));
        if (equippedIndex >= 0)
        {
            character.EquippedSkillIDs.RemoveAt(equippedIndex);
            return new SkillTreeActionResult(
                SkillTreeActionStatus.Success,
                node.ResolveId(),
                0,
                node.Skill.SkillName + " 장착 해제");
        }

        int maximum = Mathf.Max(1, data.SkillTree.MaximumEquippedSkills);
        if (character.EquippedSkillIDs.Count >= maximum)
        {
            return Failed(
                SkillTreeActionStatus.EquipLimitReached,
                node.ResolveId(),
                "장착 가능한 스킬 수를 초과했습니다.");
        }

        character.EquippedSkillIDs.Add(skillId);
        return new SkillTreeActionResult(
            SkillTreeActionStatus.Success,
            node.ResolveId(),
            0,
            node.Skill.SkillName + " 장착");
    }

    public static SkillTreeActionResult Reset(
        CharacterSaveData character,
        CharacterData data)
    {
        if (character == null)
            return Failed(SkillTreeActionStatus.InvalidCharacter, string.Empty, "캐릭터가 없습니다.");
        if (data?.SkillTree == null)
            return Failed(SkillTreeActionStatus.MissingTree, string.Empty, "스킬 트리가 없습니다.");

        Synchronize(character, data);
        var removedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        int refunded = 0;
        for (int i = character.Growth.SkillTreeUnlocks.Count - 1; i >= 0; i--)
        {
            SkillTreeUnlockSaveData unlock = character.Growth.SkillTreeUnlocks[i];
            if (unlock == null || unlock.PointsSpent <= 0)
                continue;

            refunded = SaturatingAdd(refunded, unlock.PointsSpent);
            removedNodeIds.Add(unlock.NodeId);
            character.Growth.SkillTreeUnlocks.RemoveAt(i);
        }

        if (removedNodeIds.Count == 0)
            return Failed(SkillTreeActionStatus.NoChanges, string.Empty, "초기화할 스킬 투자가 없습니다.");

        foreach (string removedNodeId in removedNodeIds)
        {
            SkillTreeNodeDefinition node = data.SkillTree.FindNode(removedNodeId);
            string skillId = ResolveSkillId(node?.Skill);
            RemoveAll(character.EquippedSkillIDs, skillId);
            if (!IsBaselineSkillUnlocked(character, data, skillId))
                RemoveAll(character.UnlockedSkillIDs, skillId);
        }

        character.Growth.SkillPointsSpent =
            CalculateSpentPoints(character.Growth.SkillTreeUnlocks);
        PowerProgressionService.SynchronizeUnlockedSkills(character, data);
        Synchronize(character, data);
        return new SkillTreeActionResult(
            SkillTreeActionStatus.Success,
            string.Empty,
            refunded,
            "스킬 포인트 " + refunded + " 반환");
    }

    public static int FindDirectionalNodeIndex(
        IReadOnlyList<SkillTreeNodeView> nodes,
        int currentIndex,
        Vector2 direction)
    {
        if (nodes == null || nodes.Count == 0)
            return 0;

        int originIndex = Mathf.Clamp(currentIndex, 0, nodes.Count - 1);
        Vector2 origin = nodes[originIndex].Definition.Position;
        Vector2 normalizedDirection = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector2.right;
        int bestIndex = originIndex;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == originIndex)
                continue;

            Vector2 offset = nodes[i].Definition.Position - origin;
            float distance = offset.magnitude;
            if (distance <= 0.01f)
                continue;

            float alignment = Vector2.Dot(offset / distance, normalizedDirection);
            if (alignment < 0.35f)
                continue;

            float score = alignment * 1000f - distance;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public static bool NormalizeUnlockRecords(
        List<SkillTreeUnlockSaveData> records,
        SkillTreeDefinition tree)
    {
        if (records == null)
            return false;

        bool changed = false;
        var known = new HashSet<string>(StringComparer.Ordinal);
        for (int i = records.Count - 1; i >= 0; i--)
        {
            SkillTreeUnlockSaveData record = records[i];
            string id = NormalizeId(record?.NodeId);
            bool unknown = tree != null && tree.FindNode(id) == null;
            if (record == null
                || string.IsNullOrEmpty(id)
                || unknown
                || !known.Add(id))
            {
                records.RemoveAt(i);
                changed = true;
                continue;
            }

            int safePoints = Mathf.Max(0, record.PointsSpent);
            if (!string.Equals(record.NodeId, id, StringComparison.Ordinal)
                || record.PointsSpent != safePoints)
            {
                record.NodeId = id;
                record.PointsSpent = safePoints;
                changed = true;
            }
        }
        return changed;
    }

    private static Dictionary<string, SkillTreeUnlockSaveData> BuildUnlockMap(
        IReadOnlyList<SkillTreeUnlockSaveData> records)
    {
        var result = new Dictionary<string, SkillTreeUnlockSaveData>(
            StringComparer.Ordinal);
        if (records == null)
            return result;

        for (int i = 0; i < records.Count; i++)
        {
            SkillTreeUnlockSaveData record = records[i];
            string id = NormalizeId(record?.NodeId);
            if (!string.IsNullOrEmpty(id) && !result.ContainsKey(id))
                result.Add(id, record);
        }

        return result;
    }

    private static SkillTreeUnlockSaveData FindUnlock(
        IReadOnlyList<SkillTreeUnlockSaveData> records,
        string nodeId)
    {
        string normalized = NormalizeId(nodeId);
        if (records == null || string.IsNullOrEmpty(normalized))
            return null;

        for (int i = 0; i < records.Count; i++)
        {
            if (string.Equals(
                NormalizeId(records[i]?.NodeId),
                normalized,
                StringComparison.Ordinal))
            {
                return records[i];
            }
        }

        return null;
    }

    private static bool ArePrerequisitesUnlocked(
        SkillTreeNodeDefinition node,
        IReadOnlyDictionary<string, SkillTreeUnlockSaveData> unlocked)
    {
        if (node?.PrerequisiteNodeIds == null)
            return true;

        for (int i = 0; i < node.PrerequisiteNodeIds.Count; i++)
        {
            string prerequisite = NormalizeId(node.PrerequisiteNodeIds[i]);
            if (!string.IsNullOrEmpty(prerequisite)
                && !unlocked.ContainsKey(prerequisite))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasPrerequisites(SkillTreeNodeDefinition node)
    {
        if (node?.PrerequisiteNodeIds == null)
            return false;

        for (int i = 0; i < node.PrerequisiteNodeIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(node.PrerequisiteNodeIds[i]))
                return true;
        }

        return false;
    }

    private static bool TryAutoEquip(
        CharacterSaveData character,
        SkillTreeDefinition tree,
        string skillId)
    {
        if (character == null || tree == null || string.IsNullOrEmpty(skillId))
            return false;

        character.EquippedSkillIDs ??= new List<string>();
        if (character.EquippedSkillIDs.Contains(skillId))
            return false;
        if (character.EquippedSkillIDs.Count
            >= Mathf.Max(1, tree.MaximumEquippedSkills))
        {
            return false;
        }

        character.EquippedSkillIDs.Add(skillId);
        return true;
    }

    private static bool IsBaselineSkillUnlocked(
        CharacterSaveData character,
        CharacterData data,
        string skillId)
    {
        if (data == null || string.IsNullOrEmpty(skillId))
            return false;

        if (data.DefaultSkills != null)
        {
            for (int i = 0; i < data.DefaultSkills.Count; i++)
            {
                if (string.Equals(
                    ResolveSkillId(data.DefaultSkills[i]),
                    skillId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        if (data.PowerUnlocks != null)
        {
            int level = Mathf.Max(1, character?.Level ?? 1);
            for (int i = 0; i < data.PowerUnlocks.Count; i++)
            {
                CharacterPowerUnlock unlock = data.PowerUnlocks[i];
                if (unlock != null
                    && level >= Mathf.Max(1, unlock.RequiredLevel)
                    && string.Equals(
                        ResolveSkillId(unlock.Skill),
                        skillId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CalculateSpentPoints(
        IReadOnlyList<SkillTreeUnlockSaveData> records)
    {
        int total = 0;
        if (records == null)
            return total;

        for (int i = 0; i < records.Count; i++)
            total = SaturatingAdd(total, Mathf.Max(0, records[i]?.PointsSpent ?? 0));
        return total;
    }

    private static bool NormalizeSkillIds(List<string> ids)
    {
        if (ids == null)
            return false;

        bool changed = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            string normalized = NormalizeId(ids[i]);
            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                ids.RemoveAt(i);
                changed = true;
            }
            else if (!string.Equals(ids[i], normalized, StringComparison.Ordinal))
            {
                ids[i] = normalized;
                changed = true;
            }
        }

        return changed;
    }

    private static bool AddUnique(ICollection<string> ids, string id)
    {
        string normalized = NormalizeId(id);
        if (ids == null || string.IsNullOrEmpty(normalized))
            return false;

        foreach (string existing in ids)
        {
            if (string.Equals(existing, normalized, StringComparison.Ordinal))
                return false;
        }

        ids.Add(normalized);
        return true;
    }

    private static void RemoveAll(List<string> ids, string id)
    {
        if (ids == null || string.IsNullOrEmpty(id))
            return;

        for (int i = ids.Count - 1; i >= 0; i--)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
                ids.RemoveAt(i);
        }
    }

    private static string ResolveSkillId(SkillData skill)
    {
        return NormalizeId(skill != null ? skill.SkillID : null);
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static int SaturatingAdd(int left, int right)
    {
        long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return sum >= int.MaxValue ? int.MaxValue : (int)sum;
    }

    private static SkillTreeActionResult Failed(
        SkillTreeActionStatus status,
        string nodeId,
        string message)
    {
        return new SkillTreeActionResult(status, nodeId, 0, message);
    }
}