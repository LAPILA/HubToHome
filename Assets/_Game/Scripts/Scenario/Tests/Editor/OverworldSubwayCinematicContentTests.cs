using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        Assert.That(sequence.Actions, Has.Count.EqualTo(7));
        Assert.That(sequence.Actions[0].ActionId, Is.EqualTo("flow.wait"));
        Assert.That(sequence.Actions[1].ActionId, Is.EqualTo("cinematic.shot.play"));
        Assert.That(sequence.Actions[2].ActionId, Is.EqualTo("flow.wait"));
        Assert.That(sequence.Actions[3].ActionId, Is.EqualTo("screen.fade"));
        Assert.That(sequence.Actions[4].ActionId, Is.EqualTo("flow.wait"));
        StringAssert.Contains("\"duration\":2", sequence.Actions[4].ParametersJson);
        Assert.That(sequence.Actions[5].ActionId, Is.EqualTo("cinematic.stage.release"));
        Assert.That(sequence.Actions[6].ActionId, Is.EqualTo("screen.fade"));

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
            for (int i = 0; i < sequence.Actions.Count; i++)
            {
                Assert.That(imported.Sequence.Actions[i].ActionId, Is.EqualTo(sequence.Actions[i].ActionId));
                Assert.That(imported.Sequence.Actions[i].ParametersJson, Is.EqualTo(sequence.Actions[i].ParametersJson));
            }

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
        Assert.That(shot.StartOrthographicSize, Is.EqualTo(10f));
        Assert.That(shot.EndOrthographicSize, Is.EqualTo(7f));
        Assert.That(shot.CameraDelay, Is.EqualTo(4.45f));
        Assert.That(shot.CameraDuration, Is.EqualTo(3.55f));
        Assert.That(shot.Motions[1].Delay, Is.EqualTo(4.45f));
        Assert.That(shot.CameraPositionDamping, Is.EqualTo(Vector3.zero));
        Assert.That(shot.ValidateDefinition().HasErrors, Is.False);
    }

    [Test]
    public void SubwayArrivalShot_StartsCameraNearTrainCenterAndFinishesTogether()
    {
        CinematicShotAsset shot = AssetDatabase.LoadAssetAtPath<CinematicShotAsset>(ShotAssetPath);
        Assert.That(shot, Is.Not.Null);

        CinematicShotMotion train = shot.Motions.Find(motion => motion.SubjectId == "subway");
        CinematicShotMotion rail = shot.Motions.Find(motion => motion.SubjectId == "camera_rail");
        Assert.That(train, Is.Not.Null);
        Assert.That(rail, Is.Not.Null);

        float trainProgressAtCameraStart = shot.CameraDelay / train.Duration;
        float trainTransformXAtCameraStart = Mathf.Lerp(
            train.StartLocalPosition.x,
            train.EndLocalPosition.x,
            trainProgressAtCameraStart);
        const float spriteCenterOffsetX = -0.1f;
        const float spriteCenterY = 3.75f;
        const float spriteHalfWidth = 11f;
        float trainCenterXAtCameraStart = trainTransformXAtCameraStart + spriteCenterOffsetX;
        float trainCenterXAtEnd = train.EndLocalPosition.x + spriteCenterOffsetX;
        float cameraLeftAtStart16By9 = -shot.StartOrthographicSize * (16f / 9f);
        float trainRightAtStart = train.StartLocalPosition.x + spriteCenterOffsetX + spriteHalfWidth;
        float railOffsetAtStart = rail.StartLocalPosition.x - trainCenterXAtCameraStart;
        float railOffsetAtEnd = rail.EndLocalPosition.x - trainCenterXAtEnd;
        float trainSpeed = (train.EndLocalPosition.x - train.StartLocalPosition.x) / train.Duration;
        float railSpeed = (rail.EndLocalPosition.x - rail.StartLocalPosition.x) / rail.Duration;

        Assert.That(train.StartLocalPosition.x, Is.EqualTo(-30f));
        Assert.That(train.EndLocalPosition.x, Is.EqualTo(24f));
        Assert.That(train.Duration, Is.EqualTo(8f));
        Assert.That(trainRightAtStart, Is.LessThan(cameraLeftAtStart16By9));
        Assert.That(trainCenterXAtCameraStart, Is.EqualTo(0f).Within(0.1f));
        Assert.That(railOffsetAtStart, Is.EqualTo(-2f).Within(0.001f));
        Assert.That(railOffsetAtEnd, Is.EqualTo(-2f).Within(0.001f));
        Assert.That(railSpeed, Is.EqualTo(trainSpeed).Within(0.0001f));
        Assert.That(rail.StartLocalPosition.y, Is.EqualTo(spriteCenterY));
        Assert.That(rail.EndLocalPosition.y, Is.EqualTo(spriteCenterY));
        Assert.That(rail.Delay, Is.EqualTo(shot.CameraDelay));
        Assert.That(rail.Duration, Is.EqualTo(shot.CameraDuration));
        Assert.That(rail.Ease, Is.EqualTo(DG.Tweening.Ease.Linear));
        Assert.That(rail.Delay + rail.Duration, Is.EqualTo(train.Delay + train.Duration).Within(0.001f));
        Assert.That(shot.CameraDelay + shot.CameraDuration, Is.EqualTo(train.Delay + train.Duration).Within(0.001f));
    }

    [Test]
    public void IntroManager_DefaultOverworldRevealFadeIsOneSecond()
    {
        GameObject gameObject = new GameObject("IntroManagerTest");
        try
        {
            IntroManager manager = gameObject.AddComponent<IntroManager>();
            var serialized = new SerializedObject(manager);
            Assert.That(serialized.FindProperty("_nextSceneFadeDuration").floatValue, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
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
