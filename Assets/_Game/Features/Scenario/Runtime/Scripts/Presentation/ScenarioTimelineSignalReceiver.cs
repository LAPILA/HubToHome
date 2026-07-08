using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum ScenarioTimelineSignalType
{
    SfxPlay,
    CameraShake,
    VfxSpawn,
    ActorPose,
    UiFlash
}

[CreateAssetMenu(
    fileName = "ScenarioTimelineSignal",
    menuName = "HubToHome/Scenario/Timeline Signal",
    order = 1201)]
public sealed class ScenarioTimelineSignalAsset : SignalAsset
{
    public ScenarioTimelineSignalType SignalType = ScenarioTimelineSignalType.SfxPlay;
    public TimelineCutsceneBindingKeyKind TargetKeyKind = TimelineCutsceneBindingKeyKind.ActorKey;
    public string TargetKey = string.Empty;
    public string Pose = "idle";
    public AudioClip AudioClip;
    public GameObject VfxPrefab;
    public float Volume = 1f;
    public float Duration = 0.15f;
    public float Intensity = 0.75f;
    public float FlashAlpha = 0.6f;
    public float Lifetime = 2f;
    public Vector3 Direction = Vector3.right;
    public Color FlashColor = Color.white;
    public bool LockHorizontal = true;
}

public sealed class ScenarioTimelineSignalReceiver : MonoBehaviour, INotificationReceiver
{
    private ITimelineCutsceneBindingSource _bindingSource;
    private IBattleCinematicRunner _battleCinematicRunner;
    private IBattleTweenCinematicService _battleTweenCinematicService;

    public void Initialize(
        ITimelineCutsceneBindingSource bindingSource,
        IBattleCinematicRunner battleCinematicRunner,
        IBattleTweenCinematicService battleTweenCinematicService)
    {
        _bindingSource = bindingSource;
        _battleCinematicRunner = battleCinematicRunner;
        _battleTweenCinematicService = battleTweenCinematicService;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        ScenarioTimelineSignalAsset signal = ResolveSignalAsset(notification);
        if (signal == null)
        {
            return;
        }

        switch (signal.SignalType)
        {
            case ScenarioTimelineSignalType.SfxPlay:
                AudioManager.Instance?.PlaySFX(signal.AudioClip, Mathf.Max(0f, signal.Volume));
                break;

            case ScenarioTimelineSignalType.CameraShake:
                if (_battleTweenCinematicService != null)
                {
                    StartCoroutine(_battleTweenCinematicService.PlayCameraShake(
                        signal.Direction,
                        signal.Intensity,
                        signal.Duration,
                        signal.LockHorizontal,
                        gameObject,
                        null));
                }
                else if (signal.Intensity > 0f)
                {
                    CameraController.Instance?.PlayHeavySlam(signal.Direction, signal.Intensity, signal.LockHorizontal);
                }
                break;

            case ScenarioTimelineSignalType.VfxSpawn:
                SpawnVfx(signal);
                break;

            case ScenarioTimelineSignalType.ActorPose:
                if (_battleCinematicRunner != null && !string.IsNullOrWhiteSpace(signal.TargetKey))
                {
                    StartCoroutine(_battleCinematicRunner.PlayActorPose(
                        signal.TargetKey,
                        string.IsNullOrWhiteSpace(signal.Pose) ? "idle" : signal.Pose,
                        Mathf.Max(0f, signal.Duration),
                        Mathf.Max(0f, signal.Intensity),
                        null));
                }
                break;

            case ScenarioTimelineSignalType.UiFlash:
                if (_battleTweenCinematicService != null)
                {
                    StartCoroutine(_battleTweenCinematicService.PlayUiFlash(
                        signal.FlashColor,
                        signal.FlashAlpha,
                        signal.Duration,
                        gameObject,
                        null));
                }
                else
                {
                    BattleUIController.Instance?.PlayScenarioUiFlash(
                        signal.FlashColor,
                        signal.FlashAlpha,
                        signal.Duration,
                        gameObject);
                }
                break;
        }
    }

    private void SpawnVfx(ScenarioTimelineSignalAsset signal)
    {
        if (signal == null || signal.VfxPrefab == null)
        {
            return;
        }

        Transform spawnTarget = ResolveTargetTransform(signal.TargetKeyKind, signal.TargetKey);
        Vector3 spawnPosition = spawnTarget != null ? spawnTarget.position : transform.position;
        Quaternion spawnRotation = spawnTarget != null ? spawnTarget.rotation : Quaternion.identity;

        GameObject spawnedVfx;
        if (ObjectPoolManager.Instance != null)
        {
            spawnedVfx = ObjectPoolManager.Instance.Spawn(signal.VfxPrefab, spawnPosition, spawnRotation);
        }
        else
        {
            spawnedVfx = Instantiate(signal.VfxPrefab, spawnPosition, spawnRotation);
            if (signal.Lifetime > 0f)
            {
                Destroy(spawnedVfx, signal.Lifetime);
            }
        }

        if (spawnedVfx != null)
        {
            CharacterVFX.ApplyRuntimeAudioNormalization(spawnedVfx);
        }
    }

    private Transform ResolveTargetTransform(TimelineCutsceneBindingKeyKind keyKind, string key)
    {
        UnityEngine.Object resolvedObject;
        string error;
        if (_bindingSource != null
            && _bindingSource.TryResolveBinding(keyKind, Normalize(key), typeof(Transform), out resolvedObject, out error)
            && resolvedObject != null)
        {
            if (resolvedObject is Transform resolvedTransform)
            {
                return resolvedTransform;
            }

            if (resolvedObject is Component resolvedComponent)
            {
                return resolvedComponent.transform;
            }

            if (resolvedObject is GameObject resolvedGameObject)
            {
                return resolvedGameObject.transform;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        GameObject sceneObject = GameObject.Find(key.Trim());
        return sceneObject != null ? sceneObject.transform : null;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static ScenarioTimelineSignalAsset ResolveSignalAsset(INotification notification)
    {
        if (notification == null)
        {
            return null;
        }

        if (notification is ScenarioTimelineSignalEmitter customEmitter)
        {
            return customEmitter.Signal;
        }

        if (notification is SignalEmitter signalEmitter)
        {
            return signalEmitter.asset as ScenarioTimelineSignalAsset;
        }

        return null;
    }
}

public sealed class ScenarioTimelineSignalEmitter : Marker, INotification
{
    [SerializeField] private ScenarioTimelineSignalAsset _signal;

    public ScenarioTimelineSignalAsset Signal
    {
        get { return _signal; }
        set { _signal = value; }
    }

    public PropertyName id
    {
        get { return new PropertyName(_signal != null ? _signal.name : nameof(ScenarioTimelineSignalEmitter)); }
    }
}