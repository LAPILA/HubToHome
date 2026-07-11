using System;
using System.Collections;
using NUnit.Framework;

public class TimelinePlayActionAdapterTests
{
    [Test]
    public void Execute_FailsWhenTimelineRunnerServiceIsMissing()
    {
        var adapter = new TimelinePlayActionAdapter();
        var action = new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\"}"
        };
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_missing_runner"));

        RunToCompletion(adapter.Execute(action, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("ITimelineCutsceneRunner is missing"));
    }

    [Test]
    public void Execute_FailsWhenCutsceneIdIsMissing()
    {
        var adapter = new TimelinePlayActionAdapter();
        var action = new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{}"
        };
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_missing_cutscene"));
        context.SetService<ITimelineCutsceneRunner>(new RecordingTimelineRunner());

        RunToCompletion(adapter.Execute(action, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("cutsceneId"));
    }

    [Test]
    public void Execute_FailsWhenBoolParameterTypeIsInvalid()
    {
        var adapter = new TimelinePlayActionAdapter();
        var action = new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\"zev_intro_clash\",\"waitForComplete\":\"oops\"}"
        };
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_invalid_bool"));
        context.SetService<ITimelineCutsceneRunner>(new RecordingTimelineRunner());

        RunToCompletion(adapter.Execute(action, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("waitForComplete"));
    }

    [Test]
    public void Execute_InvokesRunnerWithParsedParameters()
    {
        var adapter = new TimelinePlayActionAdapter();
        var action = new ScenarioActionData
        {
            ActionId = TimelinePlayActionAdapter.Id,
            ParametersJson = "{\"cutsceneId\":\" zev_intro_clash \",\"waitForComplete\":false,\"lockInput\":false,\"restoreCamera\":true,\"skipIfMissing\":true}"
        };
        var runner = new RecordingTimelineRunner();
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_success"));
        context.SetService<ITimelineCutsceneRunner>(runner);

        RunToCompletion(adapter.Execute(action, context));

        Assert.That(runner.CallCount, Is.EqualTo(1));
        Assert.That(runner.CutsceneId, Is.EqualTo("zev_intro_clash"));
        Assert.That(runner.WaitForComplete, Is.False);
        Assert.That(runner.LockInput, Is.False);
        Assert.That(runner.RestoreCamera, Is.True);
        Assert.That(runner.SkipIfMissing, Is.True);
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.NotStarted));
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 64)
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

    private sealed class RecordingTimelineRunner : ITimelineCutsceneRunner
    {
        public int CallCount { get; private set; }
        public string CutsceneId { get; private set; }
        public bool WaitForComplete { get; private set; }
        public bool LockInput { get; private set; }
        public bool RestoreCamera { get; private set; }
        public bool SkipIfMissing { get; private set; }

        public IEnumerator PlayCutscene(
            string cutsceneId,
            bool waitForComplete,
            bool lockInput,
            bool restoreCamera,
            bool skipIfMissing,
            ActionExecutionContext context)
        {
            CallCount++;
            CutsceneId = cutsceneId;
            WaitForComplete = waitForComplete;
            LockInput = lockInput;
            RestoreCamera = restoreCamera;
            SkipIfMissing = skipIfMissing;
            yield break;
        }
    }
}