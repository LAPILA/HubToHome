using System;
using System.Collections.Generic;

public readonly struct CharacterPowerView
{
    public CharacterPowerView(string skillId, string name, string description, int requiredLevel, bool unlocked, bool equipped)
    {
        SkillId = skillId ?? string.Empty;
        Name = name ?? string.Empty;
        Description = description ?? string.Empty;
        RequiredLevel = Math.Max(1, requiredLevel);
        Unlocked = unlocked;
        Equipped = equipped;
    }

    public string SkillId { get; }
    public string Name { get; }
    public string Description { get; }
    public int RequiredLevel { get; }
    public bool Unlocked { get; }
    public bool Equipped { get; }
}

public static class PowerProgressionService
{
    public static bool SynchronizeUnlockedSkills(CharacterSaveData character, CharacterData data)
    {
        if (character == null)
            return false;

        character.UnlockedSkillIDs ??= new List<string>();
        character.EquippedSkillIDs ??= new List<string>();
        var known = new HashSet<string>(character.UnlockedSkillIDs, StringComparer.Ordinal);
        bool changed = false;

        if (data != null && data.DefaultSkills != null)
        {
            for (int i = 0; i < data.DefaultSkills.Count; i++)
                changed |= AddSkillId(data.DefaultSkills[i], character.UnlockedSkillIDs, known);
        }

        if (data != null && data.PowerUnlocks != null)
        {
            int level = Math.Max(1, character.Level);
            for (int i = 0; i < data.PowerUnlocks.Count; i++)
            {
                CharacterPowerUnlock unlock = data.PowerUnlocks[i];
                if (unlock != null && level >= Math.Max(1, unlock.RequiredLevel))
                    changed |= AddSkillId(unlock.Skill, character.UnlockedSkillIDs, known);
            }
        }

        for (int i = 0; i < character.EquippedSkillIDs.Count; i++)
        {
            string id = NormalizeId(character.EquippedSkillIDs[i]);
            if (!string.IsNullOrEmpty(id) && known.Add(id))
            {
                character.UnlockedSkillIDs.Add(id);
                changed = true;
            }
        }

        return changed;
    }

    public static List<CharacterPowerView> BuildViews(CharacterSaveData character, CharacterData data)
    {
        var result = new List<CharacterPowerView>();
        if (character == null)
            return result;

        SynchronizeUnlockedSkills(character, data);
        var unlocked = new HashSet<string>(character.UnlockedSkillIDs ?? new List<string>(), StringComparer.Ordinal);
        var equipped = new HashSet<string>(character.EquippedSkillIDs ?? new List<string>(), StringComparer.Ordinal);
        var added = new HashSet<string>(StringComparer.Ordinal);

        if (data != null && data.DefaultSkills != null)
        {
            for (int i = 0; i < data.DefaultSkills.Count; i++)
                AddView(result, added, unlocked, equipped, data.DefaultSkills[i], 1);
        }

        if (data != null && data.PowerUnlocks != null)
        {
            for (int i = 0; i < data.PowerUnlocks.Count; i++)
            {
                CharacterPowerUnlock unlock = data.PowerUnlocks[i];
                if (unlock != null)
                    AddView(result, added, unlocked, equipped, unlock.Skill, unlock.RequiredLevel);
            }
        }

        if (character.UnlockedSkillIDs != null)
        {
            for (int i = 0; i < character.UnlockedSkillIDs.Count; i++)
            {
                string id = NormalizeId(character.UnlockedSkillIDs[i]);
                if (string.IsNullOrEmpty(id) || !added.Add(id))
                    continue;

                SkillData skill = SkillDatabase.FindById(id);
                result.Add(new CharacterPowerView(
                    id,
                    skill != null ? skill.SkillName : id,
                    skill != null ? skill.Description : string.Empty,
                    1,
                    true,
                    equipped.Contains(id)));
            }
        }

        result.Sort((left, right) =>
        {
            int levelCompare = left.RequiredLevel.CompareTo(right.RequiredLevel);
            return levelCompare != 0
                ? levelCompare
                : string.Compare(left.SkillId, right.SkillId, StringComparison.Ordinal);
        });
        return result;
    }

    private static void AddView(
        ICollection<CharacterPowerView> destination,
        ISet<string> added,
        ISet<string> unlocked,
        ISet<string> equipped,
        SkillData skill,
        int requiredLevel)
    {
        string id = NormalizeId(skill != null ? skill.SkillID : null);
        if (string.IsNullOrEmpty(id) || !added.Add(id))
            return;

        destination.Add(new CharacterPowerView(
            id,
            skill.SkillName,
            skill.Description,
            requiredLevel,
            unlocked.Contains(id),
            equipped.Contains(id)));
    }

    private static bool AddSkillId(SkillData skill, ICollection<string> destination, ISet<string> known)
    {
        string id = NormalizeId(skill != null ? skill.SkillID : null);
        if (string.IsNullOrEmpty(id) || !known.Add(id))
            return false;

        destination.Add(id);
        return true;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}