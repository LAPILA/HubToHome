using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [SerializeField, Min(0)] private int _maxRetainedPerPool = 20;

    private readonly Dictionary<GameObject, PoolState> _pools = new Dictionary<GameObject, PoolState>();
    private readonly Dictionary<GameObject, PoolState> _instanceOwners = new Dictionary<GameObject, PoolState>();

    private sealed class PoolState
    {
        public PoolState(GameObject prefab, int capacity)
        {
            Prefab = prefab;
            Available = new Queue<GameObject>(capacity);
            InPool = new HashSet<GameObject>();
        }

        public GameObject Prefab { get; }
        public Queue<GameObject> Available { get; }
        public HashSet<GameObject> InPool { get; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        var instances = new List<GameObject>(_instanceOwners.Keys);
        _instanceOwners.Clear();
        _pools.Clear();

        for (int i = 0; i < instances.Count; i++)
        {
            GameObject instance = instances[i];
            if (instance == null)
                continue;

            Transform instanceTransform = instance.transform;
            if (instanceTransform != null && instanceTransform.IsChildOf(transform))
                continue;

            DestroyManagedObject(instance);
        }
    }

    public void RegisterPool(GameObject prefab, int initialSize = 3)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPoolManager] null prefab은 등록할 수 없습니다.", this);
            return;
        }

        if (_pools.ContainsKey(prefab))
            return;

        int retainedLimit = Mathf.Max(0, _maxRetainedPerPool);
        int prewarmCount = Mathf.Clamp(initialSize, 0, retainedLimit);
        var state = new PoolState(prefab, prewarmCount);
        _pools.Add(prefab, state);

        for (int i = 0; i < prewarmCount; i++)
        {
            GameObject instance = CreateNew(state);
            state.InPool.Add(instance);
            state.Available.Enqueue(instance);
        }
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPoolManager] null prefab은 Spawn할 수 없습니다.", this);
            return null;
        }

        if (!_pools.TryGetValue(prefab, out PoolState state))
        {
            RegisterPool(prefab);
            if (!_pools.TryGetValue(prefab, out state))
                return null;
        }

        GameObject obj = null;
        while (state.Available.Count > 0)
        {
            GameObject candidate = state.Available.Dequeue();
            if (object.ReferenceEquals(candidate, null))
                continue;

            state.InPool.Remove(candidate);
            if (candidate == null)
            {
                _instanceOwners.Remove(candidate);
                continue;
            }

            if (!_instanceOwners.TryGetValue(candidate, out PoolState owner) || owner != state)
            {
                _instanceOwners.Remove(candidate);
                DestroyManagedObject(candidate);
                continue;
            }

            obj = candidate;
            break;
        }

        if (obj == null)
            obj = CreateNew(state);

        obj.transform.SetParent(transform, false);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public void Despawn(GameObject obj)
    {
        if (obj == null)
            return;

        if (!_instanceOwners.TryGetValue(obj, out PoolState state))
        {
            Debug.LogWarning($"[ObjectPoolManager] 이 Manager가 생성하지 않은 객체를 폐기합니다: {obj.name}", obj);
            DestroyManagedObject(obj);
            return;
        }

        if (state.InPool.Contains(obj))
            return;

        obj.SetActive(false);

        int retainedLimit = Mathf.Max(0, _maxRetainedPerPool);
        if (state.Available.Count >= retainedLimit)
        {
            _instanceOwners.Remove(obj);
            DestroyManagedObject(obj);
            return;
        }

        obj.transform.SetParent(transform, false);
        state.InPool.Add(obj);
        state.Available.Enqueue(obj);
    }

    private GameObject CreateNew(PoolState state)
    {
        GameObject obj = Instantiate(state.Prefab, transform);
        obj.name = state.Prefab.name;
        obj.SetActive(false);
        _instanceOwners.Add(obj, state);
        return obj;
    }

    private static void DestroyManagedObject(GameObject obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
