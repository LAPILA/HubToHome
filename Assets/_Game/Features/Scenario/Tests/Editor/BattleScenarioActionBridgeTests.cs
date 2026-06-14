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

    private static BattleScenarioData MakeScenario(string sequenceId, string actionId)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.Sequences.Add(MakeSequence(sequenceId, actionId));
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
            new BattleScenarioTrigger(
                ruleId,
                sequenceId,
                BattleRuleTiming.AfterCurrentSkill,
                null)
        };
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
}
