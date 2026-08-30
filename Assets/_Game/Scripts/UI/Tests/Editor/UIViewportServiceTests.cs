using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIViewportServiceTests
{
    private GameObject _cameraObject;
    private GameObject _canvasObject;

    [TearDown]
    public void TearDown()
    {
        if (_canvasObject != null)
            Object.DestroyImmediate(_canvasObject);
        if (_cameraObject != null)
            Object.DestroyImmediate(_cameraObject);
    }

    [Test]
    public void ConfigureFixedViewportUsesSharedCameraAndNeverExpand()
    {
        _cameraObject = new GameObject("Shared Gameplay Camera");
        Camera camera = _cameraObject.AddComponent<Camera>();
        camera.rect = new Rect(0.25f, 0f, 0.5f, 1f);

        _canvasObject = new GameObject("Fixed UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        UIViewportService.ConfigureFixedViewport(canvas, camera);

        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
        Assert.That(canvas.worldCamera, Is.SameAs(camera));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
    }
}
