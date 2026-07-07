using UnityEngine;

public class HazardMarker : AreaMarkerBase
{
    [Header("Hazard")]
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0f)] private float knockback = 0.5f;
    [SerializeField] private bool triggerOnEnter = true;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Hazard;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        ApplyHazard(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter || !CanInteract()) return;
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) ApplyHazard(player);
    }

    private void ApplyHazard(PlayerController player)
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = player.GetFacingVector2();
        player.NudgeFromEncounter(dir, knockback);
        Debug.Log($"[HazardMarker] 피해 요청: damage={damage}, knockback={knockback}", this);
        if (isOneShot) CompleteMarker();
    }
}