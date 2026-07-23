using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

public class GameModuleActionRunnerTests
{
    [Test]
    public void SwitchToExitsCurrentModuleAndEntersTargetModule()
    {
        var log = new List<string>();
        var registry = new GameModuleRegistry();
        Assert.That(registry.Register(new LoggingGameModule("turn_qte", log)), Is.True);
        Assert.That(registry.Register(new LoggingGameModule("aim_shooter", log)), Is.True);

        var context = new ActionExecutionContext();
        context.ModuleId = "turn_qte";
        var runner = new GameModuleActionRunner(registry);

        RunToCompletion(runner.SwitchTo("aim_shooter", context));

        Assert.That(log, Is.EqualTo(new[] { "exit:turn_qte", "enter:aim_shooter" }));
        Assert.That(context.ModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(runner.CurrentModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.NotStarted));
    }

    [Test]
    public void StartRunsRegisteredModuleAndUpdatesCurrentModule()
    {
        var log = new List<string>();
        var registry = new GameModuleRegistry();
        registry.Register(new LoggingGameModule("boxing", log));
        var context = new ActionExecutionContext();
        var runner = new GameModuleActionRunner(registry);

        RunToCompletion(runner.Start("boxing", context));

        Assert.That(log, Is.EqualTo(new[] { "start:boxing" }));
        Assert.That(context.ModuleId, Is.EqualTo("boxing"));
        Assert.That(runner.CurrentModuleId, Is.EqualTo("boxing"));
    }

