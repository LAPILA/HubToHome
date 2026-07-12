using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// UI 패널 열기/닫기를 총괄하는 싱글톤 매니저.
/// [개선] 씬 전환 시 에러 방지를 위해 런타임 패널 등록(Dictionary) 방식과 자동 뒤로가기(Pop)를 지원합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── 패널 스택 및 저장소 ──────────────────────────────────
    private readonly Stack<UIPanel> _panelStack = new Stack<UIPanel>();
    
    // 식별자(String)로 패널을 관리하여 씬이 바뀌어도 유연하게 대응
    private readonly Dictionary<string, UIPanel> _registeredPanels = new Dictionary<string, UIPanel>();
    private readonly HashSet<UIPanel> _pixelPerfectSafeAreaPanels = new HashSet<UIPanel>();

    [Header("Global Panels (씬 무관하게 항상 존재하는 UI)")]
    [SerializeField] private UIPanel _pausePanel;
    [SerializeField] private UIPanel _saveLoadPanel;

    [Header("OverWorld Panels (오버월드 UI)")]
    [SerializeField] private UIPanel _overworldPanel;
    [SerializeField] private bool _fitOverworldPanelToPixelPerfectSafeArea = true;
    [SerializeField] private Vector2 _pixelPerfectSafeAreaReferenceResolution = new Vector2(640f, 480f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 글로벌 패널 기본 등록
        if (_pausePanel != null) RegisterPanel(UIPanelId.Pause, _pausePanel);
        if (_saveLoadPanel != null) RegisterPanel(UIPanelId.SaveLoad, _saveLoadPanel);
        if (_overworldPanel != null)
            RegisterPanel(UIPanelId.Overworld, _overworldPanel, _fitOverworldPanelToPixelPerfectSafeArea);
    }

    private void Update()
    {
        // 최상단 패널 자동 닫기 (ESC 또는 X키)
        if (IsAnyPanelOpen)
        {
            if (GameInput.UICancelPressed)
            {
                UIPanel topPanel = _panelStack.Peek();
                if (topPanel == null || !topPanel.TryHandleCancelInput())
                    CloseTopPanel();
            }
        }
    }

    // ── 패널 등록 (씬 전용 UI들이 Start에서 호출) ──────────────
    public void RegisterPanel(string panelID, UIPanel panel)
    {
        RegisterPanel(panelID, panel, false);
    }

    public void RegisterPanel(string panelID, UIPanel panel, bool fitToPixelPerfectSafeArea)
    {
        if (!_registeredPanels.ContainsKey(panelID))
            _registeredPanels.Add(panelID, panel);
        else
            _registeredPanels[panelID] = panel;

        if (fitToPixelPerfectSafeArea)
            RegisterPixelPerfectSafeAreaPanel(panel);
    }

    public void UnregisterPanel(string panelID)
    {
        if (_registeredPanels.ContainsKey(panelID))
            _registeredPanels.Remove(panelID);
    }

    // ── 패널 열기 / 닫기 ────────────────────────────────────
    public void OpenPanel(string panelID)
    {
        if (_registeredPanels.TryGetValue(panelID, out var panel))
            OpenPanel(panel);
        else
            Debug.LogWarning($"[UIManager] '{panelID}' 패널을 찾을 수 없습니다! RegisterPanel이 호출되었는지 확인하세요.");
    }

    public void OpenPanel(UIPanel panel)
    {
        if (panel == null) return;
        if (_panelStack.Count > 0 && _panelStack.Peek() == panel) return; // 이미 최상단이면 무시

        EnsurePixelPerfectSafeAreaIfNeeded(panel);
        _panelStack.Push(panel);
        panel.Show();
        
        // UI가 열리면 게임 일시정지 (선택 사항)
        // Time.timeScale = 0f; 
    }

    public void CloseTopPanel()
    {
        if (_panelStack.Count == 0) return;
        var panel = _panelStack.Pop();
        panel.Hide();

        // 스택이 비면 일시정지 해제
        // if (_panelStack.Count == 0) Time.timeScale = 1f;
    }

    public void CloseAllPanels()
    {
        while (_panelStack.Count > 0)
        {
            var panel = _panelStack.Pop();
            panel.Hide();
        }
    }

    public bool IsAnyPanelOpen => _panelStack.Count > 0;

    private void RegisterPixelPerfectSafeAreaPanel(UIPanel panel)
    {
        if (panel == null) return;

        _pixelPerfectSafeAreaPanels.Add(panel);
        EnsurePixelPerfectSafeAreaIfNeeded(panel);
    }

    private void EnsurePixelPerfectSafeAreaIfNeeded(UIPanel panel)
    {
        if (panel == null || !_pixelPerfectSafeAreaPanels.Contains(panel)) return;

        UIPixelPerfectSafeAreaFitter.Ensure(panel, _pixelPerfectSafeAreaReferenceResolution);
    }
}

