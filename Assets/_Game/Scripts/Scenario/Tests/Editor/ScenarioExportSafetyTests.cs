using NUnit.Framework;
using UnityEngine;

public class ScenarioExportSafetyTests
{
    [Test]
    public void StandaloneExport_MissingBlockIdFailsWithoutMutatingAsset()
    {
        ActionSequenceAsset sequence =
            ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = "export.safety";
        sequence.Actions.Add(new ScenarioActionData
        {
            ActionId = "flow.wait",
            ParametersJson = "{\"duration\":0.1}"
        });

        try
        {
            ActionSequenceSourceExportResult result =
                ActionSequenceSourceSync.Export(sequence, "overworld");

            Assert.That(result.Success, Is.False);
            Assert.That(sequence.Actions[0].BlockId, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleExport_MissingConditionIdFailsWithoutMutatingRule()
    {
        BattleScenarioData scenario =
            ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "export.trigger.safety";
        var rule = new ScenarioTriggerRuleData
        {
            RuleId = "rule.one",
            EventId = "battle.started",
            SequenceId = "sequence.one",
            Conditions = new ScenarioTriggerConditionNodeData
            {
                Kind = ScenarioConditionNodeKind.Condition,
                ConditionId = "event.subject"
            }
        };
        scenario.TriggerRules.Add(rule);

        try
        {
            ScenarioSourceExportResult result =
                new ScenarioSourceExporter().Export(scenario);

            Assert.That(result.Success, Is.False);
            Assert.That(rule.Conditions.NodeId, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }
}
