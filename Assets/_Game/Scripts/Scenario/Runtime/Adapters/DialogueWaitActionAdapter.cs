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

        try
        {
            // 대화창의 닫힘까지 기다려 다음 공격/메뉴가 남은 패널과 겹치지 않게 합니다.
            while ((!completed || runner.IsBusy) && !context.Handle.IsCancellationRequested)
            {
                if (!completed && !runner.IsBusy)
                {
                    context.Handle.Fail("Dialogue was interrupted before completion: " + dialogueId);
                    yield break;
                }
                yield return null;
            }
        }
        finally
        {
            // F8/씬 전환/시나리오 취소는 이 실행이 연 대화만 취소합니다.
            if (!completed && runner is ICancellableDialogueRunner cancellable)
                cancellable.Cancel();
        }
    }
}
