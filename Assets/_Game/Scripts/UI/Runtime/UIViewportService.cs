using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Central runtime policy for UI that belongs inside the Pixel Perfect gameplay viewport.
/// FixedViewport UI uses the active gameplay camera as its shared output camera until a
/// dedicated UI camera is introduced by the UI policy.
/// </summary>
public sealed class UIViewportService : MonoBehaviour
{
    public enum DisplayMode
    {
        FixedViewport,
        WorldTracked,
        Fullscreen
    }

    private const string ServiceName = "[UIViewportService]";
    private static readonly Vector2 GameplayReferenceResolution = new Vector2(640f, 480f);
    private static UIViewportService s_instance;

    private readonly List<Canvas> _fixedCanvases = new List<Canvas>();
    private Camera _sharedCamera;
    private Coroutine _settleRoutine;
    private Rect _lastCameraRect;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    public static UIViewportService GetOrCreate()
    {
        if (s_instance != null)
            return s_instance;

        GameObject serviceObject = new GameObject(ServiceName);
        s_instance = serviceObject.AddComponent<UIViewportService>();
        DontDestroyOnLoad(serviceObject);
        return s_instance;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyRegisteredCanvases(true);
    }

    private void OnDestroy()
    {
        if (_settleRoutine != null)
            StopCoroutine(_settleRoutine);

        if (s_instance == this)
            s_instance = null;
    }

    private void LateUpdate()
    {
        ApplyRegisteredCanvases(false);
    }

    public void RegisterFixedViewport(Component owner)
    {
        Canvas canvas = FindCanvas(owner);
        if (canvas == null)
            return;

        if (!_fixedCanvases.Contains(canvas))
            _fixedCanvases.Add(canvas);

        ConfigureFixedViewport(canvas, ResolveSharedCamera());
    }

    public void RegisterFixedViewport(GameObject owner)
    {
        if (owner != null)
            RegisterFixedViewport(owner.transform);
    }

    public void Unregister(Component owner)
    {
        Canvas canvas = FindCanvas(owner);
        if (canvas != null)
            _fixedCanvases.Remove(canvas);
    }

    public static void ConfigureFixedViewport(Canvas canvas, Camera sharedCamera)
    {
        if (canvas == null || sharedCamera == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = sharedCamera;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameplayReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public Camera ResolveSharedCamera()
    {
        if (_sharedCamera != null && _sharedCamera.isActiveAndEnabled)
            return _sharedCamera;

        _sharedCamera = Camera.main;
        if (_sharedCamera == null)
        {
            PixelPerfectCamera pixelPerfect = FindFirstObjectByType<PixelPerfectCamera>();
            if (pixelPerfect != null)
                _sharedCamera = pixelPerfect.GetComponent<Camera>();
        }

        return _sharedCamera;
    }

    private void ApplyRegisteredCanvases(bool force)
    {
        Camera camera = ResolveSharedCamera();
        if (camera == null)
            return;

        Rect cameraRect = camera.rect;
        bool changed = force
            || _sharedCamera != camera
            || _lastCameraRect != cameraRect
            || _lastScreenWidth != Screen.width
            || _lastScreenHeight != Screen.height;

        if (!changed)
            return;

        if (_settleRoutine == null)
            _settleRoutine = StartCoroutine(CoApplyAfterDisplaySettles());
    }

    private IEnumerator CoApplyAfterDisplaySettles()
    {
        // Fullscreen 전환 직후에는 Screen 크기, PPC rect, CanvasScaler 순서가
        // 서로 다른 프레임에 확정될 수 있다. 두 프레임을 기다린 뒤 한 번에
        // Canvas → SafeArea → Layout 순서로 재계산한다.
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        Camera camera = ResolveSharedCamera();
        if (camera != null)
        {
            for (int i = _fixedCanvases.Count - 1; i >= 0; i--)
            {
                Canvas canvas = _fixedCanvases[i];
                if (canvas == null)
                {
                    _fixedCanvases.RemoveAt(i);
                    continue;
                }

                ConfigureFixedViewport(canvas, camera);
            }

            UIPixelPerfectSafeAreaFitter[] fitters = FindObjectsByType<UIPixelPerfectSafeAreaFitter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < fitters.Length; i++)
                fitters[i]?.ApplyNow();

            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < _fixedCanvases.Count; i++)
            {
                Canvas canvas = _fixedCanvases[i];
                if (canvas == null) continue;

                RectTransform root = canvas.transform as RectTransform;
                if (root != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }

            Canvas.ForceUpdateCanvases();
            _lastCameraRect = camera.rect;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        _settleRoutine = null;
    }

    private static Canvas FindCanvas(Component owner)
    {
        if (owner == null)
            return null;

        Canvas canvas = owner.GetComponent<Canvas>();
        if (canvas == null)
            canvas = owner.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            canvas = owner.GetComponentInChildren<Canvas>(true);
        return canvas;
    }
}
