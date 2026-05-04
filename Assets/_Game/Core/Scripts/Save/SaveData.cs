using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 세이브 파일 하나에 직렬화되는 전체 게임 데이터.
/// Newtonsoft.Json 으로 JSON 직렬화됩니다.
/// </summary>
[Serializable]
public class SaveData
{
    // ── 플레이어 위치 ─────────────────────────────────────────
    public string currentScene      = SceneName.Overworld;
    public float  playerX           = 0f;
    public float  playerY           = 0f;
    public int    lookingDirection  = 0; // 0=Down 1=Up 2=Left 3=Right

    // ── 캐릭터 스탯 ──────────────────────────────────────────
    public int    playerHP          = 100;
    public int    playerMaxHP       = 100;
    public int    playerMP          = 100;
    public int    playerMaxMP       = 100;

    // ── 인벤토리 ─────────────────────────────────────────────
    public List<string> inventoryItemIDs = new List<string>();

    // ── 이벤트 플래그 (Undertale 스타일) ─────────────────────
    public Dictionary<string, int> eventFlags = new Dictionary<string, int>();

    // ── 메타 ─────────────────────────────────────────────────
    public string saveTime          = "";
    public int    playtimeSeconds   = 0;
}
