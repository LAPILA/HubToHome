using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FixedViewport UI is drawn after gameplay rendering, outside lighting/post processing.
/// Only its bounds follow the game camera; authored anchors stay inside a 640x480 frame.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class FixedCanvasViewport : MonoBehaviour
{
    private static readonly Vector2 ReferenceSize = new Vector2(640f, 480f);
    private readonly List<Transform> _childrenToMove = new List<Transform>();
    private Canvas _canvas;
    private CanvasScaler _scaler;
    private RectTransform _content;
    private Transform _worldVisuals;
    private Camera _output;
    private Rect _lastOutput;
    private Vector2Int _lastScreen;
    private bool _movingChildren;

    public RectTransform ContentRect => _content;
    public Camera OutputCamera => _output;

    public void Configure(Camera output)
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (!_canvas.isRootCanvas || _canvas.renderMode == RenderMode.WorldSpace) return;
        _output = output;
        if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
        if (_scaler == null) _scaler = gameObject.AddComponent<CanvasScaler>();

        // Legacy title art contains a SpriteRenderer under the camera Canvas.
        // It is world art, not uGUI: detach before changing the Canvas transform.
        PreserveWorldVisuals();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.worldCamera = null;
        _canvas.pixelPerfect = true;
        _canvas.overrideSorting = true;
        _canvas.sortingLayerID = 0;
        // Keep authored order between settings/shop/results; Overlay already draws above the world.
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        _scaler.referenceResolution = ReferenceSize;
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight = 0.5f;
        if (_content == null)
        {
            _content = new GameObject("Fixed UI Viewport (640x480)", typeof(RectTransform))
                .GetComponent<RectTransform>();
            _content.gameObject.layer = gameObject.layer;
            _content.SetParent(transform, false);
        }
        MoveDirectChildren();
        ApplyLayout();
    }

    private void OnTransformChildrenChanged()
    {
        if (!_movingChildren && _content != null) MoveDirectChildren();
    }

    private void MoveDirectChildren()
    {
        if (_movingChildren || _content == null) return;
        _movingChildren = true;
        _childrenToMove.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == _content || !(child is RectTransform)) continue;
            if (child.TryGetComponent(out Canvas childCanvas) && childCanvas.isRootCanvas
                && childCanvas.renderMode == RenderMode.WorldSpace) continue;
            _childrenToMove.Add(child);
        }
        for (int i = 0; i < _childrenToMove.Count; i++)
        {
            var rect = (RectTransform)_childrenToMove[i];
            Vector3 position = rect.anchoredPosition3D;
            Vector2 size = rect.sizeDelta;
            rect.SetParent(_content, false);
            rect.anchoredPosition3D = position;
            rect.sizeDelta = size;
        }
        _childrenToMove.Clear();
        _movingChildren = false;
    }

    private void PreserveWorldVisuals()
    {
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;
        bool hasWorldVisuals = false;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!(child is RectTransform) && child.GetComponentInChildren<Renderer>(true) != null)
            { hasWorldVisuals = true; break; }
        }
        if (!hasWorldVisuals) return;
        // Resolve camera-Canvas transforms before capturing world poses (only once for mixed roots).
        if (_canvas.worldCamera != null) Canvas.ForceUpdateCanvases();
        _movingChildren = true;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child is RectTransform || child == _worldVisuals
                || child.GetComponentInChildren<Renderer>(true) == null) continue;
            if (_worldVisuals == null)
            {
                _worldVisuals = new GameObject("Fixed UI World Visuals").transform;
                // Inherit the owner's scene first (including persistent UI), then leave the Canvas.
                _worldVisuals.SetParent(transform, false);
                _worldVisuals.SetParent(transform.parent, true);
                _worldVisuals.gameObject.SetActive(isActiveAndEnabled);
            }
            child.SetParent(_worldVisuals, true);
        }
        _movingChildren = false;
    }

    private void OnEnable()
    {
        if (_worldVisuals != null) _worldVisuals.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_worldVisuals != null) _worldVisuals.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_worldVisuals == null) return;
        if (Application.isPlaying) Destroy(_worldVisuals.gameObject);
        else DestroyImmediate(_worldVisuals.gameObject);
    }

    private void LateUpdate()
    {
        Rect output = UIViewportService.GetOutputRect(_output);
        if (_lastOutput != output || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_content == null || _canvas == null || _scaler == null) return;
        Rect output = UIViewportService.GetOutputRect(_output);
        float scale = UIViewportService.CalculateReferenceScale(output);
        _scaler.scaleFactor = scale;
        _canvas.scaleFactor = scale;
        _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
        _content.sizeDelta = ReferenceSize;
        _content.anchoredPosition = (UIViewportService.CalculateContentScreenRect(output).center
            - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        _content.localScale = Vector3.one;
        _content.localRotation = Quaternion.identity;
        _lastOutput = output;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
    }
}
