using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

[Serializable]
public sealed class CinematicStageSubjectBinding
{
    public string SubjectId = string.Empty;
    public Transform Target;
}

public sealed class OverworldCinematicStage : MonoBehaviour, ICinematicStageRunner
{
    [SerializeField] private string _stageId = string.Empty;
    [SerializeField] private CinemachineCamera _cinematicCamera;
    [SerializeField] private List<CinematicStageSubjectBinding> _subjects = new List<CinematicStageSubjectBinding>();
    [SerializeField] private List<CinematicShotAsset> _shots = new List<CinematicShotAsset>();

    private Sequence _activeSequence;
    private CinematicShotAsset _preparedShot;

    public string StageId
    {
        get { return Normalize(_stageId); }
    }

    public bool IsPrepared
    {
        get { return _preparedShot != null; }
    }

    public ScenarioValidationResult ValidateDefinition()
    {
        var result = new ScenarioValidationResult();
        string stageId = StageId;
        if (string.IsNullOrEmpty(stageId))
        {
            result.AddError("cinematic.stage.id.required", "Overworld Cinematic Stage requires StageId.", gameObject.name);
        }

        if (_cinematicCamera == null)
        {
            result.AddError("cinematic.stage.camera.required", "Overworld Cinematic Stage requires a CinemachineCamera.", stageId);
        }

        var subjectIds = new HashSet<string>();
        for (int i = 0; i < _subjects.Count; i++)
        {
            CinematicStageSubjectBinding binding = _subjects[i];
            string objectId = stageId + ".subjects[" + i + "]";
            if (binding == null || binding.Target == null)
            {
                result.AddError("cinematic.stage.subject.required", "Cinematic Stage subject reference is missing.", objectId);
                continue;
            }

            string subjectId = Normalize(binding.SubjectId);
            if (string.IsNullOrEmpty(subjectId))
            {
                result.AddError("cinematic.stage.subject.id.required", "Cinematic Stage subject requires SubjectId.", objectId);
            }
            else if (!subjectIds.Add(subjectId))
            {
                result.AddError("cinematic.stage.subject.id.duplicate", "Cinematic Stage subject IDs must be unique.", objectId);
            }
        }

        for (int i = 0; i < _shots.Count; i++)
        {
            CinematicShotAsset shot = _shots[i];
            if (shot == null)
            {
                result.AddError("cinematic.stage.shot.required", "Cinematic Stage contains a missing shot asset.", stageId + ".shots[" + i + "]");
                continue;
            }

            result.Merge(shot.ValidateDefinition());
            if (!string.IsNullOrEmpty(stageId) && !string.Equals(stageId, Normalize(shot.StageId), StringComparison.Ordinal))
            {
                result.AddError("cinematic.stage.shot.stage.mismatch", "Cinematic Shot StageId does not match its owning stage.", shot.ShotId);
            }
        }

        return result;
    }

    public bool PrepareForSceneReveal(string shotId, out string error)
    {
        CinematicShotAsset shot;
        if (!TryGetShot(shotId, out shot, out error))
        {
            return false;
        }

        return PrepareShot(shot, out error);
    }

    public IEnumerator PrepareStage(string stageId, string shotId, ActionExecutionContext context)
    {
        string normalizedStageId = Normalize(stageId);
        if (!string.IsNullOrEmpty(normalizedStageId) && !string.Equals(normalizedStageId, StageId, StringComparison.Ordinal))
        {
            context?.Handle.Fail("Cinematic Stage ID is not owned by this stage: " + normalizedStageId);
            yield break;
        }

        string error;
        if (!PrepareForSceneReveal(shotId, out error))
        {
            context?.Handle.Fail(error);
        }
    }

    public IEnumerator PlayShot(string stageId, string shotId, ActionExecutionContext context)
    {
        ActionExecutionHandle handle = context != null ? context.Handle : null;
        string normalizedStageId = Normalize(stageId);
        if (!string.IsNullOrEmpty(normalizedStageId) && !string.Equals(normalizedStageId, StageId, StringComparison.Ordinal))
        {
            handle?.Fail("Cinematic Stage ID is not owned by this stage: " + normalizedStageId);
            yield break;
        }

        CinematicShotAsset shot;
        string error;
        if (!TryGetShot(shotId, out shot, out error) || !PrepareShot(shot, out error))
        {
            handle?.Fail(error);
            yield break;
        }

        KillActiveSequence();
        _activeSequence = DOTween.Sequence().SetUpdate(true).SetId(this);
        for (int i = 0; i < shot.Motions.Count; i++)
        {
            CinematicShotMotion motion = shot.Motions[i];
            Transform target;
            if (motion == null || !TryGetSubject(motion.SubjectId, out target))
            {
                handle?.Fail("Cinematic Shot subject is unavailable: " + (motion != null ? motion.SubjectId : "<null>"));
                KillActiveSequence();
                yield break;
            }

            target.localPosition = motion.StartLocalPosition;
            Tween tween = target.DOLocalMove(motion.EndLocalPosition, motion.Duration)
                .SetDelay(Mathf.Max(0f, motion.Delay))
                .SetEase(motion.Ease);
            _activeSequence.Join(tween);
        }

        SetCameraSize(shot.StartOrthographicSize);
        _activeSequence.Join(DOTween.To(
            () => GetCameraSize(),
            SetCameraSize,
            shot.EndOrthographicSize,
            shot.CameraDuration).SetEase(shot.CameraEase));

        while (_activeSequence != null && _activeSequence.IsActive() && !_activeSequence.IsComplete())
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                KillActiveSequence();
                yield break;
            }

