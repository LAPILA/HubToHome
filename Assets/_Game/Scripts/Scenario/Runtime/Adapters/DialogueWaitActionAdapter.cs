using System;
using System.Collections;

public sealed class DialogueWaitActionAdapter : IActionAdapter
{
    public const string Id = "dialogue.wait";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IDialogueRunner runner = context.GetService<IDialogueRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IDialogueRunner is missing for dialogue.wait.");
            yield break;
        }

        if (runner.IsBusy)
        {
            context.Handle.Fail("IDialogueRunner is already busy.");
            yield break;
        }

        string dialogueId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "id", out dialogueId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            context.Handle.Fail("dialogue.wait requires parameter 'id'.");
            yield break;
        }

        bool completed = false;
        try
        {
            runner.ShowAndWait(dialogueId.Trim(), () => completed = true);
        }
        catch (Exception exception)
        {
            context.Handle.Fail("IDialogueRunner failed to start dialogue.wait.", exception);
            yield break;
        }

        while (!completed && !context.Handle.IsCancellationRequested)
        {
            yield return null;
        }
    }
}
