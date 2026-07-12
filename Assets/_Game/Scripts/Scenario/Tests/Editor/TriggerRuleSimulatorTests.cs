using NUnit.Framework;

public class TriggerRuleSimulatorTests
{
    [Test]
    public void HpCrossedBelowMatchesAndPreservesDeferredTiming()
    {
        ScenarioTriggerRuleData rule = Rule(
            "participant.hp_changed",
            Condition(
                "number.crossed_below",
                "{\"previousPath\":\"event.previousRatio\",\"currentPath\":\"event.currentRatio\",\"threshold\":0.5}"));
        rule.Timing = ScenarioTriggerTiming.AfterCurrentSkill;

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            rule,
            Request("{\"previousRatio\":0.7,\"currentRatio\":0.49}"));

        Assert.That(result.Status, Is.EqualTo(TriggerRuleSimulationStatus.Matched), result.Message);
        Assert.That(result.Message, Does.Contain("현재 스킬 종료 후"));
        Assert.That(result.Traces, Has.Count.EqualTo(1));
        Assert.That(result.Traces[0].Matched, Is.True);
    }

    [Test]
    public void HpThatWasAlreadyBelowDoesNotCrossThresholdAgain()
    {
        ScenarioTriggerRuleData rule = Rule(
            "participant.hp_changed",
            Condition(
                "number.crossed_below",
                "{\"previousPath\":\"event.previousRatio\",\"currentPath\":\"event.currentRatio\",\"threshold\":0.5}"));

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            rule,
            Request("{\"previousRatio\":0.45,\"currentRatio\":0.3}"));

        Assert.That(result.Status, Is.EqualTo(TriggerRuleSimulationStatus.NotMatched));
    }

    [Test]
    public void EncounterMeetCountReadsNestedContextValues()
    {
        ScenarioTriggerRuleData rule = Rule(
            "battle.started",
            Condition("memory.meet_count", "{\"operator\":\"equal\",\"value\":2}"));
        TriggerRuleSimulationRequest request = Request("{}");
        request.ContextValuesJson = "{\"memory\":{\"meetCount\":2}}";

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(rule, request);

        Assert.That(result.Matched, Is.True, result.Message);
    }

    [Test]
    public void ModuleOutcomeAndAnyGroupCanMatchOneBranch()
    {
        ScenarioTriggerRuleData rule = Rule(
            "module.completed",
            Group(
                ScenarioConditionGroupMode.Any,
                Condition("module.outcome", "{\"module\":\"aim_shooter\",\"outcome\":\"victory\"}"),
                Condition("event.participant", "{\"participant\":\"other\"}")));

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            rule,
            Request("{\"module\":\"aim_shooter\",\"outcome\":\"victory\"}"));

        Assert.That(result.Matched, Is.True, result.Message);
    }

    [Test]
    public void MissingPayloadSafelyReportsConditionMismatch()
    {
        ScenarioTriggerRuleData rule = Rule(
            "participant.hp_changed",
            Condition("event.participant", "{\"participant\":\"zev\"}"));

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            rule,
            Request("{}"));

        Assert.That(result.Status, Is.EqualTo(TriggerRuleSimulationStatus.NotMatched));
        Assert.That(result.Traces, Has.Count.EqualTo(1));
        Assert.That(result.Traces[0].Matched, Is.False);
    }

    [Test]
    public void OnceHistoryCanExplainWhyMatchingRuleWillNotRunAgain()
    {
        ScenarioTriggerRuleData rule = Rule(
            "battle.started",
            Group(ScenarioConditionGroupMode.All));
        rule.Once = ScenarioTriggerOnceScope.Save;
        TriggerRuleSimulationRequest request = Request("{}");
        request.RuleAlreadyFired = true;

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(rule, request);

        Assert.That(result.Status, Is.EqualTo(TriggerRuleSimulationStatus.NotMatched));
        Assert.That(result.Message, Does.Contain("이미 실행"));
    }

    [Test]
    public void EventBindingInTargetInputsIsResolvedOnMatch()
    {
        ScenarioTriggerRuleData rule = Rule(
            "participant.defeated",
            Group(ScenarioConditionGroupMode.All));
        rule.TargetInputsJson = "{\"enemy\":{\"$bind\":\"event.subject\"}}";

        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            rule,
            Request("{\"subject\":\"zev\"}"));

        Assert.That(result.Matched, Is.True, result.Message);
        Assert.That(result.ResolvedTargetInputsJson, Does.Contain("zev"));
    }

    private static TriggerRuleSimulationRequest Request(string payload)
    {
        return new TriggerRuleSimulationRequest { PayloadJson = payload };
    }

    private static ScenarioTriggerRuleData Rule(
        string eventId,
        ScenarioTriggerConditionNodeData conditions)
    {
        return new ScenarioTriggerRuleData
        {
            RuleId = "rule.test",
            EventId = eventId,
            SequenceId = "sequence.test",
            Once = ScenarioTriggerOnceScope.Always,
            Conditions = conditions
        };
    }

    private static ScenarioTriggerConditionNodeData Group(
        ScenarioConditionGroupMode mode,
        params ScenarioTriggerConditionNodeData[] children)
    {
        var result = new ScenarioTriggerConditionNodeData
        {
            NodeId = ScenarioTriggerIdentity.Create(),
            Kind = ScenarioConditionNodeKind.Group,
            GroupMode = mode
        };
        result.Children.AddRange(children);
        return result;
    }

    private static ScenarioTriggerConditionNodeData Condition(string id, string json)
    {
        return new ScenarioTriggerConditionNodeData
        {
            NodeId = ScenarioTriggerIdentity.Create(),
            Kind = ScenarioConditionNodeKind.Condition,
            ConditionId = id,
            ParametersJson = json
        };
    }
}
