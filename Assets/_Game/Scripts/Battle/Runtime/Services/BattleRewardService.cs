using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemRewardResult
{
    public string ItemId;
    public int Amount;
}

[Serializable]
public sealed class BattleRewardResult
{
    public int Experience;
    public int Gold;
    public readonly List<ItemRewardResult> Items = new List<ItemRewardResult>();
    public readonly List<CharacterLevelUpResult> LevelUps = new List<CharacterLevelUpResult>();

    public bool HasRewards => Experience > 0 || Gold > 0 || Items.Count > 0;
}

/// <summary>
/// 일반 전투 승리와 오버월드 즉시 처치가 공유하는 보상 계산/지급 서비스입니다.
/// </summary>
public static class BattleRewardService
{
    public static BattleRewardResult Calculate(
        IReadOnlyList<EnemyData> enemies,
        Func<float> randomValue = null)
    {
        var result = new BattleRewardResult();
        if (enemies == null) return result;

        Func<float> roll = randomValue ?? (() => UnityEngine.Random.value);
        var itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy == null) continue;

            result.Experience = SaturatingAdd(result.Experience, Mathf.Max(0, enemy.EXPReward));
            result.Gold = SaturatingAdd(result.Gold, Mathf.Max(0, enemy.GoldReward));

            if (enemy.Drops != null && enemy.Drops.Count > 0)
            {
                for (int dropIndex = 0; dropIndex < enemy.Drops.Count; dropIndex++)
                {
                    EnemyDropEntry drop = enemy.Drops[dropIndex];
                    if (drop == null || string.IsNullOrWhiteSpace(drop.ItemId)) continue;
                    float chance = Mathf.Clamp01(drop.DropChance);
                    if (chance <= 0f || (chance < 1f && NormalizeRoll(roll()) >= chance)) continue;
                    AddItem(
                        itemCounts,
                        drop.ItemId,
                        RollInclusiveAmount(drop.MinAmount, drop.MaxAmount, roll));
                }
            }
            else if (enemy.DropItemIDs != null)
            {
                for (int legacyIndex = 0; legacyIndex < enemy.DropItemIDs.Count; legacyIndex++)
                    AddItem(itemCounts, enemy.DropItemIDs[legacyIndex], 1);
            }
        }

        var sortedItemIds = new List<string>(itemCounts.Keys);
        sortedItemIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < sortedItemIds.Count; i++)
        {
            string itemId = sortedItemIds[i];
            result.Items.Add(new ItemRewardResult { ItemId = itemId, Amount = itemCounts[itemId] });
        }

        return result;
    }

    public static BattleRewardResult Grant(
        IReadOnlyList<EnemyData> enemies,
        GlobalDataManager global,
        Func<float> randomValue = null)
    {
        BattleRewardResult result = Calculate(enemies, randomValue);
        if (global == null) return result;

        global.AddMoney(result.Gold);
        for (int i = result.Items.Count - 1; i >= 0; i--)
        {
            ItemRewardResult item = result.Items[i];
            if (ItemDatabase.FindById(item.ItemId) == null)
            {
                Debug.LogWarning($"[BattleReward] Unknown item ID was skipped: {item.ItemId}");
                result.Items.RemoveAt(i);
                continue;
            }

            int added = global.AddItemAndGetAddedAmount(item.ItemId, item.Amount);
            if (added <= 0) result.Items.RemoveAt(i);
            else item.Amount = added;
        }

        for (int i = 0; i < global.Party.Count; i++)
        {
            CharacterSaveData member = global.Party[i];
            if (member == null) continue;
            CharacterData data = CharacterDatabase.FindById(member.CharacterDataID);
            result.LevelUps.Add(CharacterProgressionService.GrantExperience(member, data, result.Experience));
            PowerProgressionService.SynchronizeUnlockedSkills(member, data);
        }

        return result;
    }

    private static void AddItem(IDictionary<string, int> counts, string itemId, int amount)
    {
        string normalizedId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        if (string.IsNullOrEmpty(normalizedId) || amount <= 0) return;
        int current = counts.TryGetValue(normalizedId, out int existing) ? existing : 0;
        counts[normalizedId] = SaturatingAdd(current, amount);
    }

    private static int RollInclusiveAmount(int configuredMin, int configuredMax, Func<float> roll)
    {
        int minimum = Mathf.Max(1, configuredMin);
        int maximum = Mathf.Max(minimum, configuredMax);
        if (minimum == maximum)
            return minimum;

        double normalized = NormalizeRoll(roll());
        long range = (long)maximum - minimum + 1L;
        long offset = Math.Min(range - 1L, (long)(normalized * range));
        return (int)Math.Min(int.MaxValue, (long)minimum + offset);
    }

    private static double NormalizeRoll(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
            return 0d;
        if (value >= 1f)
            return 1d;
        return value;
    }

    private static int SaturatingAdd(int left, int right)
    {
        long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return (int)Math.Min(sum, int.MaxValue);
    }
}
