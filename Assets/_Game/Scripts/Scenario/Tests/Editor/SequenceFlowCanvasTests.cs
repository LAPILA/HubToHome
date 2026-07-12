using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public class SequenceFlowCanvasTests
{
    private ActionSequenceAsset _sequence;

    [SetUp]
    public void SetUp()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "canvas.test";
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_sequence);
    }

    [Test]
    public void TwoHundredBlocksBuildViewsAndInsertionRailsWithinEditorBudget()
    {
        for (int i = 0; i < 200; i++)
        {
            _sequence.Actions.Add(Block("block-" + i));
        }
        var stack = new SequenceEditCommandStack(_sequence);
        var canvas = new SequenceFlowCanvas();
        var stopwatch = Stopwatch.StartNew();

        canvas.Bind(_sequence, stack, null, null, string.Empty);

        stopwatch.Stop();
        Assert.That(canvas.Query<ActionBlockView>().ToList(), Has.Count.EqualTo(200));
        Assert.That(canvas.Query<ActionInsertionRail>().ToList(), Has.Count.EqualTo(201));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000));
        canvas.ClearBinding();
    }

    [Test]
    public void CopyPasteCreatesIndependentIdsAndOneUndoableTransaction()
    {
        _sequence.Actions.Add(Block("a"));
        _sequence.Actions.Add(Block("b"));
        var stack = new SequenceEditCommandStack(_sequence);
        stack.SetSelection(new[] { "a", "b" }, "b");
        var canvas = new SequenceFlowCanvas();
        canvas.Bind(_sequence, stack, null, null, string.Empty);

        canvas.CopySelection();
        canvas.PasteAfterSelection();

        Assert.That(_sequence.Actions, Has.Count.EqualTo(4));
        Assert.That(_sequence.Actions[2].BlockId, Is.Not.EqualTo("a"));
        Assert.That(_sequence.Actions[3].BlockId, Is.Not.EqualTo("b"));
        stack.Undo();
        Assert.That(_sequence.Actions, Has.Count.EqualTo(2));
        canvas.ClearBinding();
    }

    [Test]
    public void DeleteSelectionIgnoresSelectedDescendantWhenAncestorIsSelected()
    {
        ScenarioActionData group = new ScenarioActionData
        {
            BlockId = "group",
            ActionId = ActionDirector.ParallelActionId,
            ParametersJson = "{}"
        };
        group.Children.Add(Block("child"));
        _sequence.Actions.Add(group);
        _sequence.Actions.Add(Block("tail"));
        var stack = new SequenceEditCommandStack(_sequence);
        stack.SetSelection(new[] { "group", "child" }, "group");
        var canvas = new SequenceFlowCanvas();
        canvas.Bind(_sequence, stack, null, null, string.Empty);

        canvas.DeleteSelection();

        Assert.That(_sequence.Actions, Has.Count.EqualTo(1));
        Assert.That(_sequence.Actions[0].BlockId, Is.EqualTo("tail"));
        stack.Undo();
        Assert.That(_sequence.Actions[0], Is.SameAs(group));
        canvas.ClearBinding();
    }

    private static ScenarioActionData Block(string blockId)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0.1}"
        };
    }
}
