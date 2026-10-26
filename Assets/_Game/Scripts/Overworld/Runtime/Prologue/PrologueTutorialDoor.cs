using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PrologueTutorialDoor : InteractableBase
{
    [SerializeField, LabelText("대상 객차")] private PrologueTutorialRoom _targetRoom;
    [SerializeField, LabelText("도착 지점 ID")] private string _spawnPointId = "default";
    [SerializeField, LabelText("접촉으로 이동")] private bool _enterOnTrigger = true;
    [SerializeField, Min(0f), LabelText("도착 재진입 방지 시간")] private float _arrivalSuppressSeconds = .25f;
    private PrologueTutorialController _controller;
    private Collider2D _trigger;
    private readonly HashSet<Collider2D> _playerContacts = new HashSet<Collider2D>();
    private bool _usedUntilExit;
    private float _blockedUntil;

    public void Configure(PrologueTutorialRoom targetRoom, string spawnPointId = "default", bool enterOnTrigger = true)
    { _targetRoom = targetRoom; _spawnPointId = spawnPointId; _enterOnTrigger = enterOnTrigger; }

    private void OnEnable()
    {
        _playerContacts.Clear();
        _usedUntilExit = false;
        _blockedUntil = 0f;
        if (_trigger == null) _trigger = GetComponent<Collider2D>();
        _controller = null;
        PrologueTutorialController[] controllers = FindObjectsByType<PrologueTutorialController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].gameObject.scene != gameObject.scene) continue;
            if (_controller != null) { _controller = null; return; }
            _controller = controllers[i];
        }
    }

    public override bool CanInteract(PlayerController player)
        => !_usedUntilExit && Time.unscaledTime >= _blockedUntil && base.CanInteract(player)
            && _controller != null && _controller.CanTravelToRoom(_targetRoom);

    public override void Interact(PlayerController player)
    {
        if (CanInteract(player) && _controller.TryTravelToRoom(_targetRoom, _spawnPointId, player))
            _usedUntilExit = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || _controller == null || player != _controller.FieldPlayer) return;
        _playerContacts.Add(other);
        if (!_enterOnTrigger) return;
        if (Time.unscaledTime < _blockedUntil || (MapTransitionService.Instance != null && MapTransitionService.Instance.IsTransitioning))
        { _usedUntilExit = true; return; }
        Interact(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !_playerContacts.Remove(other)) return;
        if (_playerContacts.Count == 0) _usedUntilExit = false;
    }

    public void SuppressForSeconds(float seconds)
    {
        float fallback = float.IsNaN(_arrivalSuppressSeconds) || float.IsInfinity(_arrivalSuppressSeconds)
            ? .25f : Mathf.Max(0f, _arrivalSuppressSeconds);
        float safe = float.IsNaN(seconds) || float.IsInfinity(seconds) ? fallback : Mathf.Max(0f, seconds);
        bool overlaps = _playerContacts.Count > 0;
        // 반경 안에 있지만 실제로 닿지 않는 안전 Spawn은 다음 정상 접근까지 막지 않습니다.
        PlayerController player = _controller != null ? _controller.FieldPlayer : null;
        if (!overlaps && player != null && _trigger != null)
        {
            Collider2D[] colliders = player.GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null && colliders[i].enabled && _trigger.bounds.Intersects(colliders[i].bounds))
                { overlaps = true; break; }
        }
        if (!overlaps) return;
        _blockedUntil = Mathf.Max(_blockedUntil, Time.unscaledTime + safe);
        _usedUntilExit = true;
    }

    private void OnDisable() { _playerContacts.Clear(); _usedUntilExit = false; }
}
