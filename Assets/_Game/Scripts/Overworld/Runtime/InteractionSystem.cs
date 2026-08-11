using UnityEngine;

/// <summary>
/// 플레이어 이동/방향 전환 시 즉시, 정지 중에는 낮은 주기로 전면을 탐색하여 상호작용 대상을 캐싱합니다.
/// 메모리 할당(GC)이 없는 NonAlloc 물리 캐스트를 사용합니다.
/// </summary>
public class InteractionSystem : MonoBehaviour
{
    public static InteractionSystem Instance { get; private set; }

    [Header("Detection Settings")]
    [SerializeField] private Vector2 _boxSize      = new Vector2(0.8f, 0.8f);
    [SerializeField] private float   _boxDistance  = 0.6f;
    [SerializeField] private LayerMask _interactLayer;
    [SerializeField, Min(0.02f)] private float _stationaryPollInterval = 0.1f;

    private IInteractable _currentTarget; 
    private readonly Collider2D[] _hitResults = new Collider2D[1]; 
    private PlayerController _player;
    private Vector2 _lastPlayerPosition;
    private int _lastFacingDirection;
    private float _nextStationaryPollTime;
    private bool _hasDetectionSample;
    
    private ContactFilter2D _contactFilter;

    private static readonly Vector2[] _directionVectors = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _contactFilter = new ContactFilter2D();
        _contactFilter.useLayerMask = true;
        _contactFilter.layerMask = _interactLayer;
        _contactFilter.useTriggers = true; // 트리거 콜라이더도 감지하도록 설정
    }

    private void Start()
    {
        CachePlayerIfNeeded();
    }

    private void Update()
    {
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) 
        {
            ClearTarget();
            _hasDetectionSample = false;
            return;
        }

        PlayerController player = CachePlayerIfNeeded();
        if (player == null)
        {
            ClearTarget();
            _hasDetectionSample = false;
            return;
        }

        Vector2 position = player.transform.position;
        int facingDirection = player.FacingDirection;
        bool movedOrTurned = !_hasDetectionSample
            || (position - _lastPlayerPosition).sqrMagnitude > 0.000001f
            || facingDirection != _lastFacingDirection;

        if (!movedOrTurned && Time.unscaledTime < _nextStationaryPollTime)
            return;

        _hasDetectionSample = true;
        _lastPlayerPosition = position;
        _lastFacingDirection = facingDirection;
        _nextStationaryPollTime = Time.unscaledTime + _stationaryPollInterval;
        DetectInteractable(player);
    }

    private void DetectInteractable(PlayerController player)
    {
        Vector2 dir = _directionVectors[player.FacingDirection];
        Vector2 origin = (Vector2)player.transform.position + dir * _boxDistance;

        int hitCount = Physics2D.OverlapBox(origin, _boxSize, 0f, _contactFilter, _hitResults);

        if (hitCount > 0)
        {
            var interactable = _hitResults[0].GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract(player))
            {
                if (_currentTarget != interactable)
                {
                    _currentTarget?.ShowHighlight(false);
                    _currentTarget = interactable;
                    _currentTarget.ShowHighlight(true); // 하이라이트 켜기
                }
                return;
            }
        }

        ClearTarget();
    }

    private PlayerController CachePlayerIfNeeded()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();

        return _player;
    }

    private void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget.ShowHighlight(false);
            _currentTarget = null;
        }
    }

    /// <summary>플레이어가 Z키를 누르면 호출 (탐색 로직 없이 캐싱된 타겟 즉시 실행)</summary>
    public void TryInteract(PlayerController player)
    {
        if (_currentTarget != null && _currentTarget.CanInteract(player))
        {
            _currentTarget.Interact(player);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 origin = (Vector2)transform.position + Vector2.down * _boxDistance;
        Gizmos.DrawWireCube(origin, _boxSize);
    }
}
