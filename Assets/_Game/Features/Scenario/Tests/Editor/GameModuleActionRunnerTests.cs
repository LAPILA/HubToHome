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
    public void BattleDefaultRegistryContainsTurnQteCompatibilityModule()
    {
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault();

        Assert.That(registry.TryGet(BattleTurnQteGameModuleRuntime.Id, out IGameModuleRuntime module), Is.True);
        Assert.That(module, Is.TypeOf<BattleTurnQteGameModuleRuntime>());
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
        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
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
        public void PublishGameModuleCompleted(
            string moduleId,
            string outcomeId = "",
            BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
        {
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
    }
}