/// <summary>
/// Keeps opted-in overlay UI inside the visible Pixel Perfect Camera reference area.
/// Use this for UI that should behave like in-game 640x480 HUD, not fullscreen system UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPixelPerfectSafeAreaFitter : MonoBehaviour
{
    private const string SafeAreaRootName = "[PPC Safe Area]";
    private static readonly Vector2 DefaultReferenceResolution = new Vector2(640f, 480f);

    [SerializeField] private Vector2 _fallbackReferenceResolution = new Vector2(640f, 480f);
    [SerializeField] private string _preferredPixelPerfectCameraName = "PPC";

    private readonly List<Transform> _childrenToMove = new List<Transform>();
    private RectTransform _root;
    private RectTransform _safeAreaRoot;
    private Canvas _canvas;
    private PixelPerfectCamera _cachedPixelPerfectCamera;
    private Vector2 _lastCanvasSize;
    private Vector2 _lastReferenceResolution;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    public static UIPixelPerfectSafeAreaFitter Ensure(Component owner, Vector2 fallbackReferenceResolution)
    {
        if (owner == null) return null;

        UIPixelPerfectSafeAreaFitter fitter = owner.GetComponent<UIPixelPerfectSafeAreaFitter>();
        if (fitter == null)
            fitter = owner.gameObject.AddComponent<UIPixelPerfectSafeAreaFitter>();

        fitter.SetFallbackReferenceResolution(fallbackReferenceResolution);
        fitter.ApplyNow();
        return fitter;
    }

    public void SetFallbackReferenceResolution(Vector2 referenceResolution)
    {
        _fallbackReferenceResolution = IsValid(referenceResolution) ? referenceResolution : DefaultReferenceResolution;
    }

    private void Awake()
    {
        ApplyNow();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyNow();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cachedPixelPerfectCamera = null;
        ApplyNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyNow();
    }

    private void LateUpdate()
    {
        if (NeedsRefresh())
            ApplyNow();
    }

    public void ApplyNow()
    {
        if (!EnsureHierarchy()) return;

        Vector2 canvasSize = ResolveCanvasSize();
        Vector2 referenceResolution = ResolveReferenceResolution();
        if (!IsValid(canvasSize) || !IsValid(referenceResolution)) return;

        Vector2 safeSize = ResolveSafeAreaSize(canvasSize, referenceResolution);

        _safeAreaRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _safeAreaRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _safeAreaRoot.pivot = new Vector2(0.5f, 0.5f);
        _safeAreaRoot.anchoredPosition = Vector2.zero;
        _safeAreaRoot.sizeDelta = safeSize;
        _safeAreaRoot.localScale = Vector3.one;
        _safeAreaRoot.localRotation = Quaternion.identity;

        _lastCanvasSize = canvasSize;
        _lastReferenceResolution = referenceResolution;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
    }

    private bool EnsureHierarchy()
    {
        if (_root == null)
            _root = transform as RectTransform;
        if (_root == null) return false;

        if (_canvas == null)
            _canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>(true);

        if (_safeAreaRoot == null)
        {
            Transform existing = _root.Find(SafeAreaRootName);
            _safeAreaRoot = existing as RectTransform;
        }

        if (_safeAreaRoot == null)
        {
            var safeAreaObject = new GameObject(SafeAreaRootName, typeof(RectTransform));
            safeAreaObject.layer = gameObject.layer;
            _safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
            _safeAreaRoot.SetParent(_root, false);
        }

        MoveDirectChildrenUnderSafeArea();
        return true;
    }

    private void MoveDirectChildrenUnderSafeArea()
    {
        _childrenToMove.Clear();

        for (int i = 0; i < _root.childCount; i++)
        {
            Transform child = _root.GetChild(i);
            if (child == _safeAreaRoot) continue;
            _childrenToMove.Add(child);
        }

        for (int i = 0; i < _childrenToMove.Count; i++)
            _childrenToMove[i].SetParent(_safeAreaRoot, false);
    }

    private bool NeedsRefresh()
    {
        if (_safeAreaRoot == null) return true;
        if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height) return true;

        Vector2 canvasSize = ResolveCanvasSize();
        if ((canvasSize - _lastCanvasSize).sqrMagnitude > 0.01f) return true;

        Vector2 referenceResolution = ResolveReferenceResolution();
        return (referenceResolution - _lastReferenceResolution).sqrMagnitude > 0.01f;
    }

    private Vector2 ResolveCanvasSize()
    {
        RectTransform canvasRect = _canvas != null ? _canvas.transform as RectTransform : _root;
        if (canvasRect != null && IsValid(canvasRect.rect.size))
            return canvasRect.rect.size;

        return new Vector2(Screen.width, Screen.height);
    }

    private Vector2 ResolveSafeAreaSize(Vector2 canvasSize, Vector2 referenceResolution)
    {
        PixelPerfectCamera pixelPerfectCamera = ResolvePixelPerfectCamera();
        if (pixelPerfectCamera == null)
            return FitAspectInsideCanvas(canvasSize, referenceResolution);

        Vector2 pixelSize = CalculatePixelPerfectOutputSize(pixelPerfectCamera, referenceResolution);
        float canvasScale = _canvas != null && _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
        Vector2 safeSize = pixelSize / canvasScale;

        safeSize.x = Mathf.Min(safeSize.x, canvasSize.x);
        safeSize.y = Mathf.Min(safeSize.y, canvasSize.y);
        return safeSize;
    }

    private static Vector2 CalculatePixelPerfectOutputSize(PixelPerfectCamera pixelPerfectCamera, Vector2 referenceResolution)
    {
        float refX = Mathf.Max(1f, referenceResolution.x);
        float refY = Mathf.Max(1f, referenceResolution.y);
        int zoom = Mathf.Max(1, Mathf.Min(Screen.width / Mathf.RoundToInt(refX), Screen.height / Mathf.RoundToInt(refY)));

        switch (pixelPerfectCamera.cropFrame)
        {
            case PixelPerfectCamera.CropFrame.Pillarbox:
                return new Vector2(refX * zoom, Screen.height);
            case PixelPerfectCamera.CropFrame.Letterbox:
                return new Vector2(Screen.width, refY * zoom);
            case PixelPerfectCamera.CropFrame.Windowbox:
                return new Vector2(refX * zoom, refY * zoom);
            case PixelPerfectCamera.CropFrame.StretchFill:
                return FitAspectInsideScreen(referenceResolution);
            default:
                return new Vector2(Screen.width, Screen.height);
        }
    }

    private static Vector2 FitAspectInsideScreen(Vector2 referenceResolution)
    {
        float targetAspect = referenceResolution.x / referenceResolution.y;
        float screenAspect = Screen.width / (float)Screen.height;

        return screenAspect > targetAspect
            ? new Vector2(Screen.height * targetAspect, Screen.height)
            : new Vector2(Screen.width, Screen.width / targetAspect);
    }

    private static Vector2 FitAspectInsideCanvas(Vector2 canvasSize, Vector2 referenceResolution)
    {
        float targetAspect = referenceResolution.x / referenceResolution.y;
        float canvasAspect = canvasSize.x / canvasSize.y;

        return canvasAspect > targetAspect
            ? new Vector2(canvasSize.y * targetAspect, canvasSize.y)
            : new Vector2(canvasSize.x, canvasSize.x / targetAspect);
    }

    private Vector2 ResolveReferenceResolution()
    {
        PixelPerfectCamera pixelPerfectCamera = ResolvePixelPerfectCamera();
        if (pixelPerfectCamera != null)
            return new Vector2(pixelPerfectCamera.refResolutionX, pixelPerfectCamera.refResolutionY);

        return IsValid(_fallbackReferenceResolution) ? _fallbackReferenceResolution : DefaultReferenceResolution;
    }

    private PixelPerfectCamera ResolvePixelPerfectCamera()
    {
        if (_cachedPixelPerfectCamera != null && _cachedPixelPerfectCamera.isActiveAndEnabled)
            return _cachedPixelPerfectCamera;

        PixelPerfectCamera[] cameras = FindObjectsByType<PixelPerfectCamera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        PixelPerfectCamera firstEnabled = null;

        for (int i = 0; i < cameras.Length; i++)
        {
            PixelPerfectCamera camera = cameras[i];
            if (camera == null || !camera.isActiveAndEnabled) continue;

            if (!string.IsNullOrEmpty(_preferredPixelPerfectCameraName)
                && camera.name == _preferredPixelPerfectCameraName)
            {
                _cachedPixelPerfectCamera = camera;
                return camera;
            }

            firstEnabled ??= camera;
        }

        _cachedPixelPerfectCamera = firstEnabled;
        return _cachedPixelPerfectCamera;
    }

    private static bool IsValid(Vector2 size)
    {
        return size.x > 0.001f && size.y > 0.001f;
    }
}
