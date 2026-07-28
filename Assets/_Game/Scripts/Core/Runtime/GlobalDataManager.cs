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
    private readonly MapReturnBookmarkStack _mapReturnBookmarks = new MapReturnBookmarkStack();
    public int Money { get; private set; } = 0;

    public event System.Action<string, int, int> FlagChanged;
    
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
    public string SpawnScene { get; set; } = SceneName.Overworld;
    public string CurrentRoomId { get; set; } = string.Empty;
    public string SpawnPointId { get; set; } = string.Empty;
    public string CurrentTrainStopId { get; set; } = string.Empty;
    public bool SpawnFallbackAllowed { get; set; }
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void InitializeDefaults()
    {
        _eventFlags.Clear();
        _inventoryDict.Clear();
        _encounterMemory.Clear();
        _overworldEnemyStates.Clear();
        _mapReturnBookmarks.Clear();
        CurrentTrainStopId = string.Empty;
        Party.Clear(); // 기존 더미 데이터 추가 로직 삭제
    }

    /// <summary>
    /// 씬 캐릭터와 대응하는 파티 저장 객체를 찾거나, 파티가 비어 있을 때 새로 만듭니다.
    /// </summary>
    public CharacterSaveData InitializePartyFromScene(PlayerCharacter scenePlayer)
    {
        if (scenePlayer == null) return null;

        CharacterData characterData = scenePlayer.CharacterData;
        string stableId = NormalizeCharacterId(characterData != null ? characterData.CharacterID : null);
        CharacterSaveData existing = FindPartyMember(stableId);
        if (existing != null)
            return existing;

        if (Party.Count > 0)
        {
            CharacterSaveData legacyLeader = Party[0];
            if (legacyLeader != null && string.IsNullOrWhiteSpace(legacyLeader.CharacterDataID))
            {
                legacyLeader.CharacterDataID = stableId;
                return legacyLeader;
            }

            Debug.LogWarning(
                $"[GlobalDataManager] Scene character has no matching party save. CharacterDataID={stableId}",
                scenePlayer);
            return null;
        }

        int startMaxHP = characterData != null ? characterData.BaseMaxHP : (scenePlayer.BaseMaxHP > 0 ? scenePlayer.BaseMaxHP : 100);
        int startMaxMP = characterData != null ? characterData.BaseMaxMP : (scenePlayer.BaseMaxMP > 0 ? scenePlayer.BaseMaxMP : 50);
        int startATK   = characterData != null ? characterData.BaseATK : (scenePlayer.BaseATK > 0 ? scenePlayer.BaseATK : 10);
        int startDEF   = characterData != null ? characterData.BaseDEF : scenePlayer.BaseDEF;
        int startSPD   = characterData != null ? characterData.BaseSPD : (scenePlayer.BaseSPD > 0 ? scenePlayer.BaseSPD : 10);

        var newData = new CharacterSaveData()
        {
            CharacterDataID = stableId,
            CharacterID = scenePlayer.DisplayName,
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
        return newData;
    }

    private CharacterSaveData FindPartyMember(string stableId)
    {
        if (string.IsNullOrEmpty(stableId))
            return null;

        for (int i = 0; i < Party.Count; i++)
        {
            CharacterSaveData member = Party[i];
            if (member != null
                && string.Equals(
                    NormalizeCharacterId(member.CharacterDataID),
                    stableId,
                    System.StringComparison.Ordinal))
            {
                return member;
            }
        }

        return null;
    }
    public bool TryApplyOverworldPartyDamage(
        int requestedDamage,
        out CharacterSaveData leader,
        out int previousHP,
        out int currentHP)
    {
        leader = Party.Count > 0 ? Party[0] : null;
        previousHP = leader != null ? Mathf.Max(1, leader.HP) : 0;
        currentHP = previousHP;
        if (leader == null || requestedDamage <= 0)
            return false;

        int maxHP = Mathf.Max(1, leader.MaxHP);
        previousHP = Mathf.Clamp(previousHP, 1, maxHP);
        currentHP = Mathf.Max(1, previousHP - requestedDamage);
        leader.HP = currentHP;
        return true;
    }

    #endregion

    #region [ Event Flags API ]
    public void SetFlag(string key, int value)
    {
        string normalizedKey = NormalizeFlagKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        int oldValue = GetFlag(normalizedKey);
        if (oldValue == value)
            return;

        _eventFlags[normalizedKey] = value;
        NotifyFlagChangedSafely(normalizedKey, oldValue, value);
    }

    public int GetFlag(string key, int defaultValue = 0)
    {
        string normalizedKey = NormalizeFlagKey(key);
        return !string.IsNullOrEmpty(normalizedKey)
            && _eventFlags.TryGetValue(normalizedKey, out int value)
                ? value
                : defaultValue;
    }

    public bool TryGetFlag(string key, out int value)
    {
        string normalizedKey = NormalizeFlagKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            value = 0;
            return false;
        }

        return _eventFlags.TryGetValue(normalizedKey, out value);
    }
    private void NotifyFlagChangedSafely(string key, int oldValue, int newValue)
    {
        System.Action<string, int, int> handlers = FlagChanged;
        if (handlers == null)
            return;

        System.Delegate[] subscribers = handlers.GetInvocationList();
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                ((System.Action<string, int, int>)subscribers[i]).Invoke(key, oldValue, newValue);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
    #endregion

    #region [ Map Return Bookmark API ]
    public int MapReturnBookmarkCount => _mapReturnBookmarks.Count;

    public MapReturnBookmarkToken PushPendingMapReturnBookmark(MapReturnBookmark bookmark)
    {
        return _mapReturnBookmarks.PushPending(bookmark);
    }

    public bool CommitMapReturnBookmark(MapReturnBookmarkToken token)
    {
        return _mapReturnBookmarks.Commit(token);
    }

    public bool RollbackMapReturnBookmark(MapReturnBookmarkToken token)
    {
        return _mapReturnBookmarks.Rollback(token);
    }

    public bool TryPeekMapReturnBookmark(
        out MapReturnBookmark bookmark,
        out MapReturnBookmarkToken token)
    {
        return _mapReturnBookmarks.TryPeek(out bookmark, out token);
    }

    public bool TryPopMapReturnBookmark(
        MapReturnBookmarkToken expectedToken,
        out MapReturnBookmark bookmark)
    {
        return _mapReturnBookmarks.TryPop(expectedToken, out bookmark);
    }

    public void ClearMapReturnBookmarks()
    {
        _mapReturnBookmarks.Clear();
    }
    #endregion

    #region [ Inventory API ]
    public void AddItem(string itemID, int amount = 1)
    {
        AddItemAndGetAddedAmount(itemID, amount);
    }

    public int AddItemAndGetAddedAmount(string itemID, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemID) || amount <= 0) return 0;

        string normalizedId = itemID.Trim();
        int current = _inventoryDict.TryGetValue(normalizedId, out int existing)
            ? Mathf.Max(0, existing)
            : 0;
        ItemData item = ItemDatabase.FindById(normalizedId);
        int maxStack = item == null
            ? int.MaxValue
            : item.IsStackable ? Mathf.Max(1, item.MaxStackSize) : 1;
        int next = (int)System.Math.Min((long)current + amount, maxStack);
        if (next <= current) return 0;

        _inventoryDict[normalizedId] = next;
        return next - current;
    }
    public bool RemoveItem(string itemID, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemID) || amount <= 0) return false;

        string normalizedId = itemID.Trim();
        if (!_inventoryDict.TryGetValue(normalizedId, out int current) || current < amount)
            return false;

        int remaining = current - amount;
        if (remaining > 0) _inventoryDict[normalizedId] = remaining;
        else _inventoryDict.Remove(normalizedId);
        return true;
    }

    public int GetItemCount(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID)) return 0;
        return _inventoryDict.TryGetValue(itemID.Trim(), out int count) ? Mathf.Max(0, count) : 0;
    }

    public IReadOnlyDictionary<string, int> GetInventory() => _inventoryDict;

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        Money = (int)System.Math.Min((long)Money + amount, int.MaxValue);
    }

    public bool SpendMoney(int amount)
    {
        if (amount < 0 || Money < amount) return false;
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
        CurrentEncounterEnemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
        CurrentEncounterDefeatsOnVictory = defeatsOnVictory;
        CurrentEncounterPlayerPreemptiveAttack = playerPreemptiveAttack;

        if (string.IsNullOrEmpty(CurrentEncounterEnemyId)) return;

        var state = GetOrCreateOverworldEnemyState(CurrentEncounterEnemyId, sceneName);
        state.SceneName = sceneName;
    }
    public void EndOverworldEnemyEncounterContext()
    {
        CurrentEncounterEnemyId = null;
        CurrentEncounterDefeatsOnVictory = false;
        CurrentEncounterPlayerPreemptiveAttack = false;
    }
    public void CancelPendingBattleEncounter()
    {
        PendingEnemies?.Clear();
        PendingBattleBGM = null;
        PendingBattleScenario = null;
        EndOverworldEnemyEncounterContext();
    }

    public OverworldEnemyRuntimeState GetOrCreateOverworldEnemyState(string enemyId, string sceneName = null)
    {
        if (string.IsNullOrWhiteSpace(enemyId)) return null;

        string normalizedId = enemyId.Trim();
        if (!_overworldEnemyStates.TryGetValue(normalizedId, out OverworldEnemyRuntimeState state) || state == null)
        {
            state = new OverworldEnemyRuntimeState
            {
                EnemyId = normalizedId,
                SceneName = sceneName ?? string.Empty,
                CooldownAlpha = 0.5f
            };
            _overworldEnemyStates[normalizedId] = state;
        }

        state.EnemyId = normalizedId;
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

        return _overworldEnemyStates.TryGetValue(enemyId.Trim(), out state) && state != null;
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

    public int GetHighestPartyLevel()
    {
        int highest = 1;
        for (int i = 0; i < Party.Count; i++)
        {
            CharacterSaveData member = Party[i];
            if (member != null)
                highest = Mathf.Max(highest, member.Level);
        }

        return highest;
    }
    #endregion

    #region [ Save & Load ]
    public SaveData ToSaveData()
    {
        var data = new SaveData
        {
            playerName       = PlayerName, // 🚨 세이브 데이터에 이름 추가!
            currentScene     = NormalizeSceneName(SpawnScene),
            currentRoomId    = CurrentRoomId,
            spawnPointId     = SpawnPointId,
            currentTrainStopId = CurrentTrainStopId,
            playerX          = SpawnX,
            playerY          = SpawnY,
            lookingDirection = LookingDir,
            
            // 안전한 깊은 복사(Deep Copy)
            InventoryDict = new Dictionary<string, int>(_inventoryDict),
            eventFlags    = new Dictionary<string, int>(_eventFlags),
            EncounterMemory = CloneEncounterMemoryDictionary(_encounterMemory),
            OverworldEnemies = CloneOverworldEnemyStates(_overworldEnemyStates),
            PartyData     = CloneParty(Party),
            Money         = Money
        };
        return data;
    }

    public void FromSaveData(SaveData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[GlobalDataManager] 비어 있는 SaveData는 불러올 수 없습니다.");
            return;
        }

        PlayerName = data.playerName ?? string.Empty;
        SpawnScene = NormalizeSceneName(data.currentScene);
        CurrentRoomId = data.currentRoomId ?? string.Empty;
        SpawnPointId = data.spawnPointId ?? string.Empty;
        CurrentTrainStopId = string.IsNullOrWhiteSpace(data.currentTrainStopId)
            ? string.Empty
            : data.currentTrainStopId.Trim();
        SpawnFallbackAllowed = false;
        SpawnX = data.playerX;
        SpawnY = data.playerY;
        LookingDir = data.lookingDirection;
        _mapReturnBookmarks.Clear();

        _inventoryDict.Clear();
        if (data.InventoryDict != null)
        {
            foreach (KeyValuePair<string, int> entry in data.InventoryDict)
                AddItem(entry.Key, entry.Value);
        }

        _eventFlags.Clear();
        if (data.eventFlags != null)
        {
            foreach (KeyValuePair<string, int> entry in data.eventFlags)
            {
                string normalizedKey = NormalizeFlagKey(entry.Key);
                if (!string.IsNullOrEmpty(normalizedKey))
                    _eventFlags[normalizedKey] = entry.Value;
            }
        }

        _encounterMemory.Clear();
        if (data.EncounterMemory != null)
        {
            foreach (KeyValuePair<string, EncounterMemorySaveData> entry in data.EncounterMemory)
            {
                string encounterId = NormalizeEncounterId(entry.Key);
                if (string.IsNullOrEmpty(encounterId))
                {
                    continue;
                }

                _encounterMemory[encounterId] = CloneEncounterMemory(entry.Value, encounterId);
            }
        }

        _overworldEnemyStates.Clear();
        if (data.OverworldEnemies != null)
        {
            foreach (KeyValuePair<string, OverworldEnemySaveData> entry in data.OverworldEnemies)
            {
                OverworldEnemySaveData savedState = entry.Value;
                string enemyId = (string.IsNullOrWhiteSpace(savedState?.EnemyId) ? entry.Key : savedState.EnemyId)?.Trim();
                if (string.IsNullOrWhiteSpace(enemyId)) continue;

                _overworldEnemyStates[enemyId] = new OverworldEnemyRuntimeState
                {
                    EnemyId = enemyId,
                    SceneName = savedState?.SceneName ?? string.Empty,
                    IsDefeated = savedState != null && savedState.IsDefeated,
                    CooldownUntilUnscaledTime = 0f,
                    CooldownAlpha = 0.5f
                };
            }
        }

        Party = CloneParty(data.PartyData);
        Money = Mathf.Max(0, data.Money);
    }

    private static List<CharacterSaveData> CloneParty(IReadOnlyList<CharacterSaveData> source)
    {
        var clone = new List<CharacterSaveData>();
        if (source == null) return clone;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSaveData member = source[i];
            if (member == null) continue;
            clone.Add(new CharacterSaveData
            {
                CharacterDataID = member.CharacterDataID ?? string.Empty,
                CharacterID = member.CharacterID ?? string.Empty,
                Level = Mathf.Max(1, member.Level),
                EXP = Mathf.Max(0, member.EXP),
                HP = member.HP,
                MaxHP = member.MaxHP,
                MP = member.MP,
                MaxMP = member.MaxMP,
                ATK = member.ATK,
                DEF = member.DEF,
                SPD = member.SPD,
                EquippedSkillIDs = member.EquippedSkillIDs != null
                    ? new List<string>(member.EquippedSkillIDs)
                    : new List<string>()
            });
        }

        return clone;
    }
    private static Dictionary<string, OverworldEnemySaveData> CloneOverworldEnemyStates(
        Dictionary<string, OverworldEnemyRuntimeState> source)
    {
        var clone = new Dictionary<string, OverworldEnemySaveData>();
        if (source == null) return clone;

        foreach (KeyValuePair<string, OverworldEnemyRuntimeState> entry in source)
        {
            OverworldEnemyRuntimeState state = entry.Value;
            string enemyId = entry.Key?.Trim();
            if (state == null || string.IsNullOrEmpty(enemyId)) continue;
            clone[enemyId] = new OverworldEnemySaveData
            {
                EnemyId = enemyId,
                SceneName = state.SceneName ?? string.Empty,
                IsDefeated = state.IsDefeated
            };
        }

        return clone;
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

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private static string NormalizeFlagKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private static string NormalizeSceneName(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? SceneName.Overworld : sceneName.Trim();
    }
    #endregion
}
