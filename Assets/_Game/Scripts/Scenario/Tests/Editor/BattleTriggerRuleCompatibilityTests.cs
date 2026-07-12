using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class BattleTriggerRuleCompatibilityTests
{
    [Test]
    public void MapsBattleStartedRuleToStableEventContract()
    {
        BattleEventRuleData legacy = Legacy(BattleEventType.BattleStarted, BattleRuleTiming.Immediate);

        bool success = BattleTriggerRuleCompatibilityMapper.TryMap(legacy, out ScenarioTriggerRuleData mapped, out string error);

        Assert.That(success, Is.True, error);
        Assert.That(mapped.EventId, Is.EqualTo(BuiltInScenarioEventIds.BattleStarted));
        Assert.That(mapped.Timing, Is.EqualTo(ScenarioTriggerTiming.Immediate));
        Assert.That(mapped.Once, Is.EqualTo(ScenarioTriggerOnceScope.Session));
    }

    [Test]
    public void MapsHpRuleToParticipantAndCrossingConditions()
    {
        BattleEventRuleData legacy = Legacy(BattleEventType.EnemyHpCrossedBelow, BattleRuleTiming.AfterCurrentSkill);
        legacy.SubjectId = "zev";
        legacy.ThresholdRatio = 0.5f;

        BattleTriggerRuleCompatibilityMapper.TryMap(legacy, out ScenarioTriggerRuleData mapped, out _);

        Assert.That(mapped.EventId, Is.EqualTo(BuiltInScenarioEventIds.ParticipantHpChanged));
        Assert.That(mapped.Conditions.Children, Has.Count.EqualTo(2));
        Assert.That(mapped.Conditions.Children[0].ConditionId, Is.EqualTo(BuiltInTriggerConditionIds.EventParticipant));
        Assert.That(mapped.Conditions.Children[1].ConditionId, Is.EqualTo(BuiltInTriggerConditionIds.NumberCrossedBelow));
        Assert.That(JObject.Parse(mapped.Conditions.Children[1].ParametersJson).Value<double>("threshold"), Is.EqualTo(0.5d));
    }

    [Test]
    public void MapsEveryLegacyEventType()
    {
        var expected = new Dictionary<BattleEventType, string>
        {
            [BattleEventType.BattleStarted] = BuiltInScenarioEventIds.BattleStarted,
            [BattleEventType.EnemyHpCrossedBelow] = BuiltInScenarioEventIds.ParticipantHpChanged,
            [BattleEventType.EnemyDefeated] = BuiltInScenarioEventIds.ParticipantDefeated,
            [BattleEventType.SkillCompleted] = BuiltInScenarioEventIds.SkillCompleted,
            [BattleEventType.GameModuleCompleted] = BuiltInScenarioEventIds.ModuleCompleted
        };

        foreach (KeyValuePair<BattleEventType, string> pair in expected)
        {
            bool success = BattleTriggerRuleCompatibilityMapper.TryMap(
                Legacy(pair.Key, DefaultTiming(pair.Key)),
                out ScenarioTriggerRuleData mapped,
                out string error);
            Assert.That(success, Is.True, pair.Key + ": " + error);
            Assert.That(mapped.EventId, Is.EqualTo(pair.Value), pair.Key.ToString());
        }
    }

    [Test]
    public void EnemyDefeatedLegacyRuleNowEvaluatesThroughGenericEngine()
    {
        BattleEventRuleData rule = Legacy(BattleEventType.EnemyDefeated, BattleRuleTiming.AfterCurrentAction);
        rule.SubjectId = "zev";

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.EnemyDefeated("zev", "player"),
            new BattleScenarioSession(),
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.True);
        Assert.That(trigger.ScenarioEvent.EventId, Is.EqualTo(BuiltInScenarioEventIds.ParticipantDefeated));
        Assert.That(trigger.SourceEvent.SourceActorId, Is.EqualTo("player"));
    }

    [Test]
    public void SkillCompletedLegacyRuleMatchesSkillId()
    {
        BattleEventRuleData rule = Legacy(BattleEventType.SkillCompleted, BattleRuleTiming.AfterCurrentSkill);
        rule.SubjectId = "player.slash";

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.SkillCompleted("player.slash", "player"),
            new BattleScenarioSession(),
            out BattleScenarioTrigger trigger);

        Assert.That(fired, Is.True);
        Assert.That(trigger.ScenarioEvent.EventId, Is.EqualTo(BuiltInScenarioEventIds.SkillCompleted));
    }

    [Test]
    public void ModuleRuleWithEmptyOutcomeAcceptsAnyOutcome()
    {
        BattleEventRuleData rule = Legacy(BattleEventType.GameModuleCompleted, BattleRuleTiming.AfterCurrentModule);
        rule.SubjectId = "aim_shooter";

        bool fired = BattleEventRuleEvaluator.TryEvaluate(
            rule,
            BattleEventData.GameModuleCompleted("aim_shooter", "timeout"),
            new BattleScenarioSession(),
            out _);

        Assert.That(fired, Is.True);
    }

    [Test]
    public void NativeRuleResolvesEventPayloadIntoTargetInputs()
    {
        var rule = NativeRule(BuiltInScenarioEventIds.ParticipantHpChanged, ScenarioTriggerTiming.Immediate);
        rule.TargetInputsJson = new JObject
        {
            ["enemy"] = ScenarioValueBinding.Create("event.subject"),
            ["ratio"] = ScenarioValueBinding.Create("event.currentRatio")
        }.ToString(Formatting.None);
        var scenarioEvent = new ScenarioEventData(BuiltInScenarioEventIds.ParticipantHpChanged);
        scenarioEvent.SetPayloadValue("subject", "zev");
        scenarioEvent.SetPayloadValue("currentRatio", 0.45f);

        bool fired = new ScenarioTriggerEvaluator().TryEvaluate(
            rule,
            scenarioEvent,
            new BattleScenarioSession(),
            null,
            out ScenarioTriggerMatch match,
            out string error);

        Assert.That(fired, Is.True, error);
        JObject inputs = JObject.Parse(match.TargetInputsJson);
        Assert.That(inputs.Value<string>("enemy"), Is.EqualTo("zev"));
        Assert.That(inputs.Value<float>("ratio"), Is.EqualTo(0.45f));
    }

    [Test]
    public void NativeDeferredRuleQueuesAtAuthoredTimingIndependentOfEmitterHint()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.TriggerRules.Add(NativeRule(
            BuiltInScenarioEventIds.ParticipantHpChanged,
            ScenarioTriggerTiming.AfterCurrentSkill));
        var router = new BattleScenarioEventRouter(new BattleScenarioRuleRunner(
            scenario,
            new BattleScenarioSession()));

        List<BattleScenarioTrigger> immediate = router.Publish(
            BattleEventData.EnemyHpCrossedBelow("zev", 0.6f, 0.4f, BattleRuleTiming.Immediate));
        List<BattleScenarioTrigger> flushed = router.Flush(BattleRuleTiming.AfterCurrentSkill);

        Assert.That(immediate, Is.Empty);
        Assert.That(flushed, Has.Count.EqualTo(1));
        Object.DestroyImmediate(scenario);
    }

    [Test]
    public void NamedCheckpointOnlyReleasesMatchingTriggers()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ScenarioTriggerRuleData rule = NativeRule(
            BuiltInScenarioEventIds.BattleStarted,
            ScenarioTriggerTiming.Checkpoint);
        rule.CheckpointId = "skill.finished";
        scenario.TriggerRules.Add(rule);
        var router = new BattleScenarioEventRouter(new BattleScenarioRuleRunner(
            scenario,
            new BattleScenarioSession()));

        List<BattleScenarioTrigger> published = router.Publish(BattleEventData.BattleStarted());
        List<BattleScenarioTrigger> wrong = router.FlushCheckpoint("action.finished");
        List<BattleScenarioTrigger> correct = router.FlushCheckpoint("skill.finished");

        Assert.That(published, Is.Empty);
        Assert.That(wrong, Is.Empty);
        Assert.That(correct, Has.Count.EqualTo(1));
        Assert.That(correct[0].CheckpointId, Is.EqualTo("skill.finished"));
        Object.DestroyImmediate(scenario);
    }

    [Test]
    public void DeferredEncounterRuleIsRememberedOnlyWhenFlushed()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ScenarioTriggerRuleData rule = NativeRule(
            BuiltInScenarioEventIds.ParticipantHpChanged,
            ScenarioTriggerTiming.AfterCurrentSkill);
        rule.Once = ScenarioTriggerOnceScope.EncounterMemory;
        scenario.TriggerRules.Add(rule);
        var session = new BattleScenarioSession();
        var router = new BattleScenarioEventRouter(new BattleScenarioRuleRunner(scenario, session));

        router.Publish(BattleEventData.EnemyHpCrossedBelow(
            "zev",
            0.6f,
            0.4f,
            BattleRuleTiming.AfterCurrentSkill));
        string[] beforeFlush = session.ExportEncounterFiredRuleIds();
        List<BattleScenarioTrigger> flushed = router.Flush(BattleRuleTiming.AfterCurrentSkill);

        Assert.That(beforeFlush, Is.Empty);
        Assert.That(flushed, Has.Count.EqualTo(1));
        Assert.That(session.ExportEncounterFiredRuleIds(), Does.Contain(rule.RuleId));
        Object.DestroyImmediate(scenario);
    }

    [Test]
    public void RepeatedDeferredEventsDispatchSessionRuleOnce()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ScenarioTriggerRuleData rule = NativeRule(
            BuiltInScenarioEventIds.ParticipantHpChanged,
            ScenarioTriggerTiming.AfterCurrentSkill);
        scenario.TriggerRules.Add(rule);
        var router = new BattleScenarioEventRouter(new BattleScenarioRuleRunner(
            scenario,
            new BattleScenarioSession()));

        router.Publish(BattleEventData.EnemyHpCrossedBelow(
            "zev", 0.6f, 0.4f, BattleRuleTiming.AfterCurrentSkill));
        router.Publish(BattleEventData.EnemyHpCrossedBelow(
            "zev", 0.7f, 0.3f, BattleRuleTiming.AfterCurrentSkill));
        List<BattleScenarioTrigger> flushed = router.Flush(BattleRuleTiming.AfterCurrentSkill);

        Assert.That(flushed, Has.Count.EqualTo(1));
        Object.DestroyImmediate(scenario);
    }

    [Test]
    public void EncounterMemoryOnceScopeImportsAndSuppressesRule()
    {
        ScenarioTriggerRuleData rule = NativeRule(BuiltInScenarioEventIds.BattleStarted, ScenarioTriggerTiming.Immediate);
        rule.Once = ScenarioTriggerOnceScope.EncounterMemory;
        var first = new BattleScenarioSession();
        var evaluator = new ScenarioTriggerEvaluator();

        bool fired = evaluator.TryEvaluate(
            rule,
            BattleEventData.BattleStarted().ToScenarioEvent(),
            first,
            null,
            out _,
            out _);
        var remembered = new BattleScenarioSession();
        remembered.ImportEncounterFiredRuleIds(first.ExportEncounterFiredRuleIds());
        bool repeated = evaluator.TryEvaluate(
            rule,
            BattleEventData.BattleStarted().ToScenarioEvent(),
            remembered,
            null,
            out _,
            out _);

        Assert.That(fired, Is.True);
        Assert.That(repeated, Is.False);
    }

    [Test]
    public void SaveOnceScopeCanBeImportedWithoutSavingBattleState()
    {
        ScenarioTriggerRuleData rule = NativeRule(BuiltInScenarioEventIds.BattleStarted, ScenarioTriggerTiming.Immediate);
        rule.Once = ScenarioTriggerOnceScope.Save;
        var first = new BattleScenarioSession();
        var evaluator = new ScenarioTriggerEvaluator();

        evaluator.TryEvaluate(
            rule,
            BattleEventData.BattleStarted().ToScenarioEvent(),
            first,
            null,
            out _,
            out _);
        var loaded = new BattleScenarioSession();
        loaded.ImportSaveFiredRuleIds(first.ExportSaveFiredRuleIds());
        bool repeated = evaluator.TryEvaluate(
            rule,
            BattleEventData.BattleStarted().ToScenarioEvent(),
            loaded,
            null,
            out _,
            out _);

        Assert.That(repeated, Is.False);
        Assert.That(first.ExportSaveFiredRuleIds(), Does.Contain(rule.RuleId));
    }

    [Test]
    public void LegacyRulesAreMappedOnceWhenRunnerIsConstructed()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        BattleEventRuleData legacy = Legacy(BattleEventType.BattleStarted, BattleRuleTiming.Immediate);
        scenario.Rules.Add(legacy);
        var runner = new BattleScenarioRuleRunner(scenario, new BattleScenarioSession());
        legacy.EventType = BattleEventType.EnemyDefeated;

        List<BattleScenarioTrigger> triggers = runner.Evaluate(BattleEventData.BattleStarted());

        Assert.That(runner.ResolvedRuleCount, Is.EqualTo(1));
        Assert.That(triggers, Has.Count.EqualTo(1));
        Object.DestroyImmediate(scenario);
    }

    [Test]
    public void LegacyAndNativeRulesRunThroughOneOrderedRunner()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        BattleEventRuleData legacy = Legacy(BattleEventType.BattleStarted, BattleRuleTiming.Immediate);
        legacy.RuleId = "legacy";
        legacy.Once = BattleRuleOnceMode.Always;
        scenario.Rules.Add(legacy);
        ScenarioTriggerRuleData native = NativeRule(BuiltInScenarioEventIds.BattleStarted, ScenarioTriggerTiming.Immediate);
        native.RuleId = "native";
        native.Once = ScenarioTriggerOnceScope.Always;
        scenario.TriggerRules.Add(native);
        var runner = new BattleScenarioRuleRunner(scenario, new BattleScenarioSession());

        List<BattleScenarioTrigger> triggers = runner.Evaluate(BattleEventData.BattleStarted());

        Assert.That(triggers.ConvertAll(item => item.RuleId), Is.EqualTo(new[] { "legacy", "native" }));
        Object.DestroyImmediate(scenario);
    }

    private static BattleEventRuleData Legacy(BattleEventType type, BattleRuleTiming timing)
    {
        return new BattleEventRuleData
        {
            RuleId = "legacy_rule",
            EventType = type,
            Timing = timing,
            Once = BattleRuleOnceMode.PerBattle,
            SequenceId = "target_sequence"
        };
    }

    private static ScenarioTriggerRuleData NativeRule(string eventId, ScenarioTriggerTiming timing)
    {
        return new ScenarioTriggerRuleData
        {
            RuleId = "native_rule",
            EventId = eventId,
            Timing = timing,
            Once = ScenarioTriggerOnceScope.Session,
            SequenceId = "target_sequence",
            Conditions = new ScenarioTriggerConditionNodeData
            {
                Kind = ScenarioConditionNodeKind.Group,
                GroupMode = ScenarioConditionGroupMode.All
            }
        };
    }

    private static BattleRuleTiming DefaultTiming(BattleEventType type)
    {
        switch (type)
        {
            case BattleEventType.BattleStarted: return BattleRuleTiming.Immediate;
            case BattleEventType.EnemyDefeated: return BattleRuleTiming.AfterCurrentAction;
            case BattleEventType.GameModuleCompleted: return BattleRuleTiming.AfterCurrentModule;
            default: return BattleRuleTiming.AfterCurrentSkill;
        }
    }
}
