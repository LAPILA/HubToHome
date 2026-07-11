using System.Collections;

public sealed class ModuleSwitchActionAdapter : IActionAdapter
{
    public const string Id = "module.switch";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IGameModuleActionRunner runner = context.GetService<IGameModuleActionRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IGameModuleActionRunner is missing for module.switch.");
            yield break;
        }

        string moduleId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "to", out moduleId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            context.Handle.Fail("module.switch requires parameter 'to'.");
            yield break;
        }

        moduleId = moduleId.Trim();
        IEnumerator routine = runner.SwitchTo(moduleId, context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "IGameModuleActionRunner failed during module.switch.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }

        if (!context.Handle.IsDone && !context.Handle.IsCancellationRequested)
        {
            context.ModuleId = moduleId;
        }
    }
}
