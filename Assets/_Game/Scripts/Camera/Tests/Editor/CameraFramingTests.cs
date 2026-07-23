using Type = System.Type;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraFramingTests
{
    private GameObject _cameraObject;
    private GameObject _centerObject;
    private GameObject _leftObject;
    private GameObject _rightObject;
    private CinemachineCamera _virtualCamera;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraObject = new GameObject("CameraFramingTest");
        _virtualCamera = _cameraObject.AddComponent<CinemachineCamera>();
        _virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        _virtualCamera.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;
        CinemachineFollow follow = _cameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(0f, 0f, -1f);
        _cameraObject.AddComponent<CinemachineImpulseSource>();
        _controller = _cameraObject.AddComponent<CameraController>();
        typeof(CameraController)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        _centerObject = new GameObject("Center");
        _leftObject = new GameObject("Left");
        _rightObject = new GameObject("Right");
        _leftObject.transform.position = new Vector3(-10f, 0f, 0f);
        _rightObject.transform.position = new Vector3(10f, 0f, 0f);
        _controller.SetDefaultTarget(_centerObject.transform, true);
        _controller.ResetCamera(0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_centerObject);
        Object.DestroyImmediate(_leftObject);
        Object.DestroyImmediate(_rightObject);
        Time.timeScale = 1f;
    }

    [Test]
    public void PresentationServiceExposesMultiTargetFramingCommand()
    {
        MethodInfo method = typeof(ICameraPresentationService).GetMethod("TryFrameTargets");

        Assert.That(
            method,
            Is.Not.Null,
            "Camera presentation must expose a multi-target framing command.");
    }

    [Test]
    public void DistantTargetsExpandFinalOrthographicLensWithinConfiguredRange()
    {
        CameraFramingSettings settings = CameraFramingSettings.CreateBattleDefault();
        settings.Damping = 0f;

        bool started = _controller.TryFrameTargets(
            new[] { _leftObject.transform, _rightObject.transform },
            settings,
            CameraControlLease.None,
            out _,
            out string error);

        Assert.That(started, Is.True, error);
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);

        float finalLens = _virtualCamera.State.Lens.OrthographicSize;
        Assert.That(finalLens, Is.GreaterThan(CameraLensDefaults.GameplayOrthographicSize));
        Assert.That(finalLens, Is.LessThanOrEqualTo(settings.MaxOrthographicSize));
    }

    [Test]
    public void ResetStopsFramingAndRestoresDefaultTargetAndLens()
    {
        Assert.That(TryStartFraming(out CameraCommandToken token, out string error), Is.True, error);
        Assert.That(_controller.IsFramingTargets, Is.True);
        Assert.That(_controller.IsCurrent(token), Is.True);

        _controller.ResetCamera(0f);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(_virtualCamera.Target.CustomLookAtTarget, Is.False);
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void TimelineLeaseStopsFramingInvalidatesTokenAndBlocksNewFrame()
    {
        Assert.That(TryStartFraming(out CameraCommandToken frameToken, out string frameError), Is.True, frameError);

        Assert.That(
            _controller.TryAcquireTimelineControl(this, out CameraControlLease lease, out string leaseError),
            Is.True,
            leaseError);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_controller.IsCurrent(frameToken), Is.False);
        Assert.That(
            _controller.TryFrameTargets(
                new[] { _leftObject.transform, _rightObject.transform },
                CameraFramingSettings.CreateBattleDefault(),
                CameraControlLease.None,
                out _,
                out _),
            Is.False);

        _controller.ReleaseTimelineControl(lease);
        Assert.That(TryStartFraming(out _, out string resumedError), Is.True, resumedError);
    }

    [Test]
    public void InvalidTargetsDoNotChangeCurrentCameraState()
    {
        Transform beforeFollow = _virtualCamera.Follow;
        float beforeLens = _virtualCamera.Lens.OrthographicSize;

        bool started = _controller.TryFrameTargets(
            new[] { _leftObject.transform, _leftObject.transform, null },
            default,
            CameraControlLease.None,
            out _,
            out string error);

        Assert.That(started, Is.False);
        Assert.That(error, Does.Contain("two unique active targets"));
        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(beforeFollow));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(beforeLens));
    }

    [Test]
    public void LosingEveryFramingTargetReturnsToDefaultCamera()
    {
        Assert.That(TryStartFraming(out _, out string error), Is.True, error);
        Object.DestroyImmediate(_leftObject);
        Object.DestroyImmediate(_rightObject);

        typeof(CameraController)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void ConsecutiveFramesRestoreAuthoredGroupFramingSettingsOnce()
    {
        CinemachineGroupFraming authored = _cameraObject.AddComponent<CinemachineGroupFraming>();
        authored.enabled = false;
        authored.FramingSize = 0.43f;
        authored.Damping = 3.2f;
        authored.CenterOffset = new Vector2(0.1f, -0.2f);
        authored.OrthoSizeRange = new Vector2(2f, 12f);

        Assert.That(TryStartFraming(out _, out string firstError), Is.True, firstError);
        Assert.That(
            _controller.TryFrameTargets(
                new[] { _rightObject.transform, _centerObject.transform },
                CameraFramingSettings.CreateBattleDefault(),
                CameraControlLease.None,
                out _,
                out string secondError),
            Is.True,
            secondError);

        _controller.ResetCamera(0f);

        Assert.That(authored.enabled, Is.False);
        Assert.That(authored.FramingSize, Is.EqualTo(0.43f).Within(0.001f));
        Assert.That(authored.Damping, Is.EqualTo(3.2f).Within(0.001f));
        Assert.That(authored.CenterOffset, Is.EqualTo(new Vector2(0.1f, -0.2f)));
        Assert.That(authored.OrthoSizeRange, Is.EqualTo(new Vector2(2f, 12f)));
    }

    [Test]
    public void ResetRestoresCustomLookAtContract()
    {
        GameObject lookAtObject = new GameObject("AuthoredLookAt");
        try
        {
            CameraTarget authoredTarget = _virtualCamera.Target;
            authoredTarget.TrackingTarget = _centerObject.transform;
            authoredTarget.CustomLookAtTarget = true;
            authoredTarget.LookAtTarget = lookAtObject.transform;
            _virtualCamera.Target = authoredTarget;

            Assert.That(TryStartFraming(out _, out string error), Is.True, error);
            _controller.ResetCamera(0f);

            Assert.That(_virtualCamera.Target.TrackingTarget, Is.EqualTo(_centerObject.transform));
            Assert.That(_virtualCamera.Target.CustomLookAtTarget, Is.True);
            Assert.That(_virtualCamera.Target.LookAtTarget, Is.EqualTo(lookAtObject.transform));
        }
        finally
        {
            Object.DestroyImmediate(lookAtObject);
        }
    }

    [Test]
    public void FramingAndResetPreserveActiveConfinerBounds()
    {
        GameObject boundsObject = new GameObject("CameraBounds");
        try
        {
            PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
            bounds.points = new[]
            {
                new Vector2(-20f, -10f),
                new Vector2(-20f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, -10f)
            };
            CinemachineConfiner2D confiner = _cameraObject.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;
            confiner.enabled = true;

            Assert.That(TryStartFraming(out _, out string error), Is.True, error);
            _controller.ResetCamera(0f);

            Assert.That(confiner.enabled, Is.True);
            Assert.That(confiner.BoundingShape2D, Is.SameAs(bounds));
            Assert.That(
                _virtualCamera.Lens.OrthographicSize,
                Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(boundsObject);
        }
    }

    [Test]
    public void TimelineLeaseBlocksDashLensTweenButKeepsAdditiveImpulseAvailable()
    {
        Assert.That(
            _controller.TryAcquireTimelineControl(this, out CameraControlLease lease, out string leaseError),
            Is.True,
            leaseError);

        float beforeLens = _virtualCamera.Lens.OrthographicSize;
        _controller.PlayDashThroughImpact();
        DOTween.Complete("CameraImpact");

        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(beforeLens).Within(0.001f));
        Assert.That(
            _controller.TryImpulse(
                Vector3.right,
                0.2f,
                0.1f,
                CameraShakeSafety.GameplaySafe,
                out string impulseError),
            Is.True,
            impulseError);

        _controller.ReleaseTimelineControl(lease);
    }

    [Test]
    public void DisableDuringOwnedHitStopRestoresCapturedTimeScale()
    {
        Time.timeScale = 0.35f;

        _controller.PlayHeavySlam(Vector3.right, 0.5f);
        Assert.That(Time.timeScale, Is.EqualTo(0.01f).Within(0.001f));

        typeof(CameraController)
            .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        Assert.That(Time.timeScale, Is.EqualTo(0.35f).Within(0.001f));
    }

    [Test]
    public void BattleCameraScopeTypeExistsForActionOwnedCleanup()
    {
        Type scopeType = typeof(CameraController).Assembly.GetType("BattleCameraActionScope");

        Assert.That(
            scopeType,
            Is.Not.Null,
            "Battle actions need a token-owned camera cleanup scope.");
    }

    [Test]
    public void BattleCameraScopeDisposeRestoresDefaultAndIsIdempotent()
    {
        BattleCameraActionScope scope = BattleCameraActionScope.Begin(
            _leftObject.transform,
            _rightObject.transform,
            0f);

        Assert.That(scope.IsActive, Is.True);
        scope.Dispose();
        scope.Dispose();

        Assert.That(scope.IsActive, Is.False);
        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void BattleCameraScopeDoesNotOverwriteNewerCameraCommand()
    {
        BattleCameraActionScope scope = BattleCameraActionScope.Begin(
            _leftObject.transform,
            _rightObject.transform,
            0f);
        Assert.That(scope.IsActive, Is.True);

        Assert.That(
            _controller.TryFocus(
                _rightObject.transform,
                3f,
                CameraShotStyle.Dynamic,
                0f,
                CameraControlLease.None,
                out CameraCommandToken newerToken,
                out string error),
            Is.True,
            error);

        scope.Dispose();

        Assert.That(_controller.IsCurrent(newerToken), Is.True);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_rightObject.transform));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(3f).Within(0.001f));
    }

    private bool TryStartFraming(out CameraCommandToken token, out string error)
    {
        CameraFramingSettings settings = CameraFramingSettings.CreateBattleDefault();
        settings.Damping = 0f;
        return _controller.TryFrameTargets(
            new[] { _leftObject.transform, _rightObject.transform },
            settings,
            CameraControlLease.None,
            out token,
            out error);
    }
}
