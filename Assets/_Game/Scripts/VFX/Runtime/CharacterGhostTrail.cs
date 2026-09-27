using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // DOTween 필수

[RequireComponent(typeof(SpriteRenderer))]
public class CharacterGhostTrail : MonoBehaviour
{
    private const float MovementEpsilonSqr = 0.000001f;
    [Header("잔상 설정")]
    [Tooltip("잔상을 남길 간격 (초 단위)")]
    [SerializeField] private float _spawnInterval = 0.05f; 
    
    [Tooltip("잔상이 완전히 사라지는 데 걸리는 시간")]
    [SerializeField] private float _ghostLifetime = 0.4f;  
    
    [Tooltip("잔상이 생성될 때의 초기 색상과 투명도 (Alpha가 1이면 원본과 똑같음)")]
    [SerializeField] private Color _ghostStartColor = new Color(1f, 1f, 1f, 0.6f); 

    [Tooltip("동시에 유지할 수 있는 잔상의 최대 개수")]
    [SerializeField, Min(1)] private int _maxGhostCount = 16;

    private SpriteRenderer _sourceRenderer;
    private Transform _movementRoot;
    private Vector3 _lastMovementPosition;
    private bool _hasVisibleGhosts;
    private bool _isTrailActive = false;
    private float _spawnTimer = 0f;

    // 잔상 오브젝트를 재활용하기 위한 풀(Pool)
    private readonly List<SpriteRenderer> _ghostPool = new List<SpriteRenderer>();
    private Transform _poolContainer;
    private int _nextGhostIndex;

    private void Awake()
    {
        _sourceRenderer = GetComponent<SpriteRenderer>();
        CharacterBase owner = GetComponentInParent<CharacterBase>();
        _movementRoot = owner != null ? owner.transform : transform;
        _lastMovementPosition = _movementRoot.position;

        // 하이어라키가 지저분해지는 걸 막기 위해 잔상들을 모아둘 빈 부모 객체 생성
        _poolContainer = new GameObject($"{gameObject.name}_GhostPool").transform;
        _poolContainer.SetParent(transform, false);

        // SetTrailActive(true) 전에는 Update 콜백 자체를 등록하지 않습니다.
        enabled = _isTrailActive;
    }

    private void LateUpdate()
    {
        UpdateTrail(Time.deltaTime);
    }

    private void UpdateTrail(float deltaTime)
    {
        if (!_isTrailActive) return;

        // DOTween 이동 후 실제 캐릭터 루트의 변위만 확인합니다.
        // 스프라이트 애니메이션/카메라 이동은 잔상 생성 조건이 아닙니다.
        Vector3 position = _movementRoot.position;
        bool moved = (position - _lastMovementPosition).sqrMagnitude > MovementEpsilonSqr;
        _lastMovementPosition = position;
        if (!moved)
        {
            ClearGhosts();
            _spawnTimer = _spawnInterval;
            return;
        }

        _spawnTimer += deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            SpawnGhost();
        }
    }

    /// <summary>
    /// 잔상 효과를 켜거나 끕니다. (이동/대시 스킬 시작 시 true, 끝나면 false 호출)
    /// </summary>
    public void SetTrailActive(bool active)
    {
        if (_isTrailActive == active)
        {
            enabled = active;
            return;
        }
        _isTrailActive = active;
        if (active)
        {
            _lastMovementPosition = _movementRoot.position;
            _spawnTimer = _spawnInterval;
        }
        else
        {
            ClearGhosts();
        }
        enabled = active;
    }

    private void SpawnGhost()
    {
        // 1. 풀에서 잔상용 렌더러를 하나 가져옵니다.
        SpriteRenderer ghost = GetGhostFromPool();
        ghost.DOKill();
        ghost.transform.SetParent(null, true);

        // 2. 현재 원본 캐릭터의 정확한 형태와 방향을 복사합니다.
        ghost.sprite = _sourceRenderer.sprite;
        ghost.transform.position = _sourceRenderer.transform.position;
        ghost.transform.localScale = _sourceRenderer.transform.localScale;
        ghost.transform.rotation = _sourceRenderer.transform.rotation;
        
        ghost.flipX = _sourceRenderer.flipX;
        ghost.flipY = _sourceRenderer.flipY;
        
        ghost.sortingLayerID = _sourceRenderer.sortingLayerID;
        ghost.sortingOrder = _sourceRenderer.sortingOrder - 1; // 원본보다 항상 한 칸 뒤에 그려지게
        
        ghost.color = _ghostStartColor;
        ghost.gameObject.SetActive(true);
        _hasVisibleGhosts = true;

        // 3. DOTween으로 부드럽게 투명도를 0으로 깎고, 완료되면 다시 풀에 반납
        ghost.DOFade(0f, _ghostLifetime)
            .SetEase(Ease.OutQuad) // 서서히 사라지도록
            .OnComplete(() => ReturnToPool(ghost));
    }

    private SpriteRenderer GetGhostFromPool()
    {
        int maxGhostCount = Mathf.Max(1, _maxGhostCount);

        // 반환된 슬롯이 있으면 새 오브젝트를 만들기 전에 먼저 재사용합니다.
        for (int offset = 0; offset < _ghostPool.Count; offset++)
        {
            int index = (_nextGhostIndex + offset) % _ghostPool.Count;
            SpriteRenderer availableGhost = _ghostPool[index];
            if (availableGhost != null && !availableGhost.gameObject.activeSelf)
            {
                _nextGhostIndex = (index + 1) % _ghostPool.Count;
                return availableGhost;
            }
        }

        if (_ghostPool.Count < maxGhostCount)
        {
            GameObject obj = new GameObject("Ghost");
            obj.transform.SetParent(_poolContainer, false);
            SpriteRenderer newGhost = obj.AddComponent<SpriteRenderer>();
            _ghostPool.Add(newGhost);
            return newGhost;
        }

        // 상한에 도달하면 가장 오래전에 사용한 슬롯부터 재사용합니다.
        if (_nextGhostIndex >= _ghostPool.Count)
            _nextGhostIndex = 0;

        SpriteRenderer ghost = _ghostPool[_nextGhostIndex];
        _nextGhostIndex = (_nextGhostIndex + 1) % _ghostPool.Count;
        return ghost;
    }

    private void ReturnToPool(SpriteRenderer ghost)
    {
        if (ghost == null)
            return;

        ghost.gameObject.SetActive(false);
        if (_poolContainer != null)
            ghost.transform.SetParent(_poolContainer, false);
    }

    private void ClearGhosts()
    {
        if (!_hasVisibleGhosts) return;
        for (int i = 0; i < _ghostPool.Count; i++)
        {
            SpriteRenderer ghost = _ghostPool[i];
            if (ghost == null || !ghost.gameObject.activeSelf) continue;
            ghost.DOKill(false);
            ReturnToPool(ghost);
        }
        _hasVisibleGhosts = false;
    }

    private void OnDisable()
    {
        _isTrailActive = false;
        _spawnTimer = 0f;
        ClearGhosts();
        enabled = false;
    }

    private void OnDestroy()
    {
        _isTrailActive = false;

        for (int i = 0; i < _ghostPool.Count; i++)
        {
            SpriteRenderer ghost = _ghostPool[i];
            if (ghost == null)
                continue;

            ghost.DOKill();
            if (!ghost.transform.IsChildOf(transform))
            {
                if (Application.isPlaying)
                    Destroy(ghost.gameObject);
                else
                    DestroyImmediate(ghost.gameObject);
            }
        }

        _ghostPool.Clear();
        _poolContainer = null;
    }
}
