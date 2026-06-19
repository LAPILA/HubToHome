using NUnit.Framework;

public class BattleEventRuleEvaluatorTests
{
    [Test]
    public void HpCrossedBelowRuleFiresOncePerBattle()
    {
        BattleEventRuleData rule = MakeHpRule("zev", 0.5f, "zev_phase2");
        var session = new BattleScenarioSession("zev_first_battle");

        bool first = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill),
            session,
            out BattleScenarioTrigger trigger);
        bool second = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.49f, 0.40f, BattleRuleTiming.AfterCurrentSkill),
            session,
            out BattleScenarioTrigger secondTrigger);

        Assert.That(first, Is.True);
        Assert.That(trigger.RuleId, Is.EqualTo("enter_phase2"));
        Assert.That(trigger.SequenceId, Is.EqualTo("zev_phase2"));
        Assert.That(second, Is.False);
        Assert.That(secondTrigger, Is.Null);
    }

    [Test]
    public void HpThresholdRequiresCrossingFromAbove()
    {
        BattleEventRuleData rule = MakeHpRule("zev", 0.5f, "zev_phase2");
        var session = new BattleScenarioSession("zev_first_battle");

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.49f, 0.40f, BattleRuleTiming.AfterCurrentSkill),
            session,
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.False);
        Assert.That(trigger, Is.Null);
    }

    [Test]
    public void TimingMustMatchTheAuthoredRule()
    {
        BattleEventRuleData rule = MakeHpRule("zev", 0.5f, "zev_phase2");
        var session = new BattleScenarioSession("zev_first_battle");

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.Immediate),
            session,
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.False);
        Assert.That(trigger, Is.Null);
    }

    [Test]
    public void AlwaysRulesCanFireRepeatedly()
    {
        BattleEventRuleData rule = MakeHpRule("zev", 0.5f, "low_hp_warning");
        rule.Once = BattleRuleOnceMode.Always;
        var session = new BattleScenarioSession("zev_first_battle");

        bool first = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill),
            session,
            out BattleScenarioTrigger firstTrigger);
        bool second = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.52f, 0.48f, BattleRuleTiming.AfterCurrentSkill),
            session,
            out BattleScenarioTrigger secondTrigger);

        Assert.That(first, Is.True);
        Assert.That(second, Is.True);
        Assert.That(firstTrigger.SequenceId, Is.EqualTo("low_hp_warning"));
        Assert.That(secondTrigger.SequenceId, Is.EqualTo("low_hp_warning"));
    }

    [Test]
    public void EncounterMemoryRulesCanBeImportedAndExported()
    {
        BattleEventRuleData rule = MakeHpRule("zev", 0.5f, "zev_phase2");
        rule.Once = BattleRuleOnceMode.PerEncounterMemory;

        var freshSession = new BattleScenarioSession("zev_first_battle", "zev");
        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill),
            freshSession,
            out BattleScenarioTrigger trigger);

        var rememberedSession = new BattleScenarioSession("zev_first_battle", "zev");
        rememberedSession.ImportEncounterFiredRuleIds(freshSession.ExportEncounterFiredRuleIds());
        bool remembered = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyHpCrossedBelow("zev", 0.51f, 0.49f, BattleRuleTiming.AfterCurrentSkill),
            rememberedSession,
            out BattleScenarioTrigger rememberedTrigger);

        Assert.That(fired, Is.True);
        Assert.That(trigger.SequenceId, Is.EqualTo("zev_phase2"));
        Assert.That(freshSession.ExportEncounterFiredRuleIds(), Does.Contain("enter_phase2"));
        Assert.That(remembered, Is.False);
        Assert.That(rememberedTrigger, Is.Null);
    }

    [Test]
    public void GameModuleCompletedRuleMatchesModuleAndOutcome()
    {
        BattleEventRuleData rule = MakeModuleRule("aim_shooter", "victory", "after_shooter_victory");
        var session = new BattleScenarioSession("zev_first_battle");

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.GameModuleCompleted("aim_shooter", "victory", BattleRuleTiming.AfterCurrentModule),
            session,
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.True);
        Assert.That(trigger.RuleId, Is.EqualTo("module_completed"));
        Assert.That(trigger.SequenceId, Is.EqualTo("after_shooter_victory"));
        Assert.That(trigger.SourceEvent.ModuleId, Is.EqualTo("aim_shooter"));
        Assert.That(trigger.SourceEvent.OutcomeId, Is.EqualTo("victory"));
    }

    [Test]
    public void GameModuleCompletedRuleRejectsWrongOutcome()
    {
        BattleEventRuleData rule = MakeModuleRule("aim_shooter", "victory", "after_shooter_victory");
        var session = new BattleScenarioSession("zev_first_battle");

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.GameModuleCompleted("aim_shooter", "timeout", BattleRuleTiming.AfterCurrentModule),
            session,
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.False);
        Assert.That(trigger, Is.Null);
    }

    private static BattleEventRuleData MakeHpRule(string enemyId, float threshold, string sequenceId)
    {
        return new BattleEventRuleData
        {
            RuleId = "enter_phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = enemyId,
            ThresholdRatio = threshold,
            SequenceId = sequenceId
        };
    }

    private static BattleEventRuleData MakeModuleRule(string moduleId, string outcomeId, string sequenceId)
    {
        return new BattleEventRuleData
        {
            RuleId = "module_completed",
            EventType = BattleEventType.GameModuleCompleted,
            Timing = BattleRuleTiming.AfterCurrentModule,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = moduleId,
            OutcomeId = outcomeId,
            SequenceId = sequenceId
        };
    }
}
