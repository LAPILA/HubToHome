using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class CinematicShotMotion
{
    public string SubjectId = string.Empty;
    public Vector3 StartLocalPosition;
    public Vector3 EndLocalPosition;
    [Min(0f)] public float Delay;
    [Min(0.01f)] public float Duration = 1f;
    public Ease Ease = Ease.InOutSine;
}

[CreateAssetMenu(fileName = "CinematicShot", menuName = "HubToHome/Cinematics/Cinematic Shot")]
public sealed class CinematicShotAsset : ScriptableObject
{
    [Tooltip("Cinematic Stage의 안정적인 stage ID입니다.")]
    public string StageId = string.Empty;

    [Tooltip("Action Sequence가 참조하는 안정적인 shot ID입니다.")]
    public string ShotId = string.Empty;

    [Tooltip("연출 제작자가 보는 한국어 표시 이름입니다.")]
    public string DisplayNameKo = string.Empty;

    [Tooltip("카메라가 따라갈 stage subject ID입니다.")]
    public string CameraRailSubjectId = string.Empty;

    [Min(0.01f)] public float StartOrthographicSize = 6f;
    [Min(0.01f)] public float EndOrthographicSize = 4.5f;
    [Min(0.01f)] public float CameraDuration = 1f;
    public Ease CameraEase = Ease.InOutSine;

    [Tooltip("동시에 움직일 stage subject 목록입니다.")]
    public List<CinematicShotMotion> Motions = new List<CinematicShotMotion>();

    public ScenarioValidationResult ValidateDefinition()
    {
        var result = new ScenarioValidationResult();
        string shotId = Normalize(ShotId);
        if (string.IsNullOrEmpty(shotId))
        {
            result.AddError("cinematic.shot.id.required", "Cinematic Shot requires ShotId.", string.Empty);
        }

        if (string.IsNullOrEmpty(Normalize(StageId)))
        {
            result.AddError("cinematic.shot.stage.required", "Cinematic Shot requires StageId.", shotId);
        }

        if (string.IsNullOrEmpty(Normalize(CameraRailSubjectId)))
        {
            result.AddError("cinematic.shot.camera_rail.required", "Cinematic Shot requires CameraRailSubjectId.", shotId);
        }

        if (StartOrthographicSize <= 0f || EndOrthographicSize <= 0f || CameraDuration <= 0f)
        {
            result.AddError("cinematic.shot.camera.invalid", "Cinematic Shot camera sizes and duration must be greater than zero.", shotId);
        }

        var subjectIds = new HashSet<string>();
        if (Motions == null || Motions.Count == 0)
        {
            result.AddError("cinematic.shot.motions.required", "Cinematic Shot requires at least one subject motion.", shotId);
            return result;
        }

        for (int i = 0; i < Motions.Count; i++)
        {
            CinematicShotMotion motion = Motions[i];
            string objectId = shotId + ".motions[" + i + "]";
            if (motion == null)
            {
                result.AddError("cinematic.shot.motion.required", "Cinematic Shot motion is missing.", objectId);
                continue;
            }

            string subjectId = Normalize(motion.SubjectId);
            if (string.IsNullOrEmpty(subjectId))
            {
                result.AddError("cinematic.shot.motion.subject.required", "Cinematic Shot motion requires SubjectId.", objectId);
            }
            else if (!subjectIds.Add(subjectId))
            {
                result.AddError("cinematic.shot.motion.subject.duplicate", "Cinematic Shot cannot move the same subject twice.", objectId);
            }

            if (motion.Delay < 0f || motion.Duration <= 0f)
            {
                result.AddError("cinematic.shot.motion.duration.invalid", "Cinematic Shot motion delay must be non-negative and duration must be greater than zero.", objectId);
            }
        }

        return result;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
