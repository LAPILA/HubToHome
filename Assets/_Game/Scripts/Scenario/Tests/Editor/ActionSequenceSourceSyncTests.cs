using System;
using NUnit.Framework;
using UnityEngine;

public class ActionSequenceSourceSyncTests
{
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
