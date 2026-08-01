using System;

public interface IDialogueRunner
{
    bool IsBusy { get; }

    void ShowAndWait(string dialogueId, Action onComplete);
}
public interface ICancellableDialogueRunner
{
    void Cancel();
}