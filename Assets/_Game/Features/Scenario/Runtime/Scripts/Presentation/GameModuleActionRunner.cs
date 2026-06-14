using System.Collections;

public sealed class GameModuleActionRunner : IGameModuleActionRunner
{
    private readonly GameModuleRegistry _registry;

    public GameModuleActionRunner(GameModuleRegistry registry, string currentModuleId = "")
    {
        _registry = registry ?? new GameModuleRegistry();
        CurrentModuleId = Normalize(currentModuleId);
    }

    public string CurrentModuleId { get; private set; }

    public IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
    {
        string targetModuleId = Normalize(moduleId);
        if (string.IsNullOrEmpty(targetModuleId))
        {
            Fail(context, "module.switch requires a target Game Module id.");
            yield break;
        }

        IGameModuleRuntime target;
        if (!_registry.TryGet(targetModuleId, out target))
        {
            Fail(context, "Game Module is not registered: " + targetModuleId);
            yield break;
        }

        string activeModuleId = ResolveActiveModuleId(context);
        if (activeModuleId == targetModuleId)
        {
            SetCurrentModule(targetModuleId, context);
            yield break;
        }

        IGameModuleRuntime current;
        if (!string.IsNullOrEmpty(activeModuleId) && _registry.TryGet(activeModuleId, out current))
        {
            IEnumerator exitRoutine = ScenarioAdapterRoutineRunner.Run(
                current.Exit(context),
                context,
                "Game Module failed while exiting: " + activeModuleId);
            while (exitRoutine.MoveNext())
            {
                yield return exitRoutine.Current;
            }

            if (IsStopped(context))
            {
                yield break;
            }
        }

        IEnumerator enterRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Enter(context),
            context,
            "Game Module failed while entering: " + targetModuleId);
        while (enterRoutine.MoveNext())
        {
            yield return enterRoutine.Current;
        }

        if (!IsStopped(context))
        {
            SetCurrentModule(targetModuleId, context);
        }
    }

    public IEnumerator Start(string moduleId, ActionExecutionContext context)
    {
        string targetModuleId = Normalize(moduleId);
        if (string.IsNullOrEmpty(targetModuleId))
        {
            Fail(context, "module.start requires a Game Module id.");
            yield break;
        }

        IGameModuleRuntime target;
        if (!_registry.TryGet(targetModuleId, out target))
        {
            Fail(context, "Game Module is not registered: " + targetModuleId);
            yield break;
        }

        IEnumerator startRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Start(context),
            context,
            "Game Module failed while starting: " + targetModuleId);
        while (startRoutine.MoveNext())
        {
            yield return startRoutine.Current;
        }

        if (!IsStopped(context))
        {
            SetCurrentModule(targetModuleId, context);
        }
    }

    private string ResolveActiveModuleId(ActionExecutionContext context)
    {
        if (!string.IsNullOrEmpty(CurrentModuleId))
        {
            return CurrentModuleId;
        }

        return context != null ? Normalize(context.ModuleId) : string.Empty;
    }

    private void SetCurrentModule(string moduleId, ActionExecutionContext context)
    {
        CurrentModuleId = Normalize(moduleId);
        if (context != null)
        {
            context.ModuleId = CurrentModuleId;
        }
    }

    private static bool IsStopped(ActionExecutionContext context)
    {
        return context != null
            && (context.Handle.IsDone || context.Handle.IsCancellationRequested);
    }

    private static void Fail(ActionExecutionContext context, string message)
    {
        if (context != null)
        {
            context.Handle.Fail(message);
        }
    }

    private static string Normalize(string moduleId)
    {
        return string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
    }
}
