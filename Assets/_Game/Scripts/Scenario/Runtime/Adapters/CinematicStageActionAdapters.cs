using System.Collections;

public sealed class CinematicStagePrepareActionAdapter : IActionAdapter
{
    public const string Id = "cinematic.stage.prepare";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        ICinematicStageRunner runner = context.GetService<ICinematicStageRunner>();
        if (runner == null)
        {
            context.Handle.Fail("ICinematicStageRunner is missing for cinematic.stage.prepare.");
            yield break;
        }

        string stageId;
        string shotId;
        string error;
        if (!TryReadStageAndShot(action, context, out stageId, out shotId, out error))
        {
            yield break;
        }

        IEnumerator routine = runner.PrepareStage(stageId, shotId, context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "ICinematicStageRunner failed during cinematic.stage.prepare.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }

    internal static bool TryReadStageAndShot(
        ScenarioActionData action,
        ActionExecutionContext context,
        out string stageId,
        out string shotId,
        out string error)
    {
        stageId = string.Empty;
        shotId = string.Empty;
        error = string.Empty;
        if (!ScenarioActionParameterReader.TryGetString(action, "stage", out stageId, out error)
            || string.IsNullOrWhiteSpace(stageId))
        {
            context.Handle.Fail(string.IsNullOrWhiteSpace(error)
                ? "cinematic stage action requires parameter 'stage'."
                : error);
            return false;
        }

        if (!ScenarioActionParameterReader.TryGetString(action, "shot", out shotId, out error)
            || string.IsNullOrWhiteSpace(shotId))
        {
            context.Handle.Fail(string.IsNullOrWhiteSpace(error)
                ? "cinematic stage action requires parameter 'shot'."
                : error);
            return false;
        }

        stageId = stageId.Trim();
        shotId = shotId.Trim();
        return true;
    }
}

public sealed class CinematicShotPlayActionAdapter : IActionAdapter
{
    public const string Id = "cinematic.shot.play";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        ICinematicStageRunner runner = context.GetService<ICinematicStageRunner>();
        if (runner == null)
        {
            context.Handle.Fail("ICinematicStageRunner is missing for cinematic.shot.play.");
            yield break;
        }

        string stageId;
        string shotId;
        string error;
        if (!CinematicStagePrepareActionAdapter.TryReadStageAndShot(action, context, out stageId, out shotId, out error))
        {
            yield break;
        }

        IEnumerator routine = runner.PlayShot(stageId, shotId, context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "ICinematicStageRunner failed during cinematic.shot.play.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}

public sealed class CinematicStageReleaseActionAdapter : IActionAdapter
{
    public const string Id = "cinematic.stage.release";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        ICinematicStageRunner runner = context.GetService<ICinematicStageRunner>();
        if (runner == null)
        {
            context.Handle.Fail("ICinematicStageRunner is missing for cinematic.stage.release.");
            yield break;
        }

        string stageId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "stage", out stageId, out error)
            || string.IsNullOrWhiteSpace(stageId))
        {
            context.Handle.Fail(string.IsNullOrWhiteSpace(error)
                ? "cinematic.stage.release requires parameter 'stage'."
                : error);
            yield break;
        }

        IEnumerator routine = runner.ReleaseStage(stageId.Trim(), context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "ICinematicStageRunner failed during cinematic.stage.release.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}
