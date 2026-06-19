using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleScenarioRuleRunnerTests
{
    [Test]
    public void EvaluateReturnsMatchingTriggersInRuleOrder()
    {
        BattleScenarioData scenario = MakeScenario();
        var runner = new BattleScenarioRuleRunner(
            scenario,
            new BattleScenarioSession(scenario.ScenarioId, scenario.MemoryKey));

        List<BattleScenarioTrigger> triggers = runner.Evaluate(
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill));

        Assert.That(triggers.Count, Is.EqualTo(2));
        Assert.That(triggers[0].RuleId, Is.EqualTo("enter_phase2"));
        Assert.That(triggers[0].SequenceId, Is.EqualTo("zev_phase2"));
        Assert.That(triggers[1].RuleId, Is.EqualTo("show_low_hp_warning"));
        Assert.That(triggers[1].SequenceId, Is.EqualTo("low_hp_warning"));

        DestroyScenario(scenario);
    }

    [Test]
    public void TryResolveSequenceFindsScenarioSequenceById()
    {
        BattleScenarioData scenario = MakeScenario();
        var runner = new BattleScenarioRuleRunner(
            scenario,
            new BattleScenarioSession(scenario.ScenarioId, scenario.MemoryKey));

        bool found = runner.TryResolveSequence("zev_phase2", out ActionSequenceAsset sequence);

        Assert.That(found, Is.True);
        Assert.That(sequence.SequenceId, Is.EqualTo("zev_phase2"));

        DestroyScenario(scenario);
    }

    private static BattleScenarioData MakeScenario()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.MemoryKey = "zev";
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "enter_phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "zev_phase2"
        });
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "show_low_hp_warning",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.Always,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "low_hp_warning"
        });
        scenario.Sequences.Add(MakeSequence("zev_phase2"));
        scenario.Sequences.Add(MakeSequence("low_hp_warning"));
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
