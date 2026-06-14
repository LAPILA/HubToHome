using System.Collections.Generic;
using UnityEngine;

public interface IBattleSessionStateReader
{
    string ScenarioId { get; }
    string PrimaryMode { get; }
    string OpeningModule { get; }
    string CurrentModuleId { get; }
    IReadOnlyList<BattleParticipantSnapshot> Participants { get; }
    bool TryGetParticipant(string subjectId, out BattleParticipantSnapshot participant);
}

public enum BattleParticipantKind
{
    Player,
    Enemy
}

public sealed class BattleParticipantSnapshot
{
    public BattleParticipantSnapshot(
        string subjectId,
        BattleParticipantKind kind,
        string displayName,
        int currentHp,
        int maxHp,
        int currentMp,
        int maxMp,
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
        CurrentMp = Mathf.Max(0, currentMp);
        MaxMp = Mathf.Max(0, maxMp);
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
    public int CurrentMp { get; }
    public int MaxMp { get; }
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

    public float MpRatio
    {
        get { return MaxMp > 0 ? Mathf.Clamp01((float)CurrentMp / MaxMp) : 0f; }
    }

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
            character.CurrentMP,
            character.MaxMP,
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
            player.CurrentMP,
            player.MaxMP,
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
            enemy.CurrentMP,
            enemy.MaxMP,
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
        IEnumerable<string> encounterFiredRuleIds = null)
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

        var ruleRunner = new BattleScenarioRuleRunner(scenarioData, _session);
        _eventRouter = new BattleScenarioEventRouter(ruleRunner);
    }

    public BattleScenarioData ScenarioData { get; }

    public BattleSessionState SessionState { get; }

    public bool HasScenario
    {
        get { return ScenarioData != null && _eventRouter != null; }
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

    public List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)
    {
        if (!HasScenario)
        {
            return new List<BattleScenarioTrigger>();
        }

        return _eventRouter.Flush(timing);
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
}

public sealed class BattleSessionState : IBattleSessionStateReader, IGameModuleStateStore
{
    private readonly List<BattleParticipantSnapshot> _participants = new List<BattleParticipantSnapshot>();

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

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
