using System;
using UnityEngine;

public enum GameLoadStartFailure
{
    None,
    Busy,
    SaveNotFound,
    RuntimeMissing,
    NoLoadableScene
}

public sealed class GameLoadStartResult
{
    private GameLoadStartResult()
    {
    }

    public bool Accepted { get; private set; }
    public int SlotIndex { get; private set; } = -1;
    public GameLoadStartFailure Failure { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public SceneLoadOperation Operation { get; private set; }

    public static GameLoadStartResult Started(int slotIndex, SceneLoadOperation operation)
    {
        return new GameLoadStartResult
        {
            Accepted = true,
            SlotIndex = slotIndex,
            Failure = GameLoadStartFailure.None,
            Operation = operation
        };
    }

    public static GameLoadStartResult Rejected(GameLoadStartFailure failure, string message)
    {
        return new GameLoadStartResult
        {
            Accepted = false,
            Failure = failure,
            Message = message ?? string.Empty
        };
    }
}

/// <summary>
/// Applies a save snapshot and owns rollback until the destination scene activates.
/// </summary>
public static class GameLoadCoordinator
{
    private static bool s_isLoading;

    public static bool IsLoading => s_isLoading;

    public static GameLoadStartResult LoadMostRecent(
        Action<SceneLoadResult> onCompleted = null)
    {
        if (s_isLoading)
            return RejectBusy();

        if (!SaveManager.TryLoadMostRecent(out int slotIndex, out SaveLoadResult loadResult))
        {
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.SaveNotFound,
                loadResult != null ? loadResult.Message : "불러올 저장 슬롯이 없습니다.");
        }

        return StartLoad(slotIndex, loadResult, onCompleted);
    }

    public static GameLoadStartResult LoadSlot(
        int slotIndex,
        Action<SceneLoadResult> onCompleted = null)
    {
        if (s_isLoading)
            return RejectBusy();

        SaveLoadResult loadResult = SaveManager.TryLoad(slotIndex);
        if (!loadResult.Success)
        {
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.SaveNotFound,
                loadResult.Message);
        }

        return StartLoad(slotIndex, loadResult, onCompleted);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        s_isLoading = false;
    }

    private static GameLoadStartResult StartLoad(
        int slotIndex,
        SaveLoadResult loadResult,
        Action<SceneLoadResult> onCompleted)
    {
        if (s_isLoading)
            return RejectBusy();

        GlobalDataManager global = GlobalDataManager.Instance;
        SceneLoader sceneLoader = SceneLoader.Instance;
        if (global == null || sceneLoader == null)
        {
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.RuntimeMissing,
                "GlobalDataManager 또는 SceneLoader가 없습니다.");
        }
        if (sceneLoader.IsLoading)
            return RejectBusy("다른 Scene 전환이 진행 중입니다.");

        SaveData loadedData = loadResult?.Data;
        if (loadedData == null)
        {
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.SaveNotFound,
                "저장 데이터가 비어 있습니다.");
        }

        string targetScene = ResolveTargetScene(sceneLoader, loadedData, out bool usedFallback);
        if (string.IsNullOrEmpty(targetScene))
        {
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.NoLoadableScene,
                "저장된 Scene과 기본 오버월드 Scene을 모두 불러올 수 없습니다.");
        }

        SaveData previousData = global.ToSaveData();
        GameState previousState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;
        float previousTimeScale = Time.timeScale;

        global.FromSaveData(loadedData);
        global.SpawnScene = targetScene;
        if (usedFallback)
        {
            global.CurrentRoomId = string.Empty;
            global.SpawnPointId = string.Empty;
            global.SpawnFallbackAllowed = true;
        }

        global.CancelPendingBattleEncounter();
        Time.timeScale = 1f;
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        s_isLoading = true;

        SceneLoadOperation operation = sceneLoader.LoadSceneWithResult(
            targetScene,
            0.35f,
            result =>
            {
                s_isLoading = false;
                if (!SceneLoadResultUtility.WasDestinationActivated(result))
                {
                    global.FromSaveData(previousData);
                    Time.timeScale = previousTimeScale;
                    GameStateManager.Instance?.ChangeState(previousState);
                }
                else if (GameStateManager.Instance != null
                    && GameStateManager.Instance.CurrentState == GameState.Cutscene)
                {
                    GameStateManager.Instance.ChangeState(GameState.Exploration);
                }

                onCompleted?.Invoke(result);
            });

        if (operation == null)
        {
            s_isLoading = false;
            global.FromSaveData(previousData);
            Time.timeScale = previousTimeScale;
            GameStateManager.Instance?.ChangeState(previousState);
            return GameLoadStartResult.Rejected(
                GameLoadStartFailure.NoLoadableScene,
                "SceneLoader가 로드 작업을 만들지 못했습니다.");
        }

        return operation.IsDone && !SceneLoadResultUtility.WasDestinationActivated(operation.Result)
            ? GameLoadStartResult.Rejected(
                GameLoadStartFailure.NoLoadableScene,
                "목적 Scene 로드가 거부됐습니다: " + operation.Result)
            : GameLoadStartResult.Started(slotIndex, operation);
    }

    private static GameLoadStartResult RejectBusy(
        string message = "다른 저장 데이터를 불러오는 중입니다.")
    {
        return GameLoadStartResult.Rejected(GameLoadStartFailure.Busy, message);
    }

    private static string ResolveTargetScene(
        SceneLoader sceneLoader,
        SaveData loadedData,
        out bool usedFallback)
    {
        string savedScene = string.IsNullOrWhiteSpace(loadedData.currentScene)
            ? string.Empty
            : loadedData.currentScene.Trim();
        if (sceneLoader.CanLoadScene(savedScene))
        {
            usedFallback = false;
            return savedScene;
        }

        if (sceneLoader.CanLoadScene(SceneName.Overworld))
        {
            usedFallback = true;
            return SceneName.Overworld;
        }

        usedFallback = false;
        return string.Empty;
    }
}