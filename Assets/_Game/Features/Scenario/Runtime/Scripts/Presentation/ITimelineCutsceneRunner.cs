using System.Collections;

public interface ITimelineCutsceneRunner
{
    IEnumerator PlayCutscene(
        string cutsceneId,
        bool waitForComplete,
        bool lockInput,
        bool restoreCamera,
        bool skipIfMissing,
        ActionExecutionContext context);
}