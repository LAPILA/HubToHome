using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public sealed class OverworldCameraBindingTests
{
    private GameObject _cameraObject;
    private GameObject _decoyCameraObject;
    private GameObject _decoyTargetObject;
    private GameObject _playerObject;
    private GameObject _boundsObject;
    private CinemachineCamera _controllerCamera;
    private CinemachineCamera _decoyCamera;
    private PlayerController _player;
    private PolygonCollider2D _bounds;

    [SetUp]
    public void SetUp()
    {
        _cameraObject = new GameObject("OverworldControllerCamera");
        _controllerCamera = _cameraObject.AddComponent<CinemachineCamera>();
        _controllerCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        _controllerCamera.Lens.OrthographicSize = 7f;
        _cameraObject.AddComponent<CinemachineFollow>();
        _cameraObject.AddComponent<CinemachineImpulseSource>();
        CameraController controller = _cameraObject.AddComponent<CameraController>();
        typeof(CameraController)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(controller, null);

        _decoyCameraObject = new GameObject("CinematicDecoyCamera");
        _decoyCamera = _decoyCameraObject.AddComponent<CinemachineCamera>();
        _decoyCameraObject.AddComponent<CinemachineFollow>();
        _decoyTargetObject = new GameObject("DecoyTarget");
        _decoyCamera.Follow = _decoyTargetObject.transform;

        _playerObject = new GameObject("OverworldPlayer");
        _player = _playerObject.AddComponent<PlayerController>();

        _boundsObject = new GameObject("RoomCameraBounds");
        _bounds = _boundsObject.AddComponent<PolygonCollider2D>();
        _bounds.points = new[]
        {
            new Vector2(-10f, -5f),
            new Vector2(-10f, 5f),
            new Vector2(10f, 5f),
            new Vector2(10f, -5f)
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_boundsObject);
        Object.DestroyImmediate(_playerObject);
        Object.DestroyImmediate(_decoyTargetObject);
        Object.DestroyImmediate(_decoyCameraObject);
        Object.DestroyImmediate(_cameraObject);
    }

    [Test]
    public void ApplyChangesOnlyTheCameraControllerOwnedVirtualCamera()
    {
        bool applied = OverworldCameraBinding.TryApply(_player, _bounds, _player);

        Assert.That(applied, Is.True);
        Assert.That(_controllerCamera.Follow, Is.EqualTo(_player.transform));
        Assert.That(
            _controllerCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));

        CinemachineConfiner2D controllerConfiner =
            _controllerCamera.GetComponent<CinemachineConfiner2D>();
        Assert.That(controllerConfiner, Is.Not.Null);
        Assert.That(controllerConfiner.enabled, Is.True);
        Assert.That(controllerConfiner.BoundingShape2D, Is.SameAs(_bounds));

        Assert.That(_decoyCamera.Follow, Is.EqualTo(_decoyTargetObject.transform));
        Assert.That(_decoyCamera.GetComponent<CinemachineConfiner2D>(), Is.Null);
    }

    [Test]
    public void ApplyWithoutBoundsDisablesAndClearsTheOwnedConfiner()
    {
        CinemachineConfiner2D confiner =
            _controllerCamera.gameObject.AddComponent<CinemachineConfiner2D>();
        confiner.BoundingShape2D = _bounds;
        confiner.enabled = true;

        bool applied = OverworldCameraBinding.TryApply(_player, null, _player);

        Assert.That(applied, Is.True);
        Assert.That(confiner.enabled, Is.False);
        Assert.That(confiner.BoundingShape2D, Is.Null);
    }
}
