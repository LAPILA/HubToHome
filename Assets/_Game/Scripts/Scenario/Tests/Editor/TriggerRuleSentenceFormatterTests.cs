using NUnit.Framework;

public class TriggerRuleSentenceFormatterTests
{
    private TriggerLibraryAsset _library;

    [SetUp]
    public void SetUp()
    {
        _library = UnityEngine.ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        _library.Events.Add(new ScenarioEventDefinition
        {
            EventId = "participant.hp_changed",
            DisplayNameKo = "참가자 HP 변경",
            SentenceTemplateKo = "{subject}의 HP가 {previousRatio}에서 {currentRatio}로 바뀌면",
            Payload =
            {
                new TriggerFieldDefinition { FieldId = "subject", DisplayNameKo = "참가자" },
                new TriggerFieldDefinition { FieldId = "previousRatio", DisplayNameKo = "이전 HP" },
                new TriggerFieldDefinition { FieldId = "currentRatio", DisplayNameKo = "현재 HP" }
            }
        });
        _library.Events.Add(new ScenarioEventDefinition
        {
            EventId = "battle.started",
            DisplayNameKo = "전투 시작",
            SentenceTemplateKo = "전투가 시작되면"
        });
        _library.Conditions.Add(new TriggerConditionDefinition
        {
            ConditionId = "event.participant",
            DisplayNameKo = "이벤트 참가자 일치",
            SentenceTemplateKo = "이벤트 참가자가 {participant}이고"
        });
        _library.Conditions.Add(new TriggerConditionDefinition
        {
            ConditionId = "number.crossed_below",
            DisplayNameKo = "임계치 아래로 통과",
            SentenceTemplateKo = "{previousPath}에서 {currentPath}로 바뀌며 {threshold} 아래를 통과했고"
        });
        _library.Conditions.Add(new TriggerConditionDefinition
        {
            ConditionId = "memory.meet_count",
            DisplayNameKo = "만남 횟수 비교",
            SentenceTemplateKo = "이 만남 횟수가 {operator} {value}이고"
        });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_library);
    }

    [Test]
    public void HpCrossingRuleReadsAsOneNaturalWhenDoSentence()
    {
        ScenarioTriggerRuleData rule = Rule(
            "participant.hp_changed",
            Group(
                ScenarioConditionGroupMode.All,
                Condition("event.participant", "{\"participant\":\"zev\"}"),
                Condition("number.crossed_below", "{\"previousPath\":\"event.previousRatio\",\"currentPath\":\"event.currentRatio\",\"threshold\":0.5}")));
        rule.SequenceId = "zev.phase_two";
        rule.Timing = ScenarioTriggerTiming.AfterCurrentSkill;
        rule.Once = ScenarioTriggerOnceScope.EncounterMemory;

        string sentence = TriggerRuleSentenceFormatter.Format(
            rule,
            _library,
            id => id == "zev.phase_two" ? "ZEV 2페이즈" : id);

        Assert.That(sentence, Does.Contain("참가자 HP 변경"));
        Assert.That(sentence, Does.Contain("zev"));
        Assert.That(sentence, Does.Contain("0.5"));
        Assert.That(sentence, Does.Contain("ZEV 2페이즈 시퀀스 실행"));
        Assert.That(sentence, Does.Contain("현재 스킬 종료 후"));
        Assert.That(sentence, Does.Contain("이 만남에서 한 번"));
    }

    [Test]
    public void AnyGroupAndNegationRemainVisibleInSentence()
    {
        ScenarioTriggerConditionNodeData first = Condition(
            "event.participant",
            "{\"participant\":\"zev\"}");
        first.Negate = true;
        ScenarioTriggerRuleData rule = Rule(
            "participant.hp_changed",
            Group(
                ScenarioConditionGroupMode.Any,
                first,
                Condition("memory.meet_count", "{\"operator\":\"greater_or_equal\",\"value\":2}")));

        string sentence = TriggerRuleSentenceFormatter.Format(rule, _library);

        Assert.That(sentence, Does.Contain(" 또는 "));
        Assert.That(sentence, Does.Contain("아님"));
        Assert.That(sentence, Does.Contain("이상"));
    }

    [Test]
    public void EmptyConditionUsesEventSentenceTemplate()
    {
        ScenarioTriggerRuleData rule = Rule(
            "battle.started",
            Group(ScenarioConditionGroupMode.All));

        string sentence = TriggerRuleSentenceFormatter.Format(rule, _library);

        Assert.That(sentence, Does.StartWith("전투가 시작되면"));
        Assert.That(sentence, Does.Contain("현재 실행에서 한 번"));
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
