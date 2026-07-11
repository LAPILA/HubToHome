using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleScenarioActionBridgeTests
{
    [Test]
    public void PlaysTriggeredSequenceThroughActionDirector()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.log");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.log", log));
        var director = new ActionDirector(registry);
        var bridge = new BattleScenarioActionBridge(runtime, director);
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        List<BattleScenarioTrigger> triggers = MakeTriggers("enter_phase2", "zev_phase2");

        try
        {
            RunToCompletion(bridge.PlayTriggers(triggers, context));

            Assert.That(log, Is.EqualTo(new[] { "test.log" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void MissingTriggeredSequenceFailsParentHandle()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.log");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.log", log));
        var director = new ActionDirector(registry);
        var bridge = new BattleScenarioActionBridge(runtime, director);
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        List<BattleScenarioTrigger> triggers = MakeTriggers("enter_phase3", "missing_sequence");

        try
        {
            RunToCompletion(bridge.PlayTriggers(triggers, context));

            Assert.That(log, Is.Empty);
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("Battle scenario sequence not found"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void EmptyTriggerListSucceedsWithoutPlayingActions()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.log");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.log", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));

        try
        {
            RunToCompletion(bridge.PlayTriggers(new List<BattleScenarioTrigger>(), context));

            Assert.That(log, Is.Empty);
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void NullTriggerIsSkippedWithoutFailingTheBatch()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.log");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.log", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));

        try
        {
            RunToCompletion(bridge.PlayTriggers(new List<BattleScenarioTrigger> { null }, context));

            Assert.That(log, Is.Empty);
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void MultipleTriggersPlaySequentially()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario(
            ("zev_phase2", "test.a"),
            ("zev_shooter_intro", "test.b"));
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.a", log));
        registry.Register(new LoggingActionAdapter("test.b", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        var triggers = new List<BattleScenarioTrigger>
        {
            MakeTrigger("enter_phase2", "zev_phase2"),
            MakeTrigger("enter_shooter", "zev_shooter_intro")
        };

        try
        {
            RunToCompletion(bridge.PlayTriggers(triggers, context));

            Assert.That(log, Is.EqualTo(new[] { "test.a", "test.b" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void ChildActionFailurePropagatesToParentHandle()
    {
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.fail");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new FailingActionAdapter("test.fail"));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));

        try
        {
            RunToCompletion(bridge.PlayTriggers(MakeTriggers("enter_phase2", "zev_phase2"), context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("adapter failed"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void ParentCancellationBeforePlaySkipsAllTriggers()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.log");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.log", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        context.Handle.Cancel("test cancel");

        try
        {
            RunToCompletion(bridge.PlayTriggers(MakeTriggers("enter_phase2", "zev_phase2"), context));

            Assert.That(log, Is.Empty);
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Canceled));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void ChildContextKeepsScenarioModeAndModule()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeScenario("zev_phase2", "test.context");
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new ContextLoggingActionAdapter("test.context", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"))
        {
            ScenarioId = "zev_first_battle",
            PrimaryMode = "battle",
            ModuleId = "turn_qte"
        };

        try
        {
            RunToCompletion(bridge.PlayTriggers(MakeTriggers("enter_phase2", "zev_phase2"), context));

            Assert.That(log, Is.EqualTo(new[] { "zev_first_battle|battle|turn_qte" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void MissingRuntimeFailsParentHandle()
    {
        var bridge = new BattleScenarioActionBridge(null, new ActionDirector(new ActionAdapterRegistry()));
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));

        RunToCompletion(bridge.PlayTriggers(MakeTriggers("enter_phase2", "zev_phase2"), context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("Battle scenario runtime is missing"));
    }

    [Test]
    public void ExecutionGatePlaysSequenceFromGameModuleCompletedEvent()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeModuleCompletedScenario();
        var runtime = new BattleScenarioRuntime(scenario);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.module_complete", log));
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(registry));
        var gate = new BattleScenarioExecutionGate(
            runtime,
            bridge,
            () => new ActionExecutionContext(new ActionExecutionHandle("battle_scenario")));

        try
        {
            gate.PublishGameModuleCompleted("aim_shooter", "victory", BattleRuleTiming.AfterCurrentModule);
            RunToCompletion(gate.Flush(BattleRuleTiming.AfterCurrentModule));

            Assert.That(log, Is.EqualTo(new[] { "test.module_complete" }));
            Assert.That(gate.LastHandle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void VerticalSliceSwitchesToDummyModuleAndRunsOutcomeSequence()
    {
        var log = new List<string>();
        BattleScenarioData scenario = MakeDummyModuleVerticalSliceScenario();
        var runtime = new BattleScenarioRuntime(scenario);
        var actionRegistry = new ActionAdapterRegistry();
        actionRegistry.Register(new LoggingActionAdapter("test.before_module", log));
        actionRegistry.Register(new LoggingActionAdapter("test.after_start", log));
        actionRegistry.Register(new LoggingActionAdapter("test.outcome_sequence", log));
        actionRegistry.Register(new ModuleSwitchActionAdapter());
        actionRegistry.Register(new ModuleStartActionAdapter());

        var moduleRegistry = new GameModuleRegistry();
        moduleRegistry.Register(new LoggingGameModule("turn_qte", log));
        moduleRegistry.Register(new LoggingGameModule(
            "dummy_shooter",
            log,
            "victory",
            BattleRuleTiming.AfterCurrentModule));

        var runner = new GameModuleActionRunner(
            moduleRegistry,
            "turn_qte",
            runtime.SessionState);

        BattleScenarioExecutionGate gate = null;
        var bridge = new BattleScenarioActionBridge(runtime, new ActionDirector(actionRegistry));
        gate = new BattleScenarioExecutionGate(
            runtime,
            bridge,
            () =>
            {
                return CreateBattleScenarioContext(runtime, runner, gate);
            });

        ActionExecutionContext rootContext = CreateBattleScenarioContext(runtime, runner, gate);
        List<BattleScenarioTrigger> triggers = MakeTriggers("enter_dummy", "enter_dummy_module");

        try
        {
            RunToCompletion(bridge.PlayTriggers(triggers, rootContext));
            RunToCompletion(gate.Flush(BattleRuleTiming.AfterCurrentModule));

            Assert.That(rootContext.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
            Assert.That(gate.LastHandle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
            Assert.That(runner.CurrentModuleId, Is.EqualTo("dummy_shooter"));
            Assert.That(runtime.SessionState.CurrentModuleId, Is.EqualTo("dummy_shooter"));
            Assert.That(log, Is.EqualTo(new[]
            {
                "test.before_module",
                "turn_qte.exit->dummy_shooter",
                "dummy_shooter.enter:turn_qte",
                "dummy_shooter.start",
                "test.after_start",
                "test.outcome_sequence"
            }));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    private static BattleScenarioData MakeScenario(string sequenceId, string actionId)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.Sequences.Add(MakeSequence(sequenceId, actionId));
        return scenario;
    }

    private static BattleScenarioData MakeScenario(params (string sequenceId, string actionId)[] sequences)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        for (int i = 0; i < sequences.Length; i++)
        {
            scenario.Sequences.Add(MakeSequence(sequences[i].sequenceId, sequences[i].actionId));
        }

        return scenario;
    }

    private static BattleScenarioData MakeModuleCompletedScenario()
    {
        BattleScenarioData scenario = MakeScenario("after_shooter_victory", "test.module_complete");
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "after_shooter_victory",
            EventType = BattleEventType.GameModuleCompleted,
            Timing = BattleRuleTiming.AfterCurrentModule,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "aim_shooter",
            OutcomeId = "victory",
            SequenceId = "after_shooter_victory"
        });
        return scenario;
    }

    private static BattleScenarioData MakeDummyModuleVerticalSliceScenario()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.OpeningModule = "turn_qte";
        scenario.Sequences.Add(MakeSequence(
            "enter_dummy_module",
            new ScenarioActionData { ActionId = "test.before_module" },
            new ScenarioActionData
            {
                ActionId = ModuleSwitchActionAdapter.Id,
                ParametersJson = "{\"to\":\"dummy_shooter\"}"
            },
            new ScenarioActionData
            {
                ActionId = ModuleStartActionAdapter.Id,
                ParametersJson = "{\"module\":\"dummy_shooter\"}"
            },
            new ScenarioActionData { ActionId = "test.after_start" }));
        scenario.Sequences.Add(MakeSequence(
            "after_dummy_victory",
            new ScenarioActionData { ActionId = "test.outcome_sequence" }));
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "after_dummy_victory",
            EventType = BattleEventType.GameModuleCompleted,
            Timing = BattleRuleTiming.AfterCurrentModule,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "dummy_shooter",
            OutcomeId = "victory",
            SequenceId = "after_dummy_victory"
        });

        return scenario;
    }

    private static ActionSequenceAsset MakeSequence(string sequenceId, string actionId)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        sequence.Actions.Add(new ScenarioActionData { ActionId = actionId });
        return sequence;
    }

    private static ActionSequenceAsset MakeSequence(string sequenceId, params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        for (int i = 0; i < actions.Length; i++)
        {
            sequence.Actions.Add(actions[i]);
        }

        return sequence;
    }

    private static ActionExecutionContext CreateBattleScenarioContext(
        BattleScenarioRuntime runtime,
        IGameModuleActionRunner runner,
        IGameModuleEventSink moduleEvents)
    {
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"))
        {
            ScenarioId = runtime.SessionState.ScenarioId,
            PrimaryMode = runtime.SessionState.PrimaryMode,
            ModuleId = runtime.SessionState.CurrentModuleId
        };
        context.SetService(runner);
        context.SetService<IBattleSessionStateReader>(runtime.SessionState);
        context.SetService<IGameModuleStateStore>(runtime.SessionState);
        context.SetService(moduleEvents);
        return context;
    }

    private static List<BattleScenarioTrigger> MakeTriggers(string ruleId, string sequenceId)
    {
        return new List<BattleScenarioTrigger>
        {
            MakeTrigger(ruleId, sequenceId)
        };
    }

    private static BattleScenarioTrigger MakeTrigger(string ruleId, string sequenceId)
    {
        return new BattleScenarioTrigger(
            ruleId,
            sequenceId,
            BattleRuleTiming.AfterCurrentSkill,
            null);
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
        }

        UnityEngine.Object.DestroyImmediate(scenario);
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

    private sealed class LoggingActionAdapter : IActionAdapter
    {
        private readonly List<string> _log;

        public LoggingActionAdapter(string actionId, List<string> log)
        {
            ActionId = actionId;
            _log = log;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            _log.Add(ActionId);
            yield break;
        }
    }

    private sealed class FailingActionAdapter : IActionAdapter
    {
        public FailingActionAdapter(string actionId)
        {
            ActionId = actionId;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            context.Handle.Fail("adapter failed: " + ActionId);
            yield break;
        }
    }

    private sealed class ContextLoggingActionAdapter : IActionAdapter
    {
        private readonly List<string> _log;

        public ContextLoggingActionAdapter(string actionId, List<string> log)
        {
            ActionId = actionId;
            _log = log;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            _log.Add(context.ScenarioId + "|" + context.PrimaryMode + "|" + context.ModuleId);
            yield break;
        }
    }

    private sealed class LoggingGameModule : IGameModuleRuntime
    {
        private readonly List<string> _log;
        private readonly string _completionOutcomeId;
        private readonly BattleRuleTiming _completionTiming;

        public LoggingGameModule(
            string moduleId,
            List<string> log,
            string completionOutcomeId = "",
            BattleRuleTiming completionTiming = BattleRuleTiming.AfterCurrentModule)
        {
            ModuleId = moduleId;
            _log = log;
            _completionOutcomeId = completionOutcomeId;
            _completionTiming = completionTiming;
        }

        public string ModuleId { get; }

        public IEnumerator Enter(GameModuleRuntimeContext context)
        {
            _log.Add(ModuleId + ".enter:" + context.PreviousModuleId);
            yield break;
        }

        public IEnumerator Exit(GameModuleRuntimeContext context)
        {
            _log.Add(ModuleId + ".exit->" + context.TargetModuleId);
            yield break;
        }

        public IEnumerator Start(GameModuleRuntimeContext context)
        {
            _log.Add(ModuleId + ".start");
            if (!string.IsNullOrWhiteSpace(_completionOutcomeId))
            {
                context.ModuleEvents.PublishGameModuleCompleted(
                    ModuleId,
                    _completionOutcomeId,
                    _completionTiming);
            }

            yield break;
        }
    }
}
