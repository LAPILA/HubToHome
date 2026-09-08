using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central runtime policy for UI that belongs inside the Pixel Perfect gameplay viewport.
/// FixedViewport UI uses the active gameplay camera as its shared output camera until a
/// dedicated UI camera is introduced by the UI policy.
/// </summary>
public sealed class UIViewportService : MonoBehaviour
{
    // UI 정책: FixedViewport Canvas만 이 서비스에 등록한다. 기준 해상도는
    // 640x480이며, 와이드 화면의 검은 여백에는 UI를 확장하지 않는다.
    public enum DisplayMode
    {
        FixedViewport,
        WorldTracked,
        Fullscreen
    }

    private const string ServiceName = "[UIViewportService]";
    private const float MissingCameraRetryInterval = 0.25f;
    private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
    private static readonly Vector2 GameplayReferenceResolution = new Vector2(640f, 480f);
    private static UIViewportService s_instance;

    private readonly List<Canvas> _fixedCanvases = new List<Canvas>();
    private Camera _sharedCamera;
    private Camera _lastAppliedCamera;
    private float _nextCameraLookupTime;
    private bool _resolveCameraAfterSettle;
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
    }

    private void OnEnable()
    {
        if (s_instance != this)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        RequestSceneRefresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (_settleRoutine != null)
        {
            StopCoroutine(_settleRoutine);
            _settleRoutine = null;
        }
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

        _sharedCamera = null;
        if (Time.unscaledTime < _nextCameraLookupTime)
            return null;

        // A camera-less loading interval must not perform a global lookup every frame.
        _nextCameraLookupTime = Time.unscaledTime + MissingCameraRetryInterval;
        Scene activeScene = SceneManager.GetActiveScene();
        Camera mainCamera = Camera.main;
        if (IsUsableCamera(mainCamera) && mainCamera.gameObject.scene == activeScene)
        {
            _sharedCamera = mainCamera;
            return _sharedCamera;
        }

        // During scene overlap an old MainCamera can still be enabled. Prefer the
        // active scene's PPC instead of retaining that old scene's cached camera.
        PixelPerfectCamera[] pixelPerfectCameras = FindObjectsByType<PixelPerfectCamera>(FindObjectsSortMode.None);
        Camera fallback = IsUsableCamera(mainCamera) ? mainCamera : null;
        for (int i = 0; i < pixelPerfectCameras.Length; i++)
        {
            PixelPerfectCamera pixelPerfect = pixelPerfectCameras[i];
            if (!pixelPerfect.isActiveAndEnabled)
                continue;

            Camera candidate = pixelPerfect.GetComponent<Camera>();
            if (!IsUsableCamera(candidate))
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                _sharedCamera = candidate;
                return _sharedCamera;
            }

            if (fallback == null)
                fallback = candidate;
        }

        _sharedCamera = fallback;
        return _sharedCamera;
    }

    private static bool IsUsableCamera(Camera camera)
    {
        return camera != null && camera.isActiveAndEnabled;
    }

    private bool HasViewportChanged(Camera camera)
    {
        // Resolution and application are separate states: opening another panel
        // may resolve the new camera before existing (including hidden) UI is rebound.
        return camera != null && (_lastAppliedCamera != camera
            || _lastCameraRect != camera.rect
            || _lastScreenWidth != Screen.width
            || _lastScreenHeight != Screen.height);
    }

    private void ApplyRegisteredCanvases(bool force)
    {
        if (!isActiveAndEnabled)
            return;

        Camera camera = ResolveSharedCamera();
        if ((force || HasViewportChanged(camera)) && _settleRoutine == null)
            _settleRoutine = StartCoroutine(CoApplyAfterDisplaySettles());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RequestSceneRefresh();
    private void HandleSceneUnloaded(Scene scene) => RequestSceneRefresh();
    private void HandleActiveSceneChanged(Scene previous, Scene current) => RequestSceneRefresh();

    private void RequestSceneRefresh()
    {
        InvalidateCameraCache();
        _resolveCameraAfterSettle = true;
        ApplyRegisteredCanvases(true);
    }

    private void InvalidateCameraCache()
    {
        _sharedCamera = null;
        _nextCameraLookupTime = 0f;
    }

    private IEnumerator CoApplyAfterDisplaySettles()
    {
        // Fullscreen 전환 직후에는 Screen 크기, PPC rect, CanvasScaler 순서가
        // 서로 다른 프레임에 확정될 수 있다. 두 프레임을 기다린 뒤 한 번에
        // Canvas → SafeArea → Layout 순서로 재계산한다.
        yield return null;
        yield return null;
        yield return EndOfFrame;

        if (_resolveCameraAfterSettle)
        {
            // Scene-loaded callbacks can run before scene-owned camera setup.
            // Re-resolve after the existing two-frame stabilization window as well.
            InvalidateCameraCache();
            _resolveCameraAfterSettle = false;
        }

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
            _lastAppliedCamera = camera;
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