            yield return null;
        }

        _activeSequence = null;
    }

    public IEnumerator ReleaseStage(string stageId, ActionExecutionContext context)
    {
        string normalizedStageId = Normalize(stageId);
        if (!string.IsNullOrEmpty(normalizedStageId) && !string.Equals(normalizedStageId, StageId, StringComparison.Ordinal))
        {
            context?.Handle.Fail("Cinematic Stage ID is not owned by this stage: " + normalizedStageId);
            yield break;
        }

        Release();
        yield break;
    }

    public void Release()
    {
        KillActiveSequence();
        _preparedShot = null;
        if (_cinematicCamera != null)
        {
            _cinematicCamera.Follow = null;
            _cinematicCamera.gameObject.SetActive(false);
        }
    }

    private bool PrepareShot(CinematicShotAsset shot, out string error)
    {
        error = string.Empty;
        if (shot == null)
        {
            error = "Cinematic Shot is missing.";
            return false;
        }

        if (_cinematicCamera == null)
        {
            error = "Cinematic Stage camera is missing.";
            return false;
        }

        Transform railTarget;
        if (!TryGetSubject(shot.CameraRailSubjectId, out railTarget))
        {
            error = "Cinematic Shot camera rail subject is unavailable: " + shot.CameraRailSubjectId;
            return false;
        }

        for (int i = 0; i < shot.Motions.Count; i++)
        {
            CinematicShotMotion motion = shot.Motions[i];
            Transform ignored;
            if (motion == null || !TryGetSubject(motion.SubjectId, out ignored))
            {
                error = "Cinematic Shot subject is unavailable: " + (motion != null ? motion.SubjectId : "<null>");
                return false;
            }
        }

        KillActiveSequence();
        _cinematicCamera.gameObject.SetActive(true);
        _cinematicCamera.Follow = railTarget;
        _preparedShot = shot;
        SetCameraSize(shot.StartOrthographicSize);
        for (int i = 0; i < shot.Motions.Count; i++)
        {
            CinematicShotMotion motion = shot.Motions[i];
            Transform target;
            if (TryGetSubject(motion.SubjectId, out target))
            {
                target.localPosition = motion.StartLocalPosition;
            }
        }

        return true;
    }

    private bool TryGetShot(string shotId, out CinematicShotAsset shot, out string error)
    {
        string normalizedShotId = Normalize(shotId);
        if (string.IsNullOrEmpty(normalizedShotId))
        {
            shot = null;
            error = "Cinematic Shot ID is required.";
            return false;
        }

        for (int i = 0; i < _shots.Count; i++)
        {
            CinematicShotAsset candidate = _shots[i];
            if (candidate != null && string.Equals(Normalize(candidate.ShotId), normalizedShotId, StringComparison.Ordinal))
            {
                shot = candidate;
                error = string.Empty;
                return true;
            }
        }

        shot = null;
        error = "Cinematic Shot was not found on stage '" + StageId + "': " + normalizedShotId;
        return false;
    }

    private bool TryGetSubject(string subjectId, out Transform target)
    {
        string normalizedSubjectId = Normalize(subjectId);
        for (int i = 0; i < _subjects.Count; i++)
        {
            CinematicStageSubjectBinding binding = _subjects[i];
            if (binding != null
                && binding.Target != null
                && string.Equals(Normalize(binding.SubjectId), normalizedSubjectId, StringComparison.Ordinal))
            {
                target = binding.Target;
                return true;
            }
        }

        target = null;
        return false;
    }

    private float GetCameraSize()
    {
        return _cinematicCamera != null ? _cinematicCamera.Lens.OrthographicSize : 0f;
    }

    private void SetCameraSize(float size)
    {
        if (_cinematicCamera != null)
        {
            _cinematicCamera.Lens.OrthographicSize = Mathf.Max(0.01f, size);
        }
    }

    private void KillActiveSequence()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }

        _activeSequence = null;
    }

    private void OnDisable()
    {
        KillActiveSequence();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
