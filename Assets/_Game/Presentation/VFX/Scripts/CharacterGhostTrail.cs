using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // DOTween 필수

[RequireComponent(typeof(SpriteRenderer))]
public class CharacterGhostTrail : MonoBehaviour
{
    [Header("잔상 설정")]
    [Tooltip("잔상을 남길 간격 (초 단위)")]
    [SerializeField] private float _spawnInterval = 0.05f; 
    
    [Tooltip("잔상이 완전히 사라지는 데 걸리는 시간")]
    [SerializeField] private float _ghostLifetime = 0.4f;  
    
    [Tooltip("잔상이 생성될 때의 초기 색상과 투명도 (Alpha가 1이면 원본과 똑같음)")]
    [SerializeField] private Color _ghostStartColor = new Color(1f, 1f, 1f, 0.6f); 

    private SpriteRenderer _sourceRenderer;
    private bool _isTrailActive = false;
    private float _spawnTimer = 0f;

    // 잔상 오브젝트를 재활용하기 위한 풀(Pool)
    private Queue<SpriteRenderer> _ghostPool = new Queue<SpriteRenderer>();
    private Transform _poolContainer;

    private void Awake()
    {
        _sourceRenderer = GetComponent<SpriteRenderer>();

        // 하이어라키가 지저분해지는 걸 막기 위해 잔상들을 모아둘 빈 부모 객체 생성
        _poolContainer = new GameObject($"{gameObject.name}_GhostPool").transform;
    }

    private void Update()
    {
        if (!_isTrailActive) return;

        // 쿨타임이 찰 때마다 잔상을 하나씩 바닥에 찍음
        _spawnTimer += Time.deltaTime;
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
        _isTrailActive = active;
        if (active) _spawnTimer = _spawnInterval; // 켜지자마자 즉시 첫 잔상 생성
    }

    private void SpawnGhost()
    {
        // 1. 풀에서 잔상용 렌더러를 하나 가져옵니다.
        SpriteRenderer ghost = GetGhostFromPool();

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

        // 3. DOTween으로 부드럽게 투명도를 0으로 깎고, 완료되면 다시 풀에 반납
        ghost.DOKill(); 
        ghost.DOFade(0f, _ghostLifetime)
            .SetEase(Ease.OutQuad) // 서서히 사라지도록
            .OnComplete(() => ReturnToPool(ghost));
    }

    private SpriteRenderer GetGhostFromPool()
    {
        if (_ghostPool.Count > 0)
        {
            return _ghostPool.Dequeue();
        }

        // 풀이 비어있다면 새로 하나 만듭니다.
        GameObject obj = new GameObject("Ghost");
        obj.transform.SetParent(_poolContainer);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        return sr;
    }

    private void ReturnToPool(SpriteRenderer ghost)
    {
        ghost.gameObject.SetActive(false);
        _ghostPool.Enqueue(ghost);
    }
}