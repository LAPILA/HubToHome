using System.Collections.Generic;
using UnityEngine;

public class GameFlagManager : MonoBehaviour
{
    public static GameFlagManager Instance { get; private set; }

    // 모든 선택지, 이벤트 달성 여부를 저장하는 딕셔너리 (저장/로드 시 이 객체만 Json으로 구우면 끝!)
    private Dictionary<string, bool> _gameFlags = new Dictionary<string, bool>();

    private void Awake() { Instance = this; }

    public void SetFlag(string flagID, bool value = true)
    {
        if (string.IsNullOrEmpty(flagID)) return;
        _gameFlags[flagID] = value;
        Debug.Log($"[Flag] {flagID} 가 {value} 로 설정되었습니다.");
    }

    public bool GetFlag(string flagID)
    {
        return _gameFlags.TryGetValue(flagID, out bool val) ? val : false;
    }
}