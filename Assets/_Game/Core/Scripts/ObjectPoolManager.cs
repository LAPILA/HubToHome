using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    private readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<string, GameObject> _prefabRegistry = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterPool(GameObject prefab, int initialSize = 10)
    {
        string key = prefab.name;
        if (_pools.ContainsKey(key)) return;

        _prefabRegistry[key] = prefab;
        var queue = new Queue<GameObject>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            queue.Enqueue(CreateNew(prefab));
        }
        _pools[key] = queue;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;
        if (!_pools.ContainsKey(key)) RegisterPool(prefab);

        GameObject obj = null;

        // 방어 로직: 파괴되지 않은 온전한 객체 찾기
        while (_pools[key].Count > 0)
        {
            obj = _pools[key].Dequeue();
            if (obj != null) break;
        }

        // 큐가 비었거나 전부 파괴되었다면 재생성
        if (obj == null)
        {
            obj = CreateNew(_prefabRegistry[key]);
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public void Despawn(GameObject obj)
    {
        string key = obj.name;
        obj.SetActive(false);

        if (!_pools.ContainsKey(key))
            _pools[key] = new Queue<GameObject>();

        _pools[key].Enqueue(obj);
    }

    private GameObject CreateNew(GameObject prefab)
    {
        var obj = Instantiate(prefab, transform);
        obj.name = prefab.name; 
        obj.SetActive(false);
        return obj;
    }
}