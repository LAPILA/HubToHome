using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pointer hold is local to the visible menu and uses unscaled time.</summary>
public sealed class FieldMenuButtonRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Action<FieldMenuCommand> _dispatch;
    private FieldMenuCommand _command;
    public FieldMenuCommand Command => _command;
    private bool _repeat, _held, _pointerGesture;
    private float _nextRepeat;
    private UnityEngine.UI.Button _button;
    public void Configure(Action<FieldMenuCommand> dispatch, FieldMenuCommand command, bool repeat)
    { _dispatch = dispatch; _command = command; _repeat = repeat; if (_button == null) _button = GetComponent<UnityEngine.UI.Button>(); }
    public void Submit()
    {
        if (_repeat && _pointerGesture) { _pointerGesture = false; return; }
        _dispatch?.Invoke(_command);
    }
    public void SubmitFromKeyboard()
    {
        // A cancelled/long pointer hold may not produce a click. Never consume the next fresh Z.
        _held = _pointerGesture = false;
        _dispatch?.Invoke(_command);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_repeat || _button == null || !_button.IsInteractable()) return;
        _held = _pointerGesture = true;
        _nextRepeat = Time.unscaledTime + 0.35f;
        _dispatch?.Invoke(_command);
    }
    public void OnPointerUp(PointerEventData eventData) { _held = false; }
    public void OnPointerExit(PointerEventData eventData) { _held = false; }
    private void Update()
    {
        if (!_held || Time.unscaledTime < _nextRepeat) return;
        if (_button == null || !_button.IsInteractable()) { _held = false; return; }
        _nextRepeat = Time.unscaledTime + 0.08f;
        _dispatch?.Invoke(_command);
    }
    private void OnDisable() { _held = _pointerGesture = false; }
}
