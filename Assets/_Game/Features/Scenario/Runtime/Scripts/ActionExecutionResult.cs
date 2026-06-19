using System;

public enum ActionExecutionStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public sealed class ActionExecutionResult
{
    public ActionExecutionStatus Status { get; }
    public string Message { get; }
    public Exception Exception { get; }

    private ActionExecutionResult(
        ActionExecutionStatus status,
        string message,
        Exception exception)
    {
        Status = status;
        Message = message;
        Exception = exception;
    }

    public static ActionExecutionResult NotStarted()
    {
        return new ActionExecutionResult(ActionExecutionStatus.NotStarted, string.Empty, null);
    }

    public static ActionExecutionResult Running()
    {
        return new ActionExecutionResult(ActionExecutionStatus.Running, string.Empty, null);
    }

    public static ActionExecutionResult Succeeded(string message = "")
    {
        return new ActionExecutionResult(ActionExecutionStatus.Succeeded, message, null);
    }

    public static ActionExecutionResult Failed(string message, Exception exception = null)
    {
        return new ActionExecutionResult(ActionExecutionStatus.Failed, message, exception);
    }

    public static ActionExecutionResult Canceled(string message = "")
    {
        return new ActionExecutionResult(ActionExecutionStatus.Canceled, message, null);
    }
}
