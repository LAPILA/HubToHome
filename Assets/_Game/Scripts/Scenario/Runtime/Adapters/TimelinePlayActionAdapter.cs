using System.Collections;

public sealed class TimelinePlayActionAdapter : IActionAdapter
{
    public const string Id = "timeline.play";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        ITimelineCutsceneRunner runner = context.GetService<ITimelineCutsceneRunner>();
        if (runner == null)
        {
            context.Handle.Fail("ITimelineCutsceneRunner is missing for timeline.play.");
            yield break;
        }

        string cutsceneId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "cutsceneId", out cutsceneId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(cutsceneId))
        {
            context.Handle.Fail("timeline.play requires parameter 'cutsceneId'.");
            yield break;
        }

        bool waitForComplete;
        if (!ScenarioActionParameterReader.TryGetBool(action, "waitForComplete", true, out waitForComplete, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        bool lockInput;
        if (!ScenarioActionParameterReader.TryGetBool(action, "lockInput", true, out lockInput, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        bool restoreCamera;
        if (!ScenarioActionParameterReader.TryGetBool(action, "restoreCamera", true, out restoreCamera, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        bool skipIfMissing;
        if (!ScenarioActionParameterReader.TryGetBool(action, "skipIfMissing", false, out skipIfMissing, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        IEnumerator routine = runner.PlayCutscene(
            cutsceneId.Trim(),
            waitForComplete,
            lockInput,
            restoreCamera,
            skipIfMissing,
            context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "ITimelineCutsceneRunner failed during timeline.play: " + cutsceneId.Trim());
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}