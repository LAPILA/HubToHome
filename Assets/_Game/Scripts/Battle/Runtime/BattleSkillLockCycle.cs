using System;
using System.Collections.Generic;

/// <summary>스킬 원본을 수정하지 않는 반복 잠금 추첨과 정상 행동 수명입니다.</summary>
public sealed class BattleSkillLockCycle<T> where T : class
{
    private readonly List<T> _candidates = new List<T>();
    private readonly List<T> _selection = new List<T>();
    private readonly HashSet<T> _unique = new HashSet<T>();
    private int _skillCount;
    private int _refreshAfterActions;
    private int _completedActions;
    private bool _actionStarted;

    public bool IsActive { get; private set; }
    public IReadOnlyList<T> Selection => _selection;
    public int CompletedActions => _completedActions;

    public void Activate(IReadOnlyList<T> skills, int skillCount, int refreshAfterActions, Func<int, int> next)
    {
        _skillCount = Math.Max(0, skillCount);
        _refreshAfterActions = Math.Max(1, refreshAfterActions);
        _completedActions = 0;
        _actionStarted = false; // 페이즈를 발생시킨 진행 중 행동은 새 잠금의 첫 턴이 아닙니다.
        IsActive = true;
        Reroll(skills, next);
    }

    public void BeginAction() => _actionStarted = IsActive;

    /// <returns>실제 새 추첨이 일어났을 때만 true입니다.</returns>
    public bool CompleteAction(IReadOnlyList<T> skills, Func<int, int> next)
    {
        if (!IsActive || !_actionStarted) return false;
        _actionStarted = false;
        if (++_completedActions < _refreshAfterActions) return false;
        _completedActions = 0;
        Reroll(skills, next);
        return true;
    }

    public void Reset()
    {
        IsActive = false;
        _actionStarted = false;
        _completedActions = 0;
        _selection.Clear();
        _candidates.Clear();
        _unique.Clear();
    }

    private void Reroll(IReadOnlyList<T> skills, Func<int, int> next)
    {
        _selection.Clear();
        _candidates.Clear();
        _unique.Clear();
        if (skills != null)
            for (int i = 0; i < skills.Count; i++)
            {
                T skill = skills[i];
                if (skill != null && _unique.Add(skill)) _candidates.Add(skill);
            }
        int count = Math.Min(_skillCount, _candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int remaining = _candidates.Count - i;
            int offset = next != null ? next(remaining) : 0;
            // 주입된 난수 공급자의 실수로 전투 흐름이 중단되지 않게 범위를 제한합니다.
            offset = Math.Max(0, Math.Min(remaining - 1, offset));
            int selected = i + offset;
            T skill = _candidates[selected];
            _candidates[selected] = _candidates[i];
            _candidates[i] = skill;
            _selection.Add(skill);
        }
    }
}
