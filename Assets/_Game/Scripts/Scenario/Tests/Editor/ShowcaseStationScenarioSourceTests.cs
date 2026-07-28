using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShowcaseStationScenarioSourceTests
{
    private const string CatalogPath = "Assets/_Game/Content/Scenarios/ActionLibrary/Generated/ActionLibrary.asset";
    private const string IntroPath = "Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_intro.sequence.yaml";
    private const string FinalePath = "Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_finale.sequence.yaml";

    [TestCase(
        IntroPath,
        "showcase.station.intro",
        "showcase.station.intro.arrival",
        "bd6488415f614245b90dbd74bc65bd99")]
    [TestCase(
        FinalePath,
        "showcase.station.finale",
        "showcase.station.finale.power_restored",
        "17f3747729da4ac9a1d934004aaecba8")]
    public void SourceImportsAsIndependentValidatedSequenceAndPreservesDialogueIdentity(
        string sourcePath,
        string expectedSequenceId,
        string expectedDialogueId,
        string expectedDialogueBlockId)
    {
        Assert.That(File.Exists(sourcePath), Is.True, sourcePath);
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(CatalogPath);
        Assert.That(catalog, Is.Not.Null, CatalogPath);

        string sourceText = File.ReadAllText(sourcePath);
        ActionSequenceSourceImportResult imported = ActionSequenceSourceSync.Import(sourceText, sourcePath);
        try
        {
            Assert.That(imported.Success, Is.True, FormatMessages(imported.Validation));
            Assert.That(imported.Sequence, Is.Not.Null);
            Assert.That(imported.Sequence.SequenceId, Is.EqualTo(expectedSequenceId));
            Assert.That(imported.Sequence.Source.SourcePath, Is.EqualTo(sourcePath));
            Assert.That(imported.Sequence.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(sourceText)));
            Assert.That(
                ScenarioBlockIdentity.TryValidateUnique(imported.Sequence.Actions, out string identityError),
                Is.True,
                identityError);

            ScenarioActionData dialogue = imported.Sequence.Actions.Find(
                action => action.ActionId == "dialogue.wait");
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.BlockId, Is.EqualTo(expectedDialogueBlockId));
            Assert.That(
                JObject.Parse(dialogue.ParametersJson).Value<string>("id"),
                Is.EqualTo(expectedDialogueId));

            ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateSequence(
                imported.Sequence,
                catalog);
            Assert.That(validation.HasErrors, Is.False, FormatMessages(validation));

            ActionSequenceSourceExportResult exported = ActionSequenceSourceSync.Export(
                imported.Sequence,
                "overworld");
            Assert.That(exported.Success, Is.True, FormatMessages(exported.Validation));
            ActionSequenceSourceImportResult roundTrip = ActionSequenceSourceSync.Import(
                exported.Text,
                sourcePath);
            try
            {
                ScenarioActionData roundTripDialogue = roundTrip.Sequence.Actions.Find(
                    action => action.ActionId == "dialogue.wait");
                Assert.That(roundTripDialogue.BlockId, Is.EqualTo(expectedDialogueBlockId));
                Assert.That(
                    JObject.Parse(roundTripDialogue.ParametersJson).Value<string>("id"),
                    Is.EqualTo(expectedDialogueId));
            }
            finally
            {
                if (roundTrip.Sequence != null)
                    Object.DestroyImmediate(roundTrip.Sequence);
            }
        }
        finally
        {
            if (imported.Sequence != null)
                Object.DestroyImmediate(imported.Sequence);
        }
    }

    private static string FormatMessages(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages == null || validation.Messages.Count == 0)
            return string.Empty;
        return string.Join("\n", validation.Messages.ConvertAll(
            message => message.Code + ": " + message.Message));
    }
}