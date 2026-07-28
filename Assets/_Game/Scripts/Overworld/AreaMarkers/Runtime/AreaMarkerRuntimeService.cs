using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AreaMarkerRuntimeService
{
    private static IShopSessionLauncher s_shopSessionLauncher;

    public static bool RegisterShopSessionLauncher(IShopSessionLauncher launcher)
    {
        if (launcher == null)
            return false;
        if (s_shopSessionLauncher != null
            && !ReferenceEquals(s_shopSessionLauncher, launcher))
        {
            return false;
        }

        s_shopSessionLauncher = launcher;
        return true;
    }

    public static void UnregisterShopSessionLauncher(IShopSessionLauncher launcher)
    {
        if (ReferenceEquals(s_shopSessionLauncher, launcher))
            s_shopSessionLauncher = null;
    }

    public static bool TryStartDialogue(
        UnityEngine.Object owner,
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
            Debug.Log("[AreaMarkerRuntimeService] 이미 대화가 재생 중이라 새 오버월드 대화를 무시합니다.", owner);
            return false;
        }

        if (dialogue != null)
        {
            return manager.TryStartDialogue(
                dialogue,
                onComplete,
                null,
                null,
                out _);
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

        bool released = false;
        Action release = () =>
        {
            if (released)
                return;
            released = true;
            DestroyTransientDialogue(transientDialogue);
        };

        bool started;
        try
        {
            started = manager.TryStartDialogue(
                transientDialogue,
                () =>
                {
                    release();
                    onComplete?.Invoke();
                },
                release,
                null,
                out _);
        }
        catch
        {
            release();
            throw;
        }

        if (!started)
            release();

        return started;
    }

    private static void DestroyTransientDialogue(DialogueData dialogue)
    {
        if (dialogue == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(dialogue);
        else
            UnityEngine.Object.DestroyImmediate(dialogue);
    }

    public static bool TryRequestConnection(
        AreaMarkerBase owner,
        PlayerController player,
        MapTransitionRequest mapTransition,
        string targetSceneName,
        string targetSpawnId,
        float fadeDuration)
    {
        MapTransitionRequest request = mapTransition;
        if (request == null || !request.IsValid(out _))
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning("[AreaMarkerRuntimeService] targetSceneName과 MapTransition이 모두 비어 있어 이동할 수 없습니다.", owner);
                return false;
            }

            request = new MapTransitionRequest
            {
                TransitionType = MapTransitionType.Scene,
                TargetSceneName = targetSceneName,
                TargetSpawnPointId = targetSpawnId,
                FadeDuration = fadeDuration
            };
        }

        if (MapTransitionService.Instance == null)
        {
            Debug.LogError("[AreaMarkerRuntimeService] MapTransitionService가 씬에 없어 이동을 실행할 수 없습니다.", owner);
            return false;
        }

        bool accepted = MapTransitionService.Instance.TryRequestTransition(request, player);
        if (accepted)
        {
            Debug.Log($"[AreaMarkerRuntimeService] 맵 이동 요청: type={request.TransitionType}, room={request.TargetRoom}, scene={request.TargetSceneName}, spawn={request.TargetSpawnPointId}", owner);
        }

        return accepted;
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
        RequestVendor(owner, vendorId, shopId, null, null);
    }

    public static bool RequestVendor(
        AreaMarkerBase owner,
        string vendorId,
        string shopId,
        ShopDefinition shop,
        Action<ShopSessionResult> onClosed)
    {
        if (shop != null && s_shopSessionLauncher == null && Application.isPlaying)
            ShopUI.EnsureGlobal();

        if (shop == null || s_shopSessionLauncher == null)
        {
            Debug.Log(
                $"[AreaMarkerRuntimeService] 상점 요청: vendorId={vendorId}, shopId={shopId}. "
                + "ShopDefinition 또는 Shop Session Launcher가 없어 연결 요청만 기록합니다.",
                owner);
            return false;
        }

        string resolvedShopId = string.IsNullOrWhiteSpace(shopId)
            ? shop.ShopId
            : shopId.Trim();
        if (!string.Equals(resolvedShopId, shop.ShopId, StringComparison.Ordinal))
        {
            Debug.LogError(
                $"[AreaMarkerRuntimeService] Vendor shopId와 ShopDefinition ID가 다릅니다: "
                + $"vendor={vendorId}, markerShop={resolvedShopId}, definition={shop.ShopId}",
                owner);
            return false;
        }

        bool callbackConsumed = false;
        try
        {
            return s_shopSessionLauncher.TryOpen(shop, vendorId, result =>
            {
                if (callbackConsumed)
                    return;
                callbackConsumed = true;
                onClosed?.Invoke(result);
            });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, owner);
            return false;
        }
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

    public static OverworldPartyDamageResult ApplyHazard(
        AreaMarkerBase owner,
        PlayerController player,
        int damage,
        float knockback,
        IOverworldPartyHealthService healthService = null)
    {
        if (player == null)
        {
            return new OverworldPartyDamageResult(
                OverworldPartyDamageStatus.PartyMissing,
                damage,
                0,
                0,
                0);
        }

        Vector2 origin = owner != null ? owner.transform.position : player.transform.position;
        Vector2 direction = ((Vector2)player.transform.position - origin).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = player.GetFacingVector2();
        player.NudgeFromEncounter(direction, knockback);

        IOverworldPartyHealthService resolvedService = healthService
            ?? new OverworldPartyHealthService(GlobalDataManager.Instance);
        PlayerCharacter scenePlayer = player.GetComponent<PlayerCharacter>();
        OverworldPartyDamageResult result = resolvedService.ApplyDamage(damage, scenePlayer);
        Debug.Log(
            $"[AreaMarkerRuntimeService] Hazard damage={result.AppliedDamage}, hp={result.CurrentHP}, knockback={knockback}",
            owner);
        return result;
    }


    public static bool RequestSublocation(
        AreaMarkerBase owner,
        string targetSceneName,
        string targetRoomId,
        string targetSpawnId,
        float fadeDuration)
    {
        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = targetSceneName,
            TargetRoomId = targetRoomId,
            TargetAreaId = targetRoomId,
            TargetSpawnPointId = targetSpawnId,
            FadeDuration = fadeDuration
        };
        return RequestSublocation(owner, request, null);
    }

    public static bool RequestSublocation(
        AreaMarkerBase owner,
        MapTransitionRequest request,
        Action<SceneLoadResult> onCompleted)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TargetSceneName))
        {
            Debug.LogWarning(
                "[AreaMarkerRuntimeService] targetSceneName이 비어 있어 sublocation 이동을 실행할 수 없습니다.",
                owner);
            return false;
        }

        if (MapTransitionService.Instance == null)
        {
            Debug.LogError(
                "[AreaMarkerRuntimeService] MapTransitionService가 씬에 없어 sublocation 이동을 실행할 수 없습니다.",
                owner);
            return false;
        }

        bool accepted = MapTransitionService.Instance.TryRequestTransition(
            request,
            null,
            onCompleted);
        if (accepted)
        {
            Debug.Log(
                "[AreaMarkerRuntimeService] 내부맵 이동 요청: scene=" + request.TargetSceneName
                + ", room=" + request.ResolvedTargetRoomId
                + ", spawn=" + request.TargetSpawnPointId,
                owner);
        }

        return accepted;
    }}