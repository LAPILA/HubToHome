using System.Collections;

public sealed class GameModuleActionRunner : IGameModuleActionRunner
{
    private readonly GameModuleRegistry _registry;
    private readonly IGameModuleStateStore _stateStore;
    private string _currentModuleId;

    public GameModuleActionRunner(
        GameModuleRegistry registry,
        string currentModuleId = "",
        IGameModuleStateStore stateStore = null)
    {
        _registry = registry ?? new GameModuleRegistry();
        _stateStore = stateStore;
        _currentModuleId = Normalize(currentModuleId);
        if (_stateStore != null && string.IsNullOrEmpty(_stateStore.CurrentModuleId))
        {
            _stateStore.SetCurrentModuleId(_currentModuleId);
        }
    }

    public string CurrentModuleId
    {
        get
        {
            string storedModuleId = _stateStore != null ? Normalize(_stateStore.CurrentModuleId) : string.Empty;
            return !string.IsNullOrEmpty(storedModuleId) ? storedModuleId : _currentModuleId;
        }
    }

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
            GameModuleRuntimeContext exitContext = CreateModuleContext(context, activeModuleId, targetModuleId);
            IEnumerator exitRoutine = ScenarioAdapterRoutineRunner.Run(
                current.Exit(exitContext),
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

        GameModuleRuntimeContext enterContext = CreateModuleContext(context, activeModuleId, targetModuleId);
        IEnumerator enterRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Enter(enterContext),
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

        string activeModuleId = ResolveActiveModuleId(context);
        GameModuleRuntimeContext startContext = CreateModuleContext(context, activeModuleId, targetModuleId);
        IEnumerator startRoutine = ScenarioAdapterRoutineRunner.Run(
            target.Start(startContext),
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

    private static GameModuleRuntimeContext CreateModuleContext(
        ActionExecutionContext actionContext,
        string previousModuleId,
        string targetModuleId)
    {
        return new GameModuleRuntimeContext(actionContext, previousModuleId, targetModuleId);
    }

    private void SetCurrentModule(string moduleId, ActionExecutionContext context)
    {
        _currentModuleId = Normalize(moduleId);
        if (_stateStore != null)
        {
            _stateStore.SetCurrentModuleId(_currentModuleId);
        }

        if (context != null)
        {
            context.ModuleId = _currentModuleId;
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

public sealed class BattleTurnQteGameModuleRuntime : IGameModuleRuntime
{
    public const string Id = "turn_qte";

    public string ModuleId
    {
        get { return Id; }
    }

    public IEnumerator Enter(GameModuleRuntimeContext context)
    {
        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }

    public IEnumerator Exit(GameModuleRuntimeContext context)
    {
        QTEManager.Instance?.ForceStop();
        BattleUIController.Instance?.SuspendBattleModuleInput();
        yield break;
    }

    public IEnumerator Start(GameModuleRuntimeContext context)
    {
        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }
}
