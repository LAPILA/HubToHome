using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class ActionSequenceSourceSyncTests
{
    [Test]
    public void ExportThenImport_RoundTripsBlockIdsAndSequenceContract()
    {
        ActionSequenceAsset source = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        source.SequenceId = "battle.zev.phase2";
        source.DisplayNameKo = "ZEV 2페이즈 전환";
        source.Contract = new ActionSequenceContractData
        {
            DescriptionKo = "QTE 전투에서 슈팅 전투로 전환한다.",
            UsageKo = "ZEV의 HP가 절반 아래로 내려간 뒤 사용한다.",
            Lifecycle = ActionSequenceLifecycle.Ready,
            Tags = { "battle", "phase-transition" },
            AllowedPrimaryModes = { "battle" }
        };
        source.Actions.Add(new ScenarioActionData
        {
            BlockId = "11111111111111111111111111111111",
            DesignerLabel = "동시 전환",
            ActionId = ActionDirector.ParallelActionId,
            Note = "카메라와 캐릭터를 함께 바꾼다.",
            Disabled = true,
            Children =
            {
                new ScenarioActionData
                {
                    BlockId = "22222222222222222222222222222222",
                    ActionId = "flow.wait",
                    ParametersJson = "{\"duration\":0.25}"
                }
            }
        });

        ActionSequenceSourceExportResult exportResult = ActionSequenceSourceSync.Export(source, "battle");
        ActionSequenceSourceImportResult importResult = ActionSequenceSourceSync.Import(
            exportResult.Text,
            "Assets/_Game/Content/Scenarios/Source/Battle/zev_phase2.sequence.yaml");

        Assert.That(exportResult.Success, Is.True);
        Assert.That(exportResult.Text, Does.Contain("description:"));
        Assert.That(exportResult.Text, Does.Contain("blockId: 11111111111111111111111111111111"));
        Assert.That(exportResult.Text, Does.Contain("children:"));
        Assert.That(importResult.Success, Is.True);
        Assert.That(importResult.Sequence.Contract.DescriptionKo, Does.Contain("슈팅"));
        Assert.That(importResult.Sequence.Contract.UsageKo, Does.Contain("절반"));
        Assert.That(importResult.Sequence.Contract.Lifecycle, Is.EqualTo(ActionSequenceLifecycle.Ready));
        Assert.That(importResult.Sequence.Contract.Tags, Is.EqualTo(new[] { "battle", "phase-transition" }));
        Assert.That(importResult.Sequence.Contract.AllowedPrimaryModes, Is.EqualTo(new[] { "battle" }));
        Assert.That(importResult.Sequence.Actions[0].BlockId, Is.EqualTo("11111111111111111111111111111111"));
        Assert.That(importResult.Sequence.Actions[0].DesignerLabel, Is.EqualTo("동시 전환"));
        Assert.That(importResult.Sequence.Actions[0].Note, Does.Contain("카메라"));
        Assert.That(importResult.Sequence.Actions[0].Disabled, Is.True);
        Assert.That(importResult.Sequence.Actions[0].Children[0].BlockId, Is.EqualTo("22222222222222222222222222222222"));

        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(importResult.Sequence);
    }

    [Test]
    public void Import_LegacySourceWithoutBlockIdsAssignsUniqueIdsBeforeReturn()
    {
        const string source =
            "id: legacy_sequence\n" +
            "primaryMode: overworld\n" +
            "sequences:\n" +
            "  legacy_sequence:\n" +
            "    - parallel:\n" +
            "      - flow.wait:\n" +
            "          duration: 0.1\n" +
            "      - screen.fade:\n" +
            "          mode: in\n";

        ActionSequenceSourceImportResult result = ActionSequenceSourceSync.Import(
            source,
            "Assets/_Game/Content/Scenarios/Source/Overworld/legacy.sequence.yaml");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Sequence.Actions[0].BlockId, Is.Not.Empty);
        Assert.That(result.Sequence.Actions[0].Children[0].BlockId, Is.Not.Empty);
        Assert.That(result.Sequence.Actions[0].Children[1].BlockId, Is.Not.Empty);
        Assert.That(result.Sequence.Actions[0].BlockId, Is.Not.EqualTo(result.Sequence.Actions[0].Children[0].BlockId));
        Assert.That(result.Sequence.Actions[0].Children[0].BlockId, Is.Not.EqualTo(result.Sequence.Actions[0].Children[1].BlockId));

        UnityEngine.Object.DestroyImmediate(result.Sequence);
    }

    [Test]
    public void ExportThenImport_RoundTripsStandaloneSequenceIncludingParallelMetadata()
    {
        ActionSequenceAsset source = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        source.SequenceId = "overworld.subway_arrival";
        source.DisplayNameKo = "지하철 도입";
        source.Actions.Add(new ScenarioActionData
        {
            DesignerLabel = "지하철 샷",
            ActionId = "cinematic.shot.play",
            ParametersJson = "{\"shot\":\"overworld.subway_arrival\"}",
            Note = "카메라와 지하철이 함께 우측으로 이동"
        });
        source.Actions.Add(new ScenarioActionData
        {
            ActionId = ActionDirector.ParallelActionId,
            Children =
            {
                new ScenarioActionData
                {
                    ActionId = "screen.fade",
                    ParametersJson = "{\"mode\":\"out\",\"duration\":0.45}"
                },
                new ScenarioActionData
                {
                    ActionId = "flow.wait",
                    ParametersJson = "{\"duration\":0.2}",
                    Disabled = true
                }
            }
        });

        ScenarioBlockIdentity.EnsureUnique(source.Actions, source.SequenceId);

        ActionSequenceSourceExportResult exportResult = ActionSequenceSourceSync.Export(source, "overworld");
        ActionSequenceSourceImportResult importResult = ActionSequenceSourceSync.Import(
            exportResult.Text,
            "Assets/_Game/Content/Scenarios/Source/Overworld/overworld_subway_arrival.sequence.yaml",
            new DateTime(2026, 7, 12, 1, 2, 3, DateTimeKind.Utc));

        Assert.That(exportResult.Success, Is.True);
        Assert.That(exportResult.Text, Does.Contain("sequences:"));
        Assert.That(importResult.Success, Is.True);
        Assert.That(importResult.Sequence.SequenceId, Is.EqualTo("overworld.subway_arrival"));
        Assert.That(importResult.Sequence.DisplayNameKo, Is.EqualTo("지하철 도입"));
        Assert.That(importResult.Sequence.Actions, Has.Count.EqualTo(2));
        Assert.That(importResult.Sequence.Actions[0].DesignerLabel, Is.EqualTo("지하철 샷"));
        Assert.That(importResult.Sequence.Actions[0].Note, Does.Contain("카메라"));
        Assert.That(importResult.Sequence.Actions[1].ActionId, Is.EqualTo(ActionDirector.ParallelActionId));
        Assert.That(importResult.Sequence.Actions[1].Children, Has.Count.EqualTo(2));
        Assert.That(importResult.Sequence.Actions[1].Children[1].Disabled, Is.True);
        Assert.That(importResult.Sequence.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(exportResult.Text)));

        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(importResult.Sequence);
    }

    [Test]
    public void ExportThenImport_RoundTripsTypedInputsAndReadableBindings()
    {
        ActionSequenceAsset source = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        source.SequenceId = "shared.actor_move";
        source.DisplayNameKo = "Actor Move";
        source.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "actor",
            DisplayNameKo = "Actor",
            DescriptionKo = "Actor to move",
            TypeId = "actorRef",
            Required = true
        });
        source.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "speed",
            DisplayNameKo = "Speed",
            TypeId = "number",
            DefaultValueJson = "1.5"
        });
        source.Actions.Add(new ScenarioActionData
        {
            BlockId = "33333333333333333333333333333333",
            ActionId = "actor.move",
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"},\"speed\":{\"$bind\":\"input.speed\"}}"
        });

        ActionSequenceSourceExportResult exportResult = ActionSequenceSourceSync.Export(source, "overworld");
        ActionSequenceSourceImportResult importResult = ActionSequenceSourceSync.Import(
            exportResult.Text,
            "Assets/_Game/Content/Scenarios/Source/Common/shared_actor_move.sequence.yaml");

        Assert.That(exportResult.Success, Is.True);
        Assert.That(exportResult.Text, Does.Contain("inputs:"));
        Assert.That(exportResult.Text, Does.Contain("id: actor"));
        Assert.That(exportResult.Text, Does.Contain("actor: ${input.actor}"));
        Assert.That(importResult.Success, Is.True);
        Assert.That(importResult.Sequence.Contract.Inputs, Has.Count.EqualTo(2));
        Assert.That(importResult.Sequence.Contract.Inputs[0].InputId, Is.EqualTo("actor"));
        Assert.That(importResult.Sequence.Contract.Inputs[0].Required, Is.True);
        Assert.That(importResult.Sequence.Contract.Inputs[1].DefaultValueJson, Is.EqualTo("1.5"));

        JObject parameters = JObject.Parse(importResult.Sequence.Actions[0].ParametersJson);
        Assert.That(parameters["actor"]["$bind"].Value<string>(), Is.EqualTo("input.actor"));
        Assert.That(parameters["speed"]["$bind"].Value<string>(), Is.EqualTo("input.speed"));

        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(importResult.Sequence);
    }

    [Test]
    public void Import_RejectsSourceWithoutMatchingSequenceEntry()
    {
        const string source = "id: overworld.subway_arrival\nprimaryMode: overworld\nsequences:\n  another_sequence:\n    - flow.wait:\n        duration: 0.1\n";

        ActionSequenceSourceImportResult result = ActionSequenceSourceSync.Import(
            source,
            "Assets/_Game/Content/Scenarios/Source/Overworld/bad.sequence.yaml");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Validation.Messages.Exists(message => message.Code == "sequence.source.actions.required"), Is.True);
    }
}
