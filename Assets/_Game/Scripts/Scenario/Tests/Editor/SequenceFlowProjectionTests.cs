using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequenceFlowProjectionTests
{
    private ActionSequenceAsset _sequence;

    [SetUp]
    public void SetUp()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "projection";
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_sequence);
    }

    [Test]
    public void BuildFlattensRecursiveTreeInDisplayOrderWithDepthAndParent()
    {
        ScenarioActionData root = Block("root", ActionDirector.ParallelActionId);
        root.Children.Add(Block("child-a", "flow.wait"));
        ScenarioActionData childGroup = Block("child-group", ActionDirector.ParallelActionId);
        childGroup.Children.Add(Block("grandchild", "screen.fade"));
        root.Children.Add(childGroup);
        _sequence.Actions.Add(root);
        _sequence.Actions.Add(Block("tail", "dialogue.wait"));

        SequenceFlowProjection projection = SequenceFlowProjection.Build(_sequence);

        Assert.That(projection.VisibleNodes, Has.Count.EqualTo(5));
        Assert.That(projection.VisibleNodes[0].BlockId, Is.EqualTo("root"));
        Assert.That(projection.VisibleNodes[1].BlockId, Is.EqualTo("child-a"));
        Assert.That(projection.VisibleNodes[2].BlockId, Is.EqualTo("child-group"));
        Assert.That(projection.VisibleNodes[3].BlockId, Is.EqualTo("grandchild"));
        Assert.That(projection.VisibleNodes[4].BlockId, Is.EqualTo("tail"));
        Assert.That(projection.VisibleNodes[3].Depth, Is.EqualTo(2));
        Assert.That(projection.VisibleNodes[3].ParentBlockId, Is.EqualTo("child-group"));
        Assert.That(projection.VisibleNodes[4].DisplayIndex, Is.EqualTo(4));
    }

    [Test]
    public void CollapsedStructuralBlockHidesDescendantsButLookupKeepsThem()
    {
        ScenarioActionData root = Block("root", ActionDirector.ParallelActionId);
        root.Children.Add(Block("child", "flow.wait"));
        _sequence.Actions.Add(root);

        SequenceFlowProjection projection = SequenceFlowProjection.Build(
            _sequence,
            collapsedBlockIds: new HashSet<string> { "root" });

        Assert.That(projection.VisibleNodes, Has.Count.EqualTo(1));
        Assert.That(projection.TryGetNode("child", out SequenceFlowNode child), Is.True);
        Assert.That(child.IsVisible, Is.False);
        Assert.That(projection.VisibleNodes[0].IsCollapsed, Is.True);
    }

    [Test]
    public void SelectionAndPrimarySelectionAreProjectedByStableBlockId()
    {
        _sequence.Actions.Add(Block("a", "flow.wait"));
        _sequence.Actions.Add(Block("b", "flow.wait"));

        SequenceFlowProjection projection = SequenceFlowProjection.Build(
            _sequence,
            selectedBlockIds: new HashSet<string> { "a", "b" },
            primarySelectionBlockId: "b");

        Assert.That(projection.GetNode("a").IsSelected, Is.True);
        Assert.That(projection.GetNode("a").IsPrimarySelection, Is.False);
        Assert.That(projection.GetNode("b").IsPrimarySelection, Is.True);
    }

    [Test]
    public void DisabledAndStructuralFlagsAreProjected()
    {
        ScenarioActionData disabled = Block("disabled", "flow.wait");
        disabled.Disabled = true;
        _sequence.Actions.Add(disabled);
        _sequence.Actions.Add(Block("parallel", ActionDirector.ParallelActionId));

        SequenceFlowProjection projection = SequenceFlowProjection.Build(_sequence);

        Assert.That(projection.GetNode("disabled").IsDisabled, Is.True);
        Assert.That(projection.GetNode("parallel").IsStructural, Is.True);
    }

    [Test]
    public void ValidationMessagesAttachToExactBlockId()
    {
        _sequence.Actions.Add(Block("good", "flow.wait"));
        _sequence.Actions.Add(Block("bad", "missing.action"));
        var validation = new ScenarioValidationResult();
        validation.AddError("unknown", "없는 액션", "block:bad");
        validation.AddWarning("warn", "확인 필요", "block:bad");

        SequenceFlowProjection projection = SequenceFlowProjection.Build(
            _sequence,
            validation: validation);

        Assert.That(projection.GetNode("good").ErrorCount, Is.EqualTo(0));
        Assert.That(projection.GetNode("bad").ErrorCount, Is.EqualTo(1));
        Assert.That(projection.GetNode("bad").WarningCount, Is.EqualTo(1));
    }

    [Test]
    public void SearchKeepsMatchingAncestorsSoNestedResultHasContext()
    {
        ScenarioActionData root = Block("root", ActionDirector.ParallelActionId);
        ScenarioActionData child = Block("child", "dialogue.wait");
        child.DesignerLabel = "결전 대사";
        root.Children.Add(child);
        _sequence.Actions.Add(root);

        SequenceFlowProjection projection = SequenceFlowProjection.Build(
            _sequence,
            searchQuery: "결전");

        Assert.That(projection.VisibleNodes, Has.Count.EqualTo(2));
        Assert.That(projection.VisibleNodes[0].BlockId, Is.EqualTo("root"));
        Assert.That(projection.VisibleNodes[1].BlockId, Is.EqualTo("child"));
        Assert.That(projection.VisibleNodes[0].IsContextOnly, Is.True);
        Assert.That(projection.VisibleNodes[1].IsContextOnly, Is.False);
    }

    [Test]
    public void TwoHundredBlocksProjectWithConstantLookupCoverage()
    {
        for (int i = 0; i < 200; i++)
        {
            _sequence.Actions.Add(Block("block-" + i, "flow.wait"));
        }

        SequenceFlowProjection projection = SequenceFlowProjection.Build(_sequence);

        Assert.That(projection.VisibleNodes, Has.Count.EqualTo(200));
        for (int i = 0; i < 200; i++)
        {
            Assert.That(projection.GetNode("block-" + i), Is.Not.Null);
        }
    }

    private static ScenarioActionData Block(string blockId, string actionId)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = "{}"
        };
    }
}
