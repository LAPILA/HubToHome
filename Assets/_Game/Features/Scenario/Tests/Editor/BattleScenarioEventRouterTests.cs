using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleScenarioEventRouterTests
{
    [Test]
    public void DeferredEventFiresWhenMatchingTimingIsFlushed()
    {
        BattleScenarioData scenario = MakeScenario();
        var router = new BattleScenarioEventRouter(
            new BattleScenarioRuleRunner(
                scenario,
                new BattleScenarioSession(scenario.ScenarioId, scenario.MemoryKey)));

        List<BattleScenarioTrigger> immediate = router.Publish(
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill));
        List<BattleScenarioTrigger> flushed = router.Flush(BattleRuleTiming.AfterCurrentSkill);
        List<BattleScenarioTrigger> secondFlush = router.Flush(BattleRuleTiming.AfterCurrentSkill);

        Assert.That(immediate.Count, Is.EqualTo(0));
        Assert.That(flushed.Count, Is.EqualTo(1));
        Assert.That(flushed[0].RuleId, Is.EqualTo("enter_phase2"));
        Assert.That(flushed[0].SequenceId, Is.EqualTo("zev_phase2"));
        Assert.That(secondFlush.Count, Is.EqualTo(0));

        DestroyScenario(scenario);
    }

    [Test]
    public void ImmediateEventEvaluatesWithoutQueueing()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var router = new BattleScenarioEventRouter(
            new BattleScenarioRuleRunner(
                scenario,
                new BattleScenarioSession(scenario.ScenarioId, scenario.MemoryKey)));

        List<BattleScenarioTrigger> triggers = router.Publish(
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.Immediate));
        List<BattleScenarioTrigger> flushed = router.Flush(BattleRuleTiming.Immediate);

        Assert.That(triggers.Count, Is.EqualTo(1));
        Assert.That(triggers[0].SequenceId, Is.EqualTo("zev_phase2"));
        Assert.That(flushed.Count, Is.EqualTo(0));

        DestroyScenario(scenario);
    }

    private static BattleScenarioData MakeScenario(BattleRuleTiming timing = BattleRuleTiming.AfterCurrentSkill)
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
