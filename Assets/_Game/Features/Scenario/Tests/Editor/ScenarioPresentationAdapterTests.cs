using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ScenarioPresentationAdapterTests
{
    [Test]
    public void FlowWaitUsesInjectedClock()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new FlowWaitActionAdapter());

        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        context.SetService<IActionClock>(new FixedActionClock(0.25f));
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0.5}"
        });

        int steps = RunToCompletion(director.Play(sequence, context));

        Assert.That(steps, Is.EqualTo(2));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    [Test]
    public void DialogueWaitStartsRunnerAndWaitsForCompletion()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new DialogueWaitActionAdapter());

        var runner = new ManualDialogueRunner();
        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        context.SetService<IDialogueRunner>(runner);
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = DialogueWaitActionAdapter.Id,
            ParametersJson = "{\"id\":\"zev.phase2\"}"
        });

        IEnumerator routine = director.Play(sequence, context);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(runner.RequestedDialogueIds, Is.EqualTo(new[] { "zev.phase2" }));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Running));

        runner.Complete();
        RunToCompletion(routine);

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    [Test]
    public void DialogueWaitFailsWhenRunnerIsBusy()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new DialogueWaitActionAdapter());

        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        context.SetService<IDialogueRunner>(new BusyDialogueRunner());
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = DialogueWaitActionAdapter.Id,
            ParametersJson = "{\"id\":\"zev.phase2\"}"
        });

        RunToCompletion(director.Play(sequence, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("already busy"));

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    private static ActionSequenceAsset MakeSequence(ScenarioActionData action)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(action);
        return sequence;
    }

    private static int RunToCompletion(IEnumerator routine, int maxSteps = 100)
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

        return steps;
    }

    private sealed class FixedActionClock : IActionClock
    {
        public FixedActionClock(float deltaTime)
        {
            DeltaTime = deltaTime;
        }

        public float DeltaTime { get; }
    }

    private sealed class ManualDialogueRunner : IDialogueRunner
    {
        private Action _onComplete;

        public bool IsBusy { get; private set; }
        public readonly List<string> RequestedDialogueIds = new List<string>();

        public void ShowAndWait(string dialogueId, Action onComplete)
        {
            IsBusy = true;
            RequestedDialogueIds.Add(dialogueId);
            _onComplete = onComplete;
        }

        public void Complete()
        {
            IsBusy = false;
            Action onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke();
        }
    }

    private sealed class BusyDialogueRunner : IDialogueRunner
    {
        public bool IsBusy
        {
            get { return true; }
        }

        public void ShowAndWait(string dialogueId, Action onComplete)
        {
            throw new InvalidOperationException("Should not start while busy.");
        }
    }
}
