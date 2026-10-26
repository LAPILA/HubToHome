using System;
using System.Collections.Generic;
using UnityEngine;

public partial class BattleManager
{
    private readonly BattleStoryWithdrawalGate _storyWithdrawal = new BattleStoryWithdrawalGate();
    private HashSet<string> _encounterRosterIds;
    private uint _encounterPolicyVersion;

    public event Action<BattlePlayerActionResult> OnPlayerActionCompleted;

    /// <summary>실행 중인 행동을 끊지 않고 다음 안전 경계에 정상 후퇴를 예약합니다.</summary>
    public bool TryRequestStoryWithdrawal(IEncounterSource source)
    {
        return isActiveAndEnabled && !_isDedicatedBattleScene && _isBattleActive && !_isBattleEnding
            && IsEncounterSourceAlive(source) && ReferenceEquals(source, _activeEncounterSource)
            && _storyWithdrawal.TryRequest(source);
    }

    /// <summary>다른 조우나 교체된 세션의 cleanup은 시작하지 않습니다.</summary>
    public bool TryAbortSeamlessBattle(IEncounterSource source)
    {
        return source != null && ReferenceEquals(source, _activeEncounterSource) && AbortSeamlessBattle();
    }

    internal static bool TryValidateEncounterRoster(GlobalDataManager global, PlayerController player,
        IEncounterSource source, out HashSet<string> rosterIds, out string error)
    {
        rosterIds = null;
        error = string.Empty;
        if (source is IEncounterStoryWithdrawalPolicy withdrawal && withdrawal.AllowStoryWithdrawal
            && !(source is IEncounterOutcomeSource))
        {
            error = "이야기 후퇴 조우에는 구분 가능한 IEncounterOutcomeSource 결과 콜백이 필요합니다.";
            return false;
        }
        if (!(source is IEncounterRosterPolicy policy)) return true;
        if (global == null || player == null || !player.TryGetComponent(out PlayerCharacter sceneCharacter))
        {
            error = "임시 조우 참가자를 확인할 플레이어 또는 저장 파티가 없습니다.";
            return false;
        }
        var savedIds = new List<string>(global.Party.Count);
        for (int i = 0; i < global.Party.Count; i++)
            savedIds.Add(global.Party[i]?.CharacterDataID);
        if (!BattleEncounterRosterSelection.TrySelect(policy.BattleRosterCharacterIds, savedIds,
                BattleRosterLimit, out rosterIds, out error)) return false;
        if (!rosterIds.Contains(sceneCharacter.CharacterID?.Trim() ?? string.Empty))
        {
            error = "심리스 조우 참가자에는 현재 필드 플레이어의 CharacterData ID가 필요합니다.";
            rosterIds = null;
            return false;
        }
        return true;
    }

    private bool IsEncounterRosterMember(string characterId)
    {
        return _encounterRosterIds == null
            || (!string.IsNullOrWhiteSpace(characterId) && _encounterRosterIds.Contains(characterId.Trim()));
    }

    private static bool IsEncounterSourceAlive(IEncounterSource source)
    {
        return source != null && (!(source is UnityEngine.Object unitySource) || unitySource != null);
    }

    void IBattlePlayerActionCompletionHost.NotifyPlayerActionCompleted(BattlePlayerActionResult result)
    {
        if (!IsBattleSessionActive || _isBattleEnding || !isActiveAndEnabled || result.Actor == null
            || !IsEncounterRosterMember(result.Actor.CharacterID)) return;
        Action<BattlePlayerActionResult> callbacks = OnPlayerActionCompleted;
        if (callbacks == null) return;
        uint version = _encounterPolicyVersion;
        foreach (Action<BattlePlayerActionResult> callback in callbacks.GetInvocationList())
        {
            if (version != _encounterPolicyVersion || !IsBattleSessionActive || _isBattleEnding) break;
            try { callback(result); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    bool IBattlePlayerActionCompletionHost.TryCompleteStoryWithdrawalAtBoundary()
    {
        if (_isDedicatedBattleScene || !_isBattleActive || _isBattleEnding || !isActiveAndEnabled
            || !IsEncounterSourceAlive(_activeEncounterSource)
            || !_storyWithdrawal.TryBeginEnding(CheckVictory(), CheckDefeat())) return false;
        ChangeState(BattleState.BattleEnd);
        return true;
    }

    void IBattlePlayerActionCompletionHost.CancelPendingStoryWithdrawal() => _storyWithdrawal.CancelPending();

    private void ResetEncounterPolicies()
    {
        unchecked { _encounterPolicyVersion++; }
        _storyWithdrawal.Reset();
        _encounterRosterIds = null;
    }
}
