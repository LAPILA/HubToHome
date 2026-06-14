using System.Collections;

public sealed class ModuleStartActionAdapter : IActionAdapter
{
    public const string Id = "module.start";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IGameModuleActionRunner runner = context.GetService<IGameModuleActionRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IGameModuleActionRunner is missing for module.start.");
            yield break;
        }

        string moduleId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "module", out moduleId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            context.Handle.Fail("module.start requires parameter 'module'.");
            yield break;
        }

        moduleId = moduleId.Trim();
        IEnumerator routine = runner.Start(moduleId, context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "IGameModuleActionRunner failed during module.start.");
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
