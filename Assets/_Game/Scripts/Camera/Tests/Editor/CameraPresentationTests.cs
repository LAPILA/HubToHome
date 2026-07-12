using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraPresentationTests
{
    private GameObject _cameraObject;
    private GameObject _centerObject;
    private GameObject _subjectObject;
    private CameraController _controller;
    private CinemachineCamera _virtualCamera;

    [SetUp]
    public void SetUp()
    {
        _cameraObject = new GameObject("CameraTest");
        _virtualCamera = _cameraObject.AddComponent<CinemachineCamera>();
        _cameraObject.AddComponent<CinemachinePositionComposer>();
        _cameraObject.AddComponent<CinemachineImpulseSource>();
        _controller = _cameraObject.AddComponent<CameraController>();

        _centerObject = new GameObject("Center");
        _subjectObject = new GameObject("Subject");
        _subjectObject.transform.position = new Vector3(8f, 3f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_centerObject);
        Object.DestroyImmediate(_subjectObject);
        Time.timeScale = 1f;
    }

    [Test]
    public void FocusUsesCinemachineTrackingWithoutSnappingVirtualCameraTransform()
    {
        Vector3 before = _cameraObject.transform.position;

        bool started = _controller.TryFocus(
            _subjectObject.transform,
            4f,
            CameraShotStyle.Dynamic,
            0f,
            CameraControlLease.None,
            out _,
            out string error);

        Assert.That(started, Is.True, error);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_subjectObject.transform));
        Assert.That(_cameraObject.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void ResetReturnsToRegisteredBattleCenter()
    {
        _controller.SetDefaultTarget(_centerObject.transform, true);
        Assert.That(_controller.TryFocus(
            _subjectObject.transform,
            4f,
            CameraShotStyle.Dynamic,
            0f,
            CameraControlLease.None,
            out _,
            out string focusError), Is.True, focusError);

        Assert.That(_controller.TryReset(
            0f,
            CameraShotStyle.GameplaySafe,
            CameraControlLease.None,
            out _,
            out string resetError), Is.True, resetError);

        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
    }

    [Test]
    public void TimelineLeaseBlocksOrdinaryFocusUntilReleased()
    {
        _controller.SetDefaultTarget(_centerObject.transform);
        Assert.That(_controller.TryReset(
            0f,
            CameraShotStyle.Static,
            CameraControlLease.None,
            out _,
            out string resetError), Is.True, resetError);
        Assert.That(_controller.TryAcquireTimelineControl(this, out CameraControlLease lease, out string leaseError), Is.True, leaseError);

        _controller.SetTarget(_subjectObject.transform);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(_controller.TryFocus(
            _subjectObject.transform,
            4f,
            CameraShotStyle.Dynamic,
            0f,
            CameraControlLease.None,
            out _,
            out _), Is.False);

        _controller.ReleaseTimelineControl(lease);

        Assert.That(_controller.TryFocus(
            _subjectObject.transform,
            4f,
            CameraShotStyle.Dynamic,
            0f,
            CameraControlLease.None,
            out _,
            out string error), Is.True, error);
    }

    [Test]
    public void ResetUsesAssignedProfileLensWhileFocusKeepsAuthoredZoom()
    {
        CameraShotProfile profile = ScriptableObject.CreateInstance<CameraShotProfile>();
        profile.Style = CameraShotStyle.GameplaySafe;
        profile.OrthographicSize = 7.25f;
        typeof(CameraController)
            .GetField("_gameplaySafeProfile", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(_controller, profile);
        _controller.SetDefaultTarget(_centerObject.transform, true);

        try
        {
            Assert.That(_controller.TryReset(
                0f,
                CameraShotStyle.GameplaySafe,
                CameraControlLease.None,
                out _,
                out string resetError), Is.True, resetError);
            Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(7.25f).Within(0.001f));

            Assert.That(_controller.TryFocus(
                _subjectObject.transform,
                3.75f,
                CameraShotStyle.GameplaySafe,
                0f,
                CameraControlLease.None,
                out _,
                out string focusError), Is.True, focusError);
            Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(3.75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ImpulseRejectsZeroDirectionAndHonorsDisabledShakeSetting()
    {
        Assert.That(_controller.TryImpulse(
            Vector3.zero,
            0.5f,
            0.1f,
            CameraShakeSafety.GameplaySafe,
            out _), Is.False);

        _controller.SetScreenShakeScaleProvider(new FixedShakeScaleProvider(0f));
        Assert.That(_controller.TryImpulse(
            Vector3.right * 10f,
            0.5f,
            0.1f,
            CameraShakeSafety.GameplaySafe,
            out string error), Is.True, error);
    }

    private sealed class FixedShakeScaleProvider : IScreenShakeScaleProvider
    {
        public FixedShakeScaleProvider(float scale) { Scale = scale; }
        public float Scale { get; }
    }
}
