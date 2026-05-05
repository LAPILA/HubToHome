using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 세이브/로드를 담당하는 정적 유틸리티 클래스.
/// </summary>
public static class SaveManager
{
    public const int ManualSlotCount = 3;
    public const int AutoSlotIndex   = 99; // 자동 저장 전용 슬롯

    private static string GetPath(int slotIndex)
        => Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

    public static void Save(SaveData data, int slotIndex)
    {
        try
        {
            data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // 보안/최적화를 위해 JSON 직렬화 옵션 추가
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(GetPath(slotIndex), json);
            Debug.Log($"<color=#00FF00>[SaveManager] 슬롯 {slotIndex} 저장 완료.</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패 (슬롯 {slotIndex}): {e.Message}");
        }
    }

    public static SaveData Load(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 로드 실패 (슬롯 {slotIndex}): {e.Message}");
            return null;
        }
    }

    public static void Delete(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] 슬롯 {slotIndex} 삭제됨.");
        }
    }

    public static bool Exists(int slotIndex) => File.Exists(GetPath(slotIndex));
}