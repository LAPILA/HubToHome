using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class OverworldPartyActionAdapterTests
{
    [TestCase(OverworldPartyActionContract.ModeId, "{\"slot\":0,\"mode\":\"hold\"}")]
    [TestCase(OverworldPartyActionContract.VisibilityId, "{\"slot\":5,\"visible\":false}")]
    [TestCase(OverworldPartyActionContract.GatherId, "{}")]
    public void ImmediateCommandsUseInjectedService(string id, string json)
    {
        var runner = new RecordingRunner();
        ActionExecutionContext context = Context(runner);
        Drain(Adapter(id).Execute(Action(id, json), context));
        Assert.That(context.Handle.Status, Is.Not.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(runner.Calls, Is.EqualTo(1));
    }

    [TestCase(OverworldPartyActionContract.ModeId, "{\"slot\":6,\"mode\":\"hold\"}")]
    [TestCase(OverworldPartyActionContract.ModeId, "{\"slot\":1.5,\"mode\":\"hold\"}")]
    [TestCase(OverworldPartyActionContract.ModeId, "{\"slot\":1,\"mode\":\"unknown\"}")]
    [TestCase(OverworldPartyActionContract.VisibilityId, "{\"slot\":1,\"visible\":1}")]
    [TestCase(OverworldPartyActionContract.MoveId, "{\"slot\":0,\"x\":1,\"y\":1,\"duration\":1}")]
    [TestCase(OverworldPartyActionContract.MoveId, "{\"slot\":1,\"x\":1e100,\"y\":1,\"duration\":1}")]
    [TestCase(OverworldPartyActionContract.MoveId, "{\"slot\":1,\"x\":1,\"y\":1,\"duration\":-1}")]
    [TestCase(OverworldPartyActionContract.MoveId, "{\"slot\":1,\"x\":1,\"y\":1}")]
    [TestCase(OverworldPartyActionContract.MoveId, "{\"slot\":1,\"x\":1,\"y\":1,\"duration\":1,\"path\":[{\"x\":1}]}")]
    [TestCase(OverworldPartyActionContract.GatherId, "{\"mode\":\"follow\"}")]
    public void InvalidParametersFailBeforeCallingProductionService(string id, string json)
    {
        var runner = new RecordingRunner();
        ActionExecutionContext context = Context(runner);
        Drain(Adapter(id).Execute(Action(id, json), context));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(runner.Calls, Is.Zero);
        Assert.That(OverworldPartyActionContract.TryRead(Action(id, json), true, out _, out _), Is.False);
    }

    [Test]
    public void MovePreservesExplicitWaypointsAndAppendsFinalDestination()
    {
        var runner = new RecordingRunner();
        var context = Context(runner);
        IEnumerator routine = new OverworldPartyMoveActionAdapter().Execute(Action(OverworldPartyActionContract.MoveId,
            "{\"slot\":1,\"x\":4,\"y\":2,\"duration\":1.2,\"path\":[{\"x\":2,\"y\":1},{\"x\":4,\"y\":1}],\"rejoin\":true}"), context);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(runner.Waypoints, Is.EqualTo(new[] { new Vector2(2, 1), new Vector2(4, 1), new Vector2(4, 2) }));
        Assert.That(runner.Rejoin, Is.True);
        runner.Operation.Complete();
        Drain(routine);
        Assert.That(context.Handle.Status, Is.Not.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(runner.Operation.CancelCount, Is.Zero);
    }

    [Test]
    public void HandleCancellationImmediatelyCancelsOnlyOwnedMove()
    {
        var runner = new RecordingRunner();
        ActionExecutionContext context = Context(runner);
        IEnumerator routine = new OverworldPartyMoveActionAdapter().Execute(Move(), context);
        Assert.That(routine.MoveNext(), Is.True);
        RecordingMove owned = runner.Operation;
        runner.Operation = new RecordingMove();
        context.Handle.Cancel();
        Assert.That(owned.CancelCount, Is.EqualTo(1));
        Assert.That(runner.Operation.CancelCount, Is.Zero);
        (routine as IDisposable)?.Dispose();
        Assert.That(owned.CancelCount, Is.EqualTo(1));
    }

    [Test]
    public void RoutineDisposalCancelsMoveAndRemovesCancellationSubscription()
    {
        var runner = new RecordingRunner();
        ActionExecutionContext context = Context(runner);
        IEnumerator routine = new OverworldPartyMoveActionAdapter().Execute(Move(), context);
        Assert.That(routine.MoveNext(), Is.True);
        (routine as IDisposable)?.Dispose();
        Assert.That(runner.Operation.CancelCount, Is.EqualTo(1));
        context.Handle.Cancel();
        Assert.That(runner.Operation.CancelCount, Is.EqualTo(1));
    }

    [Test]
    public void SupersededMoveAndMissingFollowerFailClearly()
    {
        var runner = new RecordingRunner();
        ActionExecutionContext context = Context(runner);
        IEnumerator routine = new OverworldPartyMoveActionAdapter().Execute(Move(), context);
        Assert.That(routine.MoveNext(), Is.True);
        runner.Operation.Cancel();
        Drain(routine);
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        runner.Reject = true;
        context = Context(runner);
        Drain(new OverworldPartyModeActionAdapter().Execute(Action(OverworldPartyActionContract.ModeId,
            "{\"slot\":5,\"mode\":\"follow\"}"), context));
        Assert.That(context.Handle.Result.Message, Does.Contain("missing follower"));
    }

    [Test]
    public void MissingServiceAndWrongModeFailClosed()
    {
        var context = Context(null);
        Drain(new OverworldPartyMoveActionAdapter().Execute(Move(), context));
        Assert.That(context.Handle.Result.Message, Does.Contain("IOverworldPartyActionRunner"));
        var runner = new RecordingRunner();
        context = Context(runner);
        context.PrimaryMode = "battle";
        Drain(new OverworldPartyMoveActionAdapter().Execute(Move(), context));
        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(runner.Calls, Is.Zero);
    }

    [Test]
    public void PreviewDoesNotCopyPartyServiceAndEvenInjectedPreviewIsBlocked()
    {
        var runner = new RecordingRunner();
        ActionExecutionContext preview = PreviewActionExecutionContextFactory.Create(Context(runner));
        Assert.That(preview.GetService<IOverworldPartyActionRunner>(), Is.Null);
        preview.SetService<IOverworldPartyActionRunner>(runner);
        Drain(new OverworldPartyMoveActionAdapter().Execute(Move(), preview));
        Assert.That(preview.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(runner.Calls, Is.Zero);
    }

    [Test]
    public void CatalogDeclaresLiveOnlyUnsupportedPreparationAndValidatesAllFourCommands()
    {
        const string path = "Assets/_Game/Content/Scenarios/ActionLibrary/Source/overworld_party.actions.yaml";
        var parsed = ActionLibrarySourceParser.Parse(File.ReadAllText(path), path);
        Assert.That(parsed.Success, Is.True);
        Assert.That(parsed.Document.Entries, Has.Count.EqualTo(4));
        foreach (ActionCatalogEntry entry in parsed.Document.Entries)
        {
            Assert.That(entry.PreviewSupport, Is.EqualTo(ActionPreviewSupport.LiveOnly));
            Assert.That(entry.PreparationPolicy, Is.EqualTo(ActionPreparationPolicy.Unsupported));
            Assert.That(entry.RequiredContexts, Does.Contain("overworld_party_runner"));
        }
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.AddRange(parsed.Document.Entries);
        var sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(Action(OverworldPartyActionContract.ModeId, "{\"slot\":1,\"mode\":\"typo\"}"));
        try
        {
            ScenarioValidationResult result = ScenarioCatalogValidator.ValidateSequence(sequence, catalog);
            Assert.That(result.Messages.Exists(m => m.Code == "scenario.overworld_party.parameters.invalid"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [TestCase(OverworldPartyActionContract.ModeId)]
    [TestCase(OverworldPartyActionContract.VisibilityId)]
    [TestCase(OverworldPartyActionContract.MoveId)]
    [TestCase(OverworldPartyActionContract.GatherId)]
    public void SceneRegistryRegistersEveryPartyAdapter(string id)
    {
        Assert.That(SceneActionSequenceContextFactory.CreateRegistry().TryGet(id, out _), Is.True);
        var runner = new RecordingRunner();
        ActionExecutionContext context = SceneActionSequenceContextFactory.Create(null, null, overworldPartyRunner: runner);
        Assert.That(context.GetService<IOverworldPartyActionRunner>(), Is.SameAs(runner));
    }

    [Test]
    public void UnsupportedPreparationIdentifiesExactPartyBlockWithoutCallingRunner()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        var sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        var action = Move();
        action.BlockId = "party-path";
        sequence.Actions.Add(action);
        sequence.Actions.Add(new ScenarioActionData { BlockId = "selected", ActionId = "flow.wait" });
        catalog.Entries.Add(new ActionCatalogEntry { ActionId = action.ActionId, PreparationPolicy = ActionPreparationPolicy.Unsupported });
        var runner = new RecordingRunner();
        var context = PreviewActionExecutionContextFactory.Create(Context(runner));
        var preparation = new PreparationRun(catalog, new ActionPreparationRegistry());
        try
        {
            Drain(preparation.PrepareBefore(sequence, "selected", context, new PreviewScope()));
            Assert.That(preparation.Result.Status, Is.EqualTo(PreparationRunStatus.Blocked));
            Assert.That(preparation.Result.Message, Does.Contain("party-path"));
            Assert.That(runner.Calls, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void MovePathRoundTripsThroughProductionScenarioSource()
    {
        var source = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        source.SequenceId = "overworld.party.sample";
        ScenarioActionData action = Move();
        action.BlockId = "party-move";
        action.ParametersJson = "{\"slot\":1,\"x\":4,\"y\":2,\"duration\":1,\"path\":[{\"x\":2,\"y\":1}],\"rejoin\":false}";
        source.Actions.Add(action);
        ActionSequenceSourceExportResult export = ActionSequenceSourceSync.Export(source, "overworld");
        ActionSequenceSourceImportResult import = ActionSequenceSourceSync.Import(export.Text, "Assets/_Game/Content/Scenarios/Source/Overworld/party_sample.sequence.yaml");
        try
        {
            Assert.That(export.Success, Is.True);
            Assert.That(import.Success, Is.True);
            Assert.That(JToken.DeepEquals(JObject.Parse(import.Sequence.Actions[0].ParametersJson), JObject.Parse(action.ParametersJson)), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            if (import.Sequence != null) UnityEngine.Object.DestroyImmediate(import.Sequence);
        }
    }

    private static ScenarioActionData Action(string id, string json) => new ScenarioActionData { ActionId = id, ParametersJson = json };
    private static ScenarioActionData Move() => Action(OverworldPartyActionContract.MoveId, "{\"slot\":1,\"x\":1,\"y\":2,\"duration\":1}");
    private static IActionAdapter Adapter(string id) => id == OverworldPartyActionContract.ModeId ? new OverworldPartyModeActionAdapter()
        : id == OverworldPartyActionContract.VisibilityId ? (IActionAdapter)new OverworldPartyVisibilityActionAdapter()
        : id == OverworldPartyActionContract.MoveId ? new OverworldPartyMoveActionAdapter() : new OverworldPartyGatherActionAdapter();
    private static ActionExecutionContext Context(IOverworldPartyActionRunner runner)
    {
        var context = new ActionExecutionContext { PrimaryMode = "overworld" };
        context.SetService(runner);
        return context;
    }
    private static void Drain(IEnumerator routine)
    {
        int steps = 0;
        while (routine.MoveNext()) Assert.That(++steps, Is.LessThan(100));
    }

    private sealed class RecordingRunner : IOverworldPartyActionRunner
    {
        public int Calls;
        public bool Reject;
        public RecordingMove Operation = new RecordingMove();
        public readonly List<Vector2> Waypoints = new List<Vector2>();
        public bool Rejoin;
        private bool Result(out string error) { Calls++; error = Reject ? "missing follower" : string.Empty; return !Reject; }
        public bool TrySetMode(int slot, OverworldFollowerMode mode, out string error) => Result(out error);
        public bool TrySetVisible(int slot, bool visible, out string error) => Result(out error);
        public bool TryGather(out string error) => Result(out error);
        public bool TryBeginMove(int slot, IReadOnlyList<Vector2> waypoints, float duration, bool rejoin,
            out IOverworldPartyMoveOperation operation, out string error)
        {
            Waypoints.AddRange(waypoints);
            Rejoin = rejoin;
            operation = Operation;
            return Result(out error);
        }
    }

    private sealed class RecordingMove : IOverworldPartyMoveOperation
    {
        public int CancelCount;
        public bool IsDone { get; private set; }
        public bool WasCompleted { get; private set; }
        public bool IsCancelled => IsDone && !WasCompleted;
        public string Error => string.Empty;
        public void Complete() { IsDone = true; WasCompleted = true; }
        public void Cancel() { CancelCount++; IsDone = true; }
    }

    private sealed class PreviewScope : IPreviewStateScope
    {
        public bool IsSafePreview => true;
        public bool IsRestored { get; private set; }
        public bool TryAuthorize(PreviewSideEffect effects, string block, string action, out string error) { error = string.Empty; return false; }
        public bool TryCapture(string key, IPreviewStateParticipant participant, out string error) { error = string.Empty; return false; }
        public void Restore() { IsRestored = true; }
    }
}
