using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 세이브/로드를 담당하는 정적 유틸리티 클래스.
/// Manual Slot 3개 + Auto Slot 1개를 지원합니다.
/// </summary>
public static class SaveManager
{
    // ── 슬롯 정의 ─────────────────────────────────────────────
    public const int ManualSlotCount = 3;
    public const int AutoSlotIndex   = 3; // 인덱스 3 = Auto

    private static string GetPath(int slotIndex)
        => Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

    // ── 저장 ──────────────────────────────────────────────────
    /// <param name="slotIndex">0~2: Manual, 3: Auto</param>
    public static void Save(SaveData data, int slotIndex)
    {
        try
        {
            data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(GetPath(slotIndex), json);
            Debug.Log($"[SaveManager] Slot {slotIndex} saved.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save failed (slot {slotIndex}): {e.Message}");
        }
    }

    // ── 불러오기 ──────────────────────────────────────────────
    /// <returns>저장 파일이 없으면 null 반환</returns>
    public static SaveData Load(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path))
        {
            Debug.Log($"[SaveManager] No save file at slot {slotIndex}.");
            return null;
        }
        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Load failed (slot {slotIndex}): {e.Message}");
            return null;
        }
    }

    // ── 삭제 ──────────────────────────────────────────────────
    public static void Delete(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] Slot {slotIndex} deleted.");
        }
    }

    // ── 존재 여부 확인 ────────────────────────────────────────
    public static bool Exists(int slotIndex) => File.Exists(GetPath(slotIndex));
}
