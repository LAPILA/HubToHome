using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleSessionStateReader
{
    string ScenarioId { get; }
    string PrimaryMode { get; }
    string OpeningModule { get; }
    string CurrentModuleId { get; }
    IReadOnlyList<BattleParticipantSnapshot> Participants { get; }
    IReadOnlyList<BattleSessionFlagSnapshot> Flags { get; }
    bool TryGetParticipant(string subjectId, out BattleParticipantSnapshot participant);
    bool HasFlag(string flagId);
    bool TryGetFlagValue(string flagId, out string value);
}

public interface IBattleSessionFlagStore
{
    bool SetFlag(string flagId, string value);

    bool ClearFlag(string flagId);
}

public interface IGameModuleEventSink
{
    void PublishGameModuleCompleted(
        string moduleId,
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule);
}

public interface IBattleParticipantCommandRunner
{
    BattleParticipantCommandResult ApplyPureDamage(string subjectId, int amount, ActionExecutionContext context);

    BattleParticipantCommandResult HealHp(string subjectId, int amount, ActionExecutionContext context);

    BattleParticipantCommandResult HealMp(string subjectId, int amount, ActionExecutionContext context);

    BattleParticipantCommandResult ConsumeMp(string subjectId, int amount, ActionExecutionContext context);
}

