using System;
using System.Collections.Generic;

[Serializable]
public class CharacterSaveData
{
    [UnityEngine.Header("Identity")]
    public string CharacterDataID = "";
    public string CharacterID = "Player";
    public int Level = 1;
    public int EXP = 0;
    
    [UnityEngine.Header("Stats")]
    public int HP = 100;
    public int MaxHP = 100;
    public int MP = 100;
    public int MaxMP = 100;

    public int ATK = 10;
    public int DEF = 5;
    public int SPD = 10;

    public List<string> EquippedSkillIDs = new List<string>();
}

[Serializable]
public sealed class OverworldEnemySaveData
{
    public string EnemyId = "";
    public string SceneName = "";
    public bool IsDefeated;
}

[Serializable]
public class SaveData
{
    public int schemaVersion = SaveSchema.CurrentVersion;

    // ── 1. 위치 정보 ──
    public string currentScene = SceneName.Overworld;
    public string currentRoomId = "";
    public string spawnPointId = "";
    public string currentTrainStopId = "";
    public float  playerX = 0f;
    public float  playerY = 0f;
    public int    lookingDirection = 0; 

    // ── 2. 파티 시스템 ──
    public List<CharacterSaveData> PartyData = new List<CharacterSaveData>();

    // ── 3. 소지품 및 플래그 ──
    public Dictionary<string, int> InventoryDict = new Dictionary<string, int>();
    public Dictionary<string, int> eventFlags = new Dictionary<string, int>();
    public Dictionary<string, EncounterMemorySaveData> EncounterMemory = new Dictionary<string, EncounterMemorySaveData>();
    public Dictionary<string, OverworldEnemySaveData> OverworldEnemies = new Dictionary<string, OverworldEnemySaveData>();
    public int Money = 0;

    // ── 4. 메타 데이터 ──
    public string saveTime = "";
    public int    playtimeSeconds = 0;
    public string playerName;
}
