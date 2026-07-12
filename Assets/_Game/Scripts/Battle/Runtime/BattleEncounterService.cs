using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IEncounterSource
{
    void OnEncounterResolved(bool victory, PlayerController player);
}

public static class EncounterCollisionGuard
{
    private const float NudgePadding = 0.18f;
    private static float s_globalBlockedUntil;

    public static bool IsGloballyBlocked => Time.unscaledTime < s_globalBlockedUntil;

    public static void BlockAll(float seconds)
    {
        s_globalBlockedUntil = Mathf.Max(s_globalBlockedUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    public static bool IsPlayerOverlapping(Collider2D sourceCollider, PlayerController player)
    {
        if (sourceCollider == null || player == null) return false;

        Collider2D[] playerColliders = player.GetComponents<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled) continue;

            ColliderDistance2D distance = Physics2D.Distance(sourceCollider, playerCollider);
            if (distance.isOverlapped)
                return true;
        }

        return false;
    }

    public static void NudgePlayerOutOf(Collider2D sourceCollider, PlayerController player, float minDistance)
    {
        if (sourceCollider == null || player == null) return;

        Vector2 sourceCenter = sourceCollider.bounds.center;
        Vector2 playerCenter = player.transform.position;
        Vector2 direction = playerCenter - sourceCenter;
        if (direction.sqrMagnitude < 0.0001f)
            direction = player.GetFacingVector2();
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        float distance = Mathf.Max(0.25f, minDistance);
        Collider2D[] playerColliders = player.GetComponents<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled) continue;

            ColliderDistance2D colliderDistance = Physics2D.Distance(sourceCollider, playerCollider);
            if (colliderDistance.isOverlapped)
                distance = Mathf.Max(distance, Mathf.Abs(colliderDistance.distance) + NudgePadding + minDistance);
        }

        player.NudgeFromEncounter(direction.normalized, distance);
        Physics2D.SyncTransforms();
    }
}

/// <summary>
/// 오버월드/이벤트/대화 등 다양한 진입점에서 전투 시작 준비를 공통 처리합니다.
/// 실제 전투 유닛 생성은 기존 BattleManager + Enemy_Base prefab 파이프라인을 그대로 사용합니다.
/// </summary>
public static class BattleEncounterService
{
    public static AudioClip ResolveBattleBgm(List<EnemyData> enemies, AudioClip overrideBattleBgm = null)
    {
        if (overrideBattleBgm != null) return overrideBattleBgm;

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].BattleBGM != null)
                    return enemies[i].BattleBGM;
            }
        }

        return MapSettings.CurrentDefaultBattleBGM;
    }

    public static bool StartEncounter(
        PlayerController player,
        List<EnemyData> encounterEnemies,
        AudioClip overrideBattleBgm = null,
        bool useDedicatedBattleScene = false,
        string battleSceneName = SceneName.Battle,
        float battleSceneFadeDuration = 0.08f,
        string encounterId = null,
        bool defeatsOnVictory = false,
        IEncounterSource encounterSource = null,
        BattleScenarioData battleScenarioData = null,
        bool playerPreemptiveAttack = false)
    {
        if (player == null)
        {
            Debug.LogWarning("[BattleEncounterService] PlayerController가 없어 전투를 시작할 수 없습니다.");
            return false;
        }

        if (encounterEnemies == null || encounterEnemies.Count == 0)
        {
            Debug.LogWarning("[BattleEncounterService] EncounterEnemies가 비어있어 전투를 시작할 수 없습니다.");
            return false;
        }

        var global = GlobalDataManager.Instance;
        if (global != null)
        {
            global.LastOverworldScene = SceneManager.GetActiveScene().name;
            global.PendingEnemies = new List<EnemyData>(encounterEnemies);
            global.PendingBattleBGM = ResolveBattleBgm(encounterEnemies, overrideBattleBgm);
            global.PendingBattleScenario = battleScenarioData;

            if (!string.IsNullOrWhiteSpace(encounterId))
                global.BeginOverworldEnemyEncounter(encounterId, global.LastOverworldScene, defeatsOnVictory, playerPreemptiveAttack);
        }

        player.SetBattleMode(true);
        player.SavePositionToGlobal();

        if (useDedicatedBattleScene)
        {
            SceneLoader.Instance?.LoadScene(string.IsNullOrWhiteSpace(battleSceneName) ? SceneName.Battle : battleSceneName, battleSceneFadeDuration);
            return true;
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.SetBattleScenarioData(battleScenarioData);
            BattleManager.Instance.StartSeamlessBattle(encounterEnemies, player, encounterSource);
            return true;
        }

        SceneLoader.Instance?.LoadScene(string.IsNullOrWhiteSpace(battleSceneName) ? SceneName.Battle : battleSceneName, battleSceneFadeDuration);
        return true;
    }
}
