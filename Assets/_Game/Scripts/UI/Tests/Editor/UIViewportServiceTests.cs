using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIViewportServiceTests
{
    private GameObject _cameraObject;
    private GameObject _canvasObject;
    private GameObject _replacementCameraObject;
    private GameObject _secondCanvasObject;
    private GameObject _serviceObject;

    [TearDown]
    public void TearDown()
    {
        if (_serviceObject != null)
            Object.DestroyImmediate(_serviceObject);
        if (_secondCanvasObject != null)
            Object.DestroyImmediate(_secondCanvasObject);
        if (_replacementCameraObject != null)
            Object.DestroyImmediate(_replacementCameraObject);
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

    [Test]
    public void BattleHud_RemainsOverlayAfterNormalization_AndKeepsReferenceAnchors()
    {
        _cameraObject = new GameObject("Rotating Output", typeof(Camera));
        Camera camera = _cameraObject.GetComponent<Camera>();
        _canvasObject = new GameObject("Battle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        var first = new GameObject("First", typeof(RectTransform)).GetComponent<RectTransform>();
        var second = new GameObject("Second", typeof(RectTransform)).GetComponent<RectTransform>();
        first.SetParent(canvas.transform, false); second.SetParent(canvas.transform, false);
        first.anchoredPosition = new Vector2(20f, 30f);
        BattleHudViewport.Ensure(canvas, camera);
        UIViewportService.ConfigureFixedViewport(canvas, camera);
        RectTransform viewport = canvas.GetComponent<BattleHudViewport>().ContentRect;
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(canvas.worldCamera, Is.Null);
        Assert.That(canvas.sortingOrder, Is.LessThan(0), "시스템 팝업/결과/페이드보다 뒤여야 합니다.");
        Assert.That(viewport.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(first.anchoredPosition, Is.EqualTo(new Vector2(20f, 30f)));
        Assert.That(viewport.GetChild(0), Is.SameAs(first));
        Assert.That(viewport.GetChild(1), Is.SameAs(second));
        camera.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        camera.orthographicSize = 2f;
        UIViewportService.ConfigureFixedViewport(canvas, camera);
        Assert.That(viewport.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(first.anchoredPosition, Is.EqualTo(new Vector2(20f, 30f)));
        Assert.That(viewport.childCount, Is.EqualTo(2), "재등록 시 루트를 중첩하면 안 됩니다.");
    }

    [TestCase(640f, 480f, 1f)]
    [TestCase(1280f, 960f, 2f)]
    [TestCase(1920f, 1080f, 2.25f)]
    public void BattleHud_ReferenceScaleFitsInsideViewport(float width, float height, float scale)
    {
        Assert.That(BattleHudViewport.ReferenceScale(new Rect(0f, 0f, width, height)), Is.EqualTo(scale));
    }

    [Test]
    public void DialogueViewport_PreservesBottomAnchorsAndOrderAcrossFirstOpenAndCameraChanges()
    {
        _cameraObject = new GameObject("Dialogue Output", typeof(Camera));
        Camera camera = _cameraObject.GetComponent<Camera>();
        camera.pixelRect = new Rect(40f, 30f, 640f, 480f);
        _canvasObject = new GameObject("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.sortingOrder = 998;
        var canvasRect = (RectTransform)canvas.transform;
        canvasRect.pivot = Vector2.zero;
        canvasRect.sizeDelta = new Vector2(1000f, 700f);
        var panel = new GameObject("OverworldPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.right;
        panel.anchoredPosition = new Vector2(0f, 72f);
        panel.sizeDelta = new Vector2(-64f, 144f);
        var sibling = new GameObject("CinematicPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        sibling.SetParent(canvas.transform, false);
        sibling.gameObject.SetActive(false);

        DialogueCanvasViewport.Ensure(canvas, camera);
        var viewport = canvas.GetComponent<DialogueCanvasViewport>();
        camera.transform.SetPositionAndRotation(new Vector3(12f, -4f, -10f), Quaternion.Euler(0f, 0f, 35f));
        camera.orthographicSize = 2f;
        UIViewportService.ConfigureFixedViewport(canvas, camera);
        DialogueCanvasViewport.Ensure(canvas, camera);

        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(canvas.worldCamera, Is.Null);
        Assert.That(canvas.overrideSorting, Is.True);
        Assert.That(canvas.sortingLayerID, Is.Zero);
        Assert.That(canvas.sortingOrder, Is.EqualTo(CinematicLetterboxOverlay.CanvasSortingOrder + 1),
            "대사창은 상하 레터박스 앞에 표시되어야 합니다.");
        Assert.That(canvas.sortingOrder, Is.LessThan(short.MaxValue),
            "전체 화면 전환 페이드는 대사창까지 가려야 합니다.");
        Assert.That(viewport.ContentRect.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
        Assert.That(viewport.ContentRect.GetChild(0), Is.SameAs(panel));
        Assert.That(viewport.ContentRect.GetChild(1), Is.SameAs(sibling));
        Assert.That(viewport.ContentRect.childCount, Is.EqualTo(2));
        Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(panel.anchorMax, Is.EqualTo(Vector2.right));
        Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(0f, 72f)));
        Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(-64f, 144f)));
        Assert.That(sibling.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void CameraReplacementAtSameResolutionRebindsHiddenRegisteredCanvas()
    {
        UIViewportService service = CreateInactiveService();
        Camera oldCamera = CreateCamera(ref _cameraObject, "Previous Camera");
        Camera replacement = CreateCamera(ref _replacementCameraObject, "Replacement Camera");
        _canvasObject = new GameObject("Hidden Fixed UI", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        SetField(service, "_sharedCamera", oldCamera);
        service.RegisterFixedViewport(canvas);
        ApplySettledState(service);
        _canvasObject.SetActive(false);

        // ResolveSharedCamera may be called by another panel before LateUpdate.
        SetField(service, "_sharedCamera", replacement);
        _secondCanvasObject = new GameObject("New Fixed UI", typeof(RectTransform), typeof(Canvas));
        service.RegisterFixedViewport(_secondCanvasObject);

        Assert.That(HasViewportChanged(service, replacement), Is.True,
            "Resolving a new camera must not mark previously registered canvases as applied.");
        ApplySettledState(service);

        Assert.That(canvas.worldCamera, Is.SameAs(replacement));
        Assert.That(_secondCanvasObject.GetComponent<Canvas>().worldCamera, Is.SameAs(replacement));
        Assert.That(HasViewportChanged(service, replacement), Is.False);
        Assert.That(_canvasObject.activeSelf, Is.False);
    }

    [Test]
    public void MissingCameraDoesNotCommitRefreshBeforeReplacementExists()
    {
        UIViewportService service = CreateInactiveService();
        Camera oldCamera = CreateCamera(ref _cameraObject, "Previous Camera");
        _canvasObject = new GameObject("Fixed UI", typeof(RectTransform), typeof(Canvas));
        SetField(service, "_sharedCamera", oldCamera);
        service.RegisterFixedViewport(_canvasObject);
        ApplySettledState(service);

        SetField(service, "_sharedCamera", null);
        SetField(service, "_nextCameraLookupTime", float.PositiveInfinity);
        ApplySettledState(service);

        Camera replacement = CreateCamera(ref _replacementCameraObject, "Late Camera");
        SetField(service, "_sharedCamera", replacement);
        Assert.That(HasViewportChanged(service, replacement), Is.True);
        ApplySettledState(service);
        Assert.That(_canvasObject.GetComponent<Canvas>().worldCamera, Is.SameAs(replacement));
    }

    [Test]
    public void SceneInvalidationDropsStillActiveCachedCamera()
    {
        UIViewportService service = CreateInactiveService();
        Camera oldCamera = CreateCamera(ref _cameraObject, "Previous Scene Camera");
        SetField(service, "_sharedCamera", oldCamera);

        Invoke(service, "InvalidateCameraCache");

        Assert.That(typeof(UIViewportService).GetField("_sharedCamera",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service), Is.Null);
        Assert.That(oldCamera.isActiveAndEnabled, Is.True);
    }

    private UIViewportService CreateInactiveService()
    {
        // Drive the settle iterator directly; no Play Mode or editor scene transitions needed.
        _serviceObject = new GameObject("Viewport Service Test");
        _serviceObject.SetActive(false);
        return _serviceObject.AddComponent<UIViewportService>();
    }

    private static Camera CreateCamera(ref GameObject owner, string name)
    {
        owner = new GameObject(name);
        Camera camera = owner.AddComponent<Camera>();
        camera.rect = new Rect(0.25f, 0f, 0.5f, 1f);
        return camera;
    }

    private static void ApplySettledState(UIViewportService service)
    {
        IEnumerator routine = (IEnumerator)Invoke(service, "CoApplyAfterDisplaySettles");
        while (routine.MoveNext()) { }
    }

    private static bool HasViewportChanged(UIViewportService service, Camera camera)
    {
        return (bool)Invoke(service, "HasViewportChanged", camera);
    }

    private static object Invoke(UIViewportService service, string method, params object[] args)
    {
        return typeof(UIViewportService).GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service, args);
    }

    private static void SetField(UIViewportService service, string field, object value)
    {
        typeof(UIViewportService).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, value);
    }
}
