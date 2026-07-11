using System.IO;
using NUnit.Framework;
using UnityEditor;

public sealed class OverworldSubwayCinematicContentTests
{
    private const string SequenceAssetPath = "Assets/_Game/Content/Scenarios/Runtime/Overworld/overworld_intro_subway.asset";
    private const string CatalogAssetPath = "Assets/_Game/Content/Scenarios/ActionCatalogs/OverworldCinematicActionCatalog.asset";
    private const string ShotAssetPath = "Assets/_Game/Content/Cinematics/Overworld/overworld_intro_subway_arrival.asset";

    [Test]
    public void SubwayIntroSequence_UsesValidatedCinematicActionFlow()
    {
        ActionSequenceAsset sequence = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(SequenceAssetPath);
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(CatalogAssetPath);

        Assert.That(sequence, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(sequence.SequenceId, Is.EqualTo("overworld.intro.subway"));
        Assert.That(sequence.Actions, Has.Count.EqualTo(4));
        Assert.That(sequence.Actions[0].ActionId, Is.EqualTo("cinematic.shot.play"));
        Assert.That(sequence.Actions[1].ActionId, Is.EqualTo("screen.fade"));
        Assert.That(sequence.Actions[2].ActionId, Is.EqualTo("cinematic.stage.release"));
        Assert.That(sequence.Actions[3].ActionId, Is.EqualTo("screen.fade"));

        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateSequence(sequence, catalog);
        Assert.That(validation.HasErrors, Is.False, FormatMessages(validation));
    }

    [Test]
    public void SubwayIntroSourceYaml_ImportsToTheSameActionFlow()
    {
        ActionSequenceAsset sequence = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(SequenceAssetPath);
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(CatalogAssetPath);
        Assert.That(sequence, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(sequence.Source, Is.Not.Null);
        Assert.That(File.Exists(sequence.Source.SourcePath), Is.True, sequence.Source.SourcePath);

        ActionSequenceSourceImportResult imported = ActionSequenceSourceSync.Import(
            File.ReadAllText(sequence.Source.SourcePath),
            sequence.Source.SourcePath);
        try
        {
            Assert.That(imported.Success, Is.True, FormatMessages(imported.Validation));
            Assert.That(imported.Sequence.SequenceId, Is.EqualTo(sequence.SequenceId));
            Assert.That(imported.Sequence.Actions, Has.Count.EqualTo(sequence.Actions.Count));

            ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateSequence(imported.Sequence, catalog);
            Assert.That(validation.HasErrors, Is.False, FormatMessages(validation));
        }
        finally
        {
            if (imported.Sequence != null)
            {
                UnityEngine.Object.DestroyImmediate(imported.Sequence);
            }
        }
    }

    [Test]
    public void SubwayArrivalShot_HasValidParallelTrainAndCameraRailMotion()
    {
        CinematicShotAsset shot = AssetDatabase.LoadAssetAtPath<CinematicShotAsset>(ShotAssetPath);
        Assert.That(shot, Is.Not.Null);
        Assert.That(shot.StageId, Is.EqualTo("overworld.subway_intro"));
        Assert.That(shot.ShotId, Is.EqualTo("subway_arrival"));
        Assert.That(shot.Motions, Has.Count.EqualTo(2));
        Assert.That(shot.ValidateDefinition().HasErrors, Is.False);
    }

    private static string FormatMessages(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages == null || validation.Messages.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", validation.Messages.ConvertAll(message => message.Code + ": " + message.Message));
    }
}
