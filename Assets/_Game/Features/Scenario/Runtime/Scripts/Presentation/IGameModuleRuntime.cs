using System.Collections;

public interface IGameModuleRuntime
{
    string ModuleId { get; }

    IEnumerator Enter(GameModuleRuntimeContext context);

    IEnumerator Exit(GameModuleRuntimeContext context);

    IEnumerator Start(GameModuleRuntimeContext context);
}

public sealed class GameModuleRuntimeContext
{
    public GameModuleRuntimeContext(
        ActionExecutionContext actionContext,
        string previousModuleId,
        string targetModuleId)
    {
        ActionContext = actionContext;
        PreviousModuleId = Normalize(previousModuleId);
        TargetModuleId = Normalize(targetModuleId);
    }

    public ActionExecutionContext ActionContext { get; }
    public string PreviousModuleId { get; }
    public string TargetModuleId { get; }

    public ActionExecutionHandle Handle
    {
        get { return ActionContext != null ? ActionContext.Handle : null; }
    }

    public string CurrentModuleId
    {
        get { return ActionContext != null ? Normalize(ActionContext.ModuleId) : string.Empty; }
    }

    public IBattleSessionStateReader BattleSession
    {
        get { return GetService<IBattleSessionStateReader>(); }
    }

    public IBattleParticipantCommandRunner ParticipantCommands
    {
        get { return GetService<IBattleParticipantCommandRunner>(); }
    }

    public IBattleSessionFlagStore BattleFlags
    {
        get { return GetService<IBattleSessionFlagStore>(); }
    }

    public IGameModuleEventSink ModuleEvents
    {
        get { return GetService<IGameModuleEventSink>(); }
    }

    public TService GetService<TService>()
        where TService : class
    {
        return ActionContext != null ? ActionContext.GetService<TService>() : null;
    }

    public bool TryGetService<TService>(out TService service)
        where TService : class
    {
        if (ActionContext != null)
        {
            return ActionContext.TryGetService(out service);
        }

        service = null;
        return false;
    }

    private static string Normalize(string moduleId)
    {
        return string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
    }
}
