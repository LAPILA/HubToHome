using System.Collections;

public interface IGameModuleRuntime
{
    string ModuleId { get; }

    IEnumerator Enter(ActionExecutionContext context);

    IEnumerator Exit(ActionExecutionContext context);

    IEnumerator Start(ActionExecutionContext context);
}