    [Test]
    public void UnknownModuleFailsHandle()
    {
        var context = new ActionExecutionContext();
        var runner = new GameModuleActionRunner(new GameModuleRegistry());

        RunToCompletion(runner.SwitchTo("missing_module", context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("Game Module is not registered"));
    }

    [Test]
    public void DuplicateModuleRegistrationIsRejected()
    {
        var registry = new GameModuleRegistry();
        Assert.That(registry.Register(new LoggingGameModule("turn_qte", new List<string>())), Is.True);
        Assert.That(registry.Register(new LoggingGameModule("turn_qte", new List<string>())), Is.False);
    }

    [Test]
    public void BattleTurnQteModuleUsesStableModuleId()
    {
        var module = new BattleTurnQteGameModuleRuntime();

        Assert.That(module.ModuleId, Is.EqualTo("turn_qte"));
    }

    [Test]
    public void BattleTurnQteModuleIsSafeWhenBattleSingletonsAreMissing()
    {
        var module = new BattleTurnQteGameModuleRuntime();
        var context = new ActionExecutionContext();
        var moduleContext = new GameModuleRuntimeContext(context, string.Empty, BattleTurnQteGameModuleRuntime.Id);

        RunToCompletion(module.Enter(moduleContext));
        RunToCompletion(module.Exit(moduleContext));
        RunToCompletion(module.Start(moduleContext));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.NotStarted));
    }

    [Test]
    public void BattleTurnQteModuleDelegatesLifecycleToInjectedController()
    {
        var log = new List<string>();
        var controller = new FakeTurnQteModuleController(log);
        var module = new BattleTurnQteGameModuleRuntime(controller);
        var context = new ActionExecutionContext();
        var moduleContext = new GameModuleRuntimeContext(context, "aim_shooter", BattleTurnQteGameModuleRuntime.Id);

        RunToCompletion(module.Enter(moduleContext));
        RunToCompletion(module.Start(moduleContext));
        RunToCompletion(module.Exit(moduleContext));

        Assert.That(log, Is.EqualTo(new[] { "enter:turn_qte", "start:turn_qte", "exit:turn_qte" }));
    }

    [Test]
    public void BattleTurnQteControllerOwnsTurnAndEnemyActionEntryPoints()
    {
        var log = new List<string>();
        var controller = new FakeTurnQteModuleController(log);

        RunToCompletion(controller.RunTurnCalculation());
        controller.AdvanceTurn();
        RunToCompletion(controller.BeginPlayerTurn(null));
        RunToCompletion(controller.BeginEnemyTurn());
        RunToCompletion(controller.RunEnemyAction());

        Assert.That(log, Is.EqualTo(new[] { "turn_calc", "advance_turn", "player_turn", "enemy_turn", "enemy_action" }));
    }

    [Test]
    public void BattleTurnQteControllerOwnsPlayerInputEntryPoints()
    {
        var log = new List<string>();
        var controller = new FakeTurnQteModuleController(log);

        controller.SelectPlayerAction(null, PlayerMenuAction.Attack);
        controller.SelectSubMenuAction(null, PlayerMenuAction.Skill, null, null);
        controller.CancelActionSelection();
        controller.CancelTargetSelection();
        controller.ConfirmTargetAndExecute(2);
        controller.CompleteAction();

        Assert.That(log, Is.EqualTo(new[]
        {
            "action:Attack",
            "sub_action:Skill",
            "cancel_action",
            "cancel_target",
            "target:2",
            "complete_action"
        }));
    }

    [Test]
    public void BattleDefaultRegistryContainsTurnQteCompatibilityModule()
    {
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault();

        Assert.That(registry.TryGet(BattleTurnQteGameModuleRuntime.Id, out IGameModuleRuntime module), Is.True);
        Assert.That(module, Is.TypeOf<BattleTurnQteGameModuleRuntime>());
    }

    [Test]
    public void BattleDefaultRegistryContainsAimShooterModule()
    {
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault();

        Assert.That(registry.TryGet(BattleAimShooterGameModuleRuntime.Id, out IGameModuleRuntime module), Is.True);
        Assert.That(module, Is.TypeOf<BattleAimShooterGameModuleRuntime>());
    }

    [Test]
    public void BattleDefaultRegistryInjectsTurnQteController()
    {
        var log = new List<string>();
        var controller = new FakeTurnQteModuleController(log);
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault(controller);
        registry.TryGet(BattleTurnQteGameModuleRuntime.Id, out IGameModuleRuntime module);
        var context = new ActionExecutionContext();

        RunToCompletion(module.Start(new GameModuleRuntimeContext(
            context,
            string.Empty,
            BattleTurnQteGameModuleRuntime.Id)));

        Assert.That(log, Is.EqualTo(new[] { "start:turn_qte" }));
    }

    [Test]
    public void AimShooterModuleOwnsPresentationAndDisablesTurnQteInput()
    {
        var presentation = new FakeBattleGameModulePresentationController();
        var module = new BattleAimShooterGameModuleRuntime(presentation);
        var context = new ActionExecutionContext();
        var moduleContext = new GameModuleRuntimeContext(context, BattleTurnQteGameModuleRuntime.Id, BattleAimShooterGameModuleRuntime.Id);

        RunToCompletion(module.Enter(moduleContext));
        RunToCompletion(module.Start(moduleContext));
        RunToCompletion(module.Exit(moduleContext));

        Assert.That(presentation.Log, Is.EqualTo(new[]
        {
            "apply:aim_shooter:False:AIM SHOOTER",
            "apply:aim_shooter:False:AIM SHOOTER",
            "clear:aim_shooter"
        }));
    }

    [Test]
    public void BattleDefaultRegistryInjectsAimShooterController()
    {
        var log = new List<string>();
        var controller = new FakeAimShooterModuleController(log);
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault(
            null,
            null,
            controller);
        registry.TryGet(BattleAimShooterGameModuleRuntime.Id, out IGameModuleRuntime module);
        var context = new ActionExecutionContext();

        RunToCompletion(module.Start(new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id)));

        Assert.That(log, Is.EqualTo(new[] { "start:aim_shooter" }));
    }

    [Test]
    public void BattleDefaultRegistryCreatesAimShooterControllerWhenNoneIsInjected()
    {
        var presentation = new FakeBattleGameModulePresentationController();
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault(
            null,
            presentation);
        registry.TryGet(BattleAimShooterGameModuleRuntime.Id, out IGameModuleRuntime module);
        var context = new ActionExecutionContext();

        RunToCompletion(module.Start(new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id)));

        Assert.That(presentation.Log, Is.EqualTo(new[]
        {
            "apply:aim_shooter:False:AIM SHOOTER"
        }));
    }

    [Test]
    public void AimShooterModuleDelegatesLifecycleToInjectedController()
    {
        var log = new List<string>();
        var controller = new FakeAimShooterModuleController(log);
        var module = new BattleAimShooterGameModuleRuntime(null, controller);
        var context = new ActionExecutionContext();
        var events = new FakeGameModuleEventSink();
        context.SetService<IGameModuleEventSink>(events);
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);

        RunToCompletion(module.Enter(moduleContext));
        RunToCompletion(module.Start(moduleContext));
        RunToCompletion(module.Exit(moduleContext));

        Assert.That(log, Is.EqualTo(new[]
        {
            "enter:aim_shooter",
            "start:aim_shooter",
            "exit:aim_shooter"
        }));
        Assert.That(events.Log, Is.EqualTo(new[]
        {
            "completed:aim_shooter:ready:AfterCurrentModule"
        }));
    }

    [Test]
    public void AimShooterCombatSessionDamagesAliveEnemyAndReportsVictory()
    {
        var context = new ActionExecutionContext();
        var commands = new FakeBattleParticipantCommandRunner();
        var events = new FakeGameModuleEventSink();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        context.SetService<IBattleSessionStateReader>(CreateBattleSessionWithEnemy("zev", true));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(
            moduleContext,
            new BattleAimShooterModuleSettings(damagePerHit: 7, requiredHits: 1, maxShots: 3));

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Completed, Is.True);
        Assert.That(result.OutcomeId, Is.EqualTo("victory"));
        Assert.That(result.AppliedDamage, Is.EqualTo(7));
        Assert.That(commands.Log, Is.EqualTo(new[] { "damage:zev:7" }));
        Assert.That(events.Log, Is.EqualTo(new[] { "completed:aim_shooter:victory:AfterCurrentModule" }));
    }

    [Test]
    public void AimShooterCombatSessionRejectsDeadEnemyWithoutDamage()
    {
        var context = new ActionExecutionContext();
        var commands = new FakeBattleParticipantCommandRunner();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IBattleSessionStateReader>(CreateBattleSessionWithEnemy("zev", false));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(moduleContext);

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not alive"));
        Assert.That(commands.Log, Is.Empty);
    }

    [Test]
    public void AimShooterCombatSessionReportsFailureWhenShotsRunOut()
    {
        var context = new ActionExecutionContext();
        var commands = new FakeBattleParticipantCommandRunner();
        var events = new FakeGameModuleEventSink();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        context.SetService<IBattleSessionStateReader>(CreateBattleSessionWithEnemy("zev", true));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(
            moduleContext,
            new BattleAimShooterModuleSettings(damagePerHit: 2, requiredHits: 2, maxShots: 1));

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Completed, Is.True);
        Assert.That(result.OutcomeId, Is.EqualTo("failed"));
        Assert.That(result.Hits, Is.EqualTo(1));
        Assert.That(result.ShotsRemaining, Is.EqualTo(0));
        Assert.That(events.Log, Is.EqualTo(new[] { "completed:aim_shooter:failed:AfterCurrentModule" }));
    }

    [Test]
    public void AimShooterControllerStartsSessionAndRoutesFireAtTarget()
    {
        var presentation = new FakeBattleGameModulePresentationController();
        var controller = new BattleAimShooterModuleController(
            presentation,
            new BattleAimShooterModuleSettings(damagePerHit: 4, requiredHits: 1, maxShots: 1));
        var context = new ActionExecutionContext();
        var commands = new FakeBattleParticipantCommandRunner();
        var events = new FakeGameModuleEventSink();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        context.SetService<IBattleSessionStateReader>(CreateBattleSessionWithEnemy("zev", true));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);

        RunToCompletion(controller.EnterAimShooterModule(moduleContext));
        RunToCompletion(controller.StartAimShooterModule(moduleContext));
        BattleAimShooterShotResult result = controller.FireAtTarget("zev");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Completed, Is.True);
        Assert.That(result.OutcomeId, Is.EqualTo("victory"));
        Assert.That(controller.HasActiveSession, Is.False);
        Assert.That(commands.Log, Is.EqualTo(new[] { "damage:zev:4" }));
        Assert.That(events.Log, Is.EqualTo(new[] { "completed:aim_shooter:victory:AfterCurrentModule" }));
        Assert.That(presentation.Log, Is.EqualTo(new[]
        {
            "apply:aim_shooter:False:AIM SHOOTER",
            "apply:aim_shooter:False:AIM SHOOTER"
        }));
    }

    [Test]
    public void AimShooterControllerRejectsFireBeforeSessionStarts()
    {
        var controller = new BattleAimShooterModuleController();

        BattleAimShooterShotResult result = controller.FireAtTarget("zev");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("no active combat session"));
    }

    [Test]
    public void SwitchToUpdatesInjectedModuleStateStore()
    {
        var log = new List<string>();
        var registry = new GameModuleRegistry();
        registry.Register(new LoggingGameModule("turn_qte", log));
        registry.Register(new LoggingGameModule("aim_shooter", log));
        var stateStore = new FakeModuleStateStore();
        var context = new ActionExecutionContext();
        var runner = new GameModuleActionRunner(registry, "turn_qte", stateStore);

        RunToCompletion(runner.SwitchTo("aim_shooter", context));

        Assert.That(stateStore.CurrentModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(runner.CurrentModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(context.ModuleId, Is.EqualTo("aim_shooter"));
    }

    [Test]
    public void ModuleRuntimeReceivesBattleSessionAndCommandSeams()
    {
        var registry = new GameModuleRegistry();
        var module = new InspectingGameModule("aim_shooter");
        registry.Register(module);
        var context = new ActionExecutionContext();
        var session = BattleSessionState.Create(null);
        var commands = new FakeBattleParticipantCommandRunner();
        var events = new FakeGameModuleEventSink();
        context.SetService<IBattleSessionStateReader>(session);
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        var runner = new GameModuleActionRunner(registry, "turn_qte");

        RunToCompletion(runner.Start("aim_shooter", context));

        Assert.That(module.ReceivedContext, Is.Not.Null);
        Assert.That(module.ReceivedContext.ActionContext, Is.SameAs(context));
        Assert.That(module.ReceivedContext.PreviousModuleId, Is.EqualTo("turn_qte"));
        Assert.That(module.ReceivedContext.TargetModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(module.ReceivedContext.BattleSession, Is.SameAs(session));
        Assert.That(module.ReceivedContext.BattleFlags, Is.SameAs(session));
        Assert.That(module.ReceivedContext.ParticipantCommands, Is.SameAs(commands));
        Assert.That(module.ReceivedContext.ModuleEvents, Is.SameAs(events));
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private static BattleSessionState CreateBattleSessionWithEnemy(string subjectId, bool alive)
    {
        BattleSessionState state = BattleSessionState.Create(null);
        state.SetParticipants(new[]
        {
            new BattleParticipantSnapshot(
                subjectId,
                BattleParticipantKind.Enemy,
                subjectId,
                alive ? 10 : 0,
                10,
                0,
                0,
                alive,
                false,
                false,
                false,
                false,
                false)
        });
        return state;
    }

    private sealed class LoggingGameModule : IGameModuleRuntime
    {
        private readonly List<string> _log;

        public LoggingGameModule(string moduleId, List<string> log)
        {
            ModuleId = moduleId;
            _log = log;
        }

        public string ModuleId { get; }

        public IEnumerator Enter(GameModuleRuntimeContext context)
        {
            yield return null;
            _log.Add("enter:" + ModuleId);
        }

        public IEnumerator Exit(GameModuleRuntimeContext context)
        {
            yield return null;
            _log.Add("exit:" + ModuleId);
        }

        public IEnumerator Start(GameModuleRuntimeContext context)
        {
            yield return null;
            _log.Add("start:" + ModuleId);
        }
    }

    private sealed class InspectingGameModule : IGameModuleRuntime
    {
        public InspectingGameModule(string moduleId)
        {
            ModuleId = moduleId;
        }

        public string ModuleId { get; }
        public GameModuleRuntimeContext ReceivedContext { get; private set; }

        public IEnumerator Enter(GameModuleRuntimeContext context)
        {
            ReceivedContext = context;
            yield break;
        }

        public IEnumerator Exit(GameModuleRuntimeContext context)
        {
            ReceivedContext = context;
            yield break;
        }

        public IEnumerator Start(GameModuleRuntimeContext context)
        {
            ReceivedContext = context;
            yield break;
        }
    }

    private sealed class FakeModuleStateStore : IGameModuleStateStore
    {
        public string CurrentModuleId { get; private set; } = string.Empty;

        public void SetCurrentModuleId(string moduleId)
        {
            CurrentModuleId = moduleId;
        }
    }

    private sealed class FakeBattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        public List<string> Log { get; } = new List<string>();

        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            Log.Add("damage:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 10 - amount);
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 10 + amount);
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 10 + amount);
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 10 - amount);
        }
    }

    private sealed class FakeGameModuleEventSink : IGameModuleEventSink
    {
        public List<string> Log { get; } = new List<string>();

        public void PublishGameModuleCompleted(
            string moduleId,
            string outcomeId = "",
            BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
        {
            Log.Add("completed:" + moduleId + ":" + outcomeId + ":" + timing);
        }
    }

    private sealed class FakeBattleGameModulePresentationController : IBattleGameModulePresentationController
    {
        public List<string> Log { get; } = new List<string>();

        public void ApplyGameModulePresentation(string moduleId, bool acceptsTurnQteInput, string label)
        {
            Log.Add("apply:" + moduleId + ":" + acceptsTurnQteInput + ":" + label);
        }

        public void ClearGameModulePresentation(string moduleId)
        {
            Log.Add("clear:" + moduleId);
        }
    }

    private sealed class FakeAimShooterModuleController : IBattleAimShooterModuleController
    {
        private readonly List<string> _log;

        public FakeAimShooterModuleController(List<string> log)
        {
            _log = log;
        }

        public IEnumerator EnterAimShooterModule(GameModuleRuntimeContext context)
        {
            _log.Add("enter:" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator ExitAimShooterModule(GameModuleRuntimeContext context)
        {
            _log.Add("exit:" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator StartAimShooterModule(GameModuleRuntimeContext context)
        {
            _log.Add("start:" + context.TargetModuleId);
            context.ModuleEvents?.PublishGameModuleCompleted(
                BattleAimShooterGameModuleRuntime.Id,
                "ready",
                BattleRuleTiming.AfterCurrentModule);
            yield break;
        }

        public BattleAimShooterShotResult FireAtTarget(string targetSubjectId)
        {
            _log.Add("fire:" + targetSubjectId);
            return BattleAimShooterShotResult.Succeeded(
                targetSubjectId,
                1,
                0,
                1,
                true,
                "ready",
                1);
        }
    }

    private sealed class FakeTurnQteModuleController : IBattleTurnQteModuleController
    {
        private readonly List<string> _log;

        public FakeTurnQteModuleController(List<string> log)
        {
            _log = log;
        }

        public IEnumerator EnterTurnQteModule(GameModuleRuntimeContext context)
        {
            _log.Add("enter:" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator ExitTurnQteModule(GameModuleRuntimeContext context)
        {
            _log.Add("exit:" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator StartTurnQteModule(GameModuleRuntimeContext context)
        {
            _log.Add("start:" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator RunTurnCalculation()
        {
            _log.Add("turn_calc");
            yield break;
        }

        public void AdvanceTurn()
        {
            _log.Add("advance_turn");
        }

        public IEnumerator BeginPlayerTurn(PlayerCharacter player)
        {
            _log.Add("player_turn");
            yield break;
        }

        public IEnumerator BeginEnemyTurn()
        {
            _log.Add("enemy_turn");
            yield break;
        }

        public IEnumerator RunEnemyAction()
        {
            _log.Add("enemy_action");
            yield break;
        }

        public void SelectPlayerAction(PlayerCharacter actor, PlayerMenuAction action)
        {
            _log.Add("action:" + action);
        }

        public void SelectSubMenuAction(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
        {
            _log.Add("sub_action:" + action);
        }

        public void CancelActionSelection()
        {
            _log.Add("cancel_action");
        }

        public void CancelTargetSelection()
        {
            _log.Add("cancel_target");
        }

        public void ConfirmTargetAndExecute(int targetIndex)
        {
            _log.Add("target:" + targetIndex);
        }

        public void CompleteAction()
        {
            _log.Add("complete_action");
        }

        public void CancelActiveCameraPresentation()
        {
            _log.Add("cancel_camera");
        }
    }
}
