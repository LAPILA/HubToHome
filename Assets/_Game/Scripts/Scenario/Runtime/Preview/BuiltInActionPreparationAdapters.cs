using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class BuiltInActionPreparationAdapters
{
    public static void RegisterInto(ActionPreparationRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        registry.Register(new SkipActionPreparationAdapter(FlowWaitActionAdapter.Id));
        registry.Register(new SkipActionPreparationAdapter(DialogueWaitActionAdapter.Id));
        registry.Register(new ParallelActionPreparationAdapter());
        registry.Register(new SequenceCallPreparationAdapter());
        registry.Register(new ScreenFadePreparationAdapter());
        registry.Register(new BgmCrossfadePreparationAdapter());
        registry.Register(new ModuleSwitchPreparationAdapter());
        registry.Register(new ModuleStartPreparationAdapter());
        registry.Register(new CinematicStagePreparePreparationAdapter());
        registry.Register(new CinematicShotFinalStatePreparationAdapter());
        registry.Register(new CinematicStageReleasePreparationAdapter());
    }
}

public sealed class SkipActionPreparationAdapter : IActionPreparationAdapter
{
    public SkipActionPreparationAdapter(string actionId)
    {
        ActionId = string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
    }

    public string ActionId { get; }
    public PreviewSideEffect SideEffects => PreviewSideEffect.None;

    public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
    {
        context.Skip();
        yield break;
    }
}

public sealed class ParallelActionPreparationAdapter : IActionPreparationAdapter
{
    public string ActionId => ActionDirector.ParallelActionId;
    public PreviewSideEffect SideEffects => PreviewSideEffect.None;

    public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
    {
        if (!TryReadPolicy(action, out string policy, out string previewWinner, out string error))
        {
            context.Fail(error);
            yield break;
        }

        IList<ScenarioActionData> children = action?.Children;
        if (policy == "any" || policy == "race")
        {
            if (string.IsNullOrWhiteSpace(previewWinner))
            {
                context.Block("Parallel '" + policy
                    + "' preparation requires an explicit previewWinner Block ID.");
                yield break;
            }

            ScenarioActionData winner = null;
            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    ScenarioActionData child = children[i];
                    if (child != null
                        && string.Equals(
                            child.BlockId?.Trim(),
                            previewWinner,
                            StringComparison.Ordinal))
                    {
                        winner = child;
                        break;
                    }
                }
            }

            if (winner == null)
            {
                context.Block("Parallel previewWinner was not found among direct child blocks: "
                    + previewWinner);
                yield break;
            }

            children = new[] { winner };
        }

        IEnumerator routine = context.PrepareChildren(children);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    private static bool TryReadPolicy(
        ScenarioActionData action,
        out string policy,
        out string previewWinner,
        out string error)
    {
        policy = "all";
        previewWinner = string.Empty;
        error = string.Empty;
        try
        {
            JObject parameters = string.IsNullOrWhiteSpace(action?.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
            policy = parameters.Value<string>("policy")?.Trim().ToLowerInvariant() ?? "all";
            previewWinner = parameters.Value<string>("previewWinner")?.Trim() ?? string.Empty;
        }
        catch (Exception exception)
        {
            error = "Parallel preparation parameters must be an object: " + exception.Message;
            return false;
        }

        if (policy != "all" && policy != "any" && policy != "race")
        {
            error = "Unknown parallel preparation policy: " + policy;
            return false;
        }

        return true;
    }
}

public sealed class SequenceCallPreparationAdapter : IActionPreparationAdapter
{
    public string ActionId => SequenceCallActionAdapter.Id;
    public PreviewSideEffect SideEffects => PreviewSideEffect.None;

