using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ActionDirectorTests
{
    [Test]
    public void PlaysActionsSequentially()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.a", log));
        registry.Register(new LoggingActionAdapter("test.b", log));

        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = MakeSequence("test.a", "test.b");

        RunToCompletion(director.Play(sequence, context));

        Assert.That(log, Is.EqualTo(new[] { "test.a", "test.b" }));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    [Test]
    public void ParallelActionsAdvanceTogether()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.a", log, logStartAndEnd: true));
        registry.Register(new LoggingActionAdapter("test.b", log, logStartAndEnd: true));

        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = MakeParallelSequence("test.a", "test.b");

        RunToCompletion(director.Play(sequence, context));

        Assert.That(log, Is.EqualTo(new[] { "test.a:start", "test.b:start", "test.a:end", "test.b:end" }));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    [Test]
    public void UnknownActionFailsHandle()
    {
        var director = new ActionDirector(new ActionAdapterRegistry());
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = MakeSequence("missing.action");

        RunToCompletion(director.Play(sequence, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("Unknown action id"));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    [Test]
    public void DisabledActionsAreSkipped()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingActionAdapter("test.a", log));

        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(new ScenarioActionData { ActionId = "test.a", Disabled = true });

        RunToCompletion(director.Play(sequence, context));

        Assert.That(log, Is.Empty);
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    private static ActionSequenceAsset MakeSequence(params string[] actionIds)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        for (int i = 0; i < actionIds.Length; i++)
        {
            sequence.Actions.Add(new ScenarioActionData { ActionId = actionIds[i] });
        }

        return sequence;
    }

    private static ActionSequenceAsset MakeParallelSequence(params string[] actionIds)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        var parallel = new ScenarioActionData { ActionId = ActionDirector.ParallelActionId };
        for (int i = 0; i < actionIds.Length; i++)
        {
            parallel.Children.Add(new ScenarioActionData { ActionId = actionIds[i] });
        }

        sequence.Actions.Add(parallel);
        return sequence;
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
        private readonly bool _logStartAndEnd;

        public LoggingActionAdapter(string actionId, List<string> log, bool logStartAndEnd = false)
        {
            ActionId = actionId;
            _log = log;
            _logStartAndEnd = logStartAndEnd;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            if (!_logStartAndEnd)
            {
                _log.Add(ActionId);
                yield break;
            }

            _log.Add(ActionId + ":start");
            yield return null;
            _log.Add(ActionId + ":end");
        }
    }
}
