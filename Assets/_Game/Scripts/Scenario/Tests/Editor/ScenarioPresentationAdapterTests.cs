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

    [Test]
    public void DialogueWaitIncludesClosingAnimation()
    {
        var runner = new ManualDialogueRunner();
        var context = new ActionExecutionContext();
        context.SetService<IDialogueRunner>(runner);
        IEnumerator routine = new DialogueWaitActionAdapter().Execute(new ScenarioActionData
        { ParametersJson = "{\"id\":\"closing\"}" }, context);
        Assert.That(routine.MoveNext(), Is.True);
        runner.Complete(keepVisible: true);
        Assert.That(routine.MoveNext(), Is.True, "Completion callback must not skip panel fade-out.");
        runner.Close();
        Assert.That(routine.MoveNext(), Is.False);
    }

    [Test]
    public void DialogueWaitCancellationClosesOwnedDialogue()
    {
        var runner = new ManualDialogueRunner();
        var context = new ActionExecutionContext();
        context.SetService<IDialogueRunner>(runner);
        IEnumerator routine = new DialogueWaitActionAdapter().Execute(new ScenarioActionData
        { ParametersJson = "{\"id\":\"cancel\"}" }, context);
        Assert.That(routine.MoveNext(), Is.True);
        context.Handle.Cancel();
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(runner.CancelCount, Is.EqualTo(1));
        Assert.That(runner.IsBusy, Is.False);
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

    private sealed class ManualDialogueRunner : IDialogueRunner, ICancellableDialogueRunner
    {
        private Action _onComplete;

        public bool IsBusy { get; private set; }
        public int CancelCount { get; private set; }
        public readonly List<string> RequestedDialogueIds = new List<string>();

        public void ShowAndWait(string dialogueId, Action onComplete)
        {
            IsBusy = true;
            RequestedDialogueIds.Add(dialogueId);
            _onComplete = onComplete;
        }

        public void Complete(bool keepVisible = false)
        {
            IsBusy = keepVisible;
            Action onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke();
        }

        public void Close() => IsBusy = false;
        public void Cancel()
        {
            CancelCount++;
            IsBusy = false;
            _onComplete = null;
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
