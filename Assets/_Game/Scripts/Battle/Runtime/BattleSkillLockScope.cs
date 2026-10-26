using System;
using System.Collections.Generic;

/// <summary>전투 플래그와 실제 아군 행동 완료를 관찰하는 선택형 반복 잠금 수명입니다.</summary>
public sealed class BattleSkillLockScope : IDisposable
{
    private readonly BattleManager _battle;
    private readonly string _activationFlag;
    private readonly int _skillCount;
    private readonly int _refreshAfterActions;
    private readonly Func<int, int> _next;
    private readonly Dictionary<PlayerCharacter, BattleSkillLockCycle<SkillData>> _actors
        = new Dictionary<PlayerCharacter, BattleSkillLockCycle<SkillData>>();
    private BattleFlagPresentationGate _gate;
    private bool _active;
    private bool _disposed;

    public BattleSkillLockScope(BattleManager battle, BattleSkillLockSettings settings)
    {
        _battle = battle;
        if (battle == null || settings == null || string.IsNullOrWhiteSpace(settings.ActivationFlag))
        {
            _disposed = true;
            return;
        }
        _activationFlag = settings.ActivationFlag.Trim();
        _skillCount = Math.Max(0, settings.SkillCount);
        _refreshAfterActions = Math.Max(1, settings.RefreshAfterActions);
        _next = new Random().Next;
        battle.OnBattleStarted += HandleStarted;
        battle.OnBattleEnded += HandleEnded;
        battle.OnPlayerPartyChanged += HandlePartyChanged;
        battle.OnCombatLogActionStarted += HandleActionStarted;
        battle.OnPlayerActionCompleted += HandleActionCompleted;
        if (battle.IsBattleSessionActive) BindSession();
    }

    private void HandleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies) => BindSession();
    private void HandleEnded(bool victory) => Dispose();
    private void HandlePartyChanged(List<PlayerCharacter> party) => RefreshParty();

    private void BindSession()
    {
        if (_disposed || _battle == null) return;
        _gate?.Dispose();
        _gate = null;
        _active = false;
        ClearActors();
        if (_battle.ScenarioSessionState == null) return;
        var gate = new BattleFlagPresentationGate(_battle.ScenarioSessionState, _activationFlag, SetActive);
        // 초기 true 통지가 UI/씬 정리로 재진입해 scope를 끝냈을 수 있습니다.
        if (_disposed) { gate.Dispose(); return; }
        _gate = gate;
    }

    private void SetActive(bool active)
    {
        if (_disposed || _active == active) return;
        _active = active;
        if (!active) { ClearActors(); return; }
        RefreshParty();
    }

    private void RefreshParty()
    {
        if (_disposed || !_active || _battle == null) return;
        IReadOnlyList<PlayerCharacter> party = ((IBattleTurnQteHost)_battle).PlayerParty;
        var previous = new List<PlayerCharacter>(_actors.Keys);
        for (int i = 0; i < previous.Count; i++)
        {
            PlayerCharacter actor = previous[i];
            if (!IsEligible(actor) || !Contains(party, actor))
            {
                _actors.Remove(actor);
                if (actor != null) actor.ReleaseBattleSkillLocks(this);
            }
        }
        if (party == null) return;
        for (int i = 0; i < party.Count && !_disposed && _active; i++)
        {
            PlayerCharacter actor = party[i];
            if (!IsEligible(actor) || _actors.ContainsKey(actor)) continue;
            var cycle = new BattleSkillLockCycle<SkillData>();
            _actors.Add(actor, cycle);
            cycle.Activate(actor.Skills, _skillCount, _refreshAfterActions, _next);
            actor.ReplaceBattleSkillLocks(this, cycle.Selection);
        }
    }

    private void HandleActionStarted(CharacterBase actor, SkillData skill, ItemData item)
    {
        if (_disposed || !_active || !(actor is PlayerCharacter player)) return;
        if (_actors.TryGetValue(player, out BattleSkillLockCycle<SkillData> cycle)) cycle.BeginAction();
    }

    private void HandleActionCompleted(BattlePlayerActionResult result)
    {
        PlayerCharacter actor = result.Actor;
        if (_disposed || !_active || !IsEligible(actor)
            || (result.Action != PlayerMenuAction.Attack && result.Action != PlayerMenuAction.Skill
                && result.Action != PlayerMenuAction.Item)) return;
        if (_actors.TryGetValue(actor, out BattleSkillLockCycle<SkillData> cycle)
            && cycle.CompleteAction(actor.Skills, _next))
            actor.ReplaceBattleSkillLocks(this, cycle.Selection);
    }

    private void ClearActors()
    {
        var actors = new List<PlayerCharacter>(_actors.Keys);
        _actors.Clear(); // 알림 재진입 전에 소유 목록을 먼저 비웁니다.
        for (int i = 0; i < actors.Count; i++)
            if (actors[i] != null) actors[i].ReleaseBattleSkillLocks(this);
    }

    private static bool IsEligible(PlayerCharacter actor) => actor != null && actor.IsAlive && actor.isActiveAndEnabled
        && actor.gameObject.activeInHierarchy;

    private static bool Contains(IReadOnlyList<PlayerCharacter> party, PlayerCharacter actor)
    {
        if (party == null) return false;
        for (int i = 0; i < party.Count; i++) if (party[i] == actor) return true;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active = false;
        if (_battle != null)
        {
            _battle.OnBattleStarted -= HandleStarted;
            _battle.OnBattleEnded -= HandleEnded;
            _battle.OnPlayerPartyChanged -= HandlePartyChanged;
            _battle.OnCombatLogActionStarted -= HandleActionStarted;
            _battle.OnPlayerActionCompleted -= HandleActionCompleted;
        }
        _gate?.Dispose();
        _gate = null;
        ClearActors();
    }
}
