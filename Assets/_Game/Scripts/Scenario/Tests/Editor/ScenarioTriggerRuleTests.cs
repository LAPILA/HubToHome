using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class ScenarioTriggerRuleTests
{
    [Test]
    public void BattleScenarioKeepsLegacyAndExtensibleRulesSideBySide()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        try
        {
            scenario.Rules.Add(new BattleEventRuleData { RuleId = "legacy" });
            scenario.TriggerRules.Add(new ScenarioTriggerRuleData { RuleId = "extensible" });

            Assert.That(scenario.Rules, Has.Count.EqualTo(1));
            Assert.That(scenario.TriggerRules, Has.Count.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void TriggerRulePreservesSchedulingAndTargetInputContract()
    {
        var rule = new ScenarioTriggerRuleData
        {
            RuleId = "zev.phase-two",
            EventId = "participant.hp_changed",
            Timing = ScenarioTriggerTiming.Checkpoint,
            CheckpointId = "skill.finished",
            Once = ScenarioTriggerOnceScope.Save,
            Disabled = true,
            SequenceId = "zev.phase-two.sequence",
            TargetInputsJson = "{\"enemy\":\"zev\",\"module\":\"aim_shooter\"}"
        };

        Assert.That(rule.Timing, Is.EqualTo(ScenarioTriggerTiming.Checkpoint));
        Assert.That(rule.CheckpointId, Is.EqualTo("skill.finished"));
        Assert.That(rule.Once, Is.EqualTo(ScenarioTriggerOnceScope.Save));
        Assert.That(rule.Disabled, Is.True);
        Assert.That(JObject.Parse(rule.TargetInputsJson)["module"]?.Value<string>(), Is.EqualTo("aim_shooter"));
    }

    [Test]
    public void ScenarioEventPayloadPreservesTypedValues()
    {
        var scenarioEvent = new ScenarioEventData("participant.hp_changed");
        scenarioEvent.SetPayloadValue("subject", "zev");
        scenarioEvent.SetPayloadValue("currentRatio", 0.45f);
        scenarioEvent.SetPayloadValue("defeated", false);

        Assert.That(scenarioEvent.TryGetPayloadValue("subject", out JToken subject), Is.True);
        Assert.That(subject.Value<string>(), Is.EqualTo("zev"));
        Assert.That(scenarioEvent.TryGetPayloadValue("currentRatio", out JToken ratio), Is.True);
        Assert.That(ratio.Value<float>(), Is.EqualTo(0.45f));
        Assert.That(scenarioEvent.TryGetPayloadValue("defeated", out JToken defeated), Is.True);
        Assert.That(defeated.Value<bool>(), Is.False);
    }

    [Test]
    public void AllGroupRequiresEveryCondition()
    {
        ScenarioTriggerConditionNodeData root = Group(
            ScenarioConditionGroupMode.All,
            Condition("value.equals", "{\"path\":\"event.subject\",\"value\":\"zev\"}"),
            Condition("number.compare", "{\"path\":\"event.currentRatio\",\"operator\":\"less\",\"value\":0.5}"));
        ScenarioTriggerEvaluationContext context = EventContext("zev", 0.4f);

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            root,
            context,
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void AnyGroupMatchesOneCondition()
    {
        ScenarioTriggerConditionNodeData root = Group(
            ScenarioConditionGroupMode.Any,
            Condition("value.equals", "{\"path\":\"event.subject\",\"value\":\"other\"}"),
            Condition("value.equals", "{\"path\":\"event.subject\",\"value\":\"zev\"}"));

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            root,
            EventContext("zev", 1f),
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void CrossedBelowRequiresPreviousAboveAndCurrentAtOrBelowThreshold()
    {
        ScenarioTriggerConditionNodeData condition = Condition(
            "number.crossed_below",
            "{\"previousPath\":\"event.previousRatio\",\"currentPath\":\"event.currentRatio\",\"threshold\":0.5}");
        ScenarioTriggerEvaluationContext context = EventContext("zev", 0.49f);
        context.Event.SetPayloadValue("previousRatio", 0.7f);

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            context,
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void ParticipantConditionMatchesEventSubject()
    {
        ScenarioTriggerConditionNodeData condition = Condition(
            "event.participant",
            "{\"participant\":\"zev\"}");

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            EventContext("zev", 1f),
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void ModuleOutcomeConditionAllowsOptionalOutcome()
    {
        var scenarioEvent = new ScenarioEventData("module.completed");
        scenarioEvent.SetPayloadValue("module", "aim_shooter");
        scenarioEvent.SetPayloadValue("outcome", "victory");
        var context = new ScenarioTriggerEvaluationContext(scenarioEvent);
        ScenarioTriggerConditionNodeData condition = Condition(
            "module.outcome",
            "{\"module\":\"aim_shooter\",\"outcome\":\"victory\"}");

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            context,
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void EncounterMeetCountReadsSaveBoundMemoryValue()
    {
        var context = new ScenarioTriggerEvaluationContext(new ScenarioEventData("encounter.started"));
        context.Values.SetValue("memory.meetCount", 2);
        ScenarioTriggerConditionNodeData condition = Condition(
            "memory.meet_count",
            "{\"operator\":\"greater_or_equal\",\"value\":2}");

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            context,
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void FlagStateReadsExplicitFlagPath()
    {
        var context = new ScenarioTriggerEvaluationContext(new ScenarioEventData("battle.checkpoint"));
        context.Values.SetValue("flag.shooter.unlocked", "phase2");
        ScenarioTriggerConditionNodeData condition = Condition(
            "flag.state",
            "{\"flag\":\"shooter.unlocked\",\"value\":\"phase2\"}");

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            context,
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.True);
    }

    [Test]
    public void NegatedConditionInvertsResult()
    {
        ScenarioTriggerConditionNodeData condition = Condition(
            "value.equals",
            "{\"path\":\"event.subject\",\"value\":\"zev\"}");
        condition.Negate = true;

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            EventContext("zev", 1f),
            out bool matched,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(matched, Is.False);
    }

    [Test]
    public void UnknownConditionFailsWithNodeId()
    {
        ScenarioTriggerConditionNodeData condition = Condition("missing.condition", "{}");
        condition.NodeId = "condition-node-7";

        bool success = TriggerConditionRegistry.CreateDefault().TryEvaluate(
            condition,
            EventContext("zev", 1f),
            out _,
            out string error);

        Assert.That(success, Is.False);
        Assert.That(error, Does.Contain("missing.condition"));
        Assert.That(error, Does.Contain("condition-node-7"));
    }

    private static ScenarioTriggerEvaluationContext EventContext(string subject, float currentRatio)
    {
        var scenarioEvent = new ScenarioEventData("participant.hp_changed");
        scenarioEvent.SetPayloadValue("subject", subject);
        scenarioEvent.SetPayloadValue("currentRatio", currentRatio);
        return new ScenarioTriggerEvaluationContext(scenarioEvent);
    }

    private static ScenarioTriggerConditionNodeData Condition(string id, string parameters)
    {
        return new ScenarioTriggerConditionNodeData
        {
            Kind = ScenarioConditionNodeKind.Condition,
            ConditionId = id,
            ParametersJson = parameters
        };
    }

    private static ScenarioTriggerConditionNodeData Group(
        ScenarioConditionGroupMode mode,
        params ScenarioTriggerConditionNodeData[] children)
    {
        var group = new ScenarioTriggerConditionNodeData
        {
            Kind = ScenarioConditionNodeKind.Group,
            GroupMode = mode
        };
        group.Children.AddRange(children);
        return group;
    }
}
