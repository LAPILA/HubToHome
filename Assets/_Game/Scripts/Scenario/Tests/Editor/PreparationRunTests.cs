using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class PreparationRunTests
{
    [Test]
    public void PreparesOnlyBlocksBeforeSelectedBlock()
    {
        var prepared = new List<string>();
        ActionSequenceAsset sequence = Sequence(
            "prefix",
            Action("first", "test.state"),
            Action("selected", "test.state"),
            Action("after", "test.state"));
        PreparationRun run = Runner(
            Catalog(Entry("test.state", ActionPreparationPolicy.ApplyFinalState)),
            new RecordingAdapter("test.state", prepared));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(prepared, Is.EqualTo(new[] { "first" }));
        Destroy(sequence);
    }

    [Test]
    public void SkipPresentationNeedsNoPreparationAdapter()
    {
        ActionSequenceAsset sequence = Sequence(
            "skip",
            Action("dialogue", "test.presentation"),
            Action("selected", "test.selected"));
        PreparationRun run = Runner(Catalog(
            Entry("test.presentation", ActionPreparationPolicy.SkipPresentation),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(run.Result.Steps[0].Status, Is.EqualTo(PreparationStepStatus.Skipped));
        Destroy(sequence);
    }

    [Test]
    public void UnsupportedActionBlocksWithExactBlockAndRestores()
    {
        ActionSequenceAsset sequence = Sequence(
            "unsupported",
            Action("unsafe-block", "test.unsupported"),
            Action("selected", "test.selected"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(Catalog(
            Entry("test.unsupported", ActionPreparationPolicy.Unsupported),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));

        RunToCompletion(run.PrepareBefore(sequence, "selected", PreviewContext(), scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
        Assert.That(run.Result.Message, Does.Contain("unsafe-block"));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [TestCase(PreviewSideEffect.Save)]
    [TestCase(PreviewSideEffect.Reward)]
    [TestCase(PreviewSideEffect.SceneTransition)]
    public void SafePreviewBlocksIrreversibleSideEffects(PreviewSideEffect sideEffect)
    {
        var prepared = new List<string>();
        ActionSequenceAsset sequence = Sequence(
            "unsafe",
            Action("unsafe-block", "test.unsafe"),
            Action("selected", "test.selected"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(
            Catalog(
                Entry("test.unsafe", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            new RecordingAdapter("test.unsafe", prepared, sideEffect));

        RunToCompletion(run.PrepareBefore(sequence, "selected", PreviewContext(), scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
        Assert.That(run.Result.Message, Does.Contain("unsafe-block"));
        Assert.That(prepared, Is.Empty);
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void MissingPreparationAdapterBlocksInsteadOfRunningProductionAction()
    {
        ActionSequenceAsset sequence = Sequence(
            "missing-adapter",
            Action("missing", "test.missing"),
            Action("selected", "test.selected"));
        PreparationRun run = Runner(Catalog(
            Entry("test.missing", ActionPreparationPolicy.ApplyFinalState),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
        Assert.That(run.Result.Message, Does.Contain("test.missing"));
        Assert.That(run.Result.Message, Does.Contain("missing"));
        Destroy(sequence);
    }

    [Test]
    public void AdapterFailureRestoresPreparedState()
    {
        ActionSequenceAsset sequence = Sequence(
            "failure",
            Action("broken", "test.fail"),
            Action("selected", "test.selected"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(
            Catalog(
                Entry("test.fail", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            new FailingAdapter("test.fail", "final state failed"));

        RunToCompletion(run.PrepareBefore(sequence, "selected", PreviewContext(), scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Failed));
        Assert.That(run.Result.Message, Does.Contain("final state failed"));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void RequireInputWaitsAndContinuesAfterValueIsProvided()
    {
        ActionSequenceAsset sequence = Sequence(
            "input",
            Action("choice", "test.choice"),
            Action("selected", "test.selected"));
        ActionExecutionContext context = PreviewContext();
        PreparationRun run = Runner(Catalog(
            Entry("test.choice", ActionPreparationPolicy.RequireInput),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));
        IEnumerator routine = run.PrepareBefore(sequence, "selected", context, new TestPreviewScope());

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.RequiresInput));
        Assert.That(run.Result.PendingInput.BlockId, Is.EqualTo("choice"));
        Assert.That(run.TryProvideInput(new JValue("right")), Is.True);
        RunToCompletion(routine);

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(context.TryGetValue("preview.input.choice", out JToken value), Is.True);
        Assert.That(value.Value<string>(), Is.EqualTo("right"));
        Destroy(sequence);
    }

    [Test]
    public void RequireInputUsesDeclaredPreviewDefaultWithoutWaiting()
    {
        ActionSequenceAsset sequence = Sequence(
            "input-default",
            Action("choice", "test.choice", "{\"previewDefault\":\"left\"}"),
            Action("selected", "test.selected"));
        ActionExecutionContext context = PreviewContext();
        PreparationRun run = Runner(Catalog(
            Entry("test.choice", ActionPreparationPolicy.RequireInput),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));

        RunToCompletion(run.PrepareBefore(sequence, "selected", context, new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(context.TryGetValue("preview.input.choice", out JToken value), Is.True);
        Assert.That(value.Value<string>(), Is.EqualTo("left"));
        Assert.That(run.Result.Steps[0].Status, Is.EqualTo(PreparationStepStatus.InputResolved));
        Destroy(sequence);
    }

    [Test]
    public void DisabledBlockIsReportedAndNotPrepared()
    {
        var prepared = new List<string>();
        ScenarioActionData disabled = Action("disabled", "test.state");
        disabled.Disabled = true;
        ActionSequenceAsset sequence = Sequence(
            "disabled",
            disabled,
            Action("selected", "test.selected"));
        PreparationRun run = Runner(
            Catalog(
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            new RecordingAdapter("test.state", prepared));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(prepared, Is.Empty);
        Assert.That(run.Result.Steps[0].Status, Is.EqualTo(PreparationStepStatus.Skipped));
        Destroy(sequence);
    }

    [Test]
    public void ParallelPreparationAppliesEveryChildFinalState()
    {
        var prepared = new List<string>();
        ScenarioActionData parallel = Action("parallel", ActionDirector.ParallelActionId);
        parallel.Children.Add(Action("left", "test.state"));
        parallel.Children.Add(Action("right", "test.state"));
        ActionSequenceAsset sequence = Sequence(
            "parallel",
            parallel,
            Action("selected", "test.selected"));
        ActionPreparationRegistry registry = ActionPreparationRegistry.CreateDefault();
        registry.Register(new RecordingAdapter("test.state", prepared));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(ActionDirector.ParallelActionId, ActionPreparationPolicy.ExecuteIsolated),
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            registry);

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(prepared, Is.EqualTo(new[] { "left", "right" }));
        Destroy(sequence);
    }

    [Test]
    public void ParallelAnyRequiresExplicitPreviewWinner()
    {
        ScenarioActionData parallel = Action(
            "parallel",
            ActionDirector.ParallelActionId,
            "{\"policy\":\"any\"}");
        parallel.Children.Add(Action("left", "test.state"));
        parallel.Children.Add(Action("right", "test.state"));
        ActionSequenceAsset sequence = Sequence(
            "parallel-any",
            parallel,
            Action("selected", "test.selected"));
        ActionPreparationRegistry registry = ActionPreparationRegistry.CreateDefault();
        registry.Register(new RecordingAdapter("test.state", new List<string>()));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(ActionDirector.ParallelActionId, ActionPreparationPolicy.ExecuteIsolated),
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            registry);

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
        Assert.That(run.Result.Message, Does.Contain("previewWinner"));
        Destroy(sequence);
    }

    [Test]
    public void ParallelAnyPreparesOnlyExplicitPreviewWinner()
    {
        var prepared = new List<string>();
        ScenarioActionData parallel = Action(
            "parallel",
            ActionDirector.ParallelActionId,
            "{\"policy\":\"any\",\"previewWinner\":\"right\"}");
        parallel.Children.Add(Action("left", "test.state"));
        parallel.Children.Add(Action("right", "test.state"));
        ActionSequenceAsset sequence = Sequence(
            "parallel-any-winner",
            parallel,
            Action("selected", "test.selected"));
        ActionPreparationRegistry registry = ActionPreparationRegistry.CreateDefault();
        registry.Register(new RecordingAdapter("test.state", prepared));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(ActionDirector.ParallelActionId, ActionPreparationPolicy.ExecuteIsolated),
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            registry);

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(prepared, Is.EqualTo(new[] { "right" }));
        Destroy(sequence);
    }

    [Test]
    public void SequenceCallPreparationResolvesAndPreparesNestedSequence()
    {
        var prepared = new List<string>();
        ActionSequenceAsset child = Sequence("child", Action("child-state", "test.state"));
        ActionSequenceAsset root = Sequence(
            "root",
            Action("call", SequenceCallActionAdapter.Id, "{\"sequence\":\"child\",\"inputs\":{}}"),
            Action("selected", "test.selected"));
        ActionPreparationRegistry registry = ActionPreparationRegistry.CreateDefault();
        registry.Register(new RecordingAdapter("test.state", prepared));
        var sourceContext = new ActionExecutionContext();
        sourceContext.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { child }));
        ActionExecutionContext context = PreviewContext(sourceContext);
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(SequenceCallActionAdapter.Id, ActionPreparationPolicy.ExecuteIsolated),
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            registry);

        RunToCompletion(run.PrepareBefore(root, "selected", context, new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(prepared, Is.EqualTo(new[] { "child-state" }));
        Destroy(root, child);
    }

    [Test]
    public void SequenceCallCycleFailsAndRestores()
    {
        ActionSequenceAsset cycle = Sequence(
            "cycle",
            Action("self", SequenceCallActionAdapter.Id, "{\"sequence\":\"cycle\",\"inputs\":{}}"));
        ActionSequenceAsset root = Sequence(
            "root-cycle",
            Action("call", SequenceCallActionAdapter.Id, "{\"sequence\":\"cycle\",\"inputs\":{}}"),
            Action("selected", "test.selected"));
        var sourceContext = new ActionExecutionContext();
        sourceContext.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { cycle }));
        ActionExecutionContext context = PreviewContext(sourceContext);
        var scope = new TestPreviewScope();
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(SequenceCallActionAdapter.Id, ActionPreparationPolicy.ExecuteIsolated),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            ActionPreparationRegistry.CreateDefault());

        RunToCompletion(run.PrepareBefore(root, "selected", context, scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Failed));
        Assert.That(run.Result.Message, Does.Contain("cycle"));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(root, cycle);
    }

    [Test]
    public void NormalDialogueAutoCompletesDuringPreparation()
    {
        ActionSequenceAsset sequence = Sequence(
            "dialogue",
            Action("line", DialogueWaitActionAdapter.Id, "{\"id\":\"intro.line\"}"),
            Action("selected", "test.selected"));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(DialogueWaitActionAdapter.Id, ActionPreparationPolicy.RequireInput),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            ActionPreparationRegistry.CreateDefault());

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(run.Result.PendingInput, Is.Null);
        Assert.That(run.Result.Steps[0].Status, Is.EqualTo(PreparationStepStatus.Skipped));
        Destroy(sequence);
    }

    [Test]
    public void FlowWaitPreparationConsumesNoFrames()
    {
        ActionSequenceAsset sequence = Sequence(
            "zero-wait",
            Action("wait", FlowWaitActionAdapter.Id, "{\"duration\":10}"),
            Action("selected", "test.selected"));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(FlowWaitActionAdapter.Id, ActionPreparationPolicy.SkipPresentation),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            ActionPreparationRegistry.CreateDefault());
        IEnumerator routine = run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope());
        int yieldedFrames = 0;

        while (routine.MoveNext())
        {
            yieldedFrames++;
        }

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
        Assert.That(yieldedFrames, Is.EqualTo(0));
        Destroy(sequence);
    }

    [Test]
    public void PreviewFactoryReplacesProductionPresentationAndModuleServices()
    {
        var productionScreen = new ThrowingScreenRunner();
        var productionAudio = new ThrowingAudioRunner();
        var productionModule = new ThrowingModuleRunner();
        var source = new ActionExecutionContext();
        source.SetService<IScreenTransitionRunner>(productionScreen);
        source.SetService<IAudioActionRunner>(productionAudio);
        source.SetService<IGameModuleActionRunner>(productionModule);
        ActionExecutionContext context = PreviewContext(source);
        ActionSequenceAsset sequence = Sequence(
            "isolated-services",
            Action("fade", ScreenFadeActionAdapter.Id,
                "{\"mode\":\"out\",\"color\":\"white\",\"duration\":3}"),
            Action("bgm", BgmCrossfadeActionAdapter.Id,
                "{\"clip\":\"boss.phase2\",\"duration\":3}"),
            Action("switch", ModuleSwitchActionAdapter.Id,
                "{\"to\":\"aim_shooter\"}"),
            Action("start", ModuleStartActionAdapter.Id,
                "{\"module\":\"aim_shooter\"}"),
            Action("selected", FlowWaitActionAdapter.Id, "{\"duration\":0}"));
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(ScreenFadeActionAdapter.Id, ActionPreparationPolicy.ApplyFinalState),
                Entry(BgmCrossfadeActionAdapter.Id, ActionPreparationPolicy.ApplyFinalState),
                Entry(ModuleSwitchActionAdapter.Id, ActionPreparationPolicy.ApplyFinalState),
                Entry(ModuleStartActionAdapter.Id, ActionPreparationPolicy.ApplyFinalState),
                Entry(FlowWaitActionAdapter.Id, ActionPreparationPolicy.SkipPresentation)),
            ActionPreparationRegistry.CreateDefault());

        using (var scope = new EditorPreviewStateScope())
        {
            RunToCompletion(run.PrepareBefore(sequence, "selected", context, scope));

            Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
            Assert.That(context.GetService<IScreenTransitionRunner>(), Is.TypeOf<PreviewScreenTransitionRunner>());
            Assert.That(context.GetService<IAudioActionRunner>(), Is.TypeOf<PreviewAudioActionRunner>());
            Assert.That(context.GetService<IGameModuleActionRunner>(), Is.TypeOf<PreviewGameModuleActionRunner>());
            Assert.That(((PreviewScreenTransitionRunner)context.GetService<IScreenTransitionRunner>()).Mode,
                Is.EqualTo("out"));
            Assert.That(((PreviewAudioActionRunner)context.GetService<IAudioActionRunner>()).CurrentBgmId,
                Is.EqualTo("boss.phase2"));
            PreviewGameModuleActionRunner module =
                (PreviewGameModuleActionRunner)context.GetService<IGameModuleActionRunner>();
            Assert.That(module.EnteredModuleId, Is.EqualTo("aim_shooter"));
            Assert.That(module.StartedModuleId, Is.EqualTo("aim_shooter"));
        }

        Assert.That(productionScreen.CallCount, Is.EqualTo(0));
        Assert.That(productionAudio.CallCount, Is.EqualTo(0));
        Assert.That(productionModule.CallCount, Is.EqualTo(0));
        Assert.That(context.ModuleId, Is.Empty);
        Destroy(sequence);
    }

    [Test]
    public void PreviewScreenServiceKeepsProductionValidationSemantics()
    {
        ActionSequenceAsset sequence = Sequence(
            "invalid-fade",
            Action("fade", ScreenFadeActionAdapter.Id,
                "{\"mode\":\"sideways\",\"color\":\"black\",\"duration\":2}"),
            Action("selected", FlowWaitActionAdapter.Id, "{\"duration\":0}"));
        var scope = new TestPreviewScope();
        PreparationRun run = new PreparationRun(
            Catalog(
                Entry(ScreenFadeActionAdapter.Id, ActionPreparationPolicy.ApplyFinalState),
                Entry(FlowWaitActionAdapter.Id, ActionPreparationPolicy.SkipPresentation)),
            ActionPreparationRegistry.CreateDefault());

        RunToCompletion(run.PrepareBefore(sequence, "selected", PreviewContext(), scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Failed));
        Assert.That(run.Result.Message, Does.Contain("Unsupported screen.fade mode"));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void BoundValuesAreResolvedBeforePreparationAdapterRuns()
    {
        var adapter = new RecordingAdapter("test.bound", new List<string>());
        ActionSequenceAsset sequence = Sequence(
            "binding",
            Action("bound", "test.bound", "{\"actor\":{\"$bind\":\"input.actor\"}}"),
            Action("selected", "test.selected"));
        var sourceContext = new ActionExecutionContext();
        sourceContext.SetValue("input.actor", new JValue("hero"));
        ActionExecutionContext context = PreviewContext(sourceContext);
        PreparationRun run = Runner(
            Catalog(
                Entry("test.bound", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            adapter);

        RunToCompletion(run.PrepareBefore(sequence, "selected", context, new TestPreviewScope()));

        Assert.That(JObject.Parse(adapter.LastParameters).Value<string>("actor"), Is.EqualTo("hero"));
        Destroy(sequence);
    }

    [Test]
    public void MissingSelectedBlockFailsBeforeMutationAndRestores()
    {
        var prepared = new List<string>();
        ActionSequenceAsset sequence = Sequence("missing-target", Action("state", "test.state"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(
            Catalog(Entry("test.state", ActionPreparationPolicy.ApplyFinalState)),
            new RecordingAdapter("test.state", prepared));

        RunToCompletion(run.PrepareBefore(sequence, "not-found", PreviewContext(), scope));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Failed));
        Assert.That(prepared, Is.Empty);
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void MissingBlockIdFailsWithoutMutatingRuntimeAsset()
    {
        ScenarioActionData missingId = Action(string.Empty, "test.state");
        ActionSequenceAsset sequence = Sequence(
            "missing-id",
            missingId,
            Action("selected", "test.selected"));
        PreparationRun run = Runner(Catalog(
            Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            PreviewContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Failed));
        Assert.That(missingId.BlockId, Is.Empty);
        Destroy(sequence);
    }

    [Test]
    public void SafePreviewRejectsProductionExecutionContext()
    {
        ActionSequenceAsset sequence = Sequence(
            "production-context",
            Action("state", "test.state"),
            Action("selected", "test.selected"));
        PreparationRun run = Runner(
            Catalog(
                Entry("test.state", ActionPreparationPolicy.ApplyFinalState),
                Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)),
            new RecordingAdapter("test.state", new List<string>()));

        RunToCompletion(run.PrepareBefore(
            sequence,
            "selected",
            new ActionExecutionContext(),
            new TestPreviewScope()));

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
        Assert.That(run.Result.Message, Does.Contain("detached preview execution context"));
        Destroy(sequence);
    }

    [Test]
    public void CancelRestoresScopeWhileWaitingForInput()
    {
        ActionSequenceAsset sequence = Sequence(
            "cancel",
            Action("choice", "test.choice"),
            Action("selected", "test.selected"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(Catalog(
            Entry("test.choice", ActionPreparationPolicy.RequireInput),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));
        IEnumerator routine = run.PrepareBefore(sequence, "selected", PreviewContext(), scope);

        Assert.That(routine.MoveNext(), Is.True);
        run.Cancel();
        RunToCompletion(routine);

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Canceled));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void DisposingRoutineWhileWaitingForInputRestoresScope()
    {
        ActionSequenceAsset sequence = Sequence(
            "dispose",
            Action("choice", "test.choice"),
            Action("selected", "test.selected"));
        var scope = new TestPreviewScope();
        PreparationRun run = Runner(Catalog(
            Entry("test.choice", ActionPreparationPolicy.RequireInput),
            Entry("test.selected", ActionPreparationPolicy.ApplyFinalState)));
        IEnumerator routine = run.PrepareBefore(sequence, "selected", PreviewContext(), scope);

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine, Is.InstanceOf<System.IDisposable>());
        ((System.IDisposable)routine).Dispose();

        Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Canceled));
        Assert.That(scope.RestoreCount, Is.EqualTo(1));
        Destroy(sequence);
    }

    [Test]
    public void EditorScopeCapturesEachParticipantOnceAndRestoresInReverseOrder()
    {
        var log = new List<string>();
        var first = new PreviewParticipant("first", log);
        var second = new PreviewParticipant("second", log);
        using (var scope = new EditorPreviewStateScope())
        {
            Assert.That(scope.TryCapture("first", first, out string firstError), Is.True, firstError);
            Assert.That(scope.TryCapture("first-again", first, out string duplicateError), Is.True, duplicateError);
            Assert.That(scope.TryCapture("second", second, out string secondError), Is.True, secondError);
            scope.Restore();
        }

        Assert.That(log, Is.EqualTo(new[]
        {
            "first:capture",
            "second:capture",
            "second:restore",
            "first:restore"
        }));
    }

    [Test]
    public void PreparationRegistryRejectsDuplicateActionId()
    {
        var registry = new ActionPreparationRegistry();
        registry.Register(new RecordingAdapter("test.duplicate", new List<string>()));

        Assert.Throws<System.InvalidOperationException>(() =>
            registry.Register(new RecordingAdapter("test.duplicate", new List<string>())));
    }

    [Test]
    public void PreviewModuleRunnerKeepsEnterAndStartLifecycleDistinct()
    {
        var runner = new PreviewGameModuleActionRunner("turn_qte");
        ActionExecutionContext context = PreviewContext();

        RunToCompletion(runner.SwitchTo("aim_shooter", context));
        RunToCompletion(runner.Start("aim_shooter", context));

        Assert.That(runner.CurrentModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(runner.EnteredModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(runner.StartedModuleId, Is.EqualTo("aim_shooter"));
    }

    private static PreparationRun Runner(ActionCatalogAsset catalog, params IActionPreparationAdapter[] adapters)
    {
        var registry = new ActionPreparationRegistry();
        if (adapters != null)
        {
            for (int i = 0; i < adapters.Length; i++)
            {
                registry.Register(adapters[i]);
            }
        }

        return new PreparationRun(catalog, registry);
    }

    private static ActionExecutionContext PreviewContext(ActionExecutionContext source = null)
    {
        return PreviewActionExecutionContextFactory.Create(source);
    }

    private static ActionCatalogAsset Catalog(params ActionCatalogEntry[] entries)
    {
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.AddRange(entries);
        return catalog;
    }

    private static ActionCatalogEntry Entry(string actionId, ActionPreparationPolicy policy)
    {
        return new ActionCatalogEntry
        {
            ActionId = actionId,
            DisplayNameKo = actionId,
            PreparationPolicy = policy,
            PreviewSupport = ActionPreviewSupport.SafePreview
        };
    }

    private static ActionSequenceAsset Sequence(string sequenceId, params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static ScenarioActionData Action(string blockId, string actionId, string parameters = "{}")
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = parameters
        };
    }

    private static void RunToCompletion(IEnumerator routine)
    {
        int guard = 0;
        while (routine.MoveNext())
        {
            Assert.That(guard++, Is.LessThan(2048), "Preparation coroutine did not finish.");
        }
    }

    private static void Destroy(params Object[] objects)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                Object.DestroyImmediate(objects[i]);
            }
        }
    }

    private sealed class RecordingAdapter : IActionPreparationAdapter
    {
        private readonly List<string> _prepared;

        public RecordingAdapter(
            string actionId,
            List<string> prepared,
            PreviewSideEffect sideEffects = PreviewSideEffect.SceneState)
        {
            ActionId = actionId;
            _prepared = prepared;
            SideEffects = sideEffects;
        }

        public string ActionId { get; }
        public PreviewSideEffect SideEffects { get; }
        public string LastParameters { get; private set; } = "{}";

        public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
        {
            _prepared.Add(action.BlockId);
            LastParameters = action.ParametersJson;
            yield break;
        }
    }

    private sealed class FailingAdapter : IActionPreparationAdapter
    {
        private readonly string _message;

        public FailingAdapter(string actionId, string message)
        {
            ActionId = actionId;
            _message = message;
        }

        public string ActionId { get; }
        public PreviewSideEffect SideEffects => PreviewSideEffect.SceneState;

        public IEnumerator Prepare(ScenarioActionData action, ActionPreparationContext context)
        {
            context.Fail(_message);
            yield break;
        }
    }

    private sealed class TestPreviewScope : IPreviewStateScope
    {
        public bool IsSafePreview => true;
        public bool IsRestored { get; private set; }
        public int RestoreCount { get; private set; }

        public bool TryAuthorize(
            PreviewSideEffect sideEffects,
            string blockId,
            string actionId,
            out string error)
        {
            PreviewSideEffect forbidden = PreviewSideEffect.Save
                | PreviewSideEffect.Reward
                | PreviewSideEffect.SceneTransition;
            if ((sideEffects & forbidden) != 0)
            {
                error = "Safe Preview blocked irreversible side effect at block '" + blockId + "'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCapture(
            string key,
            IPreviewStateParticipant participant,
            out string error)
        {
            error = string.Empty;
            return true;
        }

        public void Restore()
        {
            if (IsRestored)
            {
                return;
            }

            IsRestored = true;
            RestoreCount++;
        }
    }

    private sealed class PreviewParticipant : IPreviewStateParticipant
    {
        private readonly string _id;
        private readonly List<string> _log;

        public PreviewParticipant(string id, List<string> log)
        {
            _id = id;
            _log = log;
        }

        public object CapturePreviewState()
        {
            _log.Add(_id + ":capture");
            return _id + ":state";
        }

        public void RestorePreviewState(object state)
        {
            _log.Add(_id + ":restore");
        }
    }

    private sealed class ThrowingScreenRunner : IScreenTransitionRunner
    {
        public int CallCount { get; private set; }

        public IEnumerator Fade(string mode, string color, float duration, ActionExecutionHandle handle)
        {
            CallCount++;
            throw new System.InvalidOperationException("Production screen service was called.");
        }
    }

    private sealed class ThrowingAudioRunner : IAudioActionRunner
    {
        public int CallCount { get; private set; }

        public IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle)
        {
            CallCount++;
            throw new System.InvalidOperationException("Production audio service was called.");
        }
    }

    private sealed class ThrowingModuleRunner : IGameModuleActionRunner
    {
        public int CallCount { get; private set; }
        public string CurrentModuleId => string.Empty;

        public IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
        {
            CallCount++;
            throw new System.InvalidOperationException("Production module service was called.");
        }

        public IEnumerator Start(string moduleId, ActionExecutionContext context)
        {
            CallCount++;
            throw new System.InvalidOperationException("Production module service was called.");
        }
    }
}
