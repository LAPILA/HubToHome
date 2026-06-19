using System.Collections;

public interface IActionAdapter
{
    string ActionId { get; }

    IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context);
}
