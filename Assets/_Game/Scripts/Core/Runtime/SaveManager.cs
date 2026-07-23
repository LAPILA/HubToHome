using System;
using UnityEngine;

/// <summary>
/// 기존 저장 호출을 원자 저장소에 연결하는 런타임 Facade입니다.
/// </summary>
public static class SaveManager
{
    public const int ManualSlotCount = 3;
    public const int AutoSlotIndex = 99;

    private static readonly object StorageGate = new object();
    private static AtomicSaveStorage _storage;

    public static string SaveDirectoryPath => Storage.RootDirectory;

    private static AtomicSaveStorage Storage
    {
        get
        {
            lock (StorageGate)
            {
                if (_storage == null)
                {
                    _storage = new AtomicSaveStorage(
                        Application.persistentDataPath);
                }

                return _storage;
            }
        }
    }

    public static void Save(SaveData data, int slotIndex)
    {
        SaveStorageResult result = TrySave(data, slotIndex);
        if (result.Success)
        {
            Debug.Log(
                "<color=#00FF00>[SaveManager] 슬롯 "
                + slotIndex
                + " 저장 완료.</color>");
            return;
        }

        LogStorageFailure("저장", slotIndex, result);
    }

    public static SaveStorageResult TrySave(
        SaveData data,
        int slotIndex)
    {
        if (data != null)
        {
            data.schemaVersion = SaveSchema.CurrentVersion;
            data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return Storage.Save(data, slotIndex);
    }

    public static SaveData Load(int slotIndex)
    {
        SaveLoadResult result = TryLoad(slotIndex);
        if (!result.Success)
        {
            if (result.Failure == SaveLoadFailure.InvalidSlot)
            {
                Debug.LogWarning(
                    "[SaveManager] 로드 요청이 올바르지 않습니다 (슬롯 "
                    + slotIndex
                    + "): "
                    + result.Message);
            }
            else if (result.Failure != SaveLoadFailure.NotFound)
            {
                Debug.LogError(
                    "[SaveManager] 로드 실패 (슬롯 "
                    + slotIndex
                    + "): "
                    + result.Message);
            }

            return null;
        }

        if (result.Source != SaveLoadSource.Primary)
        {
            Debug.LogWarning(
                "[SaveManager] 슬롯 "
                + slotIndex
                + " 복구본 로드: "
                + result.Message);
        }
        else if (result.WasMigrated)
        {
            Debug.Log(
                "[SaveManager] 슬롯 "
                + slotIndex
                + " 저장 데이터를 v"
                + result.SourceVersion
                + "에서 v"
                + SaveSchema.CurrentVersion
                + "으로 변환했습니다.");
        }

        return result.Data;
    }

    public static SaveLoadResult TryLoad(int slotIndex)
    {
        return Storage.Load(slotIndex);
    }

    public static void Delete(int slotIndex)
    {
        SaveStorageResult result = TryDelete(slotIndex);
        if (result.Success)
        {
            Debug.Log("[SaveManager] 슬롯 " + slotIndex + " 삭제됨.");
            return;
        }

        LogStorageFailure("삭제", slotIndex, result);
    }

    public static SaveStorageResult TryDelete(int slotIndex)
    {
        return Storage.Delete(slotIndex);
    }

    public static SaveSlotInspection InspectSlot(int slotIndex)
    {
        return Storage.Inspect(slotIndex);
    }

    public static bool Exists(int slotIndex)
    {
        if (slotIndex < 0)
            return false;

        SaveSlotInspection inspection = InspectSlot(slotIndex);
        return inspection != null && inspection.IsLoadable;
    }

    public static bool HasAnySave()
    {
        for (int slotIndex = 0; slotIndex < ManualSlotCount; slotIndex++)
        {
            if (Exists(slotIndex))
                return true;
        }

        return Exists(AutoSlotIndex);
    }

    private static void LogStorageFailure(
        string operation,
        int slotIndex,
        SaveStorageResult result)
    {
        string message =
            "[SaveManager] "
            + operation
            + " 실패 (슬롯 "
            + slotIndex
            + "): "
            + (result != null ? result.Message : "결과 없음");

        if (result != null
            && result.Failure == SaveStorageFailure.InvalidArgument)
        {
            Debug.LogWarning(message);
        }
        else
        {
            Debug.LogError(message);
        }
    }
}
