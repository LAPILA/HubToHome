using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>보스/필수 전투 직전 상태만 보관합니다. 진행 중 전투나 체크포인트를 디스크에 저장하지 않습니다.</summary>
public static class BattleRetryCheckpoint
{
    public sealed class Candidate
    {
        internal SaveData Snapshot;
        internal List<EnemyData> Enemies;
        internal AudioClip Bgm;
        internal bool Dedicated;
        internal string BattleScene;
        internal float Fade;
        internal string EncounterId;
        internal bool DefeatsOnVictory;
        internal BattleScenarioData Scenario;
        internal bool Preemptive;
        internal bool PlayPresentation;
        internal bool RequiresSource;
    }

    private static Candidate s_checkpoint;
    public static bool HasCheckpoint => s_checkpoint != null;
    public static bool IsRestoring { get; private set; }
    public static string LastFailure { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { IsRestoring = false; Clear(); }

    public static void Clear()
    {
        s_checkpoint = null;
        LastFailure = string.Empty;
    }

    /// <summary>일반 불러오기/새 게임은 폐기하며, 체크포인트 자체 복구 중에는 유지합니다.</summary>
    public static void ClearUnlessRestoring()
    {
        if (!IsRestoring) Clear();
    }

    public static void CompleteEncounter(BattleEncounterOutcome outcome)
    {
        if (outcome != BattleEncounterOutcome.PartyDefeated) Clear();
    }

    public static bool ContainsBoss(IReadOnlyList<EnemyData> enemies)
    {
        if (enemies == null) return false;
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].Rank == EnemyRank.Boss) return true;
        return false;
    }

    public static Candidate Capture(GlobalDataManager global, PlayerController player,
        List<EnemyData> enemies, AudioClip bgm, bool dedicated, string battleScene, float fade,
        string encounterId, bool defeatsOnVictory, IEncounterSource source, BattleScenarioData scenario,
        bool preemptive, bool allowEscape, bool playPresentation)
    {
        if (allowEscape && !ContainsBoss(enemies)) return null;
        if (source is IEncounterDefeatPolicy training && training.ReturnToExplorationOnDefeat) return null;
        SaveData snapshot = global.ToSaveData();
        snapshot.currentScene = SceneManager.GetActiveScene().name;
        snapshot.spawnPointId = string.Empty;
        snapshot.playerX = player.transform.position.x;
        snapshot.playerY = player.transform.position.y;
        snapshot.lookingDirection = player.FacingDirection;
        return new Candidate
        {
            Snapshot = snapshot, Enemies = new List<EnemyData>(enemies), Bgm = bgm,
            Dedicated = dedicated, BattleScene = battleScene, Fade = fade,
            EncounterId = encounterId, DefeatsOnVictory = defeatsOnVictory, Scenario = scenario,
            Preemptive = preemptive, PlayPresentation = playPresentation,
            RequiresSource = source != null
        };
    }

    public static void Commit(Candidate candidate)
    {
        if (IsRestoring) return;
        s_checkpoint = candidate;
        LastFailure = string.Empty;
    }

    public static GameLoadStartResult TryRetry(Action<SceneLoadResult> onCompleted = null)
    {
        Candidate checkpoint = s_checkpoint;
        if (checkpoint == null || IsRestoring)
            return GameLoadStartResult.Rejected(GameLoadStartFailure.Busy, "재도전할 전투가 없거나 이미 복구 중입니다.");
        IsRestoring = true;
        LastFailure = string.Empty;
        SaveManager.CancelPendingAutoSave();
        GameLoadStartResult result;
        try
        {
            result = GameLoadCoordinator.LoadSnapshot(checkpoint.Snapshot, sceneResult =>
            {
                try
                {
                    if (!SceneLoadResultUtility.WasDestinationActivated(sceneResult)) return;
                    PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                    IEncounterSource source = ResolveSource(checkpoint);
                    if (player == null || (checkpoint.RequiresSource && source == null))
                    {
                        LastFailure = "직전 전투의 플레이어 또는 조우 오브젝트를 복원하지 못했습니다.";
                        return;
                    }
                    GameInput.ConsumeBattleUIInput();
                    if (!BattleEncounterService.StartEncounter(player, checkpoint.Enemies, checkpoint.Bgm,
                        checkpoint.Dedicated, checkpoint.BattleScene, checkpoint.Fade, checkpoint.EncounterId,
                        checkpoint.DefeatsOnVictory, source, checkpoint.Scenario, checkpoint.Preemptive,
                        allowEscape: false, playEntryPresentation: checkpoint.PlayPresentation))
                        LastFailure = "전투 준비에 실패했습니다. 최근 저장을 불러오거나 타이틀로 이동해 주세요.";
                }
                catch (Exception exception)
                {
                    LastFailure = "전투 복구 중 오류가 발생했습니다: " + exception.Message;
                    Debug.LogException(exception);
                }
                finally
                {
                    IsRestoring = false;
                    onCompleted?.Invoke(sceneResult);
                }
            });
        }
        catch (Exception exception)
        {
            IsRestoring = false;
            LastFailure = "전투 직전 상태를 복구하지 못했습니다: " + exception.Message;
            Debug.LogException(exception);
            return GameLoadStartResult.Rejected(GameLoadStartFailure.RuntimeMissing, LastFailure);
        }
        if (!result.Accepted) IsRestoring = false;
        return result;
    }

    private static IEncounterSource ResolveSource(Candidate checkpoint)
    {
        if (!checkpoint.RequiresSource) return null;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        IEncounterSource match = null;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!behaviours[i].gameObject.scene.IsValid() || !behaviours[i].gameObject.scene.isLoaded
                || !(behaviours[i] is IEncounterSource source)
                || !(source is IEncounterRetrySource identity)
                || !string.Equals(identity.RetryEncounterId, checkpoint.EncounterId, StringComparison.Ordinal)) continue;
            if (match != null) return null; // 중복 ID를 추측하여 다른 적과 싸우지 않습니다.
            match = source;
        }
        return match;
    }
}
