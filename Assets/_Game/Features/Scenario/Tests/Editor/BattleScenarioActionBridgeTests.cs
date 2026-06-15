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

    private static ActionSequenceAsset MakeSequence(string sequenceId, string actionId)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        sequence.Actions.Add(new ScenarioActionData { ActionId = actionId });
        return sequence;
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
}
