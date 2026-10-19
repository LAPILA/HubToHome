using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central runtime policy for UI that belongs inside the Pixel Perfect gameplay viewport.
/// FixedViewport UI uses camera-independent overlays, retaining only gameplay viewport bounds.
/// Lighting, camera movement and post processing never render the fixed UI.
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
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        if (canvas.renderMode == RenderMode.WorldSpace) return;
        if (canvas.TryGetComponent(out DialogueCanvasViewport dialogueViewport))
        {
            dialogueViewport.Configure(sharedCamera);
            return;
        }
        if (canvas.TryGetComponent(out BattleHudViewport battleHud))
        {
            battleHud.Configure(sharedCamera);
            return;
        }
        if (!canvas.TryGetComponent(out FixedCanvasViewport viewport))
            viewport = canvas.gameObject.AddComponent<FixedCanvasViewport>();
        viewport.Configure(sharedCamera);
    }

    /// <summary>Output bounds camera, not the camera used to project Overlay UI (which is null).</summary>
    public static Camera GetOutputCamera(Canvas canvas)
    {
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        if (canvas.TryGetComponent(out FixedCanvasViewport fixedViewport)) return fixedViewport.OutputCamera;
        if (canvas.TryGetComponent(out BattleHudViewport battleViewport)) return battleViewport.OutputCamera;
        if (canvas.TryGetComponent(out DialogueCanvasViewport dialogueViewport)) return dialogueViewport.OutputCamera;
        return canvas.worldCamera;
    }

    public static float CalculateReferenceScale(Rect viewport)
    {
        float fit = Mathf.Max(0.01f, Mathf.Min(viewport.width / GameplayReferenceResolution.x,
            viewport.height / GameplayReferenceResolution.y));
        // 640x480 미만에서는 모든 UI를 보존하기 위해 축소한다. 이 경우 원본 픽셀의
        // 일대일 보존은 불가능하므로 정수 확대와 같은 선명도를 보장하지 않는다.
        return fit >= 1f ? Mathf.Max(1f, Mathf.Floor(fit + 0.00001f)) : fit;
    }

    public static Rect GetOutputRect(Camera output)
    {
        var screenSize = new Vector2(Screen.width, Screen.height);
        return output != null
            ? CalculateVisibleOutputRect(output.pixelRect, screenSize)
            : new Rect(Vector2.zero, screenSize);
    }

    public static Rect CalculateVisibleOutputRect(Rect output, Vector2 screenSize)
    {
        // PPC Windowbox는 기준 해상도보다 작은 화면에서 화면 밖까지 viewport를
        // 잡을 수 있다. 고정 UI만 실제 보이는 교집합에 맞춰 잘림을 피한다.
        float width = Mathf.Max(0f, screenSize.x);
        float height = Mathf.Max(0f, screenSize.y);
        float xMin = Mathf.Clamp(output.xMin, 0f, width);
        float yMin = Mathf.Clamp(output.yMin, 0f, height);
        float xMax = Mathf.Max(xMin, Mathf.Clamp(output.xMax, 0f, width));
        float yMax = Mathf.Max(yMin, Mathf.Clamp(output.yMax, 0f, height));
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public static Rect CalculateContentScreenRect(Rect output)
    {
        float scale = CalculateReferenceScale(output);
        Vector2 size = GameplayReferenceResolution * scale;
        Vector2 origin = output.center - size * 0.5f;
        if (scale >= 1f)
        {
            // 홀수 출력 크기에서 중앙 정렬로 생기는 반 픽셀 시작점을 제거한다.
            origin.x = Mathf.Floor(origin.x + 0.00001f);
            origin.y = Mathf.Floor(origin.y + 0.00001f);
        }
        return new Rect(origin, size);
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
        return canvas != null ? canvas.rootCanvas : null;
    }
}
