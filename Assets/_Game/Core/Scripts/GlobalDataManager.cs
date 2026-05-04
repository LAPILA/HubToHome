using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 전환 시에도 데이터를 유지하는 전역 싱글톤 매니저.
/// DontDestroyOnLoad 적용. 이벤트 플래그, 인벤토리, 플레이어 상태를 관리합니다.
/// </summary>
public class GlobalDataManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    public static GlobalDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaults();
    }

    // ── 런타임 데이터 ─────────────────────────────────────────
    // 이벤트 플래그 (Undertale 스타일)
    private readonly Dictionary<string, int> _eventFlags = new Dictionary<string, int>();

    // 인벤토리 (아이템 ID 목록)
    private readonly List<string> _inventoryItemIDs = new List<string>();

    // 플레이어 상태
    public int   PlayerHP    { get; set; } = 100;
    public int   PlayerMaxHP { get; set; } = 100;

    public int   PlayerMP    { get; set; } = 100;
    public int   PlayerMaxMP { get; set; } = 100;

    // 씬 전환용 임시 위치 데이터
    public string SpawnScene     { get; set; } = SceneName.Overworld;
    public float  SpawnX         { get; set; } = 0f;
    public float  SpawnY         { get; set; } = 0f;
    public int    LookingDir     { get; set; } = 0; // 0=Down 1=Up 2=Left 3=Right

    // ── 초기화 ────────────────────────────────────────────────
    private void InitializeDefaults()
    {
        _eventFlags.Clear();
        _inventoryItemIDs.Clear();
        PlayerHP    = 100;
        PlayerMaxHP = 100;
        PlayerMP = 100;
        PlayerMaxMP = 100;
    }

    // ── 이벤트 플래그 API ─────────────────────────────────────
    public void SetFlag(string key, int value)
    {
        _eventFlags[key] = value;
    }

    public int GetFlag(string key, int defaultValue = 0)
    {
        return _eventFlags.TryGetValue(key, out int val) ? val : defaultValue;
    }

    public bool HasFlag(string key) => _eventFlags.ContainsKey(key);

    // ── 인벤토리 API ──────────────────────────────────────────
    public void AddItem(string itemID)
    {
        _inventoryItemIDs.Add(itemID);
    }

    public bool RemoveItem(string itemID) => _inventoryItemIDs.Remove(itemID);

    public bool HasItem(string itemID) => _inventoryItemIDs.Contains(itemID);

    public IReadOnlyList<string> GetInventory() => _inventoryItemIDs;

    // ── 세이브/로드 연동 ──────────────────────────────────────
    /// <summary>현재 런타임 상태를 SaveData 로 직렬화합니다.</summary>
    public SaveData ToSaveData()
    {
        var data = new SaveData
        {
            currentScene     = SpawnScene,
            playerX          = SpawnX,
            playerY          = SpawnY,
            lookingDirection = LookingDir,
            playerHP         = PlayerHP,
            playerMaxHP      = PlayerMaxHP,
        };
        data.inventoryItemIDs.AddRange(_inventoryItemIDs);
        foreach (var kv in _eventFlags)
            data.eventFlags[kv.Key] = kv.Value;
        return data;
    }

    /// <summary>SaveData 를 런타임 상태로 복원합니다.</summary>
    public void FromSaveData(SaveData data)
    {
        SpawnScene   = data.currentScene;
        SpawnX       = data.playerX;
        SpawnY       = data.playerY;
        LookingDir   = data.lookingDirection;
        PlayerHP     = data.playerHP;
        PlayerMaxHP  = data.playerMaxHP;
        PlayerMP     = data.playerMP;
        PlayerMaxMP  = data.playerMaxMP;

        _inventoryItemIDs.Clear();
        _inventoryItemIDs.AddRange(data.inventoryItemIDs);

        _eventFlags.Clear();
        foreach (var kv in data.eventFlags)
            _eventFlags[kv.Key] = kv.Value;
    }
}
