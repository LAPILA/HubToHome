using System;
using System.Collections;

public sealed class GameModuleActionRunner : IGameModuleActionRunner
{
    private readonly GameModuleRegistry _registry;
    private readonly IGameModuleStateStore _stateStore;
    private string _currentModuleId;

    public GameModuleActionRunner(
        GameModuleRegistry registry,
        string currentModuleId = "",
        IGameModuleStateStore stateStore = null)
    {
        _registry = registry ?? new GameModuleRegistry();
        _stateStore = stateStore;
        _currentModuleId = Normalize(currentModuleId);
        if (_stateStore != null && string.IsNullOrEmpty(_stateStore.CurrentModuleId))
        {
            _stateStore.SetCurrentModuleId(_currentModuleId);
        }
    }

    public string CurrentModuleId
    {
        get
        {
            string storedModuleId = _stateStore != null ? Normalize(_stateStore.CurrentModuleId) : string.Empty;
            return !string.IsNullOrEmpty(storedModuleId) ? storedModuleId : _currentModuleId;
        }
    }

    public IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
    {
        string targetModuleId = Normalize(moduleId);
        if (string.IsNullOrEmpty(targetModuleId))
        {
            Fail(context, "module.switch requires a target Game Module id.");
            yield break;
        }

        IGameModuleRuntime target;
        if (!_registry.TryGet(targetModuleId, out target))
        {
            Fail(context, "Game Module is not registered: " + targetModuleId);
            yield break;
        }

        string activeModuleId = ResolveActiveModuleId(context);
        if (activeModuleId == targetModuleId)
        {
            SetCurrentModule(targetModuleId, context);
            yield break;
        }

        IGameModuleRuntime current;
        if (!string.IsNullOrEmpty(activeModuleId) && _registry.TryGet(activeModuleId, out current))
        {
            GameModuleRuntimeContext exitContext = CreateModuleContext(context, activeModuleId, targetModuleId);
            IEnumerator exitRoutine = ScenarioAdapterRoutineRunner.Run(
                current.Exit(exitContext),
                context,
                "Game Module failed while exiting: " + activeModuleId);
            while (exitRoutine.MoveNext())
            {
                yield return exitRoutine.Current;
            }

            if (IsStopped(context))
            {
                yield break;
            }
        }

        GameModuleRuntimeContext enterContext = CreateModuleContext(context, activeModuleId, targetModuleId);
        IEnumerator enterRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Enter(enterContext),
            context,
            "Game Module failed while entering: " + targetModuleId);
        while (enterRoutine.MoveNext())
        {
            yield return enterRoutine.Current;
        }

        if (!IsStopped(context))
        {
            SetCurrentModule(targetModuleId, context);
        }
    }

    public IEnumerator Start(string moduleId, ActionExecutionContext context)
    {
        string targetModuleId = Normalize(moduleId);
        if (string.IsNullOrEmpty(targetModuleId))
        {
            Fail(context, "module.start requires a Game Module id.");
            yield break;
        }

        IGameModuleRuntime target;
        if (!_registry.TryGet(targetModuleId, out target))
        {
            Fail(context, "Game Module is not registered: " + targetModuleId);
            yield break;
        }

        string activeModuleId = ResolveActiveModuleId(context);
        GameModuleRuntimeContext startContext = CreateModuleContext(context, activeModuleId, targetModuleId);
        IEnumerator startRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Start(startContext),
            context,
            "Game Module failed while starting: " + targetModuleId);
        while (startRoutine.MoveNext())
        {
            yield return startRoutine.Current;
        }

        if (!IsStopped(context))
        {
            SetCurrentModule(targetModuleId, context);
        }
    }

    private string ResolveActiveModuleId(ActionExecutionContext context)
    {
        if (!string.IsNullOrEmpty(CurrentModuleId))
        {
            return CurrentModuleId;
        }

        return context != null ? Normalize(context.ModuleId) : string.Empty;
    }

    private static GameModuleRuntimeContext CreateModuleContext(
        ActionExecutionContext actionContext,
        string previousModuleId,
        string targetModuleId)
    {
        return new GameModuleRuntimeContext(actionContext, previousModuleId, targetModuleId);
    }

    private void SetCurrentModule(string moduleId, ActionExecutionContext context)
    {
        _currentModuleId = Normalize(moduleId);
        if (_stateStore != null)
        {
            _stateStore.SetCurrentModuleId(_currentModuleId);
        }

        if (context != null)
        {
            context.ModuleId = _currentModuleId;
        }
    }

    private static bool IsStopped(ActionExecutionContext context)
    {
        return context != null
            && (context.Handle.IsDone || context.Handle.IsCancellationRequested);
    }

    private static void Fail(ActionExecutionContext context, string message)
    {
        if (context != null)
        {
            context.Handle.Fail(message);
        }
    }

    private static string Normalize(string moduleId)
    {
        return string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
    }
}

public sealed class BattleTurnQteGameModuleRuntime : IGameModuleRuntime
{
    public const string Id = "turn_qte";

    private readonly IBattleTurnQteModuleController _controller;

    public BattleTurnQteGameModuleRuntime(IBattleTurnQteModuleController controller = null)
    {
        _controller = controller;
    }

    public string ModuleId
    {
        get { return Id; }
    }

    public IEnumerator Enter(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.EnterTurnQteModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }

