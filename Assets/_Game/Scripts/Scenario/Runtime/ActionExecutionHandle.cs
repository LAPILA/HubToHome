using System;

public sealed class ActionExecutionHandle
{
    public event Action<ActionExecutionHandle> Changed;
    public event Action<ActionExecutionHandle> CancellationRequested;

    public ActionExecutionHandle(string executionId = "")
    {
        ExecutionId = executionId;
        Result = ActionExecutionResult.NotStarted();
    }

    public string ExecutionId { get; }
    public ActionExecutionStatus Status { get; private set; } = ActionExecutionStatus.NotStarted;
    public ActionExecutionResult Result { get; private set; }
    public bool IsCancellationRequested { get; private set; }

    public bool IsDone
    {
        get
        {
            return Status == ActionExecutionStatus.Succeeded
                || Status == ActionExecutionStatus.Failed
                || Status == ActionExecutionStatus.Canceled;
        }
    }

    public void Cancel(string message = "Action execution was canceled.")
    {
        if (IsCancellationRequested)
        {
            return;
        }

        IsCancellationRequested = true;
        CancellationRequested?.Invoke(this);
        if (!IsDone)
        {
            MarkCanceled(message);
        }
    }

    public void Fail(string message, Exception exception = null)
    {
        MarkFailed(message, exception);
    }

    internal void MarkRunning()
    {
        if (IsDone)
        {
            return;
        }

        Status = ActionExecutionStatus.Running;
        Result = ActionExecutionResult.Running();
        Changed?.Invoke(this);
    }

    internal void MarkSucceeded(string message = "")
    {
        if (IsDone)
        {
            return;
        }

        Status = ActionExecutionStatus.Succeeded;
        Result = ActionExecutionResult.Succeeded(message);
        Changed?.Invoke(this);
    }

    internal void MarkFailed(string message, Exception exception = null)
    {
        if (IsDone)
        {
            return;
        }

        Status = ActionExecutionStatus.Failed;
        Result = ActionExecutionResult.Failed(message, exception);
        Changed?.Invoke(this);
    }

    internal void MarkCanceled(string message = "")
    {
        if (Status == ActionExecutionStatus.Failed || Status == ActionExecutionStatus.Succeeded)
        {
            return;
        }

        Status = ActionExecutionStatus.Canceled;
        Result = ActionExecutionResult.Canceled(message);
        Changed?.Invoke(this);
    }
}
