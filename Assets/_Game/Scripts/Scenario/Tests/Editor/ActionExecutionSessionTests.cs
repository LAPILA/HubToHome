using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class ActionExecutionSessionTests
{
    [Test]
    public void RecordsSessionSequenceAndBlockLifecycle()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence(
            "lifecycle",
            Action("a", "test.a"),
            Action("b", "test.b"));
        ActionDirector director = Director(
            new LoggingAdapter("test.a", log),
            new LoggingAdapter("test.b", log));
        var context = new ActionExecutionContext(new ActionExecutionHandle("lifecycle"));
        var session = new ActionExecutionSession();

        RunToCompletion(director.Play(new ActionPlayRequest(sequence), context, session));

        Assert.That(log, Is.EqualTo(new[] { "test.a", "test.b" }));
        Assert.That(session.Events.First().EventType, Is.EqualTo(ActionExecutionEventType.SessionStarted));
        Assert.That(session.Events.Last().EventType, Is.EqualTo(ActionExecutionEventType.SessionCompleted));
        Assert.That(Events(session, ActionExecutionEventType.BlockStarted).Select(item => item.BlockId),
            Is.EqualTo(new[] { "a", "b" }));
        Assert.That(Events(session, ActionExecutionEventType.BlockCompleted).Select(item => item.BlockId),
            Is.EqualTo(new[] { "a", "b" }));
        Assert.That(session.CurrentBlockId, Is.Empty);
        Assert.That(session.IsCompleted, Is.True);
        Destroy(sequence);
    }

    [Test]
    public void DisabledBlockIsReportedAsSkipped()
    {
        ActionSequenceAsset sequence = Sequence(
            "skip",
            new ScenarioActionData { BlockId = "skip-me", ActionId = "test.a", Disabled = true });
        var session = new ActionExecutionSession();
        var context = new ActionExecutionContext();

        RunToCompletion(Director().Play(new ActionPlayRequest(sequence), context, session));

        ActionExecutionEvent skipped = Events(session, ActionExecutionEventType.BlockSkipped).Single();
        Assert.That(skipped.BlockId, Is.EqualTo("skip-me"));
        Assert.That(skipped.Message, Is.EqualTo("disabled"));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        Destroy(sequence);
    }

    [Test]
    public void FailedBlockReportsBlockAndSessionFailure()
    {
        ActionSequenceAsset sequence = Sequence("failure", Action("bad", "test.fail"));
        var session = new ActionExecutionSession();
        var context = new ActionExecutionContext();

        RunToCompletion(Director(new FailingAdapter("test.fail", "expected failure"))
            .Play(new ActionPlayRequest(sequence), context, session));

        Assert.That(Events(session, ActionExecutionEventType.BlockFailed).Single().BlockId, Is.EqualTo("bad"));
        Assert.That(session.Events.Last().EventType, Is.EqualTo(ActionExecutionEventType.SessionFailed));
        Assert.That(context.Handle.Result.Message, Does.Contain("expected failure"));
        Destroy(sequence);
    }

    [Test]
    public void StartBlockSkipsPriorBlocksAndContinuesFromTarget()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence(
            "start",
            Action("a", "test.a"),
            Action("b", "test.b"),
            Action("c", "test.c"));
        ActionDirector director = Director(
            new LoggingAdapter("test.a", log),
            new LoggingAdapter("test.b", log),
            new LoggingAdapter("test.c", log));
        var request = ActionPlayRequest.FromBlock(sequence, "b");
        var session = new ActionExecutionSession();

        RunToCompletion(director.Play(request, new ActionExecutionContext(), session));

        Assert.That(log, Is.EqualTo(new[] { "test.b", "test.c" }));
        Assert.That(Events(session, ActionExecutionEventType.BlockSkipped).Single().BlockId, Is.EqualTo("a"));
        Destroy(sequence);
    }

    [Test]
    public void MissingStartBlockFailsBeforePlayback()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence("missing", Action("a", "test.a"));
        var context = new ActionExecutionContext();
        var session = new ActionExecutionSession();

        RunToCompletion(Director(new LoggingAdapter("test.a", log)).Play(
            ActionPlayRequest.FromBlock(sequence, "missing"),
            context,
            session));

        Assert.That(log, Is.Empty);
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("Start Block ID was not found"));
        Destroy(sequence);
    }

    [Test]
    public void PauseAndResumeStopsBeforeBlockStart()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence("pause", Action("a", "test.a"));
        var session = new ActionExecutionSession();
        session.Pause();
        IEnumerator routine = Director(new LoggingAdapter("test.a", log)).Play(
            new ActionPlayRequest(sequence),
            new ActionExecutionContext(),
            session);

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(log, Is.Empty);
        session.Resume();
        RunToCompletion(routine);

        Assert.That(log, Is.EqualTo(new[] { "test.a" }));
        Assert.That(session.Events.Any(item => item.EventType == ActionExecutionEventType.Paused), Is.True);
        Assert.That(session.Events.Any(item => item.EventType == ActionExecutionEventType.Resumed), Is.True);
        Destroy(sequence);
    }

    [Test]
    public void StepBudgetRunsExactlyOneWholeBlock()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence(
            "step",
            Action("a", "test.a"),
            Action("b", "test.b"));
        var session = new ActionExecutionSession();
        session.Pause();
        IEnumerator routine = Director(
            new LoggingAdapter("test.a", log),
            new LoggingAdapter("test.b", log)).Play(
                new ActionPlayRequest(sequence),
                new ActionExecutionContext(),
                session);

        Assert.That(routine.MoveNext(), Is.True);
        session.Step();
        AdvanceUntil(routine, () => log.Count == 1);
        Assert.That(log, Is.EqualTo(new[] { "test.a" }));
        Assert.That(session.IsPaused, Is.True);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(log, Has.Count.EqualTo(1));

        session.Step();
        RunToCompletion(routine);
        Assert.That(log, Is.EqualTo(new[] { "test.a", "test.b" }));
        Destroy(sequence);
    }

    [Test]
    public void StepWhilePausedMidBlockCompletesCurrentBlock()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = Sequence("mid", Action("slow", "test.slow"));
        var session = new ActionExecutionSession();
        IEnumerator routine = Director(new FrameAdapter("test.slow", log, 1)).Play(
            new ActionPlayRequest(sequence),
            new ActionExecutionContext(),
            session);

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(log, Is.EqualTo(new[] { "test.slow:start" }));
        session.Pause();
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(log, Has.Count.EqualTo(1));
        session.Step();
        RunToCompletion(routine);

        Assert.That(log, Is.EqualTo(new[] { "test.slow:start", "test.slow:end" }));
        Destroy(sequence);
    }

    [Test]
    public void CancellationEscapesPausedExecution()
    {
        ActionSequenceAsset sequence = Sequence("cancel", Action("a", "test.a"));
        var session = new ActionExecutionSession();
        var context = new ActionExecutionContext(new ActionExecutionHandle("cancel"));
        session.Pause();
        IEnumerator routine = Director(new LoggingAdapter("test.a", new List<string>())).Play(
            new ActionPlayRequest(sequence), context, session);

        Assert.That(routine.MoveNext(), Is.True);
        session.Cancel("user canceled");
        RunToCompletion(routine);

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Canceled));
        Assert.That(session.Events.Last().EventType, Is.EqualTo(ActionExecutionEventType.SessionCanceled));
        Destroy(sequence);
    }

    [Test]
    public void NestedSequenceEventsKeepCallingBlockAsParent()
    {
        var log = new List<string>();
        ActionSequenceAsset child = Sequence("child", Action("child-block", "test.child"));
        ScenarioActionData call = Action("call-block", SequenceCallActionAdapter.Id);
        call.ParametersJson = new JObject { ["sequence"] = "child" }.ToString();
        ActionSequenceAsset parent = Sequence("parent", call);
        var registry = new ActionAdapterRegistry();
        registry.Register(new LoggingAdapter("test.child", log));
        registry.Register(new SequenceCallActionAdapter(registry));
        var context = new ActionExecutionContext();
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { child }));
        var session = new ActionExecutionSession();

        RunToCompletion(new ActionDirector(registry).Play(
            new ActionPlayRequest(parent),
            context,
            session));

        ActionExecutionEvent childStart = Events(session, ActionExecutionEventType.BlockStarted)
            .Single(item => item.BlockId == "child-block");
        Assert.That(childStart.ParentBlockId, Is.EqualTo("call-block"));
        Assert.That(Events(session, ActionExecutionEventType.SequenceStarted).Select(item => item.SequenceId),
            Is.EqualTo(new[] { "parent", "child" }));
        Assert.That(log, Is.EqualTo(new[] { "test.child" }));
        Destroy(parent, child);
    }

    [Test]
    public void ParallelAllReportsParentAndEveryChild()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = ParallelSequence(
            "all",
            ActionParallelPolicy.All,
            Action("left", "test.left"),
            Action("right", "test.right"));
        var session = new ActionExecutionSession();

        RunToCompletion(Director(
            new FrameAdapter("test.left", log, 1),
            new FrameAdapter("test.right", log, 1)).Play(
                new ActionPlayRequest(sequence),
                new ActionExecutionContext(),
                session));

        Assert.That(Events(session, ActionExecutionEventType.BlockCompleted).Select(item => item.BlockId),
            Does.Contain("parallel"));
        Assert.That(Events(session, ActionExecutionEventType.BlockCompleted).Select(item => item.BlockId),
            Does.Contain("left").And.Contain("right"));
        Destroy(sequence);
    }

    [Test]
    public void ParallelAnyCompletesOnFirstSuccessAndCancelsRemainingChild()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = ParallelSequence(
            "any",
            ActionParallelPolicy.Any,
            Action("slow", "test.slow"),
            Action("fast", "test.fast"));
        var session = new ActionExecutionSession();
        var context = new ActionExecutionContext();

        RunToCompletion(Director(
            new FrameAdapter("test.slow", log, 5),
            new LoggingAdapter("test.fast", log)).Play(
                new ActionPlayRequest(sequence), context, session));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        Assert.That(log, Does.Contain("test.fast"));
        Assert.That(Events(session, ActionExecutionEventType.BlockCanceled).Any(item => item.BlockId == "slow"), Is.True);
        Destroy(sequence);
    }

    [Test]
    public void ParallelAnyFailsWhenEveryChildFails()
    {
        ActionSequenceAsset sequence = ParallelSequence(
            "any-fail",
            ActionParallelPolicy.Any,
            Action("a", "test.a"),
            Action("b", "test.b"));
        var context = new ActionExecutionContext();

        RunToCompletion(Director(
            new FailingAdapter("test.a", "a failed"),
            new FailingAdapter("test.b", "b failed")).Play(
                new ActionPlayRequest(sequence), context, new ActionExecutionSession()));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("no successful child"));
        Destroy(sequence);
    }

    [Test]
    public void ParallelRacePropagatesFirstTerminalFailure()
    {
        var log = new List<string>();
        ActionSequenceAsset sequence = ParallelSequence(
            "race",
            ActionParallelPolicy.Race,
            Action("slow", "test.slow"),
            Action("fail", "test.fail"));
        var context = new ActionExecutionContext();
        var session = new ActionExecutionSession();

        RunToCompletion(Director(
            new FrameAdapter("test.slow", log, 5),
            new FailingAdapter("test.fail", "race failed")).Play(
                new ActionPlayRequest(sequence), context, session));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("race winner failed"));
        Assert.That(Events(session, ActionExecutionEventType.BlockCanceled).Any(item => item.BlockId == "slow"), Is.True);
        Destroy(sequence);
    }

    [Test]
    public void HandlePublishesObservableStatusChanges()
    {
        var statuses = new List<ActionExecutionStatus>();
        var handle = new ActionExecutionHandle();
        handle.Changed += changed => statuses.Add(changed.Status);
        ActionSequenceAsset sequence = Sequence(
            "observable-handle",
            Action("a", "test.a"));
        var context = new ActionExecutionContext(handle);

        RunToCompletion(Director(new LoggingAdapter("test.a", new List<string>())).Play(
            new ActionPlayRequest(sequence),
            context,
            new ActionExecutionSession()));

        Assert.That(statuses, Is.EqualTo(new[]
        {
            ActionExecutionStatus.Running,
            ActionExecutionStatus.Succeeded
        }));
        Destroy(sequence);
    }

    private static ActionDirector Director(params IActionAdapter[] adapters)
    {
        var registry = new ActionAdapterRegistry();
        for (int i = 0; i < adapters.Length; i++)
        {
            registry.Register(adapters[i]);
        }

        return new ActionDirector(registry);
    }

    private static ActionSequenceAsset Sequence(string id, params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static ActionSequenceAsset ParallelSequence(
        string id,
        ActionParallelPolicy policy,
        params ScenarioActionData[] children)
    {
        ScenarioActionData parallel = Action("parallel", ActionDirector.ParallelActionId);
        parallel.ParametersJson = new JObject
        {
            ["policy"] = policy.ToString().ToLowerInvariant()
        }.ToString();
        parallel.Children.AddRange(children);
        return Sequence(id, parallel);
    }

    private static ScenarioActionData Action(string blockId, string actionId)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = "{}"
        };
    }

    private static List<ActionExecutionEvent> Events(
        ActionExecutionSession session,
        ActionExecutionEventType type)
    {
        return session.Events.Where(item => item.EventType == type).ToList();
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 512)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            if (++steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private static void AdvanceUntil(IEnumerator routine, System.Func<bool> condition, int maxSteps = 128)
    {
        int steps = 0;
        while (!condition())
        {
            Assert.That(routine.MoveNext(), Is.True, "Routine ended before condition was met.");
            if (++steps > maxSteps)
            {
                Assert.Fail("Condition was not met within " + maxSteps + " steps.");
            }
        }
    }

    private static void Destroy(params ActionSequenceAsset[] sequences)
    {
        for (int i = 0; i < sequences.Length; i++)
        {
            Object.DestroyImmediate(sequences[i]);
        }
    }

    private sealed class LoggingAdapter : IActionAdapter
    {
        private readonly List<string> _log;

        public LoggingAdapter(string actionId, List<string> log)
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

    private sealed class FrameAdapter : IActionAdapter
    {
        private readonly List<string> _log;
        private readonly int _frames;

        public FrameAdapter(string actionId, List<string> log, int frames)
        {
            ActionId = actionId;
            _log = log;
            _frames = frames;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            _log.Add(ActionId + ":start");
            for (int i = 0; i < _frames; i++)
            {
                yield return null;
            }

            _log.Add(ActionId + ":end");
        }
    }

    private sealed class FailingAdapter : IActionAdapter
    {
        private readonly string _message;

        public FailingAdapter(string actionId, string message)
        {
            ActionId = actionId;
            _message = message;
        }

        public string ActionId { get; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            context.Handle.Fail(_message);
            yield break;
        }
    }
}
