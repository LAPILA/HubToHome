using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 오브젝트 풀 매니저 (싱글톤).
/// Dictionary&lt;string, Queue&lt;GameObject&gt;&gt; 구조로 관리합니다.
/// 투사체, 타격 이펙트, 데미지 텍스트 등 빈번하게 생성/파괴되는 객체에 사용하세요.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    public static ObjectPoolManager Instance { get; private set; }

    // 풀 저장소: key = 프리팹 이름
    private readonly Dictionary<string, Queue<GameObject>> _pools
        = new Dictionary<string, Queue<GameObject>>();

    // 프리팹 레지스트리: 런타임에 Spawn 시 원본 참조용
    private readonly Dictionary<string, GameObject> _prefabRegistry
        = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 풀 등록 ───────────────────────────────────────────────
    /// <summary>
    /// 프리팹을 풀에 등록하고 초기 오브젝트를 미리 생성합니다.
    /// </summary>
    /// <param name="prefab">풀링할 프리팹</param>
    /// <param name="initialSize">초기 생성 수</param>
    public void RegisterPool(GameObject prefab, int initialSize = 10)
    {
        string key = prefab.name;
        if (_pools.ContainsKey(key)) return;

        _prefabRegistry[key] = prefab;
        var queue = new Queue<GameObject>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            var obj = CreateNew(prefab);
            queue.Enqueue(obj);
        }
        _pools[key] = queue;
    }

    // ── 꺼내기 (Spawn) ────────────────────────────────────────
    /// <summary>풀에서 오브젝트를 꺼내 활성화합니다.</summary>
    /// <summary>풀에서 오브젝트를 꺼내 활성화합니다.</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        if (!_pools.ContainsKey(key))
            RegisterPool(prefab);

        GameObject obj = null;

        // 🚨 핵심 방어 로직: 큐 안에 있는 멀쩡한(Destroy되지 않은) 객체를 찾을 때까지 꺼냅니다.
        while (_pools[key].Count > 0)
        {
            obj = _pools[key].Dequeue();
            
            if (obj != null) 
            {
                break; // 멀쩡한 객체를 찾았으니 루프 탈출!
            }
            // null이라면 누군가 Destroy한 것이므로 무시하고 다음 것을 꺼냅니다.
        }

        // 큐가 비었거나, 남아있던 객체들이 전부 Destroy되어서 obj가 여전히 null인 경우 새로 만듭니다.
        if (obj == null)
        {
            obj = CreateNew(_prefabRegistry[key]);
            Debug.LogWarning($"<color=orange>[ObjectPool]</color> Pool '{key}' expanded. (기존 객체 부족 또는 파괴됨)");
        }

        // 위치 적용 및 활성화
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        
        return obj;
    }

    // ── 반납 (Despawn) ────────────────────────────────────────
    /// <summary>오브젝트를 비활성화하고 풀에 반납합니다.</summary>
    public void Despawn(GameObject obj)
    {
        string key = obj.name;
        obj.SetActive(false);

        if (!_pools.ContainsKey(key))
            _pools[key] = new Queue<GameObject>();

        _pools[key].Enqueue(obj);
    }

    // ── 내부 생성 ─────────────────────────────────────────────
    private GameObject CreateNew(GameObject prefab)
    {
        var obj = Instantiate(prefab, transform);
        obj.name = prefab.name; // 이름에서 "(Clone)" 제거
        obj.SetActive(false);
        return obj;
    }
}
