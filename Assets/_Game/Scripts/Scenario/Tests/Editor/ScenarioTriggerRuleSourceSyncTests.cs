using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class ScenarioTriggerRuleSourceSyncTests
{
    [Test]
    public void ExtendedRuleRoundTripsNestedConditionsAndInputs()
    {
        BattleScenarioData source = MakeExtendedScenario();

        ScenarioSourceExportResult export = new ScenarioSourceExporter().Export(source);
        ScenarioSourceYamlWriteResult write = new ScenarioSourceYamlWriter().Write(export.Document);
        ScenarioSourceParseResult parse = new ScenarioSourceYamlParser().Parse(write.Text, "phase2.scenario.yaml");
        BattleScenarioData imported = ScenarioSourceImporter.CreateBattleScenario(
            parse.Document,
            write.Text,
            "phase2.scenario.yaml");

        try
        {
            Assert.That(export.Success, Is.True, Format(export.Validation));
            Assert.That(write.Success, Is.True, Format(write.Validation));
            Assert.That(parse.Success, Is.True, Format(parse.Validation));
            Assert.That(write.Text, Does.Contain("eventId: participant.hp_changed"));
            Assert.That(write.Text, Does.Contain("all:"));
            Assert.That(write.Text, Does.Contain("any:"));
            Assert.That(write.Text, Does.Contain("enemy: ${event.subject}"));
            Assert.That(imported.Rules, Is.Empty);
            Assert.That(imported.TriggerRules, Has.Count.EqualTo(1));

            ScenarioTriggerRuleData rule = imported.TriggerRules[0];
            Assert.That(rule.DisplayNameKo, Is.EqualTo("ZEV 2페이즈 전환"));
            Assert.That(rule.EventId, Is.EqualTo(BuiltInScenarioEventIds.ParticipantHpChanged));
            Assert.That(rule.Timing, Is.EqualTo(ScenarioTriggerTiming.Checkpoint));
            Assert.That(rule.CheckpointId, Is.EqualTo("skill.finished"));
            Assert.That(rule.Once, Is.EqualTo(ScenarioTriggerOnceScope.Save));
            Assert.That(rule.Disabled, Is.True);
            Assert.That(rule.Conditions.NodeId, Is.EqualTo("root"));
            Assert.That(rule.Conditions.Children[1].GroupMode, Is.EqualTo(ScenarioConditionGroupMode.Any));
            Assert.That(rule.Conditions.Children[1].Children[0].Negate, Is.True);
            Assert.That(JObject.Parse(rule.TargetInputsJson)["enemy"]?[ScenarioValueBinding.PropertyName]?.Value<string>(),
                Is.EqualTo("event.subject"));
        }
        finally
        {
            DestroyScenario(imported);
            DestroyScenario(source);
        }
    }

    [Test]
    public void LegacyRuleKeepsCompactWhenSyntax()
    {
        BattleScenarioData source = ScriptableObject.CreateInstance<BattleScenarioData>();
        source.ScenarioId = "legacy";
        source.Rules.Add(new BattleEventRuleData
        {
            RuleId = "phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "phase2"
        });

        ScenarioSourceYamlWriteResult write = new ScenarioSourceYamlWriter().Write(
            new ScenarioSourceExporter().Export(source).Document);
        ScenarioSourceParseResult parse = new ScenarioSourceYamlParser().Parse(write.Text, "legacy.scenario.yaml");
        BattleScenarioData imported = ScenarioSourceImporter.CreateBattleScenario(
            parse.Document,
            write.Text,
            "legacy.scenario.yaml");

        try
        {
            Assert.That(write.Text, Does.Contain("event: enemy.hp_crossed_below"));
            Assert.That(write.Text, Does.Not.Contain("eventId:"));
            Assert.That(imported.Rules, Has.Count.EqualTo(1));
            Assert.That(imported.TriggerRules, Is.Empty);
        }
        finally
        {
            DestroyScenario(imported);
            DestroyScenario(source);
        }
    }

    [Test]
    public void MixedLegacyAndExtendedRulesImportIntoSeparateRuntimeLists()
    {
        BattleScenarioData source = MakeExtendedScenario();
        source.Rules.Add(new BattleEventRuleData
        {
            RuleId = "opening",
            EventType = BattleEventType.BattleStarted,
            Timing = BattleRuleTiming.Immediate,
            Once = BattleRuleOnceMode.PerBattle,
            SequenceId = "phase2"
        });

        ScenarioSourceYamlWriteResult write = new ScenarioSourceYamlWriter().Write(
            new ScenarioSourceExporter().Export(source).Document);
        ScenarioSourceParseResult parse = new ScenarioSourceYamlParser().Parse(write.Text, "mixed.scenario.yaml");
        BattleScenarioData imported = ScenarioSourceImporter.CreateBattleScenario(
            parse.Document,
            write.Text,
            "mixed.scenario.yaml");

        try
        {
            Assert.That(parse.Document.Rules, Has.Count.EqualTo(2));
            Assert.That(parse.Document.Rules[0].Kind, Is.EqualTo(ScenarioSourceRuleKind.LegacyBattle));
            Assert.That(parse.Document.Rules[1].Kind, Is.EqualTo(ScenarioSourceRuleKind.Trigger));
            Assert.That(imported.Rules, Has.Count.EqualTo(1));
            Assert.That(imported.TriggerRules, Has.Count.EqualTo(1));
        }
        finally
        {
            DestroyScenario(imported);
            DestroyScenario(source);
        }
    }

    [Test]
    public void ConditionIdentityRepairsDuplicateIdsDeterministically()
    {
        ScenarioTriggerConditionNodeData root = Group("same", Condition("same", BuiltInTriggerConditionIds.ValueEquals));

        ScenarioTriggerIdentity.EnsureUnique(root, "scenario|rule");
        string repaired = root.Children[0].NodeId;
        ScenarioTriggerConditionNodeData second = Group("same", Condition("same", BuiltInTriggerConditionIds.ValueEquals));
        ScenarioTriggerIdentity.EnsureUnique(second, "scenario|rule");

        Assert.That(root.NodeId, Is.EqualTo("same"));
        Assert.That(repaired, Is.Not.EqualTo("same"));
        Assert.That(second.Children[0].NodeId, Is.EqualTo(repaired));
    }

    [Test]
    public void RuntimeReimportCopiesExtendedRulesIntoExistingAsset()
    {
        BattleScenarioData source = MakeExtendedScenario();
        ScenarioSourceYamlWriteResult write = new ScenarioSourceYamlWriter().Write(
            new ScenarioSourceExporter().Export(source).Document);
        BattleScenarioData target = ScriptableObject.CreateInstance<BattleScenarioData>();
        target.ScenarioId = "old";
        target.TriggerRules.Add(new ScenarioTriggerRuleData { RuleId = "old_rule" });
        var command = new ScenarioSourceRuntimeAssetReimportCommand(new ScenarioSourceYamlParser());

        ScenarioSourceRuntimeAssetReimportResult result = command.ReimportFromText(
            target,
            write.Text,
            "phase2.scenario.yaml");

        try
        {
            Assert.That(result.Success, Is.True, Format(result.Validation));
            Assert.That(target.TriggerRules, Has.Count.EqualTo(1));
            Assert.That(target.TriggerRules[0].RuleId, Is.EqualTo("phase2_trigger"));
            Assert.That(target.TriggerRules[0].Conditions.Children, Has.Count.EqualTo(2));
            Assert.That(target.TriggerRules[0].Conditions, Is.Not.SameAs(source.TriggerRules[0].Conditions));
        }
        finally
        {
            DestroyScenario(target);
            DestroyScenario(source);
        }
    }

    [Test]
    public void ValidationRejectsUnknownTargetSequenceInput()
    {
        BattleScenarioData scenario = MakeExtendedScenario();
        scenario.TriggerRules[0].TargetInputsJson = "{\"missing\":42}";
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();

        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

        Assert.That(validation.Messages.Exists(message =>
            message.Code == "scenario.trigger_rule.input.unknown"), Is.True, Format(validation));
        Object.DestroyImmediate(catalog);
        DestroyScenario(scenario);
    }

    [Test]
    public void ValidationRejectsCheckpointWithoutId()
    {
        BattleScenarioData scenario = MakeExtendedScenario();
        scenario.TriggerRules[0].CheckpointId = string.Empty;
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();

        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);

        Assert.That(validation.Messages.Exists(message =>
            message.Code == "scenario.trigger_rule.checkpoint.required"), Is.True, Format(validation));
        Object.DestroyImmediate(catalog);
        DestroyScenario(scenario);
    }

    private static BattleScenarioData MakeExtendedScenario()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_phase2";
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = "phase2";
        sequence.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "enemy",
            TypeId = "string",
            Required = true
        });
        sequence.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "ratio",
            TypeId = "number",
            Required = true
        });
        scenario.Sequences.Add(sequence);

        var nested = Group(
            "choice",
            Condition("equals", BuiltInTriggerConditionIds.ValueEquals, true),
            Condition("flag", BuiltInTriggerConditionIds.FlagState));
        nested.GroupMode = ScenarioConditionGroupMode.Any;
        scenario.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "phase2_trigger",
            DisplayNameKo = "ZEV 2페이즈 전환",
            EventId = BuiltInScenarioEventIds.ParticipantHpChanged,
            Timing = ScenarioTriggerTiming.Checkpoint,
            CheckpointId = "skill.finished",
            Once = ScenarioTriggerOnceScope.Save,
            Disabled = true,
            SequenceId = "phase2",
            TargetInputsJson = new JObject
            {
                ["enemy"] = ScenarioValueBinding.Create("event.subject"),
                ["ratio"] = 0.5
            }.ToString(Formatting.None),
            Conditions = Group(
                "root",
                Condition("participant", BuiltInTriggerConditionIds.EventParticipant),
                nested)
        });
        scenario.TriggerRules[0].Conditions.Children[0].ParametersJson = "{\"participant\":\"zev\"}";
        scenario.TriggerRules[0].Conditions.Children[1].Children[0].ParametersJson =
            "{\"path\":\"event.subject\",\"value\":\"other\"}";
        scenario.TriggerRules[0].Conditions.Children[1].Children[1].ParametersJson =
            "{\"flag\":\"phase2.unlocked\",\"value\":true}";
        return scenario;
    }

    private static ScenarioTriggerConditionNodeData Group(
        string id,
        params ScenarioTriggerConditionNodeData[] children)
    {
        var result = new ScenarioTriggerConditionNodeData
        {
            NodeId = id,
            Kind = ScenarioConditionNodeKind.Group,
            GroupMode = ScenarioConditionGroupMode.All
        };
        result.Children.AddRange(children);
        return result;
    }

    private static ScenarioTriggerConditionNodeData Condition(
        string id,
        string conditionId,
        bool negate = false)
    {
        return new ScenarioTriggerConditionNodeData
        {
            NodeId = id,
            Kind = ScenarioConditionNodeKind.Condition,
            ConditionId = conditionId,
            ParametersJson = "{}",
            Negate = negate
        };
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        if (scenario.Sequences != null)
        {
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                Object.DestroyImmediate(scenario.Sequences[i]);
            }
        }

        Object.DestroyImmediate(scenario);
    }

    private static string Format(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages == null)
        {
            return string.Empty;
        }

        var values = new System.Collections.Generic.List<string>();
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            values.Add(validation.Messages[i].Code + ": " + validation.Messages[i].Message);
        }

        return string.Join("\n", values);
    }
}
