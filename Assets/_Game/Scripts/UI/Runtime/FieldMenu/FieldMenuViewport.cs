using UnityEngine;

/// <summary>
/// Keeps the compact menu on the common 640x480 integer-scaled output and device safe area.
/// The existing Canvas and UIViewportService remain the owners of output/camera scaling.
/// </summary>
[DisallowMultipleComponent]
public sealed class FieldMenuViewport : MonoBehaviour
{
    private RectTransform _frame;
    private FieldMenuView _view;
    private Canvas _canvas;
    private RectTransform _parent;
    private Camera _lastCamera;
    private Camera _lastUICamera;
    private Rect _lastOutput;
    private Rect _lastSafeArea;
    private Rect _lastParentRect;
    private float _lastCanvasScale = -1f;
    private Rect _lastKeyboardArea;
    private Rect _lastFocusedScreenRect;
    private float _keyboardLiftScreen;
    private readonly Vector3[] _fieldCorners = new Vector3[4];
    private bool _hasLayout;

    public void Initialize(RectTransform frame, Canvas canvas)
    {
        _frame = frame;
        _view = frame != null ? frame.GetComponent<FieldMenuView>() : null;
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

    /// <summary>Only translates the frame; font size and integer output scale never change for a keyboard.</summary>
    public static float CalculateKeyboardLift(Rect field, Rect keyboard, Rect safeOutput, float padding = 12f)
    {
        if (field.width <= 0f || field.height <= 0f || keyboard.width <= 0f || keyboard.height <= 0f
            || field.xMax <= keyboard.xMin || field.xMin >= keyboard.xMax) return 0f;
        float desired = keyboard.yMax + Mathf.Max(0, padding) - field.yMin;
        float available = safeOutput.yMax - Mathf.Max(0, padding) - field.yMax;
        return Mathf.Clamp(desired, 0, Mathf.Max(0, available));
    }

    private Rect GetFocusedScreenRect(Camera uiCamera)
    {
        RectTransform field = _view != null ? _view.FocusedNumberRect : null;
        if (field == null) return Rect.zero;
        field.GetWorldCorners(_fieldCorners);
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(uiCamera, _fieldCorners[0]);
        Vector2 maximum = minimum;
        for (int i = 1; i < _fieldCorners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, _fieldCorners[i]);
            minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
        }
        // The current transform already includes the previous lift. Compare the original placement.
        minimum.y -= _keyboardLiftScreen; maximum.y -= _keyboardLiftScreen;
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
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
        Rect focusedScreenRect = GetFocusedScreenRect(uiCamera);
        Rect keyboardArea = focusedScreenRect.width > 0f && TouchScreenKeyboard.visible
            ? TouchScreenKeyboard.area : Rect.zero;
        // Some mobile platforms do not report native keyboard bounds. Use a conservative
        // lower-screen obstruction only while an actual native keyboard is visible.
        if (focusedScreenRect.width > 0f && TouchScreenKeyboard.visible && keyboardArea.height <= 0f)
            keyboardArea = new Rect(0, 0, Screen.width, Screen.height * .45f);
        if (_hasLayout && outputCamera == _lastCamera && uiCamera == _lastUICamera
            && output == _lastOutput && safeArea == _lastSafeArea
            && Mathf.Approximately(canvasScale, _lastCanvasScale) && parentRect == _lastParentRect
            && keyboardArea == _lastKeyboardArea && focusedScreenRect == _lastFocusedScreenRect)
            return;

        Rect safeOutput = CalculateSafeOutputRect(output, safeArea);
        Rect content = CalculateSafeContentRect(output, safeArea);
        if (content.width <= 0f || content.height <= 0f) return;
        float keyboardLift = CalculateKeyboardLift(focusedScreenRect, keyboardArea, safeOutput);
        Vector2 center = content.center + new Vector2(0, Mathf.Ceil(keyboardLift));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, center, uiCamera,
                out Vector2 localCenter))
            return;

        // Canvas.scaleFactor already contains the common integer fit. Apply only its ratio,
        // otherwise nesting this frame would multiply the output scale for a second time.
        float frameScale = UIViewportService.CalculateReferenceScale(safeOutput) / canvasScale;
        _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
        _frame.pivot = new Vector2(0.5f, 0.5f);
        _frame.sizeDelta = FieldMenuView.FrameSize;
        _frame.localRotation = Quaternion.identity;
        _frame.localScale = new Vector3(frameScale, frameScale, 1f);
        _frame.localPosition = new Vector3(localCenter.x, localCenter.y, 0f);

        _lastCamera = outputCamera;
        _lastUICamera = uiCamera;
        _lastOutput = output;
        _lastSafeArea = safeArea;
        _lastParentRect = parentRect;
        _lastCanvasScale = canvasScale;
        _lastKeyboardArea = keyboardArea;
        _lastFocusedScreenRect = focusedScreenRect;
        _keyboardLiftScreen = Mathf.Ceil(keyboardLift);
        _hasLayout = true;
    }
}
