using NUnit.Framework;
using UnityEngine;

public class CinematicShotDefinitionTests
{
    [Test]
    public void ValidateDefinition_RejectsMissingIdsAndDuplicateMotionSubjects()
    {
        CinematicShotAsset shot = ScriptableObject.CreateInstance<CinematicShotAsset>();
        shot.Motions.Add(new CinematicShotMotion { SubjectId = "subway" });
        shot.Motions.Add(new CinematicShotMotion { SubjectId = "subway" });

        ScenarioValidationResult result = shot.ValidateDefinition();

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages.Exists(message => message.Code == "cinematic.shot.id.required"), Is.True);
        Assert.That(result.Messages.Exists(message => message.Code == "cinematic.shot.stage.required"), Is.True);
        Assert.That(result.Messages.Exists(message => message.Code == "cinematic.shot.camera_rail.required"), Is.True);
        Assert.That(result.Messages.Exists(message => message.Code == "cinematic.shot.motion.subject.duplicate"), Is.True);

        Object.DestroyImmediate(shot);
    }

    [Test]
    public void ValidateDefinition_AcceptsParallelTrainAndCameraRailMotion()
    {
        CinematicShotAsset shot = ScriptableObject.CreateInstance<CinematicShotAsset>();
        shot.ShotId = "overworld.subway_arrival";
        shot.StageId = "overworld.arrival";
        shot.CameraRailSubjectId = "camera_rail";
        shot.Motions.Add(new CinematicShotMotion
        {
            SubjectId = "subway",
            Duration = 3.2f
        });
        shot.Motions.Add(new CinematicShotMotion
        {
            SubjectId = "camera_rail",
            Duration = 3.2f
        });

        ScenarioValidationResult result = shot.ValidateDefinition();

        Assert.That(result.HasErrors, Is.False);
        Object.DestroyImmediate(shot);
    }
}
