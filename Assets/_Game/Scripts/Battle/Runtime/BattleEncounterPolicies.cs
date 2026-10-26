using System;
using System.Collections.Generic;

/// <summary>조우 한 번만 사용할 저장 파티의 부분집합입니다. 실제 파티 순서는 변경하지 않습니다.</summary>
public interface IEncounterRosterPolicy
{
    IReadOnlyList<string> BattleRosterCharacterIds { get; }
}

/// <summary>학습 완료나 조기 처치를 정상 이야기 후퇴로 연결하는 명시적 선택입니다.</summary>
public interface IEncounterStoryWithdrawalPolicy
{
    bool AllowStoryWithdrawal { get; }
    bool WithdrawOnEarlyVictory { get; }
}

/// <summary>이름이나 표시 문자열 대신 안정적인 CharacterData ID만 검사합니다.</summary>
public static class BattleEncounterRosterSelection
{
    public static bool TrySelect(IReadOnlyList<string> requestedIds, IReadOnlyList<string> savedIds,
        int limit, out HashSet<string> selectedIds, out string error)
    {
        selectedIds = null;
        if (requestedIds == null || requestedIds.Count == 0 || requestedIds.Count > limit)
        {
            error = "조우 참가자 목록이 비어 있거나 전투 최대 인원을 초과했습니다.";
            return false;
        }

        var selected = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < requestedIds.Count; i++)
        {
            string id = requestedIds[i]?.Trim();
            if (string.IsNullOrEmpty(id) || !selected.Add(id))
            {
                error = "조우 참가자 ID가 비어 있거나 중복되었습니다.";
                return false;
            }
            int matches = 0;
            if (savedIds != null)
                for (int j = 0; j < savedIds.Count; j++)
                    if (string.Equals(id, savedIds[j]?.Trim(), StringComparison.Ordinal)) matches++;
            if (matches != 1)
            {
                error = $"저장 파티에 조우 참가자 '{id}'가 고유하게 존재하지 않습니다.";
                return false;
            }
        }
        selectedIds = selected;
        error = string.Empty;
        return true;
    }
}

/// <summary>요청은 기록만 하며 실제 종료는 행동/결과 경계에서 시작합니다.</summary>
public sealed class BattleStoryWithdrawalGate
{
    private object _source;
    private bool _allowed;
    private bool _withdrawOnVictory;
    private bool _pending;
    public bool IsEnding { get; private set; }

    public void Configure(object source, bool allowed, bool withdrawOnVictory)
    {
        Reset();
        _source = source;
        _allowed = source != null && allowed;
        _withdrawOnVictory = withdrawOnVictory;
    }

    public bool TryRequest(object source)
    {
        if (!_allowed || IsEnding || !ReferenceEquals(_source, source)) return false;
        _pending = true;
        return true;
    }

    public bool TryBeginEnding(bool victory, bool partyDefeated)
    {
        if (!_allowed) return false;
        if (IsEnding) return true;
        // 기존 동시 전멸의 승리 우선 규칙은 유지하되, 실제 패배는 위장하지 않습니다.
        if (!(victory && _withdrawOnVictory) && !(_pending && !partyDefeated)) return false;
        _pending = false;
        IsEnding = true;
        return true;
    }

    public void CancelPending() => _pending = false;

    public void Reset()
    {
        _source = null;
        _allowed = false;
        _withdrawOnVictory = false;
        _pending = false;
        IsEnding = false;
    }
}

/// <summary>소비/효과가 완료된 현재 행동만 한 번 관찰자에게 전달합니다.</summary>
public sealed class BattlePlayerActionCompletionGate
{
    private object _actor;
    private bool _completed;
    public uint Version { get; private set; }

    public void Begin(object actor)
    {
        unchecked { Version++; }
        _actor = actor;
        _completed = false;
    }

    public bool IsCurrent(object actor, uint version) => _actor != null
        && ReferenceEquals(_actor, actor) && Version == version;

    public bool TryComplete(object actor, bool effectApplied, bool executionActive, uint version)
    {
        if (!IsCurrent(actor, version) || _completed || !effectApplied || !executionActive)
            return false;
        _completed = true;
        return true;
    }

    public void Cancel()
    {
        unchecked { Version++; }
        _actor = null;
        _completed = false;
    }
}
