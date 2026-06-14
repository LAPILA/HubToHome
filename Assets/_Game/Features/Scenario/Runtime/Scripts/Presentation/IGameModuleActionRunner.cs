using System.Collections;

public interface IGameModuleActionRunner
{
    string CurrentModuleId { get; }

    IEnumerator SwitchTo(string moduleId, ActionExecutionContext context);

    IEnumerator Start(string moduleId, ActionExecutionContext context);
}
