using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequenceUsageIndexTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(_created[i]);
        }

        _created.Clear();
    }

    [Test]
    public void IndexFindsLegacyAndTriggerRuleReferences()
    {
        ActionSequenceAsset target = Sequence("battle.phase2");
        BattleScenarioData battle = Battle("zev", target);
        battle.Rules.Add(new BattleEventRuleData
        {
            RuleId = "legacy",
            SequenceId = target.SequenceId
        });
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "trigger",
            SequenceId = target.SequenceId
        });
        SequenceUsageIndex usage = BuildUsage(battle, target);

        IReadOnlyList<SequenceUsageRecord> records = usage.GetUsages(target.SequenceId);

        Assert.That(records.Exists(item => item.Kind == SequenceUsageKind.LegacyBattleRule), Is.True);
        Assert.That(records.Exists(item => item.Kind == SequenceUsageKind.TriggerRule), Is.True);
        Assert.That(records.Exists(item => item.Kind == SequenceUsageKind.ScenarioOwnership), Is.True);
    }

    [Test]
    public void IndexFindsRecursiveSequenceCallsWithSourceBlockId()
    {
        ActionSequenceAsset target = Sequence("shared.camera_reset");
        ActionSequenceAsset caller = Sequence("battle.finish");
        caller.Actions.Add(new ScenarioActionData
        {
            BlockId = "group",
            ActionId = ActionDirector.ParallelActionId,
            Children =
            {
                new ScenarioActionData
                {
                    BlockId = "call-block",
                    ActionId = SequenceCallActionAdapter.Id,
                    ParametersJson = "{\"sequence\":\"shared.camera_reset\"}"
                }
            }
        });
        SequenceUsageIndex usage = BuildUsage(null, target, caller);

        SequenceUsageRecord record = usage.GetUsages(target.SequenceId)
            .Find(item => item.Kind == SequenceUsageKind.SequenceCall);

        Assert.That(record, Is.Not.Null);
        Assert.That(record.SourceSequenceId, Is.EqualTo("battle.finish"));
        Assert.That(record.SourceBlockId, Is.EqualTo("call-block"));
    }

    [Test]
    public void MissingRuleAndCallTargetsAreReportedSeparately()
    {
        ActionSequenceAsset caller = Sequence("caller");
        caller.Actions.Add(new ScenarioActionData
        {
            BlockId = "missing-call",
            ActionId = SequenceCallActionAdapter.Id,
            ParametersJson = "{\"sequence\":\"missing.called\"}"
        });
        BattleScenarioData battle = Battle("battle", caller);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "missing-rule",
            SequenceId = "missing.rule"
        });
        SequenceUsageIndex usage = BuildUsage(battle, caller);

        Assert.That(usage.MissingTargets, Has.Count.EqualTo(2));
        Assert.That(usage.MissingTargets.Exists(item => item.TargetSequenceId == "missing.called"), Is.True);
        Assert.That(usage.MissingTargets.Exists(item => item.TargetSequenceId == "missing.rule"), Is.True);
    }

    [Test]
    public void MalformedSequenceCallIsReportedAsDiagnosticWithoutInventingTarget()
    {
        ActionSequenceAsset caller = Sequence("caller");
        caller.Actions.Add(new ScenarioActionData
        {
            BlockId = "bad-call",
            ActionId = SequenceCallActionAdapter.Id,
            ParametersJson = "{bad json"
        });

        SequenceUsageIndex usage = BuildUsage(null, caller);

        Assert.That(usage.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(usage.Diagnostics[0].SourceBlockId, Is.EqualTo("bad-call"));
        Assert.That(usage.MissingTargets, Is.Empty);
    }

    [Test]
    public void RenameImpactListsEveryIdBasedReferenceButNotObjectOwnershipAsRewrite()
    {
        ActionSequenceAsset target = Sequence("old.id");
        ActionSequenceAsset caller = Sequence("caller");
        caller.Actions.Add(Call("call", "old.id"));
        BattleScenarioData battle = Battle("battle", target, caller);
        battle.Rules.Add(new BattleEventRuleData { RuleId = "rule", SequenceId = "old.id" });
        SequenceUsageIndex usage = BuildUsage(battle, target, caller);

        SequenceReferenceImpact impact = usage.GetRenameImpact("old.id");

        Assert.That(impact.IdRewriteCount, Is.EqualTo(2));
        Assert.That(impact.OwnershipCount, Is.EqualTo(1));
        Assert.That(impact.AffectedScenarioIds, Contains.Item("battle"));
        Assert.That(impact.AffectedSequenceIds, Contains.Item("caller"));
    }

    [Test]
    public void DeleteImpactIncludesOwnershipAndAllReferences()
    {
        ActionSequenceAsset target = Sequence("target");
        ActionSequenceAsset caller = Sequence("caller");
        caller.Actions.Add(Call("call", "target"));
        BattleScenarioData battle = Battle("battle", target, caller);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "rule",
            SequenceId = "target"
        });
        SequenceUsageIndex usage = BuildUsage(battle, target, caller);

        SequenceReferenceImpact impact = usage.GetDeleteImpact("target");

        Assert.That(impact.TotalReferenceCount, Is.EqualTo(3));
        Assert.That(impact.IsSafe, Is.False);
    }

    private SequenceUsageIndex BuildUsage(
        BattleScenarioData battle,
        params ActionSequenceAsset[] sequences)
    {
        SequenceAssetIndex assets = SequenceAssetIndex.Build(
            battle != null ? new[] { battle } : Array.Empty<BattleScenarioData>(),
            sequences);
        return SequenceUsageIndex.Build(assets);
    }

    private ActionSequenceAsset Sequence(string id)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.name = id;
        _created.Add(sequence);
        return sequence;
    }

    private BattleScenarioData Battle(string id, params ActionSequenceAsset[] sequences)
    {
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.ScenarioId = id;
        battle.Sequences.AddRange(sequences);
        battle.name = id;
        _created.Add(battle);
        return battle;
    }

    private static ScenarioActionData Call(string blockId, string sequenceId)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = SequenceCallActionAdapter.Id,
            ParametersJson = "{\"sequence\":\"" + sequenceId + "\"}"
        };
    }
}

internal static class SequenceUsageIndexTestExtensions
{
    public static bool Exists(
        this IReadOnlyList<SequenceUsageRecord> entries,
        Predicate<SequenceUsageRecord> predicate)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (predicate(entries[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static SequenceUsageRecord Find(
        this IReadOnlyList<SequenceUsageRecord> entries,
        Predicate<SequenceUsageRecord> predicate)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (predicate(entries[i]))
            {
                return entries[i];
            }
        }

        return null;
    }
}
