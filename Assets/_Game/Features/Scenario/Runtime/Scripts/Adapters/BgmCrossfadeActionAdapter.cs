using System;
using System.Collections;

public sealed class BgmCrossfadeActionAdapter : IActionAdapter
{
    public const string Id = "bgm.crossfade";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IAudioActionRunner runner = context.GetService<IAudioActionRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IAudioActionRunner is missing for bgm.crossfade.");
            yield break;
        }

        string clipId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "clip", out clipId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(clipId))
        {
            context.Handle.Fail("bgm.crossfade requires parameter 'clip'.");
            yield break;
        }

        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0f, out duration, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        duration = Math.Max(0f, duration);
        IEnumerator routine = runner.CrossfadeBgm(clipId.Trim(), duration, context.Handle);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "IAudioActionRunner failed during bgm.crossfade.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}
