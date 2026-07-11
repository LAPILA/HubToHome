using System.Collections;
using UnityEngine;

public sealed class FlowWaitActionAdapter : IActionAdapter
{
    public const string Id = "flow.wait";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        float duration;
        string error;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0f, out duration, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
        {
            yield break;
        }

        IActionClock clock = context.GetService<IActionClock>() ?? UnityActionClock.Instance;
        float elapsed = 0f;
        while (elapsed < duration && !context.Handle.IsCancellationRequested)
        {
            elapsed += Mathf.Max(0f, clock.DeltaTime);
            yield return null;
        }
    }
}
