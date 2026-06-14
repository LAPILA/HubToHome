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

        RunToCompletion(module.Enter(context));
        RunToCompletion(module.Exit(context));
        RunToCompletion(module.Start(context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.NotStarted));
    }

    [Test]
    public void BattleDefaultRegistryContainsTurnQteCompatibilityModule()
    {
        GameModuleRegistry registry = BattleGameModuleRegistryFactory.CreateDefault();

        Assert.That(registry.TryGet(BattleTurnQteGameModuleRuntime.Id, out IGameModuleRuntime module), Is.True);
        Assert.That(module, Is.TypeOf<BattleTurnQteGameModuleRuntime>());
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

        public IEnumerator Enter(ActionExecutionContext context)
        {
            yield return null;
            _log.Add("enter:" + ModuleId);
        }

        public IEnumerator Exit(ActionExecutionContext context)
        {
            yield return null;
            _log.Add("exit:" + ModuleId);
        }

        public IEnumerator Start(ActionExecutionContext context)
        {
            yield return null;
            _log.Add("start:" + ModuleId);
        }
    }
}
