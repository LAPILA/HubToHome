using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
            Assert.That(frame.sizeDelta, Is.EqualTo(new Vector2(592f, 416f)));
            Assert.That(frame.localRotation, Is.EqualTo(Quaternion.identity));
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

    [TestCase(100f, 200f, 112f)]
    [TestCase(230f, 200f, 0f)]
    [TestCase(100f, 0f, 0f)]
    public void KeyboardAvoidanceLiftsOnlyEnoughToRevealEditingField(float fieldBottom, float keyboardHeight, float expected)
    {
        float lift = FieldMenuViewport.CalculateKeyboardLift(new Rect(100, fieldBottom, 72, 32),
            new Rect(0, 0, 640, keyboardHeight), new Rect(0, 0, 640, 480));
        Assert.That(lift, Is.EqualTo(expected));
    }

    [Test]
    public void KeyboardAvoidancePreservesTopSafetyAndIgnoresNonOverlappingFloatingKeyboard()
    {
        Rect field = new Rect(100, 400, 72, 32);
        Rect output = new Rect(0, 0, 640, 480);
        Assert.That(FieldMenuViewport.CalculateKeyboardLift(field, new Rect(0, 0, 640, 470), output), Is.EqualTo(36));
        Assert.That(FieldMenuViewport.CalculateKeyboardLift(field, new Rect(400, 0, 200, 470), output), Is.Zero);
    }

    [Test]
    public void CompactMenuKeepsFooterControlsAndAllThreePartyCardsInsideFrame()
    {
        var parent = new GameObject("Compact Menu Layout", typeof(RectTransform));
        try
        {
            FieldMenuView view = FieldMenuView.Create(parent.GetComponent<RectTransform>(), null, _ => { });
            Assert.That(view.Frame.sizeDelta, Is.EqualTo(new Vector2(592, 416)));
            Assert.That(view.Frame.localScale, Is.EqualTo(Vector3.one));
            string[] names = { "Settings", "Back", "Party0", "Party1", "Party2" };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform control = (RectTransform)view.Frame.Find(names[i]);
                Assert.That(control, Is.Not.Null, names[i]);
                Assert.That(control.rect.height, Is.GreaterThanOrEqualTo(28), names[i]);
                Assert.That(control.anchoredPosition.x, Is.GreaterThanOrEqualTo(0), names[i]);
                Assert.That(control.anchoredPosition.x + control.rect.width, Is.LessThanOrEqualTo(592), names[i]);
                Assert.That(-control.anchoredPosition.y + control.rect.height, Is.LessThanOrEqualTo(416), names[i]);
            }
            RectTransform scrollUp = (RectTransform)view.Frame.Find("Content/Detail/ScrollUp");
            RectTransform scrollDown = (RectTransform)view.Frame.Find("Content/Detail/ScrollDown");
            Assert.That(scrollUp.rect.size, Is.EqualTo(new Vector2(28, 28)));
            Assert.That(scrollDown.rect.size, Is.EqualTo(new Vector2(28, 28)));
            Assert.That(view.Frame.Find("Content/Companion/Body/Growth/Skills/SkillDetail/ScrollUp"), Is.Not.Null);
            Assert.That(view.Frame.Find("Content/Companion/Body/Growth/Skills/SkillDetail/ScrollDown"), Is.Not.Null);
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void CompactAttributesScrollFifthRowIntoViewWithoutScalingText()
    {
        var parent = new GameObject("Compact Attribute Layout", typeof(RectTransform));
        try
        {
            FieldMenuWidgets widgets = new FieldMenuWidgets(null, _ => { });
            FieldMenuGrowthView growth = FieldMenuGrowthView.Create(parent.GetComponent<RectTransform>(), widgets);
            ScrollRect scroll = growth.transform.Find("Attributes/AttributeRows").GetComponent<ScrollRect>();
            RectTransform lastRow = (RectTransform)scroll.content.Find("Stat4");
            Assert.That(scroll.viewport.rect.height, Is.EqualTo(156));
            Assert.That(lastRow.rect.height, Is.EqualTo(32));
            typeof(FieldMenuGrowthView).GetMethod("EnsureStatVisible", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(growth, new object[] { 4 });
            float top = -lastRow.anchoredPosition.y - scroll.content.anchoredPosition.y;
            Assert.That(top, Is.GreaterThanOrEqualTo(0));
            Assert.That(top + lastRow.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height));
            Assert.That(growth.transform.localScale, Is.EqualTo(Vector3.one));
        }
        finally { Object.DestroyImmediate(parent); }
    }
}
