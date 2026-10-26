using System;
using System.Globalization;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 저장 호출을 원자 저장소에 연결하는 런타임 Facade입니다.
/// Unity 상태와 코루틴을 다루므로 메인 스레드에서 사용합니다. 저장소의 lock은 이 계약을 대체하지 않습니다.
/// </summary>
public static class SaveManager
{
    public const int ManualSlotCount = 3;
    public const int AutoSlotIndex = 99;

    private static readonly object StorageGate = new object();
    private static readonly ProgressWriteBlockRegistry ProgressWriteBlocks = new ProgressWriteBlockRegistry();
    private static AtomicSaveStorage _storage;
    private static int _autoSaveGeneration;
    private static bool _autoSavePending;
    public static bool HasPendingAutoSave => _autoSavePending;
    public static int AutoSaveRequestVersion => _autoSaveGeneration;
    public static event Action<SaveStorageResult> AutoSaveCompleted;
    public static event Action<int, SaveStorageResult> AutoSaveCompletedWithVersion;

    public static string SaveDirectoryPath => Storage.RootDirectory;
    public static bool AreProgressWritesBlocked => ProgressWriteBlocks.IsBlocked;
    public static bool CanSaveCurrentProgress => !AreProgressWritesBlocked && !GameLoadCoordinator.IsLoading
        && (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading)
        && (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Battle)
        && (BattleManager.Instance == null || !BattleManager.Instance.IsBattleSessionActive);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingState()
    {
        CancelPendingAutoSave();
        ProgressWriteBlocks.Reset();
        AutoSaveCompleted = null;
        AutoSaveCompletedWithVersion = null;
    }

    public static bool IsManualSlot(int slotIndex) => slotIndex >= 0 && slotIndex < ManualSlotCount;

    /// <summary>
    /// 소유자가 복구를 끝낼 때까지 진행 저장/삭제를 차단합니다. 메인 스레드에서 획득합니다.
    /// 이전 자동저장 예약도 폐기하여 차단 해제 후 임시 세션 데이터가 늦게 저장되지 않게 합니다.
    /// 읽기 전용 슬롯 조회와 설정 저장은 차단하지 않습니다.
    /// </summary>
    public static IDisposable AcquireProgressWriteBlock()
    {
        IDisposable lease = ProgressWriteBlocks.Acquire();
        if (_autoSavePending) CancelPendingAutoSave();
        return lease;
    }

    private static SaveStorageResult ProgressWriteBlockedResult()
    {
        return SaveStorageResult.Failed(SaveStorageFailure.NotAllowed,
            "임시 세션의 진행 저장 차단 중에는 저장하거나 삭제할 수 없습니다.");
    }

