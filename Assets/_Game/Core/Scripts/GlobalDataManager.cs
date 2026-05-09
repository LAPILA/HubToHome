using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 전환 시에도 데이터를 유지하는 전역 싱글톤 매니저.
/// 세이브 데이터(SSOT)의 런타임 저장소 역할을 합니다.
/// </summary>
public class GlobalDataManager : MonoBehaviour
{
    public static GlobalDataManager Instance { get; private set; }

    #region [ Runtime Data ]
    // 🚨 인트로에서 설정한 플레이어의 이름이 저장되는 곳!
    public string PlayerName { get; set; } = "Rapley"; 

    private readonly Dictionary<string, int> _eventFlags = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _inventoryDict = new Dictionary<string, int>();
    
    public List<EnemyData> PendingEnemies { get; set; } = new List<EnemyData>();
    
    // 🚨 다중 파티 시스템 
    public List<CharacterSaveData> Party { get; private set; } = new List<CharacterSaveData>();
    #endregion

    #region [ Position & Scene Data ]
    public string LastOverworldScene;
    public string SpawnScene { get; set; } = "OverworldScene";
    public float  SpawnX     { get; set; } = 0f;
    public float  SpawnY     { get; set; } = 0f;
    public int    LookingDir { get; set; } = 0; 
    #endregion

    #region [ Initialization ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        _eventFlags.Clear();
        _inventoryDict.Clear();
        Party.Clear(); // 기존 더미 데이터 추가 로직 삭제
    }

    /// <summary>
    /// 게임 시작 시 글로벌 데이터가 비어있다면, 씬에 배치된 플레이어의 인스펙터 값을 기준으로 파티를 셋업합니다.
    /// </summary>
    public void InitializePartyFromScene(PlayerCharacter scenePlayer)
    {
        if (scenePlayer == null) return;

        int startMaxHP = scenePlayer.BaseMaxHP > 0 ? scenePlayer.BaseMaxHP : 100;
        int startMaxMP = scenePlayer.BaseMaxMP > 0 ? scenePlayer.BaseMaxMP : 50;
        int startATK   = scenePlayer.BaseATK > 0 ? scenePlayer.BaseATK : 10;
        int startSPD   = scenePlayer.BaseSPD > 0 ? scenePlayer.BaseSPD : 10;

        var newData = new CharacterSaveData()
        {
            CharacterID = scenePlayer.CharacterID,
            // 🚨 캐릭터 이름(ID)을 입력받은 플레이어 이름으로 덮어쓸 수도 있습니다!
            // CharacterID = string.IsNullOrEmpty(PlayerName) ? scenePlayer.CharacterID : PlayerName,
            Level       = scenePlayer.Level,
            EXP         = scenePlayer.EXP,
            MaxHP       = startMaxHP,
            HP          = startMaxHP,
            MaxMP       = startMaxMP,
            MP          = startMaxMP,
            ATK         = startATK,
            DEF         = scenePlayer.BaseDEF,
            SPD         = startSPD
        };

        Party.Add(newData);
        Debug.Log($"<color=yellow>[GlobalData] 파티원 초기화 완료: {newData.CharacterID} (이름: {PlayerName})</color>");
    }
    #endregion

    #region [ Event Flags API ]
    public void SetFlag(string key, int value) => _eventFlags[key] = value;
    public int GetFlag(string key, int defaultValue = 0) => _eventFlags.TryGetValue(key, out int val) ? val : defaultValue;
    #endregion

    #region [ Inventory API ]
    public void AddItem(string itemID, int amount = 1)
    {
        if (_inventoryDict.ContainsKey(itemID)) _inventoryDict[itemID] += amount;
        else _inventoryDict[itemID] = amount;
    }

    public bool RemoveItem(string itemID, int amount = 1)
    {
        if (!_inventoryDict.ContainsKey(itemID) || _inventoryDict[itemID] < amount) return false;

        _inventoryDict[itemID] -= amount;
        if (_inventoryDict[itemID] <= 0) _inventoryDict.Remove(itemID); 

        return true;
    }

    public int GetItemCount(string itemID) => _inventoryDict.TryGetValue(itemID, out int count) ? count : 0;
    public IReadOnlyDictionary<string, int> GetInventory() => _inventoryDict;
    #endregion

    #region [ Save & Load ]
    public SaveData ToSaveData()
    {
        var data = new SaveData
        {
            playerName       = PlayerName, // 🚨 세이브 데이터에 이름 추가!
            currentScene     = SpawnScene,
            playerX          = SpawnX,
            playerY          = SpawnY,
            lookingDirection = LookingDir,
            
            // 안전한 깊은 복사(Deep Copy)
            InventoryDict = new Dictionary<string, int>(_inventoryDict),
            eventFlags    = new Dictionary<string, int>(_eventFlags),
            PartyData     = new List<CharacterSaveData>(Party)
        };
        return data;
    }

    public void FromSaveData(SaveData data)
    {
        PlayerName   = data.playerName; // 🚨 이름 불러오기!
        SpawnScene   = data.currentScene;
        SpawnX       = data.playerX;
        SpawnY       = data.playerY;
        LookingDir   = data.lookingDirection;

        _inventoryDict.Clear();
        foreach (var kv in data.InventoryDict) _inventoryDict[kv.Key] = kv.Value;

        _eventFlags.Clear();
        foreach (var kv in data.eventFlags) _eventFlags[kv.Key] = kv.Value;

        Party = new List<CharacterSaveData>(data.PartyData);
    }
    #endregion
}