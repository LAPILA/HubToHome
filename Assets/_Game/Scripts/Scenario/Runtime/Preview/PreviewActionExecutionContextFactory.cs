public interface IPreviewExecutionContextMarker
{
}

public static class PreviewActionExecutionContextFactory
{
    public static ActionExecutionContext Create(ActionExecutionContext source = null)
    {
        ActionExecutionContext context = source != null
            ? source.CreateDetachedExecutionScope(new ActionExecutionHandle("safe-preview"))
            : new ActionExecutionContext(new ActionExecutionHandle("safe-preview"));

        context.SetService<IPreviewExecutionContextMarker>(PreviewExecutionContextMarker.Instance);
        context.SetService<IScreenTransitionRunner>(new PreviewScreenTransitionRunner());
        context.SetService<IAudioActionRunner>(new PreviewAudioActionRunner());
        context.SetService<IGameModuleActionRunner>(new PreviewGameModuleActionRunner(context));

        if (source != null
            && source.TryGetService(out IActionSequenceResolver sequenceResolver))
        {
            context.SetService(sequenceResolver);
        }

        if (source != null
            && source.TryGetService(out ICinematicStageRunner stageRunner))
        {
            context.SetService(stageRunner);
        }

        return context;
    }

    private sealed class PreviewExecutionContextMarker : IPreviewExecutionContextMarker
    {
        public static readonly PreviewExecutionContextMarker Instance =
            new PreviewExecutionContextMarker();
    }
}
