using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Explicit mobile-only pointer bridge for the field menu. Shared EventSystem pointer
/// actions remain disconnected, so this cannot enable PC mouse clicks on other game UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class FieldMenuTouchInput : MonoBehaviour
{
    private const float MaxTapSeconds = 0.55f;
    private const float DragDistanceReferencePixels = 8f;
    private static FieldMenuTouchInput s_active;
    private readonly List<RaycastResult> _raycasts = new List<RaycastResult>(32);
    private UIPanel _owner;
    private Canvas _canvas;
    private RectTransform _bounds;
    private Func<bool> _canInteract;
    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;
    private PointerEventData _pointer;
    private GameObject _pressedHit;
    private GameObject _pressedTarget;
    private GameObject _clickTarget;
    private GameObject _dragTarget;
    private Vector2 _startPosition;
    private Vector2 _lastPosition;
    private float _pressedAt;
    private int _touchId = -1;
    private bool _moved;
    private bool _dragging;
    private bool _hasFocus = true;

    // Ownership lasts until the menu GameObject is disabled, including its closing motion.
    public static bool IsActive => s_active != null && s_active.isActiveAndEnabled
        && s_active._owner != null && s_active._owner.gameObject.activeInHierarchy;

    public void Initialize(UIPanel owner, Canvas canvas, RectTransform bounds, Func<bool> canInteract)
    {
        CancelCapture();
        _owner = owner;
        _canvas = canvas;
        _bounds = bounds;
        _canInteract = canInteract;
        _raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
        if (isActiveAndEnabled && owner != null) s_active = this;
    }

    private void OnEnable()
    {
        if (_owner != null) s_active = this;
    }

    private void Update()
    {
        if (!Application.isMobilePlatform || !_hasFocus || _owner == null || _canvas == null
            || !_canvas.isActiveAndEnabled || _bounds == null || !_bounds.gameObject.activeInHierarchy
            || _canInteract == null || !_canInteract() || _raycaster == null || EventSystem.current == null)
        {
            CancelCapture();
            return;
        }

        Touchscreen screen = Touchscreen.current;
        if (screen == null) { CancelCapture(); return; }
        EnsurePointer(EventSystem.current);
        for (int i = 0; i < screen.touches.Count; i++)
        {
            var touch = screen.touches[i];
            int id = touch.touchId.ReadValue();
            if (_touchId >= 0)
            {
                if (id != _touchId) continue;
                Vector2 position = touch.position.ReadValue();
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    CancelCapture();
                else if (touch.press.isPressed)
                    MoveCapture(position);
                else
                    FinishCapture(position);
                return;
            }

            if (!touch.press.wasPressedThisFrame) continue;
            Vector2 start = touch.position.ReadValue();
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_bounds, start, camera)) continue;
            BeginCapture(id, start);
            return;
        }
        // A removed touch/device cannot leave a hold button or ScrollRect dragging forever.
        if (_touchId >= 0) CancelCapture();
    }

    private void EnsurePointer(EventSystem eventSystem)
    {
        if (_eventSystem == eventSystem && _pointer != null) return;
        CancelCapture();
        _eventSystem = eventSystem;
        _pointer = new PointerEventData(eventSystem);
    }

    private void BeginCapture(int touchId, Vector2 position)
    {
        _pointer.Reset();
        _pointer.pointerId = touchId;
        _pointer.button = PointerEventData.InputButton.Left;
        _pointer.position = _pointer.pressPosition = position;
        _pointer.delta = Vector2.zero;
        RaycastResult hit = RaycastAt(position);
        // Mirror the standard input module's selection ownership. Our buttons use
        // Navigation.None, so Selectable.OnPointerDown will not end a TMP edit.
        GameObject selection = hit.gameObject != null
            ? ExecuteEvents.GetEventHandler<ISelectHandler>(hit.gameObject) : null;
        if (_eventSystem.currentSelectedGameObject != selection)
            _eventSystem.SetSelectedGameObject(null, _pointer);
        if (hit.gameObject == null) return;

        _touchId = touchId;
        _pressedHit = hit.gameObject;
        _startPosition = _lastPosition = position;
        _pressedAt = Time.unscaledTime;
        _moved = _dragging = false;
        _pointer.pointerPressRaycast = hit;
        _pointer.rawPointerPress = _pressedHit;
        _pointer.pointerEnter = _pressedHit;
        _pointer.eligibleForClick = true;
        _clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_pressedHit);
        _pointer.pointerClick = _clickTarget;
        ScrollRect scroll = _pressedHit.GetComponentInParent<ScrollRect>();
        _dragTarget = scroll != null && scroll.transform.IsChildOf(_bounds) ? scroll.gameObject : null;
        _pointer.pointerDrag = _dragTarget;
        if (_dragTarget != null)
            ExecuteEvents.Execute(_dragTarget, _pointer, ExecuteEvents.initializePotentialDrag);
        GameInput.NotifyMenuTouchInteraction();
        ExecuteEvents.ExecuteHierarchy(_pressedHit, _pointer, ExecuteEvents.pointerEnterHandler);
        // Store the handler before dispatch so a disabling callback can release its hold.
        _pressedTarget = ExecuteEvents.GetEventHandler<IPointerDownHandler>(_pressedHit);
        _pointer.pointerPress = _pressedTarget != null ? _pressedTarget : _clickTarget;
        if (_pressedTarget != null)
            ExecuteEvents.Execute(_pressedTarget, _pointer, ExecuteEvents.pointerDownHandler);
    }

    private void MoveCapture(Vector2 position)
    {
        _pointer.delta = position - _lastPosition;
        _pointer.position = position;
        float scale = Mathf.Max(1f, _canvas.scaleFactor * Mathf.Abs(_bounds.localScale.x));
        float threshold = Mathf.Max(_eventSystem.pixelDragThreshold, DragDistanceReferencePixels * scale);
        if (!_moved && (position - _startPosition).sqrMagnitude >= threshold * threshold)
        {
            _moved = true;
            _pointer.eligibleForClick = false;
            ReleasePress();
            if (_touchId < 0) return;
            if (_dragTarget != null)
            {
                _pointer.position = _startPosition;
                _pointer.dragging = _dragging = true;
                ExecuteEvents.Execute(_dragTarget, _pointer, ExecuteEvents.beginDragHandler);
                _pointer.position = position;
            }
        }
        if (_dragging && _dragTarget != null)
            ExecuteEvents.Execute(_dragTarget, _pointer, ExecuteEvents.dragHandler);
        _lastPosition = position;
    }

    private void FinishCapture(Vector2 position)
    {
        MoveCapture(position);
        if (_touchId < 0) return;
        GameObject click = _clickTarget;
        RaycastResult hit = RaycastAt(position);
        bool activate = !_moved && _pointer.eligibleForClick && hit.gameObject == _pressedHit
            && Time.unscaledTime - _pressedAt <= MaxTapSeconds;
        ReleasePress();
        // Pointer-up callbacks can close/disable the menu. Never dispatch a stale click.
        if (_touchId < 0) return;
        bool canClick = activate && click != null && isActiveAndEnabled && _canInteract != null && _canInteract();
        _touchId = -1;
        if (canClick) ExecuteEvents.Execute(click, _pointer, ExecuteEvents.pointerClickHandler);
        CancelCapture();
    }

    private RaycastResult RaycastAt(Vector2 position)
    {
        _pointer.position = position;
        _raycasts.Clear();
        _raycaster.Raycast(_pointer, _raycasts);
        for (int i = 0; i < _raycasts.Count; i++)
        {
            RaycastResult hit = _raycasts[i];
            if (hit.gameObject == null || !hit.gameObject.transform.IsChildOf(_bounds)) continue;
            _pointer.pointerCurrentRaycast = hit;
            return hit;
        }
        _pointer.pointerCurrentRaycast = default;
        return default;
    }

    private void ReleasePress()
    {
        GameObject pressed = _pressedTarget;
        _pressedTarget = null;
        if (pressed != null && _pointer != null)
            ExecuteEvents.Execute(pressed, _pointer, ExecuteEvents.pointerUpHandler);
        if (_pointer != null) _pointer.pointerPress = null;
    }

    private void CancelCapture()
    {
        _touchId = -1;
        ReleasePress();
        GameObject drag = _dragTarget;
        GameObject entered = _pressedHit;
        bool wasDragging = _dragging;
        _dragTarget = _pressedHit = _clickTarget = null;
        _dragging = _moved = false;
        if (_pointer == null) return;
        _pointer.eligibleForClick = false;
        if (wasDragging && drag != null)
            ExecuteEvents.Execute(drag, _pointer, ExecuteEvents.endDragHandler);
        if (entered != null)
            ExecuteEvents.ExecuteHierarchy(entered, _pointer, ExecuteEvents.pointerExitHandler);
        _pointer.dragging = false;
        _pointer.pointerDrag = _pointer.pointerClick = _pointer.rawPointerPress = _pointer.pointerEnter = null;
    }

    private void OnApplicationFocus(bool focused)
    {
        _hasFocus = focused;
        if (!focused) CancelCapture();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) CancelCapture();
    }

    private void OnDisable()
    {
        CancelCapture();
        if (s_active == this) s_active = null;
    }

    private void OnDestroy()
    {
        CancelCapture();
        if (s_active == this) s_active = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => s_active = null;
}
