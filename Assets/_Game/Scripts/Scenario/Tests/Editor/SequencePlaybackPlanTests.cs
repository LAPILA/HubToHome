using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequencePlaybackPlanTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            if (_created[i] != null)
            {
                Object.DestroyImmediate(_created[i]);
            }
        }
        _created.Clear();
    }

    [Test]
    public void FullPreviewClonesEveryBlockAndAppendsDisabledSentinel()
    {
        ActionSequenceAsset source = Sequence(
            Action("a"),
            Action("b"));

        ActionSequenceAsset plan = SequencePlaybackController.BuildPreparationSequence(
            source,
            string.Empty,
            true,
            out string sentinelId);
        _created.Add(plan);

        Assert.That(plan.Actions.Count, Is.EqualTo(3));
        Assert.That(plan.Actions[0].BlockId, Is.EqualTo("a"));
        Assert.That(plan.Actions[1].BlockId, Is.EqualTo("b"));
        Assert.That(plan.Actions[2].BlockId, Is.EqualTo(sentinelId));
        Assert.That(plan.Actions[2].Disabled, Is.True);
        Assert.That(plan.Actions[0], Is.Not.SameAs(source.Actions[0]));
    }

    [Test]
    public void ThroughNestedBlockIncludesOnlyAuthoredPrefixAndTarget()
    {
        ScenarioActionData parallel = Action("group", ActionDirector.ParallelActionId);
        parallel.Children.Add(Action("child.before"));
        parallel.Children.Add(Action("child.target"));
        parallel.Children.Add(Action("child.after"));
        ActionSequenceAsset source = Sequence(Action("root.before"), parallel, Action("root.after"));

        ActionSequenceAsset plan = SequencePlaybackController.BuildPreparationSequence(
            source,
            "child.target",
            true,
            out _);
        _created.Add(plan);

        Assert.That(plan.Actions.Count, Is.EqualTo(3));
        Assert.That(plan.Actions[0].BlockId, Is.EqualTo("root.before"));
        Assert.That(plan.Actions[1].BlockId, Is.EqualTo("group"));
        Assert.That(plan.Actions[1].Children.Count, Is.EqualTo(2));
        Assert.That(plan.Actions[1].Children[1].BlockId, Is.EqualTo("child.target"));
    }

    [Test]
    public void BeforeNestedBlockPreparesEarlierSiblingsButNotTarget()
    {
        ScenarioActionData parallel = Action("group", ActionDirector.ParallelActionId);
        parallel.Children.Add(Action("child.before"));
        parallel.Children.Add(Action("child.target"));
        ActionSequenceAsset source = Sequence(parallel);

        ActionSequenceAsset plan = SequencePlaybackController.BuildPreparationSequence(
            source,
            "child.target",
            false,
            out _);
        _created.Add(plan);

        Assert.That(plan.Actions[0].BlockId, Is.EqualTo("group"));
        Assert.That(plan.Actions[0].Children.Count, Is.EqualTo(1));
        Assert.That(plan.Actions[0].Children[0].BlockId, Is.EqualTo("child.before"));
    }

    [Test]
    public void MissingTargetReturnsNoPlan()
    {
        ActionSequenceAsset source = Sequence(Action("a"));

        ActionSequenceAsset plan = SequencePlaybackController.BuildPreparationSequence(
            source,
            "missing",
            true,
            out _);

        Assert.That(plan, Is.Null);
    }

    private ActionSequenceAsset Sequence(params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = "test.sequence";
        sequence.Actions.AddRange(actions);
        _created.Add(sequence);
        return sequence;
    }

    private static ScenarioActionData Action(string blockId, string actionId = FlowWaitActionAdapter.Id)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = "{}"
        };
    }
}
