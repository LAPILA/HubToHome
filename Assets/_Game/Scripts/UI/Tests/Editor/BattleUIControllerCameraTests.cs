using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleUIControllerCameraTests
{
    [Test]
    public void BattlePlayfield_ReservesHudAndQueueBeforeEntrySlideAt640By480()
    {
        Assert.That(TryCalculatePlayfield(new Rect(0f, 0f, 640f, 480f),
            new Rect(0f, 0f, 640f, 480f), out Rect area), Is.True);

        Assert.That(area, Is.EqualTo(Rect.MinMaxRect(8f, 170f, 632f, 424f)));
    }

    [TestCase(1280f, 960f, 16f, 340f, 1264f, 848f)]
    [TestCase(1920f, 1080f, 336f, 400f, 1584f, 908f)]
    public void BattlePlayfield_UsesIntegerScaled640By480Content(float width, float height,
        float left, float bottom, float right, float top)
    {
        Rect output = new Rect(0f, 0f, width, height);
        Assert.That(TryCalculatePlayfield(output, output, out Rect area), Is.True);
        Assert.That(area, Is.EqualTo(Rect.MinMaxRect(left, bottom, right, top)));
    }

    [Test]
    public void BattlePlayfield_IntersectsDeviceSafeAreaAndCameraOutput()
    {
        Assert.That(TryCalculatePlayfield(new Rect(0f, 0f, 640f, 480f),
            new Rect(30f, 20f, 600f, 420f), out Rect area), Is.True);
        Assert.That(area, Is.EqualTo(Rect.MinMaxRect(30f, 170f, 630f, 424f)));
    }

    [Test]
    public void BattlePlayfield_InvalidOutputOrNoRemainingAreaFailsSafely()
    {
        Rect output = new Rect(0f, 0f, 640f, 480f);
        Assert.That(TryCalculatePlayfield(default, output, out Rect area), Is.False);
        Assert.That(area, Is.EqualTo(default(Rect)));
        Assert.That(TryCalculatePlayfield(new Rect(float.NaN, 0f, 640f, 480f), output, out area), Is.False);
        Assert.That(area, Is.EqualTo(default(Rect)));
        Assert.That(TryCalculatePlayfield(output, new Rect(0f, 0f, 640f, 100f), out area), Is.False);
        Assert.That(area, Is.EqualTo(default(Rect)));
    }

    [Test]
    public void BattlePlayfield_UninitializedInactiveControllerFailsSafely()
    {
        var uiObject = new GameObject("Uninitialized Battle UI", typeof(RectTransform));
        uiObject.SetActive(false);
        try
        {
            BattleUIController controller = uiObject.AddComponent<BattleUIController>();
            Assert.That(controller.TryGetBattlePlayfieldScreenRect(out Camera camera, out Rect area), Is.False);
            Assert.That(camera, Is.Null);
            Assert.That(area, Is.EqualTo(default(Rect)));
        }
        finally
        {
            Object.DestroyImmediate(uiObject);
        }
    }

    private static bool TryCalculatePlayfield(Rect output, Rect deviceSafeArea, out Rect area)
    {
        MethodInfo method = typeof(BattleUIController).GetMethod("TryCalculateBattlePlayfieldScreenRect",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        object[] arguments = { output, deviceSafeArea, default(Rect) };
        bool success = (bool)method.Invoke(null, arguments);
        area = (Rect)arguments[2];
        return success;
    }

    [Test]
    public void TryResolveWorldCamera_BindsCameraToInactiveScreenSpaceCanvas()
    {
        GameObject cameraObject = null;
        GameObject uiObject = null;

        try
        {
            Camera expectedCamera = Camera.main;
            if (expectedCamera == null)
            {
                cameraObject = new GameObject("Test Main Camera");
                cameraObject.tag = "MainCamera";
                expectedCamera = cameraObject.AddComponent<Camera>();
            }

            uiObject = new GameObject("Inactive Battle UI", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = uiObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            uiObject.SetActive(false);

            BattleUIController controller = uiObject.AddComponent<BattleUIController>();

            Assert.That(controller.TryResolveWorldCamera(), Is.True);
            Assert.That(canvas.worldCamera, Is.SameAs(expectedCamera));
        }
        finally
        {
            if (uiObject != null) Object.DestroyImmediate(uiObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        }
    }
}
