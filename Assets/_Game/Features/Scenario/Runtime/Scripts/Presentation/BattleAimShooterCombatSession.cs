using System;

public sealed class BattleAimShooterModuleSettings
{
    public BattleAimShooterModuleSettings(
        int damagePerHit = 1,
        int requiredHits = 1,
        int maxShots = 1,
        string victoryOutcomeId = "victory",
        string failureOutcomeId = "failed")
    {
        DamagePerHit = Math.Max(1, damagePerHit);
        RequiredHits = Math.Max(1, requiredHits);
        MaxShots = Math.Max(1, maxShots);
        VictoryOutcomeId = NormalizeOutcome(victoryOutcomeId, "victory");
        FailureOutcomeId = NormalizeOutcome(failureOutcomeId, "failed");
    }

    public int DamagePerHit { get; }
    public int RequiredHits { get; }
    public int MaxShots { get; }
    public string VictoryOutcomeId { get; }
    public string FailureOutcomeId { get; }

    private static string NormalizeOutcome(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

public sealed class BattleAimShooterCombatSession
{
    private readonly GameModuleRuntimeContext _context;
    private readonly BattleAimShooterModuleSettings _settings;
    private bool _completed;
    private string _outcomeId = string.Empty;
    private int _shotsUsed;
    private int _hits;

    public BattleAimShooterCombatSession(
        GameModuleRuntimeContext context,
        BattleAimShooterModuleSettings settings = null)
    {
        _context = context;
        _settings = settings ?? new BattleAimShooterModuleSettings();
    }

    public int ShotsUsed
    {
        get { return _shotsUsed; }
    }

    public int ShotsRemaining
    {
        get { return Math.Max(0, _settings.MaxShots - _shotsUsed); }
    }

    public int Hits
    {
        get { return _hits; }
    }

    public bool IsCompleted
    {
        get { return _completed; }
    }

    public string OutcomeId
    {
        get { return _outcomeId; }
    }

    public BattleAimShooterShotResult FireAt(string targetSubjectId)
    {
        if (_completed)
        {
            return BattleAimShooterShotResult.Failed(
                Normalize(targetSubjectId),
                _shotsUsed,
                ShotsRemaining,
                _hits,
                _outcomeId,
                "aim_shooter session is already completed.");
        }

        string normalizedTarget = Normalize(targetSubjectId);
        if (string.IsNullOrEmpty(normalizedTarget))
        {
            return Fail(normalizedTarget, "aim_shooter requires a target subject id.");
        }

        IBattleSessionStateReader battleSession = _context != null ? _context.BattleSession : null;
        if (battleSession != null)
        {
            BattleParticipantSnapshot target;
            if (!battleSession.TryGetParticipant(normalizedTarget, out target))
            {
                return Fail(normalizedTarget, "aim_shooter target is not in the current battle session: " + normalizedTarget);
            }

            if (target.Kind != BattleParticipantKind.Enemy)
            {
                return Fail(normalizedTarget, "aim_shooter can only target enemies: " + normalizedTarget);
            }

            if (!target.IsAlive)
            {
                return Fail(normalizedTarget, "aim_shooter target is not alive: " + normalizedTarget);
            }
        }

        IBattleParticipantCommandRunner commands = _context != null ? _context.ParticipantCommands : null;
        if (commands == null)
        {
            return Fail(normalizedTarget, "IBattleParticipantCommandRunner is missing for aim_shooter.");
        }

        BattleParticipantCommandResult commandResult = commands.ApplyPureDamage(
            normalizedTarget,
            _settings.DamagePerHit,
            _context != null ? _context.ActionContext : null);

        if (commandResult == null)
        {
            return Fail(normalizedTarget, "aim_shooter damage command did not return a result.");
        }

        if (!commandResult.Success)
        {
            return Fail(normalizedTarget, string.IsNullOrWhiteSpace(commandResult.Message)
                ? "aim_shooter damage command failed for target: " + normalizedTarget
                : commandResult.Message);
        }

        _shotsUsed++;
        _hits++;

        string outcome = string.Empty;
        if (_hits >= _settings.RequiredHits)
        {
            Complete(_settings.VictoryOutcomeId);
            outcome = _outcomeId;
        }
        else if (_shotsUsed >= _settings.MaxShots)
        {
            Complete(_settings.FailureOutcomeId);
            outcome = _outcomeId;
        }

        return BattleAimShooterShotResult.Succeeded(
            normalizedTarget,
            _shotsUsed,
            ShotsRemaining,
            _hits,
            _completed,
            outcome,
            commandResult.AppliedAmount);
    }

    private BattleAimShooterShotResult Fail(string targetSubjectId, string message)
    {
        return BattleAimShooterShotResult.Failed(
            targetSubjectId,
            _shotsUsed,
            ShotsRemaining,
            _hits,
            _outcomeId,
            message);
    }

    private void Complete(string outcomeId)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _outcomeId = string.IsNullOrWhiteSpace(outcomeId) ? string.Empty : outcomeId.Trim();
        _context?.ModuleEvents?.PublishGameModuleCompleted(
            BattleAimShooterGameModuleRuntime.Id,
            _outcomeId,
            BattleRuleTiming.AfterCurrentModule);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class BattleAimShooterShotResult
{
    private BattleAimShooterShotResult(
        bool success,
        string targetSubjectId,
        int shotsUsed,
        int shotsRemaining,
        int hits,
        bool completed,
        string outcomeId,
        int appliedDamage,
        string message)
    {
        Success = success;
        TargetSubjectId = string.IsNullOrWhiteSpace(targetSubjectId) ? string.Empty : targetSubjectId.Trim();
        ShotsUsed = Math.Max(0, shotsUsed);
        ShotsRemaining = Math.Max(0, shotsRemaining);
        Hits = Math.Max(0, hits);
        Completed = completed;
        OutcomeId = string.IsNullOrWhiteSpace(outcomeId) ? string.Empty : outcomeId.Trim();
        AppliedDamage = Math.Max(0, appliedDamage);
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string TargetSubjectId { get; }
    public int ShotsUsed { get; }
    public int ShotsRemaining { get; }
    public int Hits { get; }
    public bool Completed { get; }
    public string OutcomeId { get; }
    public int AppliedDamage { get; }
    public string Message { get; }

    public static BattleAimShooterShotResult Succeeded(
        string targetSubjectId,
        int shotsUsed,
        int shotsRemaining,
        int hits,
        bool completed,
        string outcomeId,
        int appliedDamage)
    {
        return new BattleAimShooterShotResult(
            true,
            targetSubjectId,
            shotsUsed,
            shotsRemaining,
            hits,
            completed,
            outcomeId,
            appliedDamage,
            string.Empty);
    }

    public static BattleAimShooterShotResult Failed(
        string targetSubjectId,
        int shotsUsed,
        int shotsRemaining,
        int hits,
        string outcomeId,
        string message)
    {
        return new BattleAimShooterShotResult(
            false,
            targetSubjectId,
            shotsUsed,
            shotsRemaining,
            hits,
            false,
            outcomeId,
            0,
            message);
    }
}
