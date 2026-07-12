using NUnit.Framework;
using UnityEngine;

public class BattleScenarioEditCommandStackTests
{
    private BattleScenarioData _battle;

    [SetUp]
    public void SetUp()
    {
        _battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        _battle.ScenarioId = "battle.test";
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_battle);
    }

    [Test]
    public void AddUndoRedoTracksDirtyState()
    {
        var stack = new BattleScenarioEditCommandStack(_battle);
        ScenarioTriggerRuleData rule = ScenarioTriggerRuleFactory.Create(
            "battle.started",
            "intro");

        stack.Execute(BattleScenarioEditCommands.AddTriggerRule(rule));
        Assert.That(_battle.TriggerRules, Has.Count.EqualTo(1));
        Assert.That(stack.IsDirty, Is.True);
        Assert.That(stack.Undo(), Is.True);
        Assert.That(_battle.TriggerRules, Is.Empty);
        Assert.That(stack.IsDirty, Is.False);
        Assert.That(stack.Redo(), Is.True);
        Assert.That(_battle.TriggerRules[0].RuleId, Is.EqualTo(rule.RuleId));
    }

    [Test]
    public void ReplaceDeepCopiesRecursiveConditionTreeAndUndoes()
    {
        ScenarioTriggerRuleData original = ScenarioTriggerRuleFactory.Create();
        original.Conditions.Children.Add(new ScenarioTriggerConditionNodeData
        {
            NodeId = ScenarioTriggerIdentity.Create(),
            Kind = ScenarioConditionNodeKind.Condition,
            ConditionId = "value.equals",
            ParametersJson = "{\"path\":\"event.subject\",\"value\":\"old\"}"
        });
        _battle.TriggerRules.Add(original);
        var stack = new BattleScenarioEditCommandStack(_battle);
        ScenarioTriggerRuleData changed = ScenarioTriggerIdentity.CloneRule(original);
        changed.Conditions.Children[0].ParametersJson = "{\"path\":\"event.subject\",\"value\":\"new\"}";

        stack.Execute(BattleScenarioEditCommands.ReplaceTriggerRule(original.RuleId, changed));
        changed.Conditions.Children[0].ParametersJson = "{}";
        Assert.That(_battle.TriggerRules[0].Conditions.Children[0].ParametersJson, Does.Contain("new"));
        stack.Undo();
        Assert.That(_battle.TriggerRules[0].Conditions.Children[0].ParametersJson, Does.Contain("old"));
    }

    [Test]
    public void DeleteAndMoveAreUndoable()
    {
        ScenarioTriggerRuleData first = ScenarioTriggerRuleFactory.Create();
        ScenarioTriggerRuleData second = ScenarioTriggerRuleFactory.Create();
        _battle.TriggerRules.Add(first);
        _battle.TriggerRules.Add(second);
        var stack = new BattleScenarioEditCommandStack(_battle);

        stack.Execute(BattleScenarioEditCommands.MoveTriggerRule(second.RuleId, 0));
        Assert.That(_battle.TriggerRules[0].RuleId, Is.EqualTo(second.RuleId));
        stack.Undo();
        Assert.That(_battle.TriggerRules[0].RuleId, Is.EqualTo(first.RuleId));
        stack.Execute(BattleScenarioEditCommands.DeleteTriggerRule(first.RuleId));
        Assert.That(_battle.TriggerRules, Has.Count.EqualTo(1));
        stack.Undo();
        Assert.That(_battle.TriggerRules, Has.Count.EqualTo(2));
    }

    [Test]
    public void LegacyConversionRemovesDuplicateRuntimeRuleAndCanUndo()
    {
        var legacy = new BattleEventRuleData
        {
            RuleId = "legacy.hp",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            SubjectId = "zev",
            SequenceId = "phase.two"
        };
        _battle.Rules.Add(legacy);
        Assert.That(BattleTriggerRuleCompatibilityMapper.TryMap(
            legacy,
            out ScenarioTriggerRuleData mapped,
            out string error), Is.True, error);
        var stack = new BattleScenarioEditCommandStack(_battle);

        stack.Execute(BattleScenarioEditCommands.ConvertLegacyRule(0, mapped));
        Assert.That(_battle.Rules, Is.Empty);
        Assert.That(_battle.TriggerRules, Has.Count.EqualTo(1));
        stack.Undo();
        Assert.That(_battle.Rules, Has.Count.EqualTo(1));
        Assert.That(_battle.TriggerRules, Is.Empty);
    }

    [Test]
    public void SequenceContractCommandPreservesInputDefinitionsAcrossUndo()
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        try
        {
            sequence.Contract.Inputs.Add(new SequenceInputDefinition { InputId = "actor" });
            var stack = new SequenceEditCommandStack(sequence);
            ActionSequenceContractData changed = ActionSequenceContractData.CopyOf(sequence.Contract);
            changed.Inputs[0].InputId = "target";

            stack.Execute(SequenceEditCommands.SetSequenceContract(changed));
            changed.Inputs[0].InputId = "mutated_after_execute";
            Assert.That(sequence.Contract.Inputs[0].InputId, Is.EqualTo("target"));
            stack.Undo();
            Assert.That(sequence.Contract.Inputs[0].InputId, Is.EqualTo("actor"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }
}
