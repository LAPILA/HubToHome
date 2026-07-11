using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 내 모든 진행 상황, 선택지, 카운터를 저장하는 전역 플래그 매니저입니다.
/// (추후 세이브/로드 시 이 딕셔너리만 JSON으로 직렬화하면 됩니다.)
/// </summary>
public class GameFlagManager : MonoBehaviour
{
    public static GameFlagManager Instance { get; private set; }

    // 🚨 업계 표준: bool 대신 int를 사용하여 진행도와 카운터까지 모두 커버합니다.
    private Dictionary<string, int> _gameFlags = new Dictionary<string, int>();

    private void Awake() 
    { 
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; 
        
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    /// <summary> 특정 플래그의 값을 명시적으로 설정합니다. (기본값 1 = True) </summary>
    public void SetFlag(string flagID, int value = 1)
    {
        if (string.IsNullOrEmpty(flagID)) return;
        _gameFlags[flagID] = value;
        Debug.Log($"<color=cyan>[GameFlag]</color> {flagID} 플래그가 {value} (으)로 설정되었습니다.");
    }

    /// <summary> 특정 플래그의 값을 반환합니다. (없으면 0 반환) </summary>
    public int GetFlag(string flagID)
    {
        if (string.IsNullOrEmpty(flagID)) return 0;
        return _gameFlags.TryGetValue(flagID, out int val) ? val : 0;
    }

    /// <summary> 특정 플래그의 값을 누적합니다. (적 처치 수, 아이템 획득 수 등) </summary>
    public void AddFlag(string flagID, int amount = 1)
    {
        if (string.IsNullOrEmpty(flagID)) return;
        int current = GetFlag(flagID);
        SetFlag(flagID, current + amount);
    }

    /// <summary> 플래그가 1 이상인지(True) 확인하는 편의성 함수입니다. </summary>
    public bool HasFlag(string flagID)
    {
        return GetFlag(flagID) > 0;
    }
}