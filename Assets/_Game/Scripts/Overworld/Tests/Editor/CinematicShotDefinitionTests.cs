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

    [Test]
    public void ValidateDefinition_RejectsNegativeCameraDelay()
    {
        CinematicShotAsset shot = ScriptableObject.CreateInstance<CinematicShotAsset>();
        shot.ShotId = "negative_delay";
        shot.StageId = "overworld.arrival";
        shot.CameraRailSubjectId = "camera_rail";
        shot.CameraDelay = -0.1f;
        shot.Motions.Add(new CinematicShotMotion { SubjectId = "camera_rail" });

        ScenarioValidationResult result = shot.ValidateDefinition();

        Assert.That(result.Messages.Exists(message => message.Code == "cinematic.shot.camera.invalid"), Is.True);
        Object.DestroyImmediate(shot);
    }

    [Test]
    public void ValidateDefinition_RejectsNegativeCameraPositionDamping()
    {
        CinematicShotAsset shot = ScriptableObject.CreateInstance<CinematicShotAsset>();
        shot.ShotId = "negative_damping";
        shot.StageId = "overworld.arrival";
        shot.CameraRailSubjectId = "camera_rail";
        shot.CameraPositionDamping = new Vector3(-0.1f, 0f, 0f);
        shot.Motions.Add(new CinematicShotMotion { SubjectId = "camera_rail" });

        ScenarioValidationResult result = shot.ValidateDefinition();

        Assert.That(
            result.Messages.Exists(message => message.Code == "cinematic.shot.camera.damping.invalid"),
            Is.True);
        Object.DestroyImmediate(shot);
    }
}
