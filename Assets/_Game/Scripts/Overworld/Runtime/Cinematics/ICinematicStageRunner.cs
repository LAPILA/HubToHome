using System.Collections;

public interface ICinematicStageRunner
{
    IEnumerator PrepareStage(string stageId, string shotId, ActionExecutionContext context);

    IEnumerator PlayShot(string stageId, string shotId, ActionExecutionContext context);

    IEnumerator ReleaseStage(string stageId, ActionExecutionContext context);
}
