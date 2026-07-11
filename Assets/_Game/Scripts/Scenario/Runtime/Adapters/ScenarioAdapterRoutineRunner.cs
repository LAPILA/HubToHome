using System;
using System.Collections;

public static class ScenarioAdapterRoutineRunner
{
    public static IEnumerator Run(
        IEnumerator routine,
        ActionExecutionContext context,
        string failureMessage)
    {
        if (routine == null)
        {
            yield break;
        }

        ActionExecutionHandle handle = context != null ? context.Handle : null;
        while (handle == null || (!handle.IsDone && !handle.IsCancellationRequested))
        {
            bool moved;
            try
            {
                moved = routine.MoveNext();
            }
            catch (Exception exception)
            {
                if (handle != null)
                {
                    handle.Fail(failureMessage, exception);
                }

                yield break;
            }

            if (!moved)
            {
                yield break;
            }

            yield return routine.Current;
        }
    }
}
