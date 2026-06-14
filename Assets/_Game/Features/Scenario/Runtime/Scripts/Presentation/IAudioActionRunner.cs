using System.Collections;

public interface IAudioActionRunner
{
    IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle);
}
