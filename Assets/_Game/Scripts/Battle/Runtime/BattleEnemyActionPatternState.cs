using System;
using System.Collections.Generic;

/// <summary>배우 한 명의 후속 스킬 선택·정상 완료·회복 턴만 소유하는 전투 수명 상태입니다.</summary>
public sealed class BattleEnemyActionPatternState
{
    private readonly EnemyFollowupSkillPattern[] _patterns;
    private readonly int[] _completed;
    private readonly bool[] _used;
    private readonly bool[] _activeAtStart;
    private int _selected = -1;
    private bool _actionOpen;
    public int RecoveryTurnsRemaining { get; private set; }

    public BattleEnemyActionPatternState(IReadOnlyList<EnemyFollowupSkillPattern> patterns)
    {
        int count = patterns != null ? patterns.Count : 0;
        _patterns = new EnemyFollowupSkillPattern[count];
        for (int i = 0; i < count; i++) _patterns[i] = patterns[i];
        _completed = new int[count];
        _used = new bool[count];
        _activeAtStart = new bool[count];
    }

    /// <summary>조건은 행동 시작에만 읽습니다. 도중에 켜진 페이즈 플래그는 이 행동을 소급해 세지 않습니다.</summary>
    public SkillData BeginAction(IBattleSessionStateReader session, bool allowFollowup = true)
    {
        CancelAction();
        _actionOpen = true;
        for (int i = 0; i < _patterns.Length; i++)
        {
            EnemyFollowupSkillPattern pattern = _patterns[i];
            if (pattern == null || pattern.Skill == null || (pattern.OncePerBattle && _used[i])) continue;
            bool active = string.IsNullOrWhiteSpace(pattern.RequiredBattleFlag)
                || (session != null && session.TryGetFlagValue(pattern.RequiredBattleFlag.Trim(), out string value)
                    && string.Equals(value, string.IsNullOrWhiteSpace(pattern.RequiredFlagValue) ? "true" : pattern.RequiredFlagValue.Trim(), StringComparison.Ordinal));
            _activeAtStart[i] = active;
            if (!active) _completed[i] = 0;
            if (allowFollowup && active && _selected < 0 && _completed[i] >= Math.Max(0, pattern.NormalActionsBefore))
                _selected = i;
        }
        return _selected >= 0 ? _patterns[_selected].Skill : null;
    }

    /// <summary>공격을 실제 완료했을 때만 호출합니다. 선택·취소·예고만으로 횟수나 휴식이 생기지 않습니다.</summary>
    public void CompleteAction(bool completed, int recoveryTurns = 0)
    {
        if (!_actionOpen) return;
        if (completed)
        {
            if (_selected >= 0)
            {
                _used[_selected] = true;
                _completed[_selected] = 0;
            }
            else
            {
                for (int i = 0; i < _patterns.Length; i++)
                    if (_activeAtStart[i] && _completed[i] < int.MaxValue) _completed[i]++;
            }
            RecoveryTurnsRemaining = Math.Max(RecoveryTurnsRemaining, Math.Max(0, recoveryTurns));
        }
        CancelAction();
    }

    public bool TryConsumeRecoveryTurn()
    {
        if (RecoveryTurnsRemaining <= 0) return false;
        RecoveryTurnsRemaining--;
        return true;
    }

    public void CancelAction()
    {
        _actionOpen = false;
        _selected = -1;
        Array.Clear(_activeAtStart, 0, _activeAtStart.Length);
    }
}
