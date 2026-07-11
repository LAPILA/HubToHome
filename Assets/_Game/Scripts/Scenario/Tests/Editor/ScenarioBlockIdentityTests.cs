using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ScenarioBlockIdentityTests
{
    [Test]
    public void EnsureUniqueAssignsIdsRecursivelyWithoutChangingExistingUniqueIds()
    {
        const string existingId = "0123456789abcdef0123456789abcdef";
        var child = new ScenarioActionData { ActionId = "test.child" };
        var root = new ScenarioActionData
        {
            BlockId = existingId,
            ActionId = "test.root",
            Children = new List<ScenarioActionData> { child }
        };
        var actions = new List<ScenarioActionData> { root };

        ScenarioBlockIdentity.EnsureUnique(actions);

        Assert.That(root.BlockId, Is.EqualTo(existingId));
        Assert.That(child.BlockId, Is.Not.Empty);
        Assert.That(child.BlockId, Is.Not.EqualTo(existingId));
        Assert.That(Guid.TryParseExact(child.BlockId, "N", out _), Is.True);
    }

    [Test]
    public void EnsureUniqueRepairsBlankAndDuplicateIdsAcrossTheWholeTree()
    {
        const string duplicateId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var actions = new List<ScenarioActionData>
        {
            new ScenarioActionData
            {
                BlockId = duplicateId,
                ActionId = "test.first",
                Children = new List<ScenarioActionData>
                {
                    new ScenarioActionData { BlockId = duplicateId, ActionId = "test.child" }
                }
            },
            new ScenarioActionData { BlockId = duplicateId, ActionId = "test.second" },
            new ScenarioActionData { BlockId = " ", ActionId = "test.blank" },
            null
        };

        ScenarioBlockIdentity.EnsureUnique(actions);

        List<string> ids = CollectIds(actions);
        Assert.That(ids, Has.Count.EqualTo(4));
        Assert.That(new HashSet<string>(ids), Has.Count.EqualTo(4));
        Assert.That(ids, Has.All.Matches<string>(id => Guid.TryParseExact(id, "N", out _)));
        Assert.That(actions[0].BlockId, Is.EqualTo(duplicateId));
    }

    [Test]
    public void ClonePreservingIdsCopiesDesignerDataAndNestedIdentity()
    {
        var source = new ScenarioActionData
        {
            BlockId = "11111111111111111111111111111111",
            DesignerLabel = "연출 시작",
            ActionId = "flow.parallel",
            ParametersJson = "{\"duration\":1.5}",
            Note = "기획 메모",
            Disabled = true,
            Children = new List<ScenarioActionData>
            {
                new ScenarioActionData
                {
                    BlockId = "22222222222222222222222222222222",
                    DesignerLabel = "자식",
                    ActionId = "flow.wait"
                }
            }
        };

        ScenarioActionData clone = ScenarioBlockIdentity.ClonePreservingIds(source);

        Assert.That(clone, Is.Not.SameAs(source));
        Assert.That(clone.BlockId, Is.EqualTo(source.BlockId));
        Assert.That(clone.DesignerLabel, Is.EqualTo(source.DesignerLabel));
        Assert.That(clone.ParametersJson, Is.EqualTo(source.ParametersJson));
        Assert.That(clone.Note, Is.EqualTo(source.Note));
        Assert.That(clone.Disabled, Is.True);
        Assert.That(clone.Children[0], Is.Not.SameAs(source.Children[0]));
        Assert.That(clone.Children[0].BlockId, Is.EqualTo(source.Children[0].BlockId));
    }

    [Test]
    public void CloneWithNewIdsCreatesIndependentIdentityForTheEntireSubtree()
    {
        var source = new ScenarioActionData
        {
            BlockId = "11111111111111111111111111111111",
            ActionId = "flow.parallel",
            Children = new List<ScenarioActionData>
            {
                new ScenarioActionData
                {
                    BlockId = "22222222222222222222222222222222",
                    ActionId = "flow.wait"
                }
            }
        };

        ScenarioActionData clone = ScenarioBlockIdentity.CloneWithNewIds(source);

        Assert.That(clone.BlockId, Is.Not.Empty.And.Not.EqualTo(source.BlockId));
        Assert.That(clone.Children[0].BlockId, Is.Not.Empty.And.Not.EqualTo(source.Children[0].BlockId));
        Assert.That(clone.BlockId, Is.Not.EqualTo(clone.Children[0].BlockId));
        Assert.That(Guid.TryParseExact(clone.BlockId, "N", out _), Is.True);
        Assert.That(Guid.TryParseExact(clone.Children[0].BlockId, "N", out _), Is.True);
    }

    [Test]
    public void EnsureUniqueWithSeedProducesStableIdsForEquivalentLegacyTrees()
    {
        List<ScenarioActionData> first = MakeLegacyTree();
        List<ScenarioActionData> second = MakeLegacyTree();

        ScenarioBlockIdentity.EnsureUnique(first, "legacy.sequence");
        ScenarioBlockIdentity.EnsureUnique(second, "legacy.sequence");

        Assert.That(CollectIds(first), Is.EqualTo(CollectIds(second)));
        Assert.That(CollectIds(first), Has.All.Matches<string>(id => Guid.TryParseExact(id, "N", out _)));
    }

    private static List<ScenarioActionData> MakeLegacyTree()
    {
        return new List<ScenarioActionData>
        {
            new ScenarioActionData
            {
                ActionId = "flow.parallel",
                Children = new List<ScenarioActionData>
                {
                    new ScenarioActionData { ActionId = "flow.wait" },
                    new ScenarioActionData { ActionId = "screen.fade" }
                }
            }
        };
    }

    private static List<string> CollectIds(List<ScenarioActionData> actions)
    {
        var ids = new List<string>();
        CollectIds(actions, ids);
        return ids;
    }

    private static void CollectIds(List<ScenarioActionData> actions, List<string> ids)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            ids.Add(action.BlockId);
            CollectIds(action.Children, ids);
        }
    }
}
