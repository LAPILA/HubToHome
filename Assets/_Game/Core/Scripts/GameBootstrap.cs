using System;
using UnityEngine;

/// <summary>
/// 게임 시작 시 가장 먼저 실행되는 부트스트랩 오브젝트.
/// GlobalDataManager, SceneLoader, AudioManager, ObjectPoolManager 등 DontDestroyOnLoad 싱글톤들을 초기화합니다.
/// [DefaultExecutionOrder(-100)] 속성으로 인해 다른 어떤 스크립트보다 먼저 Awake가 실행됩니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    [Header("Core Prefabs (DontDestroyOnLoad)")]
    [SerializeField] private GameObject _globalDataManagerPrefab;
    [SerializeField] private GameObject _sceneLoaderPrefab;
    [SerializeField] private GameObject _audioManagerPrefab;
    [SerializeField] private GameObject _objectPoolManagerPrefab;
    [SerializeField] private GameObject _dialogueManagerPrefab;
    [SerializeField] private GameObject _uiManagerPrefab;
    [SerializeField] private GameObject _gameStateManagerPrefab;
    
    [SerializeField] private GameObject _gameFlagManagerPrefab;

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
        SpawnIfNotExists<GameStateManager>(_gameStateManagerPrefab);
        SpawnIfNotExists<GameFlagManager>(_gameFlagManagerPrefab);
        
        Debug.Log("<color=#00FFFF>[GameBootstrap] 모든 코어 시스템 초기화 완료!</color>");
    }

    private void SpawnIfNotExists<T>(GameObject prefab) where T : MonoBehaviour
    {
        if (FindFirstObjectByType<T>() != null) return;
        
        if (prefab == null)
        {
            Debug.LogWarning($"[GameBootstrap] {typeof(T).Name} 프리팹이 할당되지 않았습니다.");
            return;
        }
        
        var obj = Instantiate(prefab);
        obj.name = prefab.name; 
        
        obj.transform.SetParent(null);
        DontDestroyOnLoad(obj);
    }
}