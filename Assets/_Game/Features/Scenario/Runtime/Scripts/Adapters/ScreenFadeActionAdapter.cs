using System;
using System.Collections;

public sealed class ScreenFadeActionAdapter : IActionAdapter
{
    public const string Id = "screen.fade";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IScreenTransitionRunner runner = context.GetService<IScreenTransitionRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IScreenTransitionRunner is missing for screen.fade.");
            yield break;
        }

        string mode;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "mode", out mode, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            context.Handle.Fail("screen.fade requires parameter 'mode'.");
            yield break;
        }

        string color;
        if (!ScenarioActionParameterReader.TryGetString(action, "color", out color, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            color = "black";
        }

        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0f, out duration, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        duration = Math.Max(0f, duration);
        IEnumerator routine = runner.Fade(mode.Trim(), color.Trim(), duration, context.Handle);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "IScreenTransitionRunner failed during screen.fade.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}
