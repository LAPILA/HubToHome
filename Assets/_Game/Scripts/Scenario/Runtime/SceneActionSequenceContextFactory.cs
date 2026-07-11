public static class SceneActionSequenceContextFactory
{
    public static ActionDirector CreateDirector()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new FlowWaitActionAdapter());
        registry.Register(new ScreenFadeActionAdapter());
        registry.Register(new CinematicStagePrepareActionAdapter());
        registry.Register(new CinematicShotPlayActionAdapter());
        registry.Register(new CinematicStageReleaseActionAdapter());
        registry.Register(new SequenceCallActionAdapter(registry));
        return new ActionDirector(registry);
    }

    public static ActionExecutionContext Create(
        ActionSequenceAsset sequence,
        ICinematicStageRunner cinematicStageRunner,
        IScreenTransitionRunner screenTransitionRunner = null,
        IActionClock clock = null,
        IActionSequenceResolver sequenceResolver = null)
    {
        var context = new ActionExecutionContext(new ActionExecutionHandle("scene_action_sequence"));
        context.ScenarioId = sequence != null ? sequence.SequenceId : string.Empty;
        context.PrimaryMode = "overworld";
        if (cinematicStageRunner != null)
        {
            context.SetService<ICinematicStageRunner>(cinematicStageRunner);
        }

        if (screenTransitionRunner != null)
        {
            context.SetService<IScreenTransitionRunner>(screenTransitionRunner);
        }

        if (clock != null)
        {
            context.SetService<IActionClock>(clock);
        }

        if (sequenceResolver != null)
        {
            context.SetService<IActionSequenceResolver>(sequenceResolver);
        }

        return context;
    }
}
