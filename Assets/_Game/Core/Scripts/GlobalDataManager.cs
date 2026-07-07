using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OverworldEnemyRuntimeState
{
    public string EnemyId;
    public string SceneName;
    public bool IsDefeated;
    public float CooldownUntilUnscaledTime;
    public float CooldownAlpha = 0.5f;
}

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
    private readonly Dictionary<string, OverworldEnemyRuntimeState> _overworldEnemyStates = new Dictionary<string, OverworldEnemyRuntimeState>();
    private readonly Dictionary<string, EncounterMemorySaveData> _encounterMemory = new Dictionary<string, EncounterMemorySaveData>();
    public int Money { get; private set; } = 0;
    
    public List<EnemyData> PendingEnemies { get; set; } = new List<EnemyData>();
    public AudioClip PendingBattleBGM { get; set; }
    public BattleScenarioData PendingBattleScenario { get; set; }
    public string CurrentEncounterEnemyId { get; private set; }
    public bool CurrentEncounterDefeatsOnVictory { get; private set; }
    public bool CurrentEncounterPlayerPreemptiveAttack { get; private set; }
    
    // 🚨 다중 파티 시스템 
    public List<CharacterSaveData> Party { get; private set; } = new List<CharacterSaveData>();
    #endregion

    #region [ Position & Scene Data ]
    public string LastOverworldScene;
    public string SpawnScene { get; set; } = "OverworldScene";
    public string CurrentRoomId { get; set; } = string.Empty;
    public string SpawnPointId { get; set; } = string.Empty;
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
        _encounterMemory.Clear();
        Party.Clear(); // 기존 더미 데이터 추가 로직 삭제
    }

    /// <summary>
    /// 게임 시작 시 글로벌 데이터가 비어있다면, 씬에 배치된 플레이어의 인스펙터 값을 기준으로 파티를 셋업합니다.
    /// </summary>
    public void InitializePartyFromScene(PlayerCharacter scenePlayer)
    {
        if (scenePlayer == null) return;

        CharacterData characterData = scenePlayer.CharacterData;

        int startMaxHP = characterData != null ? characterData.BaseMaxHP : (scenePlayer.BaseMaxHP > 0 ? scenePlayer.BaseMaxHP : 100);
        int startMaxMP = characterData != null ? characterData.BaseMaxMP : (scenePlayer.BaseMaxMP > 0 ? scenePlayer.BaseMaxMP : 50);
        int startATK   = characterData != null ? characterData.BaseATK : (scenePlayer.BaseATK > 0 ? scenePlayer.BaseATK : 10);
        int startDEF   = characterData != null ? characterData.BaseDEF : scenePlayer.BaseDEF;
        int startSPD   = characterData != null ? characterData.BaseSPD : (scenePlayer.BaseSPD > 0 ? scenePlayer.BaseSPD : 10);

        var newData = new CharacterSaveData()
        {
            CharacterDataID = characterData != null ? characterData.CharacterID : string.Empty,
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
            DEF         = startDEF,
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

    public void AddMoney(int amount)
    {
        Money = Mathf.Max(0, Money + amount);
    }

    public bool SpendMoney(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (Money < amount) return false;

        Money -= amount;
        return true;
    }
    #endregion

    #region [ Encounter Memory API ]
    public EncounterMemorySaveData GetOrCreateEncounterMemory(string encounterId)
    {
        string normalizedId = NormalizeEncounterId(encounterId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return null;
        }

        if (!_encounterMemory.TryGetValue(normalizedId, out EncounterMemorySaveData memory) || memory == null)
        {
            memory = new EncounterMemorySaveData
            {
                EncounterId = normalizedId
            };
            _encounterMemory[normalizedId] = memory;
        }

        memory.EncounterId = normalizedId;
        if (memory.SeenBeatIds == null)
        {
            memory.SeenBeatIds = new List<string>();
        }

        return memory;
    }

    public bool TryGetEncounterMemory(string encounterId, out EncounterMemorySaveData memory)
    {
        string normalizedId = NormalizeEncounterId(encounterId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            memory = null;
            return false;
        }

        return _encounterMemory.TryGetValue(normalizedId, out memory) && memory != null;
    }

    public IReadOnlyDictionary<string, EncounterMemorySaveData> GetEncounterMemory()
    {
        return CloneEncounterMemoryDictionary(_encounterMemory);
    }

    public int IncrementEncounterMeetCount(string encounterId)
    {
        EncounterMemorySaveData memory = GetOrCreateEncounterMemory(encounterId);
        if (memory == null)
        {
            return 0;
        }

        memory.MeetCount = Mathf.Max(0, memory.MeetCount) + 1;
        return memory.MeetCount;
    }

    public void MarkEncounterDefeated(string encounterId)
    {
        EncounterMemorySaveData memory = GetOrCreateEncounterMemory(encounterId);
        if (memory != null)
        {
            memory.Defeated = true;
        }
    }

    public void RememberEncounterBeatIds(string encounterId, IEnumerable<string> beatIds)
    {
        if (beatIds == null)
        {
            return;
        }

        EncounterMemorySaveData memory = GetOrCreateEncounterMemory(encounterId);
        if (memory == null)
        {
            return;
        }

        foreach (string beatId in beatIds)
        {
            AddUniqueSeenBeatId(memory, beatId);
        }
    }

    public string[] GetEncounterSeenBeatIds(string encounterId)
    {
        if (!TryGetEncounterMemory(encounterId, out EncounterMemorySaveData memory) || memory.SeenBeatIds == null)
        {
            return new string[0];
        }

        return memory.SeenBeatIds.ToArray();
    }
    #endregion

    #region [ Overworld Enemy Runtime State ]
    public void BeginOverworldEnemyEncounter(string enemyId, string sceneName, bool defeatsOnVictory, bool playerPreemptiveAttack = false)
    {
        if (string.IsNullOrWhiteSpace(enemyId)) return;

        CurrentEncounterEnemyId = enemyId;
        CurrentEncounterDefeatsOnVictory = defeatsOnVictory;
        CurrentEncounterPlayerPreemptiveAttack = playerPreemptiveAttack;

        var state = GetOrCreateOverworldEnemyState(enemyId, sceneName);
        state.SceneName = sceneName;
    }

    public void EndOverworldEnemyEncounterContext()
    {
        CurrentEncounterEnemyId = null;
        CurrentEncounterDefeatsOnVictory = false;
        CurrentEncounterPlayerPreemptiveAttack = false;
    }

    public OverworldEnemyRuntimeState GetOrCreateOverworldEnemyState(string enemyId, string sceneName = null)
    {
        if (string.IsNullOrWhiteSpace(enemyId)) return null;

        if (!_overworldEnemyStates.TryGetValue(enemyId, out var state) || state == null)
        {
            state = new OverworldEnemyRuntimeState
            {
                EnemyId = enemyId,
                SceneName = sceneName ?? string.Empty,
                CooldownAlpha = 0.5f
            };
            _overworldEnemyStates[enemyId] = state;
        }

        if (!string.IsNullOrWhiteSpace(sceneName))
            state.SceneName = sceneName;

        return state;
    }

    public bool TryGetOverworldEnemyState(string enemyId, out OverworldEnemyRuntimeState state)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            state = null;
            return false;
        }

        return _overworldEnemyStates.TryGetValue(enemyId, out state) && state != null;
    }

    public void MarkOverworldEnemyEscaped(string enemyId, string sceneName, float cooldownDuration, float cooldownAlpha)
    {
        var state = GetOrCreateOverworldEnemyState(enemyId, sceneName);
        if (state == null) return;

        state.SceneName = sceneName;
        state.IsDefeated = false;
        state.CooldownUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0f, cooldownDuration);
        state.CooldownAlpha = Mathf.Clamp01(cooldownAlpha);
    }

    public void MarkOverworldEnemyDefeated(string enemyId, string sceneName)
    {
        var state = GetOrCreateOverworldEnemyState(enemyId, sceneName);
        if (state == null) return;

        state.SceneName = sceneName;
        state.IsDefeated = true;
        state.CooldownUntilUnscaledTime = 0f;
    }

    public void ClearOverworldEnemyCooldown(string enemyId)
    {
        if (!TryGetOverworldEnemyState(enemyId, out var state)) return;
        state.CooldownUntilUnscaledTime = 0f;
    }

    public float GetOverworldEnemyCooldownRemaining(string enemyId)
    {
        if (!TryGetOverworldEnemyState(enemyId, out var state)) return 0f;
        return Mathf.Max(0f, state.CooldownUntilUnscaledTime - Time.unscaledTime);
    }
    #endregion

    #region [ Save & Load ]
    public SaveData ToSaveData()
    {
        var data = new SaveData
        {
            playerName       = PlayerName, // 🚨 세이브 데이터에 이름 추가!
            currentScene     = SpawnScene,
            currentRoomId    = CurrentRoomId,
            spawnPointId     = SpawnPointId,
            playerX          = SpawnX,
            playerY          = SpawnY,
            lookingDirection = LookingDir,
            
            // 안전한 깊은 복사(Deep Copy)
            InventoryDict = new Dictionary<string, int>(_inventoryDict),
            eventFlags    = new Dictionary<string, int>(_eventFlags),
            EncounterMemory = CloneEncounterMemoryDictionary(_encounterMemory),
            PartyData     = new List<CharacterSaveData>(Party),
            Money         = Money
        };
        return data;
    }

    public void FromSaveData(SaveData data)
    {
        PlayerName   = data.playerName; // 🚨 이름 불러오기!
        SpawnScene   = data.currentScene;
        CurrentRoomId = data.currentRoomId;
        SpawnPointId = data.spawnPointId;
        SpawnX       = data.playerX;
        SpawnY       = data.playerY;
        LookingDir   = data.lookingDirection;

        _inventoryDict.Clear();
        foreach (var kv in data.InventoryDict) _inventoryDict[kv.Key] = kv.Value;

        _eventFlags.Clear();
        foreach (var kv in data.eventFlags) _eventFlags[kv.Key] = kv.Value;

        _encounterMemory.Clear();
        if (data.EncounterMemory != null)
        {
            foreach (var kv in data.EncounterMemory)
            {
                string encounterId = NormalizeEncounterId(kv.Key);
                if (string.IsNullOrEmpty(encounterId))
                {
                    continue;
                }

                _encounterMemory[encounterId] = CloneEncounterMemory(kv.Value, encounterId);
            }
        }

        Party = new List<CharacterSaveData>(data.PartyData);
        Money = Mathf.Max(0, data.Money);
    }

    private static Dictionary<string, EncounterMemorySaveData> CloneEncounterMemoryDictionary(
        Dictionary<string, EncounterMemorySaveData> source)
    {
        var clone = new Dictionary<string, EncounterMemorySaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (var kv in source)
        {
            string encounterId = NormalizeEncounterId(kv.Key);
            if (string.IsNullOrEmpty(encounterId))
            {
                continue;
            }

            clone[encounterId] = CloneEncounterMemory(kv.Value, encounterId);
        }

        return clone;
    }

    private static EncounterMemorySaveData CloneEncounterMemory(
        EncounterMemorySaveData source,
        string fallbackEncounterId)
    {
        var clone = new EncounterMemorySaveData
        {
            EncounterId = NormalizeEncounterId(fallbackEncounterId),
            MeetCount = source != null ? Mathf.Max(0, source.MeetCount) : 0,
            Defeated = source != null && source.Defeated,
            SeenBeatIds = new List<string>()
        };

        if (source != null && source.SeenBeatIds != null)
        {
            for (int i = 0; i < source.SeenBeatIds.Count; i++)
            {
                AddUniqueSeenBeatId(clone, source.SeenBeatIds[i]);
            }
        }

        return clone;
    }

    private static void AddUniqueSeenBeatId(EncounterMemorySaveData memory, string beatId)
    {
        if (memory == null || string.IsNullOrWhiteSpace(beatId))
        {
            return;
        }

        if (memory.SeenBeatIds == null)
        {
            memory.SeenBeatIds = new List<string>();
        }

        string normalizedBeatId = beatId.Trim();
        if (!memory.SeenBeatIds.Contains(normalizedBeatId))
        {
            memory.SeenBeatIds.Add(normalizedBeatId);
        }
    }

    private static string NormalizeEncounterId(string encounterId)
    {
        return string.IsNullOrWhiteSpace(encounterId) ? string.Empty : encounterId.Trim();
    }
    #endregion
}
