using System;
using System.Collections;
using System.Collections.Generic;

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
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0 && (handle == null || (!handle.IsDone && !handle.IsCancellationRequested)))
            {
                IEnumerator current = stack.Peek();
                bool moved;
                try
                {
                    moved = current.MoveNext();
                }
                catch (Exception exception)
                {
                    handle?.Fail(failureMessage, exception);
                    yield break;
                }

                if (!moved)
                {
                    Dispose(stack.Pop(), handle, failureMessage);
                    continue;
                }

                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
                else
                    yield return current.Current;
            }
        }
        finally
        {
            while (stack.Count > 0)
                Dispose(stack.Pop(), handle, failureMessage);
        }
    }

    private static void Dispose(IEnumerator routine, ActionExecutionHandle handle, string failureMessage)
    {
        try
        {
            (routine as IDisposable)?.Dispose();
        }
        catch (Exception exception)
        {
            handle?.Fail(failureMessage, exception);
        }
    }
}
