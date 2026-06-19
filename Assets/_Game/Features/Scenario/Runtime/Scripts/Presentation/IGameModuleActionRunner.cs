using System.Collections;

public interface IGameModuleActionRunner
{
    string CurrentModuleId { get; }

    IEnumerator SwitchTo(string moduleId, ActionExecutionContext context);

    IEnumerator Start(string moduleId, ActionExecutionContext context);
}

public interface IGameModuleStateStore
{
    string CurrentModuleId { get; }

    void SetCurrentModuleId(string moduleId);
}
