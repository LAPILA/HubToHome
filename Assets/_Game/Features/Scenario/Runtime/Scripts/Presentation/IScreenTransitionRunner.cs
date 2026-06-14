using System.Collections;

public interface IScreenTransitionRunner
{
    IEnumerator Fade(string mode, string color, float duration, ActionExecutionHandle handle);
}
