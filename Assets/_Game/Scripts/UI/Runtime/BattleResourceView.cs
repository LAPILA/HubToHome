using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders the selected actor's resource inside the existing HUD Image.</summary>
[DisallowMultipleComponent]
public sealed class BattleResourceView : MonoBehaviour
{
    private Image _image;
    private CharacterBattleResource _state;
    private BattleResourceStage _stage;
    private int _frame;
    private float _remaining;
    private bool _subscribed;

    public void Bind(Image image, CharacterBattleResource state)
    {
        Unsubscribe();
        _image = image;
        _state = state;
        Subscribe();
        Refresh();
    }

    private void OnEnable() { Subscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); }

    private void Subscribe()
    {
        if (_subscribed || _state == null || !isActiveAndEnabled) return;
        _state.Changed += Refresh;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_subscribed && _state != null) _state.Changed -= Refresh;
        _subscribed = false;
    }

    private void Refresh()
    {
        _stage = _state?.Definition != null ? _state.Definition.GetStage(_state.Step) : null;
        _frame = 0;
        ShowFrame();
    }

    private void ShowFrame()
    {
        BattleResourceFrame frame = _stage?.Frames != null && _frame < _stage.Frames.Length
            ? _stage.Frames[_frame] : null;
        _remaining = frame != null ? Mathf.Max(.01f, frame.Duration) : 0f;
        if (_image == null) return;
        _image.sprite = frame?.Sprite;
        _image.enabled = _image.sprite != null;
        _image.preserveAspect = true;
        _image.raycastTarget = false;
    }

    private void Update()
    {
        // No per-frame sprite writes for the current single-frame step assets.
        if (_image == null || !_image.enabled || _stage?.Frames == null || _stage.Frames.Length < 2
            || Time.timeScale <= 0f || GameInput.IsDefenseInputBlocked) return;
        _remaining -= Time.unscaledDeltaTime;
        if (_remaining > 0f) return;
        _frame++;
        if (_frame >= _stage.Frames.Length)
        {
            if (!_stage.Loop) { _frame = _stage.Frames.Length - 1; _remaining = float.PositiveInfinity; return; }
            _frame = 0;
        }
        ShowFrame();
    }
}
