using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SequenceEditCommandStackTests
{
    private ActionSequenceAsset _sequence;

    [SetUp]
    public void SetUp()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "edit-tests";
    }

    [TearDown]
    public void TearDown()
    {
        if (_sequence != null)
        {
            UnityEngine.Object.DestroyImmediate(_sequence);
        }
    }

    [Test]
    public void InsertUndoRedoPreservesInsertedObjectAndBlockId()
    {
        var stack = new SequenceEditCommandStack(_sequence);
        ScenarioActionData inserted = Action("inserted", "flow.wait");

        stack.Execute(SequenceEditCommands.Insert(string.Empty, 0, inserted));

        Assert.That(_sequence.Actions, Is.EqualTo(new[] { inserted }));
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("inserted"));
        Assert.That(stack.CanUndo, Is.True);
        Assert.That(EditorUtility.IsDirty(_sequence), Is.True);

        Assert.That(stack.Undo(), Is.True);
        Assert.That(_sequence.Actions, Is.Empty);
        Assert.That(stack.Redo(), Is.True);
        Assert.That(_sequence.Actions[0], Is.SameAs(inserted));
        Assert.That(_sequence.Actions[0].BlockId, Is.EqualTo("inserted"));
    }

    [Test]
    public void MoveRootBlockUndoRedoPreservesSelection()
    {
        ScenarioActionData a = Action("a", "test.a");
        ScenarioActionData b = Action("b", "test.b");
        ScenarioActionData c = Action("c", "test.c");
        _sequence.Actions.AddRange(new[] { a, b, c });
        var stack = new SequenceEditCommandStack(_sequence);
        stack.SetSelection("a");

        stack.Execute(SequenceEditCommands.Move("a", string.Empty, 2));

        AssertIds(_sequence.Actions, "b", "c", "a");
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("a"));
        stack.Undo();
        AssertIds(_sequence.Actions, "a", "b", "c");
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("a"));
        stack.Redo();
        AssertIds(_sequence.Actions, "b", "c", "a");
    }

    [Test]
    public void MoveBlockAcrossNestedListsIsReversible()
    {
        ScenarioActionData left = Group("left", Action("left-child", "test.child"));
        ScenarioActionData right = Group("right");
        _sequence.Actions.AddRange(new[] { left, right });
        var stack = new SequenceEditCommandStack(_sequence);

        stack.Execute(SequenceEditCommands.Move("left-child", "right", 0));

        Assert.That(left.Children, Is.Empty);
        AssertIds(right.Children, "left-child");
        stack.Undo();
        AssertIds(left.Children, "left-child");
        Assert.That(right.Children, Is.Empty);
        stack.Redo();
        Assert.That(left.Children, Is.Empty);
        AssertIds(right.Children, "left-child");
    }

    [Test]
    public void DuplicateCreatesIndependentRecursiveBlockIdsAndSelectsClone()
    {
        ScenarioActionData source = Group(
            "source",
            Action("source-child", "test.child"));
        _sequence.Actions.Add(source);
        var stack = new SequenceEditCommandStack(_sequence);

        stack.Execute(SequenceEditCommands.Duplicate("source"));

        Assert.That(_sequence.Actions.Count, Is.EqualTo(2));
        ScenarioActionData clone = _sequence.Actions[1];
        Assert.That(clone, Is.Not.SameAs(source));
        Assert.That(clone.BlockId, Is.Not.EqualTo(source.BlockId));
        Assert.That(clone.Children[0].BlockId, Is.Not.EqualTo(source.Children[0].BlockId));
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo(clone.BlockId));

        stack.Undo();
        Assert.That(_sequence.Actions, Is.EqualTo(new[] { source }));
        stack.Redo();
        Assert.That(_sequence.Actions[1], Is.SameAs(clone));
    }

    [Test]
    public void DeleteUndoRestoresSameObjectAtOriginalIndex()
    {
        ScenarioActionData a = Action("a", "test.a");
        ScenarioActionData b = Action("b", "test.b");
        ScenarioActionData c = Action("c", "test.c");
        _sequence.Actions.AddRange(new[] { a, b, c });
        var stack = new SequenceEditCommandStack(_sequence);
        stack.SetSelection("b");

        stack.Execute(SequenceEditCommands.Delete("b"));

        AssertIds(_sequence.Actions, "a", "c");
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("c"));
        stack.Undo();
        AssertIds(_sequence.Actions, "a", "b", "c");
        Assert.That(_sequence.Actions[1], Is.SameAs(b));
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("b"));
    }

    [Test]
    public void SetEnabledAndParametersRoundTripExactly()
    {
        ScenarioActionData action = Action("target", "test.action", "{\"value\":1}");
        _sequence.Actions.Add(action);
        var stack = new SequenceEditCommandStack(_sequence);

        stack.Execute(SequenceEditCommands.SetEnabled("target", false));
        stack.Execute(SequenceEditCommands.SetParameters("target", "{\"value\":2}"));

        Assert.That(action.Disabled, Is.True);
        Assert.That(action.ParametersJson, Is.EqualTo("{\"value\":2}"));
        stack.Undo();
        Assert.That(action.ParametersJson, Is.EqualTo("{\"value\":1}"));
        stack.Undo();
        Assert.That(action.Disabled, Is.False);
        stack.Redo();
        stack.Redo();
        Assert.That(action.Disabled, Is.True);
        Assert.That(action.ParametersJson, Is.EqualTo("{\"value\":2}"));
    }

    [Test]
    public void CommittedTransactionBecomesOneUndoEntry()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);

        using (SequenceEditTransaction transaction = stack.BeginTransaction("복합 편집"))
        {
            stack.Execute(SequenceEditCommands.Insert(
                string.Empty,
                1,
                Action("b", "test.b")));
            stack.Execute(SequenceEditCommands.Insert(
                string.Empty,
                2,
                Action("c", "test.c")));
            transaction.Commit();
        }

        AssertIds(_sequence.Actions, "a", "b", "c");
        Assert.That(stack.UndoLabel, Is.EqualTo("복합 편집"));
        stack.Undo();
        AssertIds(_sequence.Actions, "a");
        Assert.That(stack.CanUndo, Is.False);
        stack.Redo();
        AssertIds(_sequence.Actions, "a", "b", "c");
    }

    [Test]
    public void UncommittedTransactionRollsBackEveryCommand()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);

        using (stack.BeginTransaction("취소할 편집"))
        {
            stack.Execute(SequenceEditCommands.Insert(
                string.Empty,
                1,
                Action("b", "test.b")));
            stack.Execute(SequenceEditCommands.SetEnabled("a", false));
        }

        AssertIds(_sequence.Actions, "a");
        Assert.That(_sequence.Actions[0].Disabled, Is.False);
        Assert.That(stack.CanUndo, Is.False);
        Assert.That(stack.IsDirty, Is.False);
    }

    [Test]
    public void FailedCommandInsideTransactionRollsBackPreviousCommands()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);

        Assert.Throws<InvalidOperationException>(() =>
        {
            using (SequenceEditTransaction transaction = stack.BeginTransaction("실패 편집"))
            {
                stack.Execute(SequenceEditCommands.Insert(
                    string.Empty,
                    1,
                    Action("b", "test.b")));
                stack.Execute(SequenceEditCommands.Delete("missing"));
                transaction.Commit();
            }
        });

        AssertIds(_sequence.Actions, "a");
        Assert.That(stack.CanUndo, Is.False);
    }

    [Test]
    public void NewCommandAfterUndoClearsRedoBranch()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);
        stack.Execute(SequenceEditCommands.SetEnabled("a", false));
        stack.Undo();
        Assert.That(stack.CanRedo, Is.True);

        stack.Execute(SequenceEditCommands.SetParameters("a", "{\"new\":true}"));

        Assert.That(stack.CanRedo, Is.False);
        Assert.That(_sequence.Actions[0].Disabled, Is.False);
    }

    [Test]
    public void MultiSelectionSurvivesMoveAndUndoByBlockId()
    {
        _sequence.Actions.AddRange(new[]
        {
            Action("a", "test.a"),
            Action("b", "test.b"),
            Action("c", "test.c")
        });
        var stack = new SequenceEditCommandStack(_sequence);
        stack.SetSelection(new[] { "a", "c" }, "c");

        stack.Execute(SequenceEditCommands.Move("a", string.Empty, 2));

        Assert.That(stack.SelectedBlockIds, Is.EqualTo(new[] { "a", "c" }));
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("c"));
        stack.Undo();
        Assert.That(stack.SelectedBlockIds, Is.EqualTo(new[] { "a", "c" }));
    }

    [Test]
    public void DirtyStateReturnsToSavedRevisionThroughUndo()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);
        stack.MarkSaved();
        Assert.That(stack.IsDirty, Is.False);

        stack.Execute(SequenceEditCommands.SetEnabled("a", false));
        Assert.That(stack.IsDirty, Is.True);
        stack.Undo();
        Assert.That(stack.IsDirty, Is.False);
        stack.Redo();
        Assert.That(stack.IsDirty, Is.True);
        stack.MarkSaved();
        Assert.That(stack.IsDirty, Is.False);
    }

    [Test]
    public void InvalidInsertWithDuplicateRecursiveIdDoesNotEnterHistory()
    {
        _sequence.Actions.Add(Action("existing", "test.a"));
        ScenarioActionData duplicate = Group(
            "new-parent",
            Action("existing", "test.child"));
        var stack = new SequenceEditCommandStack(_sequence);

        Assert.Throws<InvalidOperationException>(() =>
            stack.Execute(SequenceEditCommands.Insert(string.Empty, 1, duplicate)));

        AssertIds(_sequence.Actions, "existing");
        Assert.That(stack.CanUndo, Is.False);
        Assert.That(stack.IsDirty, Is.False);
    }

    [Test]
    public void ActionIdDesignerLabelAndNoteEditsUndoAsOneTransaction()
    {
        ScenarioActionData action = Action("a", "old.action");
        action.DesignerLabel = "이전 이름";
        action.Note = "이전 메모";
        _sequence.Actions.Add(action);
        var stack = new SequenceEditCommandStack(_sequence);

        using (SequenceEditTransaction transaction = stack.BeginTransaction("블록 설명 편집"))
        {
            stack.Execute(SequenceEditCommands.SetActionId("a", "new.action"));
            stack.Execute(SequenceEditCommands.SetDesignerLabel("a", "새 이름"));
            stack.Execute(SequenceEditCommands.SetNote("a", "새 메모"));
            transaction.Commit();
        }

        Assert.That(action.ActionId, Is.EqualTo("new.action"));
        Assert.That(action.DesignerLabel, Is.EqualTo("새 이름"));
        Assert.That(action.Note, Is.EqualTo("새 메모"));
        Assert.That(stack.UndoLabel, Is.EqualTo("블록 설명 편집"));

        stack.Undo();
        Assert.That(action.ActionId, Is.EqualTo("old.action"));
        Assert.That(action.DesignerLabel, Is.EqualTo("이전 이름"));
        Assert.That(action.Note, Is.EqualTo("이전 메모"));
    }

    [Test]
    public void WrapContiguousBlocksInParallelUndoRedoPreservesObjects()
    {
        ScenarioActionData a = Action("a", "test.a");
        ScenarioActionData b = Action("b", "test.b");
        ScenarioActionData tail = Action("tail", "test.tail");
        _sequence.Actions.AddRange(new[] { a, b, tail });
        var stack = new SequenceEditCommandStack(_sequence);

        stack.Execute(SequenceEditCommands.WrapInParallel(
            new[] { "a", "b" },
            "wrapper"));

        AssertIds(_sequence.Actions, "wrapper", "tail");
        Assert.That(_sequence.Actions[0].ActionId, Is.EqualTo(ActionDirector.ParallelActionId));
        Assert.That(_sequence.Actions[0].Children, Is.EqualTo(new[] { a, b }));
        Assert.That(stack.PrimarySelectionBlockId, Is.EqualTo("wrapper"));

        stack.Undo();
        AssertIds(_sequence.Actions, "a", "b", "tail");
        Assert.That(_sequence.Actions[0], Is.SameAs(a));
        Assert.That(_sequence.Actions[1], Is.SameAs(b));

        stack.Redo();
        AssertIds(_sequence.Actions, "wrapper", "tail");
        Assert.That(_sequence.Actions[0].Children[0], Is.SameAs(a));
    }

    [Test]
    public void WrapRejectsNonContiguousOrDifferentParentSelectionsWithoutMutation()
    {
        ScenarioActionData group = Group("group", Action("child", "test.child"));
        _sequence.Actions.AddRange(new[]
        {
            Action("a", "test.a"),
            Action("middle", "test.middle"),
            Action("b", "test.b"),
            group
        });
        var stack = new SequenceEditCommandStack(_sequence);

        Assert.Throws<InvalidOperationException>(() =>
            stack.Execute(SequenceEditCommands.WrapInParallel(new[] { "a", "b" })));
        Assert.Throws<InvalidOperationException>(() =>
            stack.Execute(SequenceEditCommands.WrapInParallel(new[] { "a", "child" })));

        AssertIds(_sequence.Actions, "a", "middle", "b", "group");
        Assert.That(group.Children[0].BlockId, Is.EqualTo("child"));
        Assert.That(stack.CanUndo, Is.False);
    }

    [Test]
    public void ReplaceRangeWithSequenceCallIsReversible()
    {
        ScenarioActionData a = Action("a", "test.a");
        ScenarioActionData b = Action("b", "test.b");
        _sequence.Actions.AddRange(new[] { a, b });
        ScenarioActionData call = Action(
            "call",
            SequenceCallActionAdapter.Id,
            "{\"sequence\":\"extracted\"}");
        var stack = new SequenceEditCommandStack(_sequence);

        stack.Execute(SequenceEditCommands.ReplaceWith(
            new[] { "a", "b" },
            call,
            "시퀀스로 추출"));

        AssertIds(_sequence.Actions, "call");
        stack.Undo();
        AssertIds(_sequence.Actions, "a", "b");
        stack.Redo();
        AssertIds(_sequence.Actions, "call");
    }

    [Test]
    public void ExtractCommandKeepsBattleOwnershipAndCallReplacementInOneHistoryEntry()
    {
        ScenarioActionData source = Action("source", "test.source");
        _sequence.Actions.Add(source);
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.Sequences.Add(_sequence);
        ActionSequenceAsset extracted = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        extracted.SequenceId = "extracted";
        ScenarioActionData call = Action(
            "call",
            SequenceCallActionAdapter.Id,
            "{\"sequence\":\"extracted\"}");
        var stack = new SequenceEditCommandStack(_sequence);

        try
        {
            stack.Execute(SequenceEditCommands.ExtractToSequence(
                new[] { "source" },
                call,
                battle,
                extracted));

            AssertIds(_sequence.Actions, "call");
            Assert.That(battle.Sequences, Contains.Item(extracted));

            stack.Undo();
            AssertIds(_sequence.Actions, "source");
            Assert.That(battle.Sequences.Contains(extracted), Is.False);

            stack.Redo();
            AssertIds(_sequence.Actions, "call");
            Assert.That(battle.Sequences, Contains.Item(extracted));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(extracted);
            UnityEngine.Object.DestroyImmediate(battle);
        }
    }

    [Test]
    public void ChangedEventFiresForExecuteUndoRedoAndSelection()
    {
        _sequence.Actions.Add(Action("a", "test.a"));
        var stack = new SequenceEditCommandStack(_sequence);
        var reasons = new List<SequenceEditChangeReason>();
        stack.Changed += change => reasons.Add(change.Reason);

        stack.SetSelection("a");
        stack.Execute(SequenceEditCommands.SetEnabled("a", false));
        stack.Undo();
        stack.Redo();

        Assert.That(reasons, Is.EqualTo(new[]
        {
            SequenceEditChangeReason.Selection,
            SequenceEditChangeReason.Execute,
            SequenceEditChangeReason.Undo,
            SequenceEditChangeReason.Redo
        }));
    }

    private static ScenarioActionData Action(
        string blockId,
        string actionId,
        string parameters = "{}")
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = parameters
        };
    }

    private static ScenarioActionData Group(string blockId, params ScenarioActionData[] children)
    {
        ScenarioActionData group = Action(blockId, ActionDirector.ParallelActionId);
        group.Children.AddRange(children);
        return group;
    }

    private static void AssertIds(IList<ScenarioActionData> actions, params string[] expected)
    {
        var actual = new List<string>();
        for (int i = 0; i < actions.Count; i++)
        {
            actual.Add(actions[i].BlockId);
        }

        Assert.That(actual, Is.EqualTo(expected));
    }
}
