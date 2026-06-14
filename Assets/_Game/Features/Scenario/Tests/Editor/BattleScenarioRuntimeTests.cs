using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleScenarioRuntimeTests
{
    [Test]
    public void DeferredHpCrossingTriggersWhenMatchingTimingIsFlushed()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.AfterCurrentSkill);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.AfterCurrentSkill);
            List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.AfterCurrentSkill);

            Assert.That(runtime.HasScenario, Is.True);
            Assert.That(immediate.Count, Is.EqualTo(0));
            Assert.That(flushed.Count, Is.EqualTo(1));
            Assert.That(flushed[0].RuleId, Is.EqualTo("enter_phase2"));
            Assert.That(flushed[0].SequenceId, Is.EqualTo("zev_phase2"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void ImmediateHpCrossingTriggersWithoutQueueing()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.Immediate);
            List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.Immediate);

            Assert.That(immediate.Count, Is.EqualTo(1));
            Assert.That(immediate[0].RuleId, Is.EqualTo("enter_phase2"));
            Assert.That(immediate[0].SequenceId, Is.EqualTo("zev_phase2"));
            Assert.That(flushed.Count, Is.EqualTo(0));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void InvalidMaxHpDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                0,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void WrongSubjectDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "other_enemy",
                51,
                49,
                100,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void AlreadyBelowThresholdDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                49,
                40,
                100,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void TryResolveSequenceReturnsFalseWhenMissing()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            bool found = runtime.TryResolveSequence("missing_sequence", out ActionSequenceAsset sequence);

            Assert.That(found, Is.False);
            Assert.That(sequence, Is.Null);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void NullScenarioIsSafeNoOp()
    {
        var runtime = new BattleScenarioRuntime(null);

        List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
            "zev",
            51,
            49,
            100,
            BattleRuleTiming.Immediate);
        List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.Immediate);
        bool found = runtime.TryResolveSequence("zev_phase2", out ActionSequenceAsset sequence);

        Assert.That(runtime.HasScenario, Is.False);
        Assert.That(immediate, Is.Empty);
        Assert.That(flushed, Is.Empty);
        Assert.That(found, Is.False);
        Assert.That(sequence, Is.Null);
    }

    [Test]
    public void CreatesBattleSessionStateFromScenarioOpeningModule()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        scenario.OpeningModule = "aim_shooter";
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            Assert.That(runtime.SessionState.ScenarioId, Is.EqualTo("zev_first_battle"));
            Assert.That(runtime.SessionState.PrimaryMode, Is.EqualTo("battle"));
            Assert.That(runtime.SessionState.OpeningModule, Is.EqualTo("aim_shooter"));
            Assert.That(runtime.SessionState.CurrentModuleId, Is.EqualTo("aim_shooter"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void NullScenarioSessionStateDefaultsToTurnQte()
    {
        var runtime = new BattleScenarioRuntime(null);

        Assert.That(runtime.SessionState.PrimaryMode, Is.EqualTo("battle"));
        Assert.That(runtime.SessionState.OpeningModule, Is.EqualTo(BattleTurnQteGameModuleRuntime.Id));
        Assert.That(runtime.SessionState.CurrentModuleId, Is.EqualTo(BattleTurnQteGameModuleRuntime.Id));
    }

    private static BattleScenarioData MakeScenario(BattleRuleTiming timing)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.MemoryKey = "zev";
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "enter_phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = timing,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "zev_phase2"
        });
        scenario.Sequences.Add(MakeSequence("zev_phase2"));
        return scenario;
    }

    private static ActionSequenceAsset MakeSequence(string sequenceId)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        return sequence;
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
        }

        UnityEngine.Object.DestroyImmediate(scenario);
    }
}
