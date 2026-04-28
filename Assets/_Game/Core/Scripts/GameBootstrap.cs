using UnityEngine;

/// <summary>
/// 게임 시작 시 가장 먼저 실행되는 부트스트랩 오브젝트.
/// GlobalDataManager, SceneLoader, AudioManager, ObjectPoolManager,
/// DialogueManager, UIManager 등 DontDestroyOnLoad 싱글톤들을 초기화합니다.
/// 
/// 사용법: 빈 씬(Bootstrap Scene) 또는 TitleScene의 첫 번째 오브젝트로 배치하세요.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Core Prefabs (DontDestroyOnLoad)")]
    [SerializeField] private GameObject _globalDataManagerPrefab;
    [SerializeField] private GameObject _sceneLoaderPrefab;
    [SerializeField] private GameObject _audioManagerPrefab;
    [SerializeField] private GameObject _objectPoolManagerPrefab;
    [SerializeField] private GameObject _dialogueManagerPrefab;
    [SerializeField] private GameObject _uiManagerPrefab;

    private void Awake()
    {
        InitializeSingletons();
    }

    private void InitializeSingletons()
    {
        SpawnIfNotExists<GlobalDataManager>(_globalDataManagerPrefab);
        SpawnIfNotExists<SceneLoader>(_sceneLoaderPrefab);
        SpawnIfNotExists<AudioManager>(_audioManagerPrefab);
        SpawnIfNotExists<ObjectPoolManager>(_objectPoolManagerPrefab);
        SpawnIfNotExists<DialogueManager>(_dialogueManagerPrefab);
        SpawnIfNotExists<UIManager>(_uiManagerPrefab);

        Debug.Log("[GameBootstrap] All core systems initialized.");
    }

    private void SpawnIfNotExists<T>(GameObject prefab) where T : MonoBehaviour
    {
        if (FindFirstObjectByType<T>() != null) return;
        if (prefab == null)
        {
            Debug.LogWarning($"[GameBootstrap] Prefab for {typeof(T).Name} is not assigned.");
            return;
        }
        Instantiate(prefab);
    }
}
