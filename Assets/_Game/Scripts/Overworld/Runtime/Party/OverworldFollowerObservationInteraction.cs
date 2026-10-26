using UnityEngine;

/// <summary>Room 수명의 충돌 없는 대화 프록시. 실제 동료의 표시나 파티는 복제하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class OverworldFollowerObservationInteraction : MonoBehaviour, IInteractable
{
    private OverworldFollowerObservation _owner;
    private BoxCollider2D _trigger;

    internal void Configure(OverworldFollowerObservation owner, Vector2 size, Vector2 offset)
    {
        _owner = owner;
        _trigger = GetComponent<BoxCollider2D>();
        if (_trigger == null) _trigger = gameObject.AddComponent<BoxCollider2D>();
        _trigger.isTrigger = true;
        _trigger.size = size;
        _trigger.offset = offset;
        _trigger.enabled = false;
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) gameObject.layer = layer;
    }

    internal void SetAvailable(bool available, Vector3 position)
    {
        transform.position = position;
        if (_trigger != null) _trigger.enabled = available;
    }

    public bool CanInteract(PlayerController player)
        => _trigger != null && _trigger.enabled && _owner != null && _owner.CanInteract(player);

    public void Interact(PlayerController player)
    {
        if (CanInteract(player)) _owner.TryInteract(player);
    }

    public void ShowHighlight(bool show) { }
}
