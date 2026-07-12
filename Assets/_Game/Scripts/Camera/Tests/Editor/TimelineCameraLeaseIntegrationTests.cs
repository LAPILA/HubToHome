using System.Collections;
using NUnit.Framework;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class TimelineCameraLeaseIntegrationTests
{
    private GameObject _cameraObject;
    private GameObject _subjectObject;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraObject = new GameObject("TimelineCameraLeaseTest");
        _cameraObject.AddComponent<CinemachineCamera>();
        _cameraObject.AddComponent<CinemachinePositionComposer>();
        _cameraObject.AddComponent<CinemachineImpulseSource>();
        _controller = _cameraObject.AddComponent<CameraController>();
        typeof(CameraController)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);
        _subjectObject = new GameObject("TimelineCameraSubject");
        _controller.SetDefaultTarget(_subjectObject.transform);
    }

    [TearDown]
    public void TearDown()
    {
        PlayableDirector director = FindDirector();
        if (director != null)
        {
            director.Stop();
            director = FindDirector();
            if (director != null)
            {
                Object.DestroyImmediate(director.gameObject);
            }
        }

        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_subjectObject);
    }

    [Test]
    public void AsyncTimelineBlocksOrdinaryCameraCommandsUntilPlaybackStops()
    {
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1d;
        TimelineCutsceneData cutscene = ScriptableObject.CreateInstance<TimelineCutsceneData>();
        cutscene.CutsceneId = "camera_lease";
        cutscene.TimelineAsset = timeline;
        TimelineCutsceneCatalog catalog = ScriptableObject.CreateInstance<TimelineCutsceneCatalog>();
        catalog.Cutscenes.Add(cutscene);

        try
        {
            Assert.That(CameraController.Instance, Is.SameAs(_controller), "CameraController singleton was not initialized in EditMode.");
            Assert.That(_controller.IsReady, Is.True, "Cinemachine components were not initialized before Timeline playback.");

            var runner = new TimelineCutsceneRunner(catalog);
            var context = new ActionExecutionContext(new ActionExecutionHandle("camera_lease_test"));
            RunToCompletion(runner.PlayCutscene("camera_lease", false, false, false, false, context));

            Assert.That(_controller.TryFocus(
                _subjectObject.transform,
                3f,
                CameraShotStyle.Dynamic,
                0f,
                CameraControlLease.None,
                out _,
                out _), Is.False);

            PlayableDirector director = FindDirector();
            Assert.That(director, Is.Not.Null);
            director.Stop();

            Assert.That(_controller.TryFocus(
                _subjectObject.transform,
                3f,
                CameraShotStyle.Dynamic,
                0f,
                CameraControlLease.None,
                out _,
                out string error), Is.True, error);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(cutscene);
            Object.DestroyImmediate(timeline);
        }
    }

    private static PlayableDirector FindDirector()
    {
        PlayableDirector[] directors = Resources.FindObjectsOfTypeAll<PlayableDirector>();
        for (int i = 0; i < directors.Length; i++)
        {
            PlayableDirector director = directors[i];
            if (director != null && director.gameObject.name == "TimelineCutsceneDirector_camera_lease")
            {
                return director;
            }
        }

        return null;
    }

    private static void RunToCompletion(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
        }
    }
}
