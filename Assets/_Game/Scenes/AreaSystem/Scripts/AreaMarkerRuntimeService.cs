using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AreaMarkerRuntimeService
{
    public static bool TryStartDialogue(
        AreaMarkerBase owner,
        DialogueData dialogue,
        string fallbackText,
        SpeakerData fallbackSpeaker,
        EmotionType fallbackEmotion,
        Action onComplete = null)
    {
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[AreaMarkerRuntimeService] DialogueManager가 없어 대화를 시작할 수 없습니다.", owner);
            return false;
        }

        if (manager.IsPlaying)
        {
            Debug.Log("[AreaMarkerRuntimeService] 이미 대화가 재생 중이라 새 Area Marker 대화를 무시합니다.", owner);
            return false;
        }

        if (dialogue != null)
        {
            manager.StartDialogue(dialogue, onComplete);
            return true;
        }

        if (string.IsNullOrWhiteSpace(fallbackText))
        {
            Debug.LogWarning("[AreaMarkerRuntimeService] DialogueData와 fallback text가 모두 비어 있습니다.", owner);
            return false;
        }

        DialogueData transientDialogue = ScriptableObject.CreateInstance<DialogueData>();
        transientDialogue.name = "Runtime_AreaMarkerDialogue";
        transientDialogue.Style = DialogueStyle.Overworld;
        transientDialogue.Nodes.Add(new DialogueNode
        {
            Speaker = fallbackSpeaker,
            Emotion = fallbackEmotion,
            DefaultText = fallbackText
        });

        manager.StartDialogue(transientDialogue, () =>
        {
            if (transientDialogue != null)
                UnityEngine.Object.Destroy(transientDialogue);
            onComplete?.Invoke();
        });

        return true;
    }

    public static bool TryRequestConnection(
        AreaMarkerBase owner,
        PlayerController player,
        MapTransitionRequest mapTransition,
        string targetSceneName,
        string targetSpawnId,
        float fadeDuration)
    {
        if (mapTransition != null && mapTransition.IsValid(out string _))
        {
            if (MapTransitionService.Instance == null)
            {
                Debug.LogError("[AreaMarkerRuntimeService] MapTransitionService가 씬에 없어 Room 이동을 실행할 수 없습니다.", owner);
                return false;
            }

            MapTransitionService.Instance.RequestTransition(mapTransition, player);
            Debug.Log($"[AreaMarkerRuntimeService] 맵 이동 요청: type={mapTransition.TransitionType}, room={mapTransition.TargetRoom}, scene={mapTransition.TargetSceneName}, spawn={mapTransition.TargetSpawnPointId}", owner);
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[AreaMarkerRuntimeService] targetSceneName과 MapTransition이 모두 비어 있어 이동할 수 없습니다.", owner);
            return false;
        }

        player?.SavePositionToGlobal();
        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.SpawnScene = targetSceneName;
            GlobalDataManager.Instance.SpawnPointId = targetSpawnId;
            if (player != null)
            {
                GlobalDataManager.Instance.SpawnX = player.transform.position.x;
                GlobalDataManager.Instance.SpawnY = player.transform.position.y;
            }
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(targetSceneName, fadeDuration);
        else
            SceneManager.LoadScene(targetSceneName);

        Debug.Log($"[AreaMarkerRuntimeService] 씬 이동 요청: scene={targetSceneName}, spawn={targetSpawnId}", owner);
        return true;
    }

    public static bool TryStartEncounter(
        AreaMarkerBase owner,
        PlayerController player,
        List<EnemyData> enemies,
        AudioClip battleBgmOverride,
        bool useDedicatedBattleScene,
        string battleSceneName,
        float battleFadeDuration,
        string battleEncounterId,
        bool defeatsOnVictory,
        IEncounterSource encounterSource,
        BattleScenarioData battleScenarioData,
        bool playerAdvantage)
    {
        if (player == null || enemies == null || enemies.Count == 0)
        {
            return false;
        }

        return BattleEncounterService.StartEncounter(
            player,
            enemies,
            battleBgmOverride,
            useDedicatedBattleScene,
            battleSceneName,
            battleFadeDuration,
            battleEncounterId,
            defeatsOnVictory,
            encounterSource,
            battleScenarioData,
            playerAdvantage);
    }

    public static void GrantItem(AreaMarkerBase owner, string itemId, int amount)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && GlobalDataManager.Instance != null)
            GlobalDataManager.Instance.AddItem(itemId, amount);

        Debug.Log($"[AreaMarkerRuntimeService] 아이템 획득: itemId={itemId}, amount={amount}", owner);
    }

    public static void RequestVendor(AreaMarkerBase owner, string vendorId, string shopId)
    {
        Debug.Log($"[AreaMarkerRuntimeService] 상점 요청: vendorId={vendorId}, shopId={shopId}. 현재는 Shop UI를 자동으로 열지 않는 연결 지점입니다.", owner);
    }

    public static bool RequestSavePoint(AreaMarkerBase owner, PlayerController player, string savePointId, int slotIndex = 0)
    {
        if (GlobalDataManager.Instance == null)
        {
            Debug.LogWarning("[AreaMarkerRuntimeService] GlobalDataManager가 없어 저장을 수행할 수 없습니다.", owner);
            return false;
        }

        player?.SavePositionToGlobal();
        SaveManager.Save(GlobalDataManager.Instance.ToSaveData(), Mathf.Max(0, slotIndex));
        Debug.Log($"[AreaMarkerRuntimeService] 저장 지점 요청: savePointId={savePointId}, slot={slotIndex}", owner);
        return true;
    }

    public static void ApplyHazard(AreaMarkerBase owner, PlayerController player, int damage, float knockback)
    {
        if (player == null)
            return;

        Vector2 dir = ((Vector2)player.transform.position - (Vector2)owner.transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f)
            dir = player.GetFacingVector2();

        player.NudgeFromEncounter(dir, knockback);
        Debug.Log($"[AreaMarkerRuntimeService] Hazard 요청: damage={damage}, knockback={knockback}. 현재는 넉백만 적용되고 HP는 감소하지 않습니다.", owner);
    }

    public static void CompletePuzzle(AreaMarkerBase owner, string puzzleId, string solvedFlag)
    {
        Debug.Log($"[AreaMarkerRuntimeService] 퍼즐 요청: puzzleId={puzzleId}, solvedFlag={solvedFlag}. 현재는 퍼즐 플레이 없이 solvedFlag를 즉시 설정합니다.", owner);
        if (!string.IsNullOrWhiteSpace(solvedFlag))
            GlobalDataManager.Instance?.SetFlag(solvedFlag, 1);
    }

    public static bool RequestSublocation(
        AreaMarkerBase owner,
        string targetSceneName,
        string targetAreaId,
        string targetSpawnId,
        float fadeDuration)
    {
        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.CurrentRoomId = targetAreaId;
            GlobalDataManager.Instance.SpawnPointId = targetSpawnId;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[AreaMarkerRuntimeService] targetSceneName이 비어 있어 sublocation 이동을 실행할 수 없습니다.", owner);
            return false;
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(targetSceneName, fadeDuration);
        else
            SceneManager.LoadScene(targetSceneName);

        Debug.Log($"[AreaMarkerRuntimeService] 내부맵 이동 요청: scene={targetSceneName}, area={targetAreaId}, spawn={targetSpawnId}", owner);
        return true;
    }
}