    public IEnumerator Exit(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.ExitTurnQteModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        QTEManager.Instance?.ForceStop();
        BattleUIController.Instance?.SuspendBattleModuleInput();
        yield break;
    }

    public IEnumerator Start(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.StartTurnQteModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }
}

public sealed class BattleAimShooterGameModuleRuntime : IGameModuleRuntime
{
    public const string Id = "aim_shooter";

    private readonly IBattleGameModulePresentationController _presentation;
    private readonly IBattleAimShooterModuleController _controller;

    public BattleAimShooterGameModuleRuntime(
        IBattleGameModulePresentationController presentation = null,
        IBattleAimShooterModuleController controller = null)
    {
        _presentation = presentation;
        _controller = controller;
    }

    public string ModuleId
    {
        get { return Id; }
    }

    public IEnumerator Enter(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.EnterAimShooterModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        ApplyPresentation("AIM SHOOTER");
        yield break;
    }

    public IEnumerator Exit(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.ExitAimShooterModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        ClearPresentation();
        yield break;
    }

    public IEnumerator Start(GameModuleRuntimeContext context)
    {
        if (_controller != null)
        {
            IEnumerator routine = _controller.StartAimShooterModule(context);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            yield break;
        }

        ApplyPresentation("AIM SHOOTER");
        yield break;
    }

    private void ApplyPresentation(string label)
    {
        if (_presentation != null)
        {
            _presentation.ApplyGameModulePresentation(Id, false, label);
            return;
        }

        BattleUIController.Instance?.ApplyGameModulePresentation(Id, false, label);
    }

    private void ClearPresentation()
    {
        if (_presentation != null)
        {
            _presentation.ClearGameModulePresentation(Id);
            return;
        }

        BattleUIController.Instance?.ClearGameModulePresentation(Id);
    }
}

public interface IBattleGameModulePresentationController
{
    void ApplyGameModulePresentation(string moduleId, bool acceptsTurnQteInput, string label);

    void ClearGameModulePresentation(string moduleId);
}

public interface IBattleAimShooterModuleController
{
    IEnumerator EnterAimShooterModule(GameModuleRuntimeContext context);

    IEnumerator ExitAimShooterModule(GameModuleRuntimeContext context);

    IEnumerator StartAimShooterModule(GameModuleRuntimeContext context);

    BattleAimShooterShotResult FireAtTarget(string targetSubjectId);
}

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

public sealed class BattleAimShooterModuleController : IBattleAimShooterModuleController
{
    private readonly IBattleGameModulePresentationController _presentation;
    private readonly BattleAimShooterModuleSettings _settings;
    private BattleAimShooterCombatSession _session;

    public BattleAimShooterModuleController(
        IBattleGameModulePresentationController presentation = null,
        BattleAimShooterModuleSettings settings = null)
    {
        _presentation = presentation;
        _settings = settings ?? new BattleAimShooterModuleSettings();
    }

    public bool HasActiveSession
    {
        get { return _session != null && !_session.IsCompleted; }
    }

    public IEnumerator EnterAimShooterModule(GameModuleRuntimeContext context)
    {
        ApplyPresentation();
        yield break;
    }

    public IEnumerator ExitAimShooterModule(GameModuleRuntimeContext context)
    {
        _session = null;
        ClearPresentation();
        yield break;
    }

    public IEnumerator StartAimShooterModule(GameModuleRuntimeContext context)
    {
        ApplyPresentation();
        _session = new BattleAimShooterCombatSession(context, _settings);
        yield break;
    }

    public BattleAimShooterShotResult FireAtTarget(string targetSubjectId)
    {
        if (_session == null)
        {
            return BattleAimShooterShotResult.Failed(
                targetSubjectId,
                0,
                0,
                0,
                string.Empty,
                "aim_shooter has no active combat session.");
        }

        BattleAimShooterShotResult result = _session.FireAt(targetSubjectId);
        if (_session.IsCompleted)
        {
            _session = null;
        }

        return result;
    }

    private void ApplyPresentation()
    {
        if (_presentation != null)
        {
            _presentation.ApplyGameModulePresentation(
                BattleAimShooterGameModuleRuntime.Id,
                false,
                "AIM SHOOTER");
            return;
        }

        BattleUIController.Instance?.ApplyGameModulePresentation(
            BattleAimShooterGameModuleRuntime.Id,
            false,
            "AIM SHOOTER");
    }

    private void ClearPresentation()
    {
        if (_presentation != null)
        {
            _presentation.ClearGameModulePresentation(BattleAimShooterGameModuleRuntime.Id);
            return;
        }

        BattleUIController.Instance?.ClearGameModulePresentation(BattleAimShooterGameModuleRuntime.Id);
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

public interface IBattleTurnQteModuleController
{
    IEnumerator EnterTurnQteModule(GameModuleRuntimeContext context);

    IEnumerator ExitTurnQteModule(GameModuleRuntimeContext context);

    IEnumerator StartTurnQteModule(GameModuleRuntimeContext context);

    IEnumerator RunTurnCalculation();

    void AdvanceTurn();

    IEnumerator BeginPlayerTurn(PlayerCharacter player);

    IEnumerator BeginEnemyTurn();

    IEnumerator RunEnemyAction();

    void SelectPlayerAction(PlayerCharacter actor, PlayerMenuAction action);

    void SelectSubMenuAction(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item);

    void CancelActionSelection();

    void CancelTargetSelection();

    void ConfirmTargetAndExecute(int targetIndex);

    void CompleteAction();
}
