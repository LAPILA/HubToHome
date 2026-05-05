using System;
using System.Collections.Generic;

[Serializable]
public class CharacterSaveData
{
    public string CharacterID = "Hero";
    public int Level = 1;
    public int EXP = 0;
    
    public int HP = 100;
    public int MaxHP = 100;
    public int MP = 50;
    public int MaxMP = 50;

    public int ATK = 10;
    public int DEF = 5;
    public int SPD = 10;

    public List<string> EquippedSkillIDs = new List<string>();
}

[Serializable]
public class SaveData
{
    // ── 1. 위치 정보 ──
    public string currentScene = "OverworldScene";
    public float  playerX = 0f;
    public float  playerY = 0f;
    public int    lookingDirection = 0; 

    // ── 2. 파티 시스템 ──
    public List<CharacterSaveData> PartyData = new List<CharacterSaveData>();

    // ── 3. 소지품 및 플래그 ──
    // 🚨 핵심: 리스트가 아니라 Dictionary로 수량까지 저장합니다.
    public Dictionary<string, int> InventoryDict = new Dictionary<string, int>();
    public Dictionary<string, int> eventFlags = new Dictionary<string, int>();

    // ── 4. 메타 데이터 ──
    public string saveTime = "";
    public int    playtimeSeconds = 0;
}