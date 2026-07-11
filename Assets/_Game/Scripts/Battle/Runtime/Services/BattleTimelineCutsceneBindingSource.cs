using System;
using Unity.Cinemachine;
using UnityEngine;

public sealed class BattleTimelineCutsceneBindingSource : ITimelineCutsceneBindingSource
{
    private readonly BattleManager _battleManager;

    public BattleTimelineCutsceneBindingSource(BattleManager battleManager)
    {
        _battleManager = battleManager;
    }

    public bool TryResolveBinding(
        TimelineCutsceneBindingKeyKind keyKind,
        string key,
        Type expectedType,
        out UnityEngine.Object value,
        out string error)
    {
        value = null;
        error = string.Empty;

        string normalizedKey = Normalize(key);
        switch (keyKind)
        {
            case TimelineCutsceneBindingKeyKind.ActorKey:
                value = ResolveActor(normalizedKey);
                break;
            case TimelineCutsceneBindingKeyKind.CameraKey:
                value = ResolveCamera(normalizedKey, expectedType);
                break;
            case TimelineCutsceneBindingKeyKind.AudioKey:
                value = ResolveAudio(normalizedKey);
                break;
            case TimelineCutsceneBindingKeyKind.SceneObjectName:
                value = GameObject.Find(normalizedKey);
                break;
        }

        if (value != null)
        {
            return true;
        }

        error = "Battle timeline binding source could not resolve key '" + normalizedKey + "' for " + keyKind + ".";
        return false;
    }

    private UnityEngine.Object ResolveActor(string actorKey)
    {
        if (_battleManager == null || string.IsNullOrEmpty(actorKey))
        {
            return null;
        }

        if (_battleManager._playerParty != null)
        {
            for (int i = 0; i < _battleManager._playerParty.Count; i++)
            {
                PlayerCharacter player = _battleManager._playerParty[i];
                if (Matches(actorKey, player != null ? player.CharacterID : null)
                    || Matches(actorKey, player != null ? player.DisplayName : null)
                    || Matches(actorKey, player != null ? player.gameObject.name : null))
                {
                    return player;
                }
            }
        }

        if (_battleManager._enemies != null)
        {
            for (int i = 0; i < _battleManager._enemies.Count; i++)
            {
                EnemyCharacter enemy = _battleManager._enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                string subjectId = BattleScenarioSubjectResolver.ResolveSubjectId(enemy);
                string enemyName = enemy.Data != null ? enemy.Data.EnemyName : string.Empty;
                if (Matches(actorKey, subjectId)
                    || Matches(actorKey, enemyName)
                    || Matches(actorKey, enemy.gameObject.name))
                {
                    return enemy;
                }
            }
        }

        return null;
    }

    private static UnityEngine.Object ResolveCamera(string cameraKey, Type expectedType)
    {
        CameraController controller = CameraController.Instance;
        if (controller == null)
        {
            return null;
        }

        string normalized = Normalize(cameraKey);
        if (string.IsNullOrEmpty(normalized)
            || normalized == "battle"
            || normalized == "main"
            || normalized == "vcam"
            || normalized == "virtual_camera")
        {
            if (expectedType == typeof(CameraController))
            {
                return controller;
            }

            if (expectedType == typeof(CinemachineCamera) || expectedType == null || expectedType == typeof(UnityEngine.Object))
            {
                return controller.VirtualCamera != null ? (UnityEngine.Object)controller.VirtualCamera : controller;
            }

            return controller.VirtualCamera != null ? (UnityEngine.Object)controller.VirtualCamera : controller;
        }

        if (normalized == "center")
        {
            return controller.CenterTarget;
        }

        if (normalized == "controller")
        {
            return controller;
        }

        return null;
    }

    private static UnityEngine.Object ResolveAudio(string audioKey)
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return null;
        }

        switch (Normalize(audioKey))
        {
            case "":
            case "audio_manager":
                return audioManager;
            case "bgm":
            case "bgm_primary":
            case "bgm_a":
                return audioManager.PrimaryBgmSource != null ? (UnityEngine.Object)audioManager.PrimaryBgmSource : audioManager;
            case "bgm_secondary":
            case "bgm_b":
                return audioManager.SecondaryBgmSource != null ? (UnityEngine.Object)audioManager.SecondaryBgmSource : audioManager;
            case "sfx":
                return audioManager.SfxSource != null ? (UnityEngine.Object)audioManager.SfxSource : audioManager;
            case "voice":
                return audioManager.VoiceSource != null ? (UnityEngine.Object)audioManager.VoiceSource : audioManager;
            default:
                return null;
        }
    }

    private static bool Matches(string left, string right)
    {
        return !string.IsNullOrEmpty(left)
            && string.Equals(left, Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}