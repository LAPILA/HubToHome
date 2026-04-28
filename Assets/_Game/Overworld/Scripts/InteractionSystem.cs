using UnityEngine;

/// <summary>
/// 플레이어 전면의 IInteractable 오브젝트를 감지하는 독립 컴포넌트.
/// OverlapBox를 사용하여 플레이어가 바라보는 방향 앞을 탐색합니다.
/// </summary>
public class InteractionSystem : MonoBehaviour
{
    public static InteractionSystem Instance { get; private set; }

    [Header("Detection")]
    [SerializeField] private Vector2 _boxSize      = new Vector2(0.8f, 0.8f);
    [SerializeField] private float   _boxDistance  = 0.6f;
    [SerializeField] private LayerMask _interactLayer;

    // 방향 벡터 캐싱 (0=Down 1=Up 2=Left 3=Right)
    private static readonly Vector2[] _directionVectors =
    {
        Vector2.down,
        Vector2.up,
        Vector2.left,
        Vector2.right,
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 플레이어 전면의 IInteractable을 탐색하고 Interact()를 호출합니다.
    /// PlayerController.TryInteract()에서 호출됩니다.
    /// </summary>
    public void TryInteract(PlayerController player)
    {
        Vector2 dir    = _directionVectors[player.FacingDirection];
        Vector2 origin = (Vector2)player.transform.position + dir * _boxDistance;

        Collider2D hit = Physics2D.OverlapBox(origin, _boxSize, 0f, _interactLayer);
        if (hit == null) return;

        var interactable = hit.GetComponent<IInteractable>();
        if (interactable == null) return;

        if (interactable.CanInteract(player))
            interactable.Interact(player);
    }

    // ── 디버그 시각화 ─────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        // 에디터에서 감지 범위 시각화 (Down 방향 기준)
        Gizmos.color = Color.yellow;
        Vector2 origin = (Vector2)transform.position + Vector2.down * _boxDistance;
        Gizmos.DrawWireCube(origin, _boxSize);
    }
}
