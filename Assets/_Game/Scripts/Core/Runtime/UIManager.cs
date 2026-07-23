using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// UI 패널 열기/닫기를 총괄하는 싱글톤 매니저.
/// [개선] 씬 전환 시 에러 방지를 위해 런타임 패널 등록(Dictionary) 방식과 자동 뒤로가기(Pop)를 지원합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private readonly Stack<PanelStackEntry> _panelStack =
        new Stack<PanelStackEntry>();
    private readonly Dictionary<string, UIPanel> _registeredPanels =
        new Dictionary<string, UIPanel>(System.StringComparer.Ordinal);
    private readonly HashSet<UIPanel> _pixelPerfectSafeAreaPanels = new HashSet<UIPanel>();
    private readonly List<PanelStackEntry> _stackBuffer =
        new List<PanelStackEntry>();
    private readonly List<string> _stalePanelIds = new List<string>();
    private readonly List<UIPanel> _staleSafeAreaPanels = new List<UIPanel>();

    private struct PanelStackEntry
    {
        public UIPanel Panel;
        public GameObject PreviousSelection;

        public PanelStackEntry(UIPanel panel, GameObject previousSelection)
        {
            Panel = panel;
            PreviousSelection = previousSelection;
        }
    }

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
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        // 글로벌 패널 기본 등록
        if (_pausePanel != null) RegisterPanel(UIPanelId.Pause, _pausePanel);
        if (_saveLoadPanel != null) RegisterPanel(UIPanelId.SaveLoad, _saveLoadPanel);
        if (_overworldPanel != null)
            RegisterPanel(UIPanelId.Overworld, _overworldPanel, _fitOverworldPanelToPixelPerfectSafeArea);
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UIPanel topPanel = TopPanel;
        if (topPanel != null
            && GameInput.UICancelPressed
            && !topPanel.TryHandleCancelInput())
            CloseTopPanel();
    }

    public void RegisterPanel(string panelID, UIPanel panel)
    {
        RegisterPanel(panelID, panel, false);
    }

    public void RegisterPanel(string panelID, UIPanel panel, bool fitToPixelPerfectSafeArea)
    {
        if (string.IsNullOrWhiteSpace(panelID) || panel == null)
        {
            Debug.LogWarning("[UIManager] 패널 ID와 패널 참조가 모두 필요합니다.", this);
            return;
        }

        PruneInvalidState();
        if (_registeredPanels.TryGetValue(panelID, out UIPanel previous)
            && previous != null
            && previous != panel)
        {
            RemovePanelFromStack(previous, true, true);
            if (!IsPanelRegistered(previous, panelID))
                _pixelPerfectSafeAreaPanels.Remove(previous);
        }

        _registeredPanels[panelID] = panel;

        if (fitToPixelPerfectSafeArea)
            RegisterPixelPerfectSafeAreaPanel(panel);
    }

    public void UnregisterPanel(string panelID)
    {
        if (string.IsNullOrWhiteSpace(panelID)
            || !_registeredPanels.TryGetValue(panelID, out UIPanel panel))
            return;

        _registeredPanels.Remove(panelID);
        if (panel != null)
        {
            RemovePanelFromStack(panel, true, true);
            if (!IsPanelRegistered(panel, null))
                _pixelPerfectSafeAreaPanels.Remove(panel);
        }
    }

    public void OpenPanel(string panelID)
    {
        PruneInvalidState();
        if (!string.IsNullOrWhiteSpace(panelID)
            && _registeredPanels.TryGetValue(panelID, out UIPanel panel)
            && panel != null)
        {
            OpenPanel(panel);
        }
        else
            Debug.LogWarning($"[UIManager] '{panelID}' 패널을 찾을 수 없습니다! RegisterPanel이 호출되었는지 확인하세요.");
    }

    public void OpenPanel(UIPanel panel)
    {
        if (panel == null) return;
        PruneInvalidState();
        if (_panelStack.Count > 0 && _panelStack.Peek().Panel == panel)
            return;

        EnsurePixelPerfectSafeAreaIfNeeded(panel);
        RemovePanelFromStack(panel, false, false);
        EventSystem eventSystem = ResolveEventSystem();
        GameObject previousSelection = eventSystem != null
            ? eventSystem.currentSelectedGameObject
            : null;
        _panelStack.Push(new PanelStackEntry(panel, previousSelection));
        panel.Show();
        panel.FocusDefaultSelection();
    }

    public void CloseTopPanel()
    {
        if (_panelStack.Count == 0) return;

        PanelStackEntry entry = _panelStack.Pop();
        if (entry.Panel == null)
        {
            RestoreSelection(entry.PreviousSelection, null, false);
            PruneInvalidState();
            return;
        }

        UIPanel panel = entry.Panel;
        panel.Hide();
        RestoreSelection(entry.PreviousSelection, panel, false);
    }

    public void CloseAllPanels()
    {
        CloseAllPanels(false);
    }

    public void CloseAllPanelsImmediate()
    {
        CloseAllPanels(true);
    }

    public bool IsAnyPanelOpen
    {
        get
        {
            PruneInvalidState();
            return _panelStack.Count > 0;
        }
    }

    public int OpenPanelCount
    {
        get
        {
            PruneInvalidState();
            return _panelStack.Count;
        }
    }

    public UIPanel TopPanel
    {
        get
        {
            PruneInvalidState();
            return _panelStack.Count > 0 ? _panelStack.Peek().Panel : null;
        }
    }

    private void CloseAllPanels(bool immediate)
    {
        GameObject selectionToRestore = null;
        bool removedAny = false;
        while (_panelStack.Count > 0)
        {
            PanelStackEntry entry = _panelStack.Pop();
            selectionToRestore = entry.PreviousSelection;
            removedAny = true;
            if (entry.Panel == null)
                continue;

            if (immediate)
                entry.Panel.HideImmediate();
            else
                entry.Panel.Hide();
        }

        if (removedAny)
            RestoreSelection(selectionToRestore, null, true);
    }

    private bool RemovePanelFromStack(
        UIPanel panel,
        bool hidePanel,
        bool restoreTopSelection)
    {
        if (panel == null || _panelStack.Count == 0)
            return false;

        _stackBuffer.Clear();
        bool removed = false;
        bool removedTop = false;
        GameObject selectionToRestore = null;
        bool isTop = true;

        while (_panelStack.Count > 0)
        {
            PanelStackEntry entry = _panelStack.Pop();
            if (entry.Panel == panel)
            {
                removed = true;
                if (isTop)
                {
                    removedTop = true;
                    selectionToRestore = entry.PreviousSelection;
                }
            }
            else if (entry.Panel != null)
            {
                _stackBuffer.Add(entry);
            }
            isTop = false;
        }

        RebuildStackFromBuffer();
        if (removed && hidePanel && panel != null)
            panel.HideImmediate();
        if (removedTop && restoreTopSelection)
            RestoreSelection(selectionToRestore, panel, false);
        return removed;
    }

    private void PruneInvalidState()
    {
        PruneInvalidStackEntries();
        PruneInvalidRegistrations();
    }

    private void PruneInvalidStackEntries()
    {
        if (_panelStack.Count == 0)
            return;

        _stackBuffer.Clear();
        bool scanningTop = true;
        bool removedTop = false;
        GameObject selectionToRestore = null;

        while (_panelStack.Count > 0)
        {
            PanelStackEntry entry = _panelStack.Pop();
            if (entry.Panel == null)
            {
                if (scanningTop)
                {
                    removedTop = true;
                    selectionToRestore = entry.PreviousSelection;
                }
                continue;
            }

            scanningTop = false;
            _stackBuffer.Add(entry);
        }

        RebuildStackFromBuffer();
        if (removedTop)
            RestoreSelection(selectionToRestore, null, false);
    }

    private void RebuildStackFromBuffer()
    {
        for (int i = _stackBuffer.Count - 1; i >= 0; i--)
            _panelStack.Push(_stackBuffer[i]);
        _stackBuffer.Clear();
    }

    private void PruneInvalidRegistrations()
    {
        _stalePanelIds.Clear();
        foreach (KeyValuePair<string, UIPanel> pair in _registeredPanels)
        {
            if (pair.Value == null)
                _stalePanelIds.Add(pair.Key);
        }
        for (int i = 0; i < _stalePanelIds.Count; i++)
            _registeredPanels.Remove(_stalePanelIds[i]);

        _staleSafeAreaPanels.Clear();
        foreach (UIPanel panel in _pixelPerfectSafeAreaPanels)
        {
            if (panel == null)
                _staleSafeAreaPanels.Add(panel);
        }
        for (int i = 0; i < _staleSafeAreaPanels.Count; i++)
            _pixelPerfectSafeAreaPanels.Remove(_staleSafeAreaPanels[i]);
    }

    private bool IsPanelRegistered(UIPanel panel, string ignoredPanelId)
    {
        foreach (KeyValuePair<string, UIPanel> pair in _registeredPanels)
        {
            if (pair.Key != ignoredPanelId && pair.Value == panel)
                return true;
        }
        return false;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        CloseAllPanelsImmediate();
        PruneInvalidState();
    }

    private void RestoreSelection(
        GameObject previousSelection,
        UIPanel closingPanel,
        bool clearWhenUnavailable)
    {
        EventSystem eventSystem = ResolveEventSystem();
        if (eventSystem == null)
            return;

        if (previousSelection != null && previousSelection.activeInHierarchy)
        {
            eventSystem.SetSelectedGameObject(previousSelection);
            return;
        }

        GameObject currentSelection = eventSystem.currentSelectedGameObject;
        bool selectionBelongsToClosingPanel = currentSelection != null
            && closingPanel != null
            && currentSelection.transform.IsChildOf(closingPanel.transform);
        if (clearWhenUnavailable || selectionBelongsToClosingPanel)
            eventSystem.SetSelectedGameObject(null);
    }

    protected virtual EventSystem ResolveEventSystem()
    {
        return EventSystem.current;
    }

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
