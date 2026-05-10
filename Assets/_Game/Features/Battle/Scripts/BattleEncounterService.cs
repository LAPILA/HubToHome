using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        string battleSceneName = "BattleScene",
        float battleSceneFadeDuration = 0.08f,
        string encounterId = null,
        bool defeatsOnVictory = false)
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

            if (!string.IsNullOrWhiteSpace(encounterId))
                global.BeginOverworldEnemyEncounter(encounterId, global.LastOverworldScene, defeatsOnVictory);
        }

        player.SetBattleMode(true);
        player.SavePositionToGlobal();

        if (useDedicatedBattleScene)
        {
            SceneLoader.Instance?.LoadScene(string.IsNullOrWhiteSpace(battleSceneName) ? "BattleScene" : battleSceneName, battleSceneFadeDuration);
            return true;
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartSeamlessBattle(encounterEnemies, player);
            return true;
        }

        SceneLoader.Instance?.LoadScene(string.IsNullOrWhiteSpace(battleSceneName) ? "BattleScene" : battleSceneName, battleSceneFadeDuration);
        return true;
    }
}