    public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
    {
        if (!TryReadCall(action, out string sequenceId, out JObject inputs, out string error))
        {
            context.Fail(error);
            yield break;
        }

        if (!context.ExecutionContext.TryGetService(out IActionSequenceResolver resolver))
        {
            context.Fail("Sequence preparation requires an IActionSequenceResolver.");
            yield break;
        }

        if (!resolver.TryResolveSequence(sequenceId, out ActionSequenceAsset sequence) || sequence == null)
        {
            context.Fail("Preparation sequence call target was not found: " + sequenceId);
            yield break;
        }

        var childHandle = new ActionExecutionHandle("preparation:sequence_call:" + sequenceId);
        ActionExecutionContext childContext = context.ExecutionContext.CreateChild(childHandle);
        if (!SequenceInputBinder.TryBindInputs(
                sequence.Contract != null ? sequence.Contract.Inputs : null,
                inputs,
                childContext,
                out error))
        {
            context.Fail("Preparation sequence call input binding failed: " + error);
            yield break;
        }

        IEnumerator routine = context.PrepareSequence(sequence, childContext);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        context.ExecutionContext.ModuleId = childContext.ModuleId;
    }

    private static bool TryReadCall(
        ScenarioActionData action,
        out string sequenceId,
        out JObject inputs,
        out string error)
    {
        sequenceId = string.Empty;
        inputs = new JObject();
        error = string.Empty;
        JObject parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(action?.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
        }
        catch (Exception exception)
        {
            error = "sequence.call preparation parameters must be an object: " + exception.Message;
            return false;
        }

        JToken sequenceToken = parameters["sequence"];
        if (sequenceToken == null || sequenceToken.Type != JTokenType.String)
        {
            error = "sequence.call preparation requires a string 'sequence' parameter.";
            return false;
        }

        sequenceId = sequenceToken.Value<string>()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(sequenceId))
        {
            error = "sequence.call preparation requires a non-empty sequence ID.";
            return false;
        }

        JToken inputsToken = parameters["inputs"];
        if (inputsToken == null || inputsToken.Type == JTokenType.Null)
        {
            return true;
        }

        inputs = inputsToken as JObject;
        if (inputs == null)
        {
            error = "sequence.call preparation 'inputs' must be an object.";
            return false;
        }

        return true;
    }
}

public sealed class ScreenFadePreparationAdapter
    : RuntimeActionPreparationAdapter<IScreenTransitionRunner>
{
    public ScreenFadePreparationAdapter()
        : base(
            ScreenFadeActionAdapter.Id,
            PreviewSideEffect.Presentation | PreviewSideEffect.SceneState,
            new ScreenFadeActionAdapter(),
            true)
    {
    }
}

public sealed class BgmCrossfadePreparationAdapter
    : RuntimeActionPreparationAdapter<IAudioActionRunner>
{
    public BgmCrossfadePreparationAdapter()
        : base(
            BgmCrossfadeActionAdapter.Id,
            PreviewSideEffect.Presentation,
            new BgmCrossfadeActionAdapter(),
            true)
    {
    }
}

public sealed class ModuleSwitchPreparationAdapter
    : RuntimeActionPreparationAdapter<IGameModuleActionRunner>
{
    public ModuleSwitchPreparationAdapter()
        : base(
            ModuleSwitchActionAdapter.Id,
            PreviewSideEffect.GameplayState,
            new ModuleSwitchActionAdapter(),
            false)
    {
    }
}

public sealed class ModuleStartPreparationAdapter
    : RuntimeActionPreparationAdapter<IGameModuleActionRunner>
{
    public ModuleStartPreparationAdapter()
        : base(
            ModuleStartActionAdapter.Id,
            PreviewSideEffect.GameplayState,
            new ModuleStartActionAdapter(),
            false)
    {
    }
}

public sealed class CinematicStagePreparePreparationAdapter
    : RuntimeActionPreparationAdapter<ICinematicStageRunner>
{
    public CinematicStagePreparePreparationAdapter()
        : base(
            CinematicStagePrepareActionAdapter.Id,
            PreviewSideEffect.Presentation | PreviewSideEffect.SceneState,
            new CinematicStagePrepareActionAdapter(),
            false)
    {
    }
}