    /// <summary>필드 좌표를 복사본에 반영합니다. 임시 전투 좌표나 이전 입구 ID를 저장하지 않습니다.</summary>
    public static SaveStorageResult TrySaveProgress(int slotIndex, PlayerController player)
    {
        if (AreProgressWritesBlocked) return ProgressWriteBlockedResult();
        if (!IsManualSlot(slotIndex) && slotIndex != AutoSlotIndex)
            return SaveStorageResult.Failed(SaveStorageFailure.InvalidArgument, "올바르지 않은 저장 슬롯입니다.");
        GlobalDataManager global = GlobalDataManager.Instance;
        if (!CanSaveCurrentProgress || global == null || player == null || !player.isActiveAndEnabled)
            return SaveStorageResult.Failed(SaveStorageFailure.NotAllowed, "필드에서 저장할 수 있는 상태가 아닙니다.");
        GameState state = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState : GameState.Exploration;
        if (state != GameState.Exploration)
            return SaveStorageResult.Failed(SaveStorageFailure.NotAllowed, "대화와 연출이 끝난 뒤 저장할 수 있습니다.");

        try
        {
            SaveData snapshot = global.ToSaveData();
            snapshot.currentScene = SceneManager.GetActiveScene().name;
            snapshot.spawnPointId = string.Empty;
            snapshot.playerX = player.transform.position.x;
            snapshot.playerY = player.transform.position.y;
            snapshot.lookingDirection = player.FacingDirection;
            bool isRest = IsManualSlot(slotIndex);
            if (isRest) ManualSaveRestPolicy.PrepareSnapshot(snapshot);
            SaveStorageResult result = TrySave(snapshot, slotIndex);
            if (result.Success && isRest)
            {
                // 파일 기록이 성공한 뒤에만 휴식 결과를 확정합니다. 표시 복구 실패가 저장 성공을 뒤집지 않습니다.
                try
                {
                    global.ApplyManualSaveRest(snapshot);
                    OverworldEnemy.RefreshAfterManualSaveRest();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, global);
                }
            }
            return result;
        }
        catch (Exception exception)
        {
            return SaveStorageResult.Failed(SaveStorageFailure.Serialization,
                "진행 상황을 정리하지 못했습니다: " + exception.Message);
        }
    }

    /// <summary>저장 오브젝트에서만 호출합니다. 메뉴 어디서나 저장하는 기능은 제공하지 않습니다.</summary>
    public static bool TryPromptManualSave(UnityEngine.Object owner, PlayerController player,
        int preferredSlot = 0, Action<bool> onCompleted = null)
    {
        if (owner == null || player == null || !CanSaveCurrentProgress || !OverworldActionGate.AllowsWorldActions
            || DialogueManager.Instance == null) return false;
        // 기존 오브젝트의 기본 슬롯을 첫 선택으로 유지하되 모든 수동 칸을 선택할 수 있습니다.
        int first = IsManualSlot(preferredSlot) ? preferredSlot : 0;
        var slots = new int[ManualSlotCount];
        var labels = new string[ManualSlotCount + 1];
        for (int i = 0; i < ManualSlotCount; i++)
        {
            int slot = slots[i] = (first + i) % ManualSlotCount;
            SaveLoadResult saved = TryLoad(slot);
            labels[i] = "저장 " + (slot + 1) + (saved.Success ? " · " + saved.Data.saveTime + " 덮어쓰기"
                : saved.Failure == SaveLoadFailure.NotFound ? " · 빈 칸" : " · 읽기 불가 (다른 칸 권장)");
        }
        labels[ManualSlotCount] = "취소";
        return DialogueManager.Instance.TryStartChoicePrompt("어느 칸에 저장할까요?", labels, selected =>
        {
            if (selected < 0 || selected >= slots.Length || owner == null || player == null)
            { onCompleted?.Invoke(false); return; }
            SaveStorageResult result = TrySaveProgress(slots[selected], player);
            onCompleted?.Invoke(result.Success);
            if (!result.Success) Debug.LogWarning("[SaveManager] " + result.Message, owner);
            DialogueManager.Instance?.TryStartChoicePrompt(
                result.Success ? "저장되었습니다." : "저장하지 못했습니다.\n" + result.Message,
                new[] { "확인" }, _ => { });
        }, () => onCompleted?.Invoke(false));
    }

    /// <summary>승리/컷씬 완료 콜백에서 요청. 중복 요청은 합치고 안전한 필드 복귀 뒤 자동 칸에만 씁니다.</summary>
    public static bool RequestAutoSave()
    {
        if (AreProgressWritesBlocked) return false;
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || GameLoadCoordinator.IsLoading) return false;
        if (_autoSavePending) return true;
        _autoSavePending = true;
        int generation = ++_autoSaveGeneration;
        global.StartCoroutine(AutoSaveWhenReady(global, generation));
        return true;
    }

    public static void CancelPendingAutoSave()
    {
        _autoSavePending = false;
        ++_autoSaveGeneration;
    }

    private static IEnumerator AutoSaveWhenReady(GlobalDataManager owner, int generation)
    {
        int readyFrames = 0;
        while (owner != null && owner == GlobalDataManager.Instance && generation == _autoSaveGeneration)
        {
            yield return null;
            if (generation != _autoSaveGeneration) yield break;
            bool ready = CanSaveCurrentProgress && OverworldActionGate.AllowsWorldActions
                && !GameInput.IsConfigModalActive
                && (DialogueManager.Instance == null || !DialogueManager.Instance.IsPresentationVisible);
            readyFrames = ready ? readyFrames + 1 : 0;
            if (readyFrames < 2) continue;

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            SaveStorageResult result = TrySaveProgress(AutoSlotIndex, player);
            _autoSavePending = false;
            if (!result.Success) Debug.LogWarning("[SaveManager] 자동 저장 실패: " + result.Message);
            NotifyAutoSaveCompleted(generation, result);
            yield break;
        }
    }

    private static void NotifyAutoSaveCompleted(int version, SaveStorageResult result)
    {
        // 콜백이 새 저장을 예약하더라도 끝난 요청의 버전을 전달합니다.
        // 잘못된 표시 구독자가 다른 종료 화면/기존 저장 구독을 막지 않게 격리합니다.
        Action<int, SaveStorageResult> versioned = AutoSaveCompletedWithVersion;
        if (versioned != null)
        {
            foreach (Action<int, SaveStorageResult> callback in versioned.GetInvocationList())
            {
                try { callback(version, result); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        Action<SaveStorageResult> legacy = AutoSaveCompleted;
        if (legacy == null) return;
        foreach (Action<SaveStorageResult> callback in legacy.GetInvocationList())
        {
            try { callback(result); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

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
        if (AreProgressWritesBlocked) return ProgressWriteBlockedResult();
        if (data != null && slotIndex >= 0 && !CanSaveCurrentProgress)
            return SaveStorageResult.Failed(SaveStorageFailure.NotAllowed,
                "전투·후열 선택·저장 복구 중에는 진행 상황을 저장할 수 없습니다.");
        return Storage.Save(data, slotIndex,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
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
        if (AreProgressWritesBlocked) return ProgressWriteBlockedResult();
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

    public static bool TryLoadMostRecent(
        out int slotIndex,
        out SaveLoadResult loadResult)
    {
        slotIndex = -1;
        loadResult = null;
        DateTime bestTimestamp = DateTime.MinValue;
        int[] candidates = { 0, 1, 2, AutoSlotIndex };

        for (int i = 0; i < candidates.Length; i++)
        {
            int candidateSlot = candidates[i];
            SaveLoadResult candidate = TryLoad(candidateSlot);
            if (!candidate.Success || candidate.Data == null)
                continue;

            DateTime timestamp = ParseSaveTime(candidate.Data.saveTime);
            if (loadResult != null && timestamp < bestTimestamp)
                continue;

            bestTimestamp = timestamp;
            slotIndex = candidateSlot;
            loadResult = candidate;
        }

        if (loadResult != null)
            return true;

        loadResult = SaveLoadResult.Failed(
            SaveLoadFailure.NotFound,
            "불러올 수 있는 저장 슬롯이 없습니다.");
        return false;
    }

    private static DateTime ParseSaveTime(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
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
