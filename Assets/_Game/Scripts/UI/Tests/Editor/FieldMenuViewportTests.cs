using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FieldMenuViewportTests
{
    [Test]
    public void OverlayMenuKeepsGameplayOutputBoundsSeparateFromUiProjectionCamera()
    {
        GameObject outputObject = new GameObject("Field Menu Output", typeof(Camera));
        GameObject canvasObject = new GameObject("Field Menu Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            Camera outputCamera = outputObject.GetComponent<Camera>();
            outputCamera.rect = new Rect(0.2f, 0.1f, 0.6f, 0.7f);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            UIViewportService.ConfigureFixedViewport(canvas, outputCamera);
            FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
            var frame = new GameObject("Field Menu Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(viewport.ContentRect, false);
            Canvas.ForceUpdateCanvases();

            FieldMenuViewport menu = canvasObject.AddComponent<FieldMenuViewport>();
            menu.Initialize(frame, canvas);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.worldCamera, Is.Null);
            Assert.That(typeof(FieldMenuViewport).GetField("_lastCamera", flags).GetValue(menu),
                Is.SameAs(outputCamera));
            Assert.That(typeof(FieldMenuViewport).GetField("_lastUICamera", flags).GetValue(menu), Is.Null);
            Rect output = UIViewportService.GetOutputRect(outputCamera);
            Assert.That(typeof(FieldMenuViewport).GetField("_lastOutput", flags).GetValue(menu), Is.EqualTo(output));
            Rect content = FieldMenuViewport.CalculateSafeContentRect(output, Screen.safeArea);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport.ContentRect,
                content.center, null, out Vector2 expectedCenter), Is.True);
            Assert.That(frame.localPosition.x, Is.EqualTo(expectedCenter.x).Within(.001f));
            Assert.That(frame.localPosition.y, Is.EqualTo(expectedCenter.y).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(outputObject);
        }
    }

    [Test]
    public void SafeOutputIntersectsCameraViewportWithNotchArea()
    {
        Rect result = FieldMenuViewport.CalculateSafeOutputRect(
            new Rect(160f, 0f, 1600f, 1080f), new Rect(200f, 40f, 1720f, 1000f));
        Assert.That(result, Is.EqualTo(new Rect(200f, 40f, 1560f, 1000f)));
    }

    [Test]
    public void LargerDeviceSafeAreaDoesNotExpandGameplayOutput()
    {
        Rect output = new Rect(320f, 60f, 1280f, 960f);
        Assert.That(FieldMenuViewport.CalculateSafeOutputRect(output, new Rect(0f, 0f, 1920f, 1080f)),
            Is.EqualTo(output));
    }

    [TestCase(640f, 480f, 640f, 480f)]
    [TestCase(960f, 768f, 640f, 480f)]
    [TestCase(1920f, 1080f, 1280f, 960f)]
    [TestCase(2532f, 1170f, 1280f, 960f)]
    [TestCase(320f, 240f, 320f, 240f)]
    public void SafeContentUsesSharedIntegerFitWithSmallOutputException(
        float width, float height, float contentWidth, float contentHeight)
    {
        Rect output = new Rect(0f, 0f, width, height);
        Rect content = FieldMenuViewport.CalculateSafeContentRect(output, output);
        Assert.That(content.size, Is.EqualTo(new Vector2(contentWidth, contentHeight)));
        Assert.That(content, Is.EqualTo(UIViewportService.CalculateContentScreenRect(output)));
    }

    [Test]
    public void AsymmetricSafeAreaMovesContentCenterAndSnapsOriginToPixels()
    {
        Rect content = FieldMenuViewport.CalculateSafeContentRect(
            new Rect(0f, 0f, 1920f, 1080f), new Rect(211f, 31f, 1500f, 1001f));
        Assert.That(content.size, Is.EqualTo(new Vector2(1280f, 960f)));
        Assert.That(content.position, Is.EqualTo(new Vector2(321f, 51f)));
        Assert.That(content.center.x, Is.Not.EqualTo(960f));
    }

    [Test]
    public void NotchCanReduceScaleWithoutStretchingContent()
    {
        Rect content = FieldMenuViewport.CalculateSafeContentRect(
            new Rect(0f, 0f, 1280f, 960f), new Rect(0f, 40f, 1280f, 920f));
        Assert.That(content.size, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(content.center, Is.EqualTo(new Vector2(640f, 500f)));
    }

    [Test]
    public void DisjointSafeAreaDoesNotInventAnOffscreenContentRectangle()
    {
        Rect output = new Rect(100f, 100f, 640f, 480f);
        Rect content = FieldMenuViewport.CalculateSafeContentRect(output, new Rect(2000f, 0f, 200f, 200f));
        Assert.That(content.size, Is.EqualTo(Vector2.zero));
        Assert.That(content.position, Is.EqualTo(output.center));
    }

    [Test]
    public void TouchingSafeAreaEdgeHasNoVisibleContent()
    {
        Rect content = FieldMenuViewport.CalculateSafeContentRect(
            new Rect(0f, 0f, 640f, 480f), new Rect(640f, 0f, 640f, 480f));
        Assert.That(content.size, Is.EqualTo(Vector2.zero));
    }
}
