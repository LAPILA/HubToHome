using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

public sealed class SequenceMakerCommandPropertyTests
{
    private ActionSequenceAsset _sequence;

    [TearDown]
    public void TearDown()
    {
        if (_sequence != null)
        {
            UnityEngine.Object.DestroyImmediate(_sequence);
        }
    }

    [Test]
    public void RandomCommandHistoriesRoundTripAcrossOneHundredSeeds()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            CreateSequence(seed);
            var random = new System.Random(seed);
            var stack = new SequenceEditCommandStack(_sequence);
            string baseline = Export();
            int nextId = 100;

            for (int step = 0; step < 30; step++)
            {
                ExecuteRandomValidCommand(stack, random, ref nextId);
                AssertInvariants(stack, seed, step);
            }

            string edited = Export();
            int undoCount = 0;
            while (stack.Undo())
            {
                undoCount++;
                AssertInvariants(stack, seed, 1000 + undoCount);
            }
            Assert.That(undoCount, Is.GreaterThan(0), "seed " + seed);
            Assert.That(Export(), Is.EqualTo(baseline), "Undo mismatch at seed " + seed);

            int redoCount = 0;
            while (stack.Redo())
            {
                redoCount++;
                AssertInvariants(stack, seed, 2000 + redoCount);
            }
            Assert.That(redoCount, Is.EqualTo(undoCount), "seed " + seed);
            Assert.That(Export(), Is.EqualTo(edited), "Redo mismatch at seed " + seed);

            UnityEngine.Object.DestroyImmediate(_sequence);
            _sequence = null;
        }
    }

    [Test]
    public void ThousandBlocksProjectAndRemainAddressableWithinEditorBudget()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "property.performance";
        for (int i = 0; i < 1000; i++)
        {
            _sequence.Actions.Add(Action("block-" + i, i));
        }

        var stopwatch = Stopwatch.StartNew();
        SequenceFlowProjection projection = SequenceFlowProjection.Build(_sequence);
        stopwatch.Stop();

        Assert.That(projection.AllNodes, Has.Count.EqualTo(1000));
        for (int i = 0; i < 1000; i++)
        {
            Assert.That(projection.GetNode("block-" + i), Is.Not.Null);
        }
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
    }

    private void CreateSequence(int seed)
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "property.seed." + seed;
        _sequence.DisplayNameKo = "Property " + seed;
        for (int i = 0; i < 6; i++)
        {
            _sequence.Actions.Add(Action("seed-" + seed + "-root-" + i, i));
        }
        ScenarioActionData group = Action(
            "seed-" + seed + "-group",
            ActionDirector.ParallelActionId,
            "{}");
        group.Children.Add(Action("seed-" + seed + "-child-a", 10));
        group.Children.Add(Action("seed-" + seed + "-child-b", 11));
        _sequence.Actions.Add(group);
    }

    private void ExecuteRandomValidCommand(
        SequenceEditCommandStack stack,
        System.Random random,
        ref int nextId)
    {
        List<ScenarioActionData> all = AllBlocks();
        ScenarioActionData selected = all[random.Next(all.Count)];
        stack.SetSelection(selected.BlockId);
        int operation = random.Next(7);
        switch (operation)
        {
            case 0:
            {
                string id = NextId(ref nextId);
                int index = random.Next(_sequence.Actions.Count + 1);
                stack.Execute(SequenceEditCommands.Insert(
                    string.Empty,
                    index,
                    Action(id, nextId)));
                break;
            }
            case 1:
            {
                if (_sequence.Actions.Count > 1)
                {
                    ScenarioActionData root =
                        _sequence.Actions[random.Next(_sequence.Actions.Count)];
                    int index = random.Next(_sequence.Actions.Count);
                    stack.Execute(SequenceEditCommands.Move(root.BlockId, string.Empty, index));
                }
                else
                {
                    stack.Execute(SequenceEditCommands.SetEnabled(
                        selected.BlockId,
                        !selected.Disabled));
                }
                break;
            }
            case 2:
                stack.Execute(SequenceEditCommands.Duplicate(selected.BlockId));
                break;
            case 3:
                if (all.Count > 3)
                {
                    stack.Execute(SequenceEditCommands.Delete(selected.BlockId));
                }
                else
                {
                    stack.Execute(SequenceEditCommands.SetNote(
                        selected.BlockId,
                        "seed-note-" + nextId++));
                }
                break;
            case 4:
                stack.Execute(SequenceEditCommands.SetEnabled(
                    selected.BlockId,
                    !selected.Disabled));
                break;
            case 5:
                stack.Execute(SequenceEditCommands.SetParameters(
                    selected.BlockId,
                    "{\"value\":" + random.Next(1000) + "}"));
                break;
            default:
                if (_sequence.Actions.Count >= 2)
                {
                    int first = random.Next(_sequence.Actions.Count - 1);
                    stack.Execute(SequenceEditCommands.WrapInParallel(
                        new[]
                        {
                            _sequence.Actions[first].BlockId,
                            _sequence.Actions[first + 1].BlockId
                        },
                        NextId(ref nextId)));
                }
                else
                {
                    stack.Execute(SequenceEditCommands.SetDesignerLabel(
                        selected.BlockId,
                        "label-" + nextId++));
                }
                break;
        }
    }

    private void AssertInvariants(SequenceEditCommandStack stack, int seed, int step)
    {
        Assert.That(
            SequenceBlockTree.TryValidateUniqueIds(_sequence, out string error),
            Is.True,
            "seed " + seed + ", step " + step + ": " + error);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        List<ScenarioActionData> all = AllBlocks();
        for (int i = 0; i < all.Count; i++)
        {
            Assert.That(ids.Add(all[i].BlockId), Is.True);
        }
        for (int i = 0; i < stack.SelectedBlockIds.Count; i++)
        {
            Assert.That(
                ids.Contains(stack.SelectedBlockIds[i]),
                Is.True,
                "Stale selection at seed " + seed + ", step " + step);
        }
        if (!string.IsNullOrWhiteSpace(stack.PrimarySelectionBlockId))
        {
            Assert.That(ids.Contains(stack.PrimarySelectionBlockId), Is.True);
        }
    }

    private List<ScenarioActionData> AllBlocks()
    {
        var result = new List<ScenarioActionData>();
        Collect(_sequence.Actions, result);
        return result;
    }

    private static void Collect(
        IList<ScenarioActionData> source,
        List<ScenarioActionData> result)
    {
        if (source == null)
        {
            return;
        }
        for (int i = 0; i < source.Count; i++)
        {
            ScenarioActionData action = source[i];
            if (action == null)
            {
                continue;
            }
            result.Add(action);
            Collect(action.Children, result);
        }
    }

    private string Export()
    {
        ActionSequenceSourceExportResult result = ActionSequenceSourceSync.Export(_sequence);
        Assert.That(result.Success, Is.True);
        return result.Text;
    }

    private static ScenarioActionData Action(
        string blockId,
        int value,
        string parameters = null)
    {
        return Action(
            blockId,
            FlowWaitActionAdapter.Id,
            parameters ?? "{\"duration\":" + (value % 10) + "}");
    }

    private static ScenarioActionData Action(
        string blockId,
        string actionId,
        string parameters)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = parameters
        };
    }

    private static string NextId(ref int nextId)
    {
        return "generated-" + nextId++;
    }
}
