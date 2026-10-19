using UnityEngine;

/// <summary>
/// Fits only the field menu's 640x480 frame into the gameplay output and device safe area.
/// The existing Canvas and UIViewportService remain the owners of output/camera scaling.
/// </summary>
[DisallowMultipleComponent]
public sealed class FieldMenuViewport : MonoBehaviour
{
    private static readonly Vector2 ReferenceSize = new Vector2(640f, 480f);
    private RectTransform _frame;
    private Canvas _canvas;
    private RectTransform _parent;
    private Camera _lastCamera;
    private Camera _lastUICamera;
    private Rect _lastOutput;
    private Rect _lastSafeArea;
    private Rect _lastParentRect;
    private float _lastCanvasScale = -1f;
    private bool _hasLayout;

    public void Initialize(RectTransform frame, Canvas canvas)
    {
        _frame = frame;
        _canvas = canvas;
        _parent = frame != null ? frame.parent as RectTransform : null;
        _hasLayout = false;
        ApplyIfChanged();
    }

    private void OnEnable() => _hasLayout = false;
    private void LateUpdate() => ApplyIfChanged();

    public static Rect CalculateSafeOutputRect(Rect output, Rect safeArea)
    {
        float xMin = Mathf.Max(output.xMin, safeArea.xMin);
        float yMin = Mathf.Max(output.yMin, safeArea.yMin);
        float xMax = Mathf.Min(output.xMax, safeArea.xMax);
        float yMax = Mathf.Min(output.yMax, safeArea.yMax);
        if (xMax <= xMin || yMax <= yMin)
            return new Rect(output.center, Vector2.zero);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public static Rect CalculateSafeContentRect(Rect output, Rect safeArea)
    {
        Rect safeOutput = CalculateSafeOutputRect(output, safeArea);
        return safeOutput.width > 0f && safeOutput.height > 0f
            ? UIViewportService.CalculateContentScreenRect(safeOutput)
            : safeOutput;
    }

    private void ApplyIfChanged()
    {
        if (_frame == null || _canvas == null) return;
        RectTransform currentParent = _frame.parent as RectTransform;
        if (_parent != currentParent)
        {
            _parent = currentParent;
            _hasLayout = false;
        }
        if (_parent == null) return;
        Canvas rootCanvas = _canvas.rootCanvas;
        Camera outputCamera = UIViewportService.GetOutputCamera(rootCanvas);
        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        // Overlay still follows the gameplay output bounds; only UI coordinate conversion is camera-less.
        Rect output = UIViewportService.GetOutputRect(outputCamera);
        Rect safeArea = Screen.safeArea;
        float canvasScale = Mathf.Max(0.0001f, rootCanvas.scaleFactor);
        Rect parentRect = _parent.rect;
        if (_hasLayout && outputCamera == _lastCamera && uiCamera == _lastUICamera
            && output == _lastOutput && safeArea == _lastSafeArea
            && Mathf.Approximately(canvasScale, _lastCanvasScale) && parentRect == _lastParentRect)
            return;

        Rect safeOutput = CalculateSafeOutputRect(output, safeArea);
        Rect content = CalculateSafeContentRect(output, safeArea);
        if (content.width <= 0f || content.height <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, content.center, uiCamera,
                out Vector2 localCenter))
            return;

        // Canvas.scaleFactor already contains the common integer fit. Apply only its ratio,
        // otherwise nesting this frame would multiply the output scale for a second time.
        float frameScale = UIViewportService.CalculateReferenceScale(safeOutput) / canvasScale;
        _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
        _frame.pivot = new Vector2(0.5f, 0.5f);
        _frame.sizeDelta = ReferenceSize;
        _frame.localRotation = Quaternion.identity;
        _frame.localScale = new Vector3(frameScale, frameScale, 1f);
        _frame.localPosition = new Vector3(localCenter.x, localCenter.y, 0f);

        _lastCamera = outputCamera;
        _lastUICamera = uiCamera;
        _lastOutput = output;
        _lastSafeArea = safeArea;
        _lastParentRect = parentRect;
        _lastCanvasScale = canvasScale;
        _hasLayout = true;
    }
}