public sealed class BattleParticipantCommandResult
{
    private BattleParticipantCommandResult(
        bool success,
        string subjectId,
        int requestedAmount,
        int appliedAmount,
        int previousValue,
        int currentValue,
        string message)
    {
        Success = success;
        SubjectId = Normalize(subjectId);
        RequestedAmount = Mathf.Max(0, requestedAmount);
        AppliedAmount = Mathf.Max(0, appliedAmount);
        PreviousValue = Mathf.Max(0, previousValue);
        CurrentValue = Mathf.Max(0, currentValue);
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string SubjectId { get; }
    public int RequestedAmount { get; }
    public int AppliedAmount { get; }
    public int PreviousValue { get; }
    public int CurrentValue { get; }
    public string Message { get; }

    public static BattleParticipantCommandResult Succeeded(
        string subjectId,
        int requestedAmount,
        int appliedAmount,
        int previousValue,
        int currentValue)
    {
        return new BattleParticipantCommandResult(
            true,
            subjectId,
            requestedAmount,
            appliedAmount,
            previousValue,
            currentValue,
            string.Empty);
    }

    public static BattleParticipantCommandResult Failed(string subjectId, string message)
    {
        return new BattleParticipantCommandResult(
            false,
            subjectId,
            0,
            0,
            0,
            0,
            message);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public enum BattleParticipantKind
{
    Player,
    Enemy
}

public sealed class BattleSessionFlagSnapshot
{
    public BattleSessionFlagSnapshot(string flagId, string value)
    {
        FlagId = Normalize(flagId);
        Value = string.IsNullOrWhiteSpace(value) ? "true" : value.Trim();
    }

    public string FlagId { get; }
    public string Value { get; }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class BattleParticipantSnapshot
{
    public BattleParticipantSnapshot(
        string subjectId,
        BattleParticipantKind kind,
        string displayName,
        int currentHp,
        int maxHp,
        int currentAp,
        int maxAp,
        bool isAlive,
        bool isBound,
        bool isStunned,
        bool isBerserk,
        bool isDefending,
        bool isInvincible)
    {
        SubjectId = Normalize(subjectId);
        Kind = kind;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? SubjectId : displayName.Trim();
        CurrentHp = Mathf.Max(0, currentHp);
        MaxHp = Mathf.Max(0, maxHp);
        CurrentAp = Mathf.Max(0, currentAp);
        MaxAp = Mathf.Max(0, maxAp);
        IsAlive = isAlive;
        IsBound = isBound;
        IsStunned = isStunned;
        IsBerserk = isBerserk;
        IsDefending = isDefending;
        IsInvincible = isInvincible;
    }

    public string SubjectId { get; }
    public BattleParticipantKind Kind { get; }
    public string DisplayName { get; }
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public int CurrentAp { get; }
    public int MaxAp { get; }

    [Obsolete("Use CurrentAp.")]
    public int CurrentMp => CurrentAp;

    [Obsolete("Use MaxAp.")]
    public int MaxMp => MaxAp;
    public bool IsAlive { get; }
    public bool IsBound { get; }
    public bool IsStunned { get; }
    public bool IsBerserk { get; }
    public bool IsDefending { get; }
    public bool IsInvincible { get; }

    public float HpRatio
    {
        get { return MaxHp > 0 ? Mathf.Clamp01((float)CurrentHp / MaxHp) : 0f; }
    }

    public float ApRatio
    {
        get { return MaxAp > 0 ? Mathf.Clamp01((float)CurrentAp / MaxAp) : 0f; }
    }

    [Obsolete("Use ApRatio.")]
    public float MpRatio => ApRatio;

    public static BattleParticipantSnapshot FromCharacter(CharacterBase character)
    {
        if (character == null)
        {
            return null;
        }

        PlayerCharacter player = character as PlayerCharacter;
        if (player != null)
        {
            return FromPlayer(player);
        }

        EnemyCharacter enemy = character as EnemyCharacter;
        if (enemy != null)
        {
            return FromEnemy(enemy);
        }

        return new BattleParticipantSnapshot(
            BattleScenarioSubjectResolver.ResolveSubjectId(character),
            BattleParticipantKind.Player,
            character.name,
            character.CurrentHP,
            character.MaxHP,
            character.CurrentAP,
            character.MaxAP,
            character.IsAlive,
            character.IsBound,
            character.IsStunned,
            character.IsBerserk,
            character.IsDefending,
            character.IsInvincible);
    }

    public static BattleParticipantSnapshot FromPlayer(PlayerCharacter player)
    {
        if (player == null)
        {
            return null;
        }

        string subjectId = !string.IsNullOrWhiteSpace(player.CharacterID)
            ? player.CharacterID
            : BattleScenarioSubjectResolver.ResolveSubjectId(player);

        return new BattleParticipantSnapshot(
            subjectId,
            BattleParticipantKind.Player,
            player.DisplayName,
            player.CurrentHP,
            player.MaxHP,
            player.CurrentAP,
            player.MaxAP,
            player.IsAlive,
            player.IsBound,
            player.IsStunned,
            player.IsBerserk,
            player.IsDefending,
            player.IsInvincible);
    }

    public static BattleParticipantSnapshot FromEnemy(EnemyCharacter enemy)
    {
        if (enemy == null)
        {
            return null;
        }

        string displayName = enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName)
            ? enemy.Data.EnemyName
            : enemy.name;

        return new BattleParticipantSnapshot(
            BattleScenarioSubjectResolver.ResolveEnemySubjectId(enemy),
            BattleParticipantKind.Enemy,
            displayName,
            enemy.CurrentHP,
            enemy.MaxHP,
            enemy.CurrentAP,
            enemy.MaxAP,
            enemy.IsAlive,
            enemy.IsBound,
            enemy.IsStunned,
            enemy.IsBerserk,
            enemy.IsDefending,
            enemy.IsInvincible);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class BattleScenarioRuntime
{
    private readonly BattleScenarioEventRouter _eventRouter;
    private readonly BattleScenarioSession _session;

    public BattleScenarioRuntime(
        BattleScenarioData scenarioData,
        IEnumerable<string> encounterFiredRuleIds = null,
        IEnumerable<string> saveFiredRuleIds = null,
        ActionExecutionContext triggerValues = null)
    {
        ScenarioData = scenarioData;
        SessionState = BattleSessionState.Create(scenarioData);
        if (scenarioData == null)
        {
            return;
        }

        _session = new BattleScenarioSession(
            scenarioData.ScenarioId,
            scenarioData.MemoryKey);
        _session.ImportEncounterFiredRuleIds(encounterFiredRuleIds);
        _session.ImportSaveFiredRuleIds(saveFiredRuleIds);

        var ruleRunner = new BattleScenarioRuleRunner(
            scenarioData,
            _session,
            values: triggerValues);
        _eventRouter = new BattleScenarioEventRouter(ruleRunner);
    }

    public BattleScenarioData ScenarioData { get; }

    public BattleSessionState SessionState { get; }

    public bool HasScenario
    {
        get { return ScenarioData != null && _eventRouter != null; }
    }

    public List<BattleScenarioTrigger> PublishBattleStarted(BattleRuleTiming timing = BattleRuleTiming.Immediate)
    {
        if (!HasScenario)
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Publish(BattleEventData.BattleStarted(timing));
    }

    public List<BattleScenarioTrigger> PublishEnemyHpCrossedBelow(
        string subjectId,
        int previousHp,
        int currentHp,
        int maxHp,
        BattleRuleTiming timing)
    {
        if (!HasScenario || string.IsNullOrWhiteSpace(subjectId) || maxHp <= 0)
        {
            return new List<BattleScenarioTrigger>();
        }

        BattleEventData battleEvent = BattleEventData.EnemyHpCrossedBelow(
            subjectId,
            Mathf.Clamp01((float)previousHp / maxHp),
            Mathf.Clamp01((float)currentHp / maxHp),
            timing);

        return _eventRouter.Publish(battleEvent);
    }

    public List<BattleScenarioTrigger> PublishGameModuleCompleted(
        string moduleId,
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
    {
        if (!HasScenario || string.IsNullOrWhiteSpace(moduleId))
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Publish(BattleEventData.GameModuleCompleted(
            moduleId,
            outcomeId,
            timing));
    }

    public List<BattleScenarioTrigger> PublishEnemyDefeated(
        string subjectId,
        string sourceActorId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentAction)
    {
        if (!HasScenario || string.IsNullOrWhiteSpace(subjectId))
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Publish(BattleEventData.EnemyDefeated(
            subjectId,
            sourceActorId,
            timing));
    }

    public List<BattleScenarioTrigger> PublishSkillCompleted(
        string skillId,
        string sourceActorId = "",
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentSkill)
    {
        if (!HasScenario || string.IsNullOrWhiteSpace(skillId))
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Publish(BattleEventData.SkillCompleted(
            skillId,
            sourceActorId,
            outcomeId,
            timing));
    }

    public List<BattleScenarioTrigger> PublishScenarioEvent(ScenarioEventData scenarioEvent)
    {
        return HasScenario
            ? _eventRouter.Publish(scenarioEvent)
            : new List<BattleScenarioTrigger>();
    }

    public List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)
    {
        if (!HasScenario)
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Flush(timing);
    }

    public List<BattleScenarioTrigger> FlushCheckpoint(string checkpointId)
    {
        return HasScenario
            ? _eventRouter.FlushCheckpoint(checkpointId)
            : new List<BattleScenarioTrigger>();
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        sequence = null;
        return HasScenario && _eventRouter.TryResolveSequence(sequenceId, out sequence);
    }

    public string[] ExportEncounterFiredRuleIds()
    {
        return _session != null ? _session.ExportEncounterFiredRuleIds() : new string[0];
    }

    public string[] ExportSaveFiredRuleIds()
    {
        return _session != null ? _session.ExportSaveFiredRuleIds() : new string[0];
    }
}

public sealed class BattleSessionState : IBattleSessionStateReader, IGameModuleStateStore, IBattleSessionFlagStore
{
    private readonly List<BattleParticipantSnapshot> _participants = new List<BattleParticipantSnapshot>();
    private readonly List<BattleSessionFlagSnapshot> _flags = new List<BattleSessionFlagSnapshot>();

    private BattleSessionState(
        string scenarioId,
        string primaryMode,
        string openingModule)
    {
        ScenarioId = Normalize(scenarioId);
        PrimaryMode = string.IsNullOrWhiteSpace(primaryMode) ? "battle" : primaryMode.Trim();
        OpeningModule = Normalize(openingModule);
        CurrentModuleId = OpeningModule;
    }

    public string ScenarioId { get; }
    public string PrimaryMode { get; }
    public string OpeningModule { get; }
    public string CurrentModuleId { get; private set; }
    public IReadOnlyList<BattleParticipantSnapshot> Participants
    {
        get { return _participants; }
    }
    public IReadOnlyList<BattleSessionFlagSnapshot> Flags
    {
        get { return _flags; }
    }

    public static BattleSessionState Create(BattleScenarioData scenarioData)
    {
        if (scenarioData == null)
        {
            return new BattleSessionState(string.Empty, "battle", BattleTurnQteGameModuleRuntime.Id);
        }

        string openingModule = string.IsNullOrWhiteSpace(scenarioData.OpeningModule)
            ? BattleTurnQteGameModuleRuntime.Id
            : scenarioData.OpeningModule;

        return new BattleSessionState(
            scenarioData.ScenarioId,
            scenarioData.PrimaryMode,
            openingModule);
    }

    public void SetCurrentModuleId(string moduleId)
    {
        CurrentModuleId = Normalize(moduleId);
    }

    public void SetParticipants(IEnumerable<BattleParticipantSnapshot> participants)
    {
        _participants.Clear();
        if (participants == null)
        {
            return;
        }

        foreach (BattleParticipantSnapshot participant in participants)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.SubjectId))
            {
                continue;
            }

            _participants.Add(participant);
        }
    }

    public bool TryGetParticipant(string subjectId, out BattleParticipantSnapshot participant)
    {
        participant = null;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        string normalized = Normalize(subjectId);
        for (int i = 0; i < _participants.Count; i++)
        {
            BattleParticipantSnapshot candidate = _participants[i];
            if (candidate != null && candidate.SubjectId == normalized)
            {
                participant = candidate;
                return true;
            }
        }

        return false;
    }

    public bool HasFlag(string flagId)
    {
        string value;
        return TryGetFlagValue(flagId, out value);
    }

    public bool TryGetFlagValue(string flagId, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        string normalized = Normalize(flagId);
        for (int i = 0; i < _flags.Count; i++)
        {
            BattleSessionFlagSnapshot flag = _flags[i];
            if (flag != null && string.Equals(flag.FlagId, normalized, StringComparison.Ordinal))
            {
                value = flag.Value;
                return true;
            }
        }

        return false;
    }

    public bool SetFlag(string flagId, string value)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        string normalized = Normalize(flagId);
        string normalizedValue = string.IsNullOrWhiteSpace(value) ? "true" : value.Trim();
        for (int i = 0; i < _flags.Count; i++)
        {
            BattleSessionFlagSnapshot flag = _flags[i];
            if (flag != null && string.Equals(flag.FlagId, normalized, StringComparison.Ordinal))
            {
                _flags[i] = new BattleSessionFlagSnapshot(normalized, normalizedValue);
                return true;
            }
        }

        _flags.Add(new BattleSessionFlagSnapshot(normalized, normalizedValue));
        return true;
    }

    public bool ClearFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        string normalized = Normalize(flagId);
        for (int i = _flags.Count - 1; i >= 0; i--)
        {
            BattleSessionFlagSnapshot flag = _flags[i];
            if (flag != null && string.Equals(flag.FlagId, normalized, StringComparison.Ordinal))
            {
                _flags.RemoveAt(i);
                return true;
            }
        }

        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
