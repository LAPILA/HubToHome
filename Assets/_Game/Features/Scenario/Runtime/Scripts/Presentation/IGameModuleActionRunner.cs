using System.Collections;

public interface IGameModuleActionRunner
{
    IEnumerator SwitchTo(string moduleId, ActionExecutionContext context);

    IEnumerator Start(string moduleId, ActionExecutionContext context);
}
