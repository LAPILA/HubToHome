using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class FixedCanvasViewportTests
{
    private GameObject _canvasObject;
    private GameObject _outputObject;
    private GameObject _worldVisualsObject;

    [TearDown]
    public void TearDown()
    {
        if (_canvasObject != null) Object.DestroyImmediate(_canvasObject);
        if (_worldVisualsObject != null) Object.DestroyImmediate(_worldVisualsObject);
        if (_outputObject != null) Object.DestroyImmediate(_outputObject);
    }

    [TestCase(-300)]
    [TestCase(0)]
    [TestCase(900)]
    public void FixedViewportUsesOverlayRegardlessOfAuthoredSortOrder(int sortingOrder)
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = output;
        canvas.sortingOrder = sortingOrder;
        canvas.sortingLayerName = "UI";

        UIViewportService.ConfigureFixedViewport(canvas, output);

        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        Assert.That(viewport, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(canvas.worldCamera, Is.Null);
        Assert.That(canvas.pixelPerfect, Is.True);
        Assert.That(canvas.overrideSorting, Is.True);
        Assert.That(canvas.sortingLayerID, Is.Zero);
        Assert.That(canvas.sortingOrder, Is.EqualTo(sortingOrder));
        Assert.That(viewport.OutputCamera, Is.SameAs(output));
        Assert.That(UIViewportService.GetOutputCamera(canvas), Is.SameAs(output));
        Assert.That(canvas.GetComponent<CanvasScaler>().uiScaleMode,
            Is.EqualTo(CanvasScaler.ScaleMode.ConstantPixelSize));
    }

    [Test]
    public void FirstConfigurePreservesAuthoredGeometryInactiveChildrenAndSiblingOrder()
    {
        Canvas canvas = CreateCanvas();
        var canvasRect = (RectTransform)canvas.transform;
        canvasRect.pivot = Vector2.zero;
        canvasRect.sizeDelta = new Vector2(1000f, 700f);
        RectTransform first = CreateChild("First", canvas.transform);
        first.anchorMin = new Vector2(0.1f, 0f);
        first.anchorMax = new Vector2(0.9f, 0f);
        first.pivot = new Vector2(0.25f, 0.75f);
        first.anchoredPosition3D = new Vector3(13f, 72f, 4f);
        first.sizeDelta = new Vector2(-64f, 144f);
        first.localScale = new Vector3(0.75f, 0.75f, 1f);
        first.localRotation = Quaternion.Euler(0f, 0f, 8f);
        RectTransform second = CreateChild("Second", canvas.transform);
        second.gameObject.SetActive(false);
        RectTransform third = CreateChild("Third", canvas.transform);

        UIViewportService.ConfigureFixedViewport(canvas, CreateOutput());

        RectTransform content = canvas.GetComponent<FixedCanvasViewport>().ContentRect;
        Assert.That(content.name, Is.EqualTo("Fixed UI Viewport (640x480)"));
        Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(content.GetChild(0), Is.SameAs(first));
        Assert.That(content.GetChild(1), Is.SameAs(second));
        Assert.That(content.GetChild(2), Is.SameAs(third));
        Assert.That(first.anchorMin, Is.EqualTo(new Vector2(0.1f, 0f)));
        Assert.That(first.anchorMax, Is.EqualTo(new Vector2(0.9f, 0f)));
        Assert.That(first.pivot, Is.EqualTo(new Vector2(0.25f, 0.75f)));
        Assert.That(first.anchoredPosition3D, Is.EqualTo(new Vector3(13f, 72f, 4f)));
        Assert.That(first.sizeDelta, Is.EqualTo(new Vector2(-64f, 144f)));
        Assert.That(first.localScale, Is.EqualTo(new Vector3(0.75f, 0.75f, 1f)));
        Assert.That(Quaternion.Angle(first.localRotation, Quaternion.Euler(0f, 0f, 8f)), Is.LessThan(.001f));
        Assert.That(second.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ReconfigureDoesNotNestContentOrDuplicateChildren()
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        RectTransform child = CreateChild("Authored Child", canvas.transform);
        UIViewportService.ConfigureFixedViewport(canvas, output);
        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        RectTransform content = viewport.ContentRect;

        UIViewportService.ConfigureFixedViewport(canvas, output);
        UIViewportService.ConfigureFixedViewport(canvas, output);

        Assert.That(viewport.ContentRect, Is.SameAs(content));
        Assert.That(canvas.transform.childCount, Is.EqualTo(1));
        Assert.That(content.childCount, Is.EqualTo(1));
        Assert.That(content.GetChild(0), Is.SameAs(child));
        Assert.That(canvas.GetComponents<FixedCanvasViewport>().Length, Is.EqualTo(1));
    }

    [Test]
    public void DirectChildrenCreatedAfterConfigureJoinContentOnceThroughHierarchyCallback()
    {
        Canvas canvas = CreateCanvas();
        RectTransform original = CreateChild("Original", canvas.transform);
        UIViewportService.ConfigureFixedViewport(canvas, CreateOutput());
        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        RectTransform lateChild = CreateChild("Late Child", canvas.transform);
        lateChild.anchorMin = Vector2.zero;
        lateChild.anchorMax = Vector2.right;
        lateChild.anchoredPosition = new Vector2(17f, 23f);
        lateChild.sizeDelta = new Vector2(-12f, 98f);

        // Drive the Unity callback explicitly as well so the EditMode test does not depend on editor scheduling.
        Invoke(viewport, "OnTransformChildrenChanged");
        Invoke(viewport, "OnTransformChildrenChanged");

        Assert.That(viewport.ContentRect.childCount, Is.EqualTo(2));
        Assert.That(viewport.ContentRect.GetChild(0), Is.SameAs(original));
        Assert.That(viewport.ContentRect.GetChild(1), Is.SameAs(lateChild));
        Assert.That(lateChild.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(lateChild.anchorMax, Is.EqualTo(Vector2.right));
        Assert.That(lateChild.anchoredPosition, Is.EqualTo(new Vector2(17f, 23f)));
        Assert.That(lateChild.sizeDelta, Is.EqualTo(new Vector2(-12f, 98f)));
    }

    [Test]
    public void InactiveCanvasCanBeConfiguredWithoutChangingVisibility()
    {
        Canvas canvas = CreateCanvas();
        _canvasObject.SetActive(false);
        RectTransform child = CreateChild("Inactive Content", canvas.transform);

        UIViewportService.ConfigureFixedViewport(canvas, CreateOutput());

        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        Assert.That(_canvasObject.activeSelf, Is.False);
        Assert.That(viewport.ContentRect.GetChild(0), Is.SameAs(child));
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
    }

    [Test]
    public void OffCenterOutputUsesSharedContentCenterAndIntegerScale()
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        output.rect = new Rect(0.13f, 0.07f, 0.61f, 0.79f);

        UIViewportService.ConfigureFixedViewport(canvas, output);

        AssertLayoutMatchesOutput(canvas, output);
        Assert.That(canvas.GetComponent<FixedCanvasViewport>().ContentRect.anchoredPosition.sqrMagnitude,
            Is.GreaterThan(0f), "잘린 출력 영역의 중심을 전체 화면 중심으로 대체하면 안 됩니다.");
        output.rect = new Rect(0.29f, 0.11f, 0.54f, 0.73f);
        Invoke(canvas.GetComponent<FixedCanvasViewport>(), "LateUpdate");
        AssertLayoutMatchesOutput(canvas, output);
    }

    [Test]
    public void IndependentWorldSpaceCanvasIsNotConvertedOrReparented()
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = output;
        canvas.sortingOrder = 23;
        RectTransform child = CreateChild("World Child", canvas.transform);

        UIViewportService.ConfigureFixedViewport(canvas, output);

        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(canvas.worldCamera, Is.SameAs(output));
        Assert.That(canvas.sortingOrder, Is.EqualTo(23));
        Assert.That(canvas.GetComponent<FixedCanvasViewport>(), Is.Null);
        Assert.That(child.parent, Is.SameAs(canvas.transform));
    }

    [Test]
    public void NestedCanvasRegistrationConfiguresOnlyRootAndPreservesLocalSortingOverride()
    {
        Canvas root = CreateCanvas();
        Camera output = CreateOutput();
        RectTransform childRect = CreateChild("Nested Canvas", root.transform);
        Canvas nested = childRect.gameObject.AddComponent<Canvas>();
        nested.overrideSorting = true;
        nested.sortingOrder = 17;

        UIViewportService.ConfigureFixedViewport(nested, output);

        Assert.That(root.GetComponent<FixedCanvasViewport>(), Is.Not.Null);
        Assert.That(nested.GetComponent<FixedCanvasViewport>(), Is.Null);
        Assert.That(nested.overrideSorting, Is.True);
        Assert.That(nested.sortingOrder, Is.EqualTo(17));
        Assert.That(UIViewportService.GetOutputCamera(nested), Is.SameAs(output));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SpecializedRootViewportKeepsOwnershipWhenNestedCanvasRegisters(bool dialogue)
    {
        Canvas root = CreateCanvas();
        Camera output = CreateOutput();
        Canvas nested = CreateChild("Nested Canvas", root.transform).gameObject.AddComponent<Canvas>();
        if (dialogue) DialogueCanvasViewport.Ensure(root, output);
        else BattleHudViewport.Ensure(root, output);

        UIViewportService.ConfigureFixedViewport(nested, output);

        Assert.That(root.GetComponent<FixedCanvasViewport>(), Is.Null);
        Assert.That(nested.GetComponent<FixedCanvasViewport>(), Is.Null);
        Assert.That(root.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(UIViewportService.GetOutputCamera(nested), Is.SameAs(output));
        Assert.That(root.sortingOrder, Is.EqualTo(dialogue ? short.MaxValue - 1 : -100));
    }

    [Test]
    public void CameraCanvasWorldArtKeepsWorldPoseOutsideOverlayContent()
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        output.orthographic = true;
        output.transform.position = new Vector3(3f, -2f, -10f);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = output;
        canvas.planeDistance = 5f;
        Canvas.ForceUpdateCanvases();
        var art = new GameObject("Legacy World Art", typeof(SpriteRenderer)).transform;
        art.SetParent(canvas.transform, false);
        art.localPosition = new Vector3(120f, -30f, 2f);
        art.localRotation = Quaternion.Euler(0f, 0f, 13f);
        art.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        art.gameObject.SetActive(false);
        RectTransform ui = CreateChild("Authored UI", canvas.transform);
        Vector3 position = art.position;
        Quaternion rotation = art.rotation;
        Vector3 scale = art.lossyScale;

        UIViewportService.ConfigureFixedViewport(canvas, output);
        Canvas.ForceUpdateCanvases();

        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        Transform worldRoot = art.parent;
        _worldVisualsObject = worldRoot.gameObject;
        Assert.That(worldRoot.name, Is.EqualTo("Fixed UI World Visuals"));
        Assert.That(worldRoot.parent, Is.SameAs(canvas.transform.parent));
        Assert.That(art.IsChildOf(canvas.transform), Is.False);
        Assert.That(ui.parent, Is.SameAs(viewport.ContentRect));
        Assert.That(Vector3.Distance(art.position, position), Is.LessThan(.001f));
        Assert.That(Quaternion.Angle(art.rotation, rotation), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(art.lossyScale, scale), Is.LessThan(.001f));
        Assert.That(art.gameObject.activeSelf, Is.False);

        UIViewportService.ConfigureFixedViewport(canvas, output);
        Assert.That(art.parent, Is.SameAs(worldRoot), "재설정으로 월드 시각요소 소유자를 중복 생성하면 안 됩니다.");
    }

    [Test]
    public void DetachedWorldArtFollowsOwnerDisableEnableAndDestroyCallbacks()
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = output;
        var art = new GameObject("World Art", typeof(SpriteRenderer)).transform;
        art.SetParent(canvas.transform, false);
        UIViewportService.ConfigureFixedViewport(canvas, output);
        FixedCanvasViewport viewport = canvas.GetComponent<FixedCanvasViewport>();
        GameObject worldRoot = art.parent.gameObject;
        _worldVisualsObject = worldRoot;

        // Drive lifecycle callbacks directly; this test does not enter Play Mode.
        Invoke(viewport, "OnDisable");
        Assert.That(worldRoot.activeSelf, Is.False);
        Invoke(viewport, "OnEnable");
        Assert.That(worldRoot.activeSelf, Is.True);
        Invoke(viewport, "OnDestroy");
        Assert.That(worldRoot == null, Is.True);
        Assert.That(art == null, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacySafeAreaFitterNestsInsideFixedContentRegardlessOfInitializationOrder(bool fitterAfterViewport)
    {
        Canvas canvas = CreateCanvas();
        Camera output = CreateOutput();
        output.rect = new Rect(0.13f, 0.07f, 0.61f, 0.79f);
        RectTransform child = CreateChild("Authored Child", canvas.transform);
        UIPixelPerfectSafeAreaFitter fitter;
        if (fitterAfterViewport)
        {
            UIViewportService.ConfigureFixedViewport(canvas, output);
            fitter = UIPixelPerfectSafeAreaFitter.Ensure(canvas, new Vector2(640f, 480f));
        }
        else
        {
            fitter = UIPixelPerfectSafeAreaFitter.Ensure(canvas, new Vector2(640f, 480f));
            UIViewportService.ConfigureFixedViewport(canvas, output);
        }

        fitter.ApplyNow();
        UIViewportService.ConfigureFixedViewport(canvas, output);
        fitter.ApplyNow();

        RectTransform content = canvas.GetComponent<FixedCanvasViewport>().ContentRect;
        Assert.That(canvas.transform.childCount, Is.EqualTo(1));
        Assert.That(content.parent, Is.SameAs(canvas.transform));
        Assert.That(content.childCount, Is.EqualTo(1));
        Transform safeArea = content.GetChild(0);
        Assert.That(safeArea.name, Is.EqualTo("[PPC Safe Area]"));
        Assert.That(safeArea.childCount, Is.EqualTo(1));
        Assert.That(safeArea.GetChild(0), Is.SameAs(child));
        Assert.That(((RectTransform)safeArea).anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(safeArea.localScale, Is.EqualTo(Vector3.one));
        AssertLayoutMatchesOutput(canvas, output);
    }

    private Canvas CreateCanvas()
    {
        _canvasObject = new GameObject("Fixed Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        return _canvasObject.GetComponent<Canvas>();
    }

    private Camera CreateOutput()
    {
        _outputObject = new GameObject("Gameplay Output", typeof(Camera));
        return _outputObject.GetComponent<Camera>();
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        child.SetParent(parent, false);
        return child;
    }

    private static void AssertLayoutMatchesOutput(Canvas canvas, Camera output)
    {
        Rect outputRect = UIViewportService.GetOutputRect(output);
        float scale = UIViewportService.CalculateReferenceScale(outputRect);
        Vector2 expectedCenter = (UIViewportService.CalculateContentScreenRect(outputRect).center
            - new Vector2(Screen.width, Screen.height) * .5f) / scale;
        RectTransform content = canvas.GetComponent<FixedCanvasViewport>().ContentRect;
        Assert.That(content.anchoredPosition.x, Is.EqualTo(expectedCenter.x).Within(.001f));
        Assert.That(content.anchoredPosition.y, Is.EqualTo(expectedCenter.y).Within(.001f));
        Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(content.localScale, Is.EqualTo(Vector3.one));
        Assert.That(canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
        Assert.That(canvas.GetComponent<CanvasScaler>().scaleFactor, Is.EqualTo(scale).Within(.001f));
    }

    private static void Invoke(FixedCanvasViewport viewport, string method)
    {
        typeof(FixedCanvasViewport).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(viewport, null);
    }
}
