public interface IScenarioTriggerHistory
{
    bool HasRuleFired(string ruleKey, ScenarioTriggerOnceScope scope);

    void MarkRuleFired(string ruleKey, ScenarioTriggerOnceScope scope);
}