public sealed class CinematicStageReleasePreparationAdapter
    : RuntimeActionPreparationAdapter<ICinematicStageRunner>
{
    public CinematicStageReleasePreparationAdapter()
        : base(
            CinematicStageReleaseActionAdapter.Id,
            PreviewSideEffect.Presentation | PreviewSideEffect.SceneState,
            new CinematicStageReleaseActionAdapter(),
            false)
    {
    }
}

public sealed class CinematicShotFinalStatePreparationAdapter : IActionPreparationAdapter
{
    public string ActionId => CinematicShotPlayActionAdapter.Id;
    public PreviewSideEffect SideEffects => PreviewSideEffect.Presentation | PreviewSideEffect.SceneState;

    public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
    {
        ICinematicStageRunner stageRunner = context.ExecutionContext.GetService<ICinematicStageRunner>();
        if (stageRunner == null)
        {
            context.Fail("ICinematicStageRunner is missing for cinematic shot preparation.");
            yield break;
        }

        if (!context.TryTrackState("cinematic.stage", stageRunner))
        {
            yield break;
        }

        if (!(stageRunner is ICinematicStagePreparationRunner preparationRunner))
        {
            context.Fail("Cinematic Stage does not expose final-state preparation.");
            yield break;
        }

        if (!CinematicStagePrepareActionAdapter.TryReadStageAndShot(
                action,
                context.ExecutionContext,
                out string stageId,
                out string shotId,
                out string error))
        {
            context.Fail(string.IsNullOrWhiteSpace(error)
                ? context.ExecutionContext.Handle.Result.Message
                : error);
            yield break;
        }

        IEnumerator routine = preparationRunner.ApplyShotFinalState(
            stageId,
            shotId,
            context.ExecutionContext);
        while (routine != null && routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public abstract class RuntimeActionPreparationAdapter<TService> : IActionPreparationAdapter
    where TService : class
{
    private readonly IActionAdapter _runtimeAdapter;
    private readonly bool _zeroDuration;

    protected RuntimeActionPreparationAdapter(
        string actionId,
        PreviewSideEffect sideEffects,
        IActionAdapter runtimeAdapter,
        bool zeroDuration)
    {
        ActionId = actionId;
        SideEffects = sideEffects;
        _runtimeAdapter = runtimeAdapter;
        _zeroDuration = zeroDuration;
    }

    public string ActionId { get; }
    public PreviewSideEffect SideEffects { get; }

    public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
    {
        TService service = context.ExecutionContext.GetService<TService>();
        if (service == null)
        {
            context.Fail(typeof(TService).Name + " is missing for " + ActionId + " preparation.");
            yield break;
        }

        if (!context.TryTrackState(ActionId + ":" + typeof(TService).Name, service))
        {
            yield break;
        }

        ScenarioActionData preparedAction = action;
        if (_zeroDuration)
        {
            if (!TryCloneWithZeroDuration(action, out preparedAction, out string cloneError))
            {
                context.Fail(cloneError);
                yield break;
            }
        }

        IEnumerator routine = _runtimeAdapter.Execute(preparedAction, context.ExecutionContext);
        while (routine != null && routine.MoveNext())
        {
            yield return routine.Current;
        }

        if (context.ExecutionContext.Handle.Status == ActionExecutionStatus.Failed)
        {
            context.Fail(context.ExecutionContext.Handle.Result.Message);
        }
    }

    private static bool TryCloneWithZeroDuration(
        ScenarioActionData action,
        out ScenarioActionData clone,
        out string error)
    {
        clone = ScenarioBlockIdentity.ClonePreservingIds(action);
        error = string.Empty;
        try
        {
            JObject parameters = string.IsNullOrWhiteSpace(clone.ParametersJson)
                ? new JObject()
                : JObject.Parse(clone.ParametersJson);
            parameters["duration"] = 0f;
            clone.ParametersJson = parameters.ToString(Newtonsoft.Json.Formatting.None);
            return true;
        }
        catch (Exception exception)
        {
            error = "Preparation parameters must be an object: " + exception.Message;
            return false;
        }
    }
}
