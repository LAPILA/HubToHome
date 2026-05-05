using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 전환 시에도 데이터를 유지하는 전역 싱글톤 매니저.
/// 세이브 데이터(SSOT)의 런타임 저장소 역할을 합니다.
/// </summary>
public class GlobalDataManager : MonoBehaviour
{
    public static GlobalDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaults();
    }

    // ── 런타임 데이터 ─────────────────────────────────────────
    private readonly Dictionary<string, int> _eventFlags = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _inventoryDict = new Dictionary<string, int>();
    public List<EnemyData> PendingEnemies { get; set; } = new List<EnemyData>();
    // 🚨 핵심: 파티 시스템 (Deltarune 스타일 대응)
    public List<CharacterSaveData> Party { get; private set; } = new List<CharacterSaveData>();

    // 씬 전환용 임시 위치 데이터
    public string LastOverworldScene;
    public string SpawnScene { get; set; } = "OverworldScene";
    public float  SpawnX     { get; set; } = 0f;
    public float  SpawnY     { get; set; } = 0f;
    public int    LookingDir { get; set; } = 0; 

    private void InitializeDefaults()
    {
        _eventFlags.Clear();
        _inventoryDict.Clear();
        Party.Clear();
        
        // 새 게임 시작 시 기본 주인공 파티에 추가
        Party.Add(new CharacterSaveData { CharacterID = "Hero" });
    }

    // ── 이벤트 플래그 API ───────────────────────
    public void SetFlag(string key, int value) => _eventFlags[key] = value;
    public int GetFlag(string key, int defaultValue = 0) => _eventFlags.TryGetValue(key, out int val) ? val : defaultValue;

    // ── 인벤토리 API (수량 기반) ───────────────────────
    public void AddItem(string itemID, int amount = 1)
    {
        if (_inventoryDict.ContainsKey(itemID))
            _inventoryDict[itemID] += amount;
        else
            _inventoryDict[itemID] = amount;
    }

    public bool RemoveItem(string itemID, int amount = 1)
    {
        if (!_inventoryDict.ContainsKey(itemID) || _inventoryDict[itemID] < amount)
            return false; // 수량이 부족하거나 없음

        _inventoryDict[itemID] -= amount;
        if (_inventoryDict[itemID] <= 0)
            _inventoryDict.Remove(itemID); // 0개가 되면 목록에서 삭제

        return true;
    }

    public int GetItemCount(string itemID)
    {
        return _inventoryDict.TryGetValue(itemID, out int count) ? count : 0;
    }

    public IReadOnlyDictionary<string, int> GetInventory() => _inventoryDict;

    // ── 세이브/로드 연동 ──────────────────────────────────────
    public SaveData ToSaveData()
    {
        var data = new SaveData
        {
            currentScene     = SpawnScene,
            playerX          = SpawnX,
            playerY          = SpawnY,
            lookingDirection = LookingDir
        };
        
        // 1. 인벤토리 및 플래그 복사 (안전한 Dictionary 복사 방식)
        data.InventoryDict = new Dictionary<string, int>(_inventoryDict);
        data.eventFlags = new Dictionary<string, int>(_eventFlags);

        // 2. 파티 멤버 데이터 복사
        data.PartyData = new List<CharacterSaveData>(Party);

        return data;
    }

    public void FromSaveData(SaveData data)
    {
        // 1. 위치 정보 복원
        SpawnScene   = data.currentScene;
        SpawnX       = data.playerX;
        SpawnY       = data.playerY;
        LookingDir   = data.lookingDirection;

        // 2. 인벤토리 및 플래그 복원
        _inventoryDict.Clear();
        foreach (var kv in data.InventoryDict) _inventoryDict[kv.Key] = kv.Value;

        _eventFlags.Clear();
        foreach (var kv in data.eventFlags) _eventFlags[kv.Key] = kv.Value;

        // 3. 파티 멤버 복원
        Party = new List<CharacterSaveData>(data.PartyData);
    }
}