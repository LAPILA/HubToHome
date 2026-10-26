using System;
using System.Collections.Generic;

public partial class PlayerCharacter
{
    // 키는 전투 효과의 소유자입니다. 오래된 scope의 해제가 새 효과를 지우지 않습니다.
    private readonly Dictionary<object, HashSet<SkillData>> _battleSkillLocks = new Dictionary<object, HashSet<SkillData>>();
    public event Action OnBattleSkillAvailabilityChanged;

    public bool IsBattleSkillLocked(SkillData skill)
    {
        if (skill == null) return false;
        foreach (HashSet<SkillData> locked in _battleSkillLocks.Values)
            if (locked.Contains(skill)) return true;
        return false;
    }

    public void ReplaceBattleSkillLocks(object owner, IReadOnlyList<SkillData> skills)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var next = new HashSet<SkillData>();
        if (skills != null)
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null) next.Add(skills[i]);
        if (next.Count == 0) { ReleaseBattleSkillLocks(owner); return; }
        if (_battleSkillLocks.TryGetValue(owner, out HashSet<SkillData> previous) && previous.SetEquals(next)) return;
        _battleSkillLocks[owner] = next;
        OnBattleSkillAvailabilityChanged?.Invoke();
    }

    public void ReleaseBattleSkillLocks(object owner)
    {
        if (owner != null && _battleSkillLocks.Remove(owner)) OnBattleSkillAvailabilityChanged?.Invoke();
    }

    private void ClearBattleSkillLocks()
    {
        if (_battleSkillLocks.Count == 0) return;
        _battleSkillLocks.Clear();
        OnBattleSkillAvailabilityChanged?.Invoke();
    }
}
