using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public interface ITimelineCutsceneBindingSource
{
    bool TryResolveBinding(
        TimelineCutsceneBindingKeyKind keyKind,
        string key,
        Type expectedType,
        out UnityEngine.Object value,
        out string error);
}

public sealed class TimelineCutscenePlaybackLifetime : MonoBehaviour
{
    private PlayableDirector _director;
    private string _cutsceneId = string.Empty;
    private ActionExecutionHandle _handle;
    private GameStateManager _stateManager;
    private GameState _previousState = GameState.Exploration;
    private bool _restoreState;
    private bool _restoreCamera;
    private bool _cleanupCompleted;
    private bool _stopRequested;
    private bool _isSubscribed;
    private Action _onCleanupCompleted;

    public bool IsCleanupCompleted => _cleanupCompleted;

    public void Initialize(
        PlayableDirector director,
        string cutsceneId,
        ActionExecutionHandle handle,
        GameStateManager stateManager,
        GameState previousState,
        bool restoreState,
        bool restoreCamera,
        Action onCleanupCompleted)
    {
        _director = director;
        _cutsceneId = string.IsNullOrWhiteSpace(cutsceneId) ? string.Empty : cutsceneId.Trim();
        _handle = handle;
        _stateManager = stateManager;
        _previousState = previousState;
        _restoreState = restoreState;
        _restoreCamera = restoreCamera;
        _onCleanupCompleted = onCleanupCompleted;

        if (_director != null)
        {
            _director.stopped += HandleDirectorStopped;
            _isSubscribed = true;
        }
    }

    private void Update()
    {
        if (_cleanupCompleted || _handle == null || !_handle.IsCancellationRequested)
        {
            return;
        }

        RequestStop("[TimelineCutsceneRunner] timeline.play canceled: " + _cutsceneId);
    }

    public void RequestStop(string logMessage)
    {
        if (_cleanupCompleted)
        {
            return;
        }

        if (!_stopRequested && !string.IsNullOrWhiteSpace(logMessage))
        {
            Debug.LogWarning(logMessage);
        }

        _stopRequested = true;

        if (_director == null)
        {
            CleanupNow();
            return;
        }

        if (_director.state == PlayState.Playing)
        {
            _director.Stop();
            return;
        }

        CleanupNow();
    }

    public void CleanupNow()
    {
        if (_cleanupCompleted)
        {
            return;
        }

        _cleanupCompleted = true;
        Unsubscribe();

        if (_director != null)
        {
            if (_director.state == PlayState.Playing)
            {
                _director.Stop();
            }

            _director.playableAsset = null;
            _director = null;
        }

        if (_restoreCamera)
        {
            CameraController.Instance?.ResetCamera(0.25f);
        }

        if (_restoreState && _stateManager != null)
        {
            _stateManager.ChangeState(_previousState);
        }

        Action callback = _onCleanupCompleted;
        _onCleanupCompleted = null;
        callback?.Invoke();

        TimelineCutsceneRunner.DestroyObjectSafe(gameObject);
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (_cleanupCompleted || stoppedDirector != _director)
        {
            return;
        }

        CleanupNow();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _director == null)
        {
            _isSubscribed = false;
            return;
        }

        _director.stopped -= HandleDirectorStopped;
        _isSubscribed = false;
    }
}

public sealed class TimelineCutsceneRunner : ITimelineCutsceneRunner
{
    private readonly TimelineCutsceneCatalog _catalog;
    private readonly ITimelineCutsceneBindingSource _bindingSource;
    private readonly IBattleCinematicRunner _battleCinematicRunner;
    private readonly IBattleTweenCinematicService _battleTweenCinematicService;

    public TimelineCutsceneRunner(
        TimelineCutsceneCatalog catalog,
        ITimelineCutsceneBindingSource bindingSource = null,
        IBattleCinematicRunner battleCinematicRunner = null,
        IBattleTweenCinematicService battleTweenCinematicService = null)
    {
        _catalog = catalog;
        _bindingSource = bindingSource;
        _battleCinematicRunner = battleCinematicRunner;
        _battleTweenCinematicService = battleTweenCinematicService;
    }

    public IEnumerator PlayCutscene(
        string cutsceneId,
        bool waitForComplete,
        bool lockInput,
        bool restoreCamera,
        bool skipIfMissing,
        ActionExecutionContext context)
    {
        ActionExecutionHandle handle = context != null ? context.Handle : null;
        string normalizedCutsceneId = Normalize(cutsceneId);
        if (string.IsNullOrEmpty(normalizedCutsceneId))
        {
            Fail(handle, "timeline.play requires a non-empty cutsceneId.");
            yield break;
        }

        if (_catalog == null)
        {
            if (TrySkip(skipIfMissing, "timeline.play skipped because TimelineCutsceneCatalog is missing for '" + normalizedCutsceneId + "'."))
            {
                yield break;
            }

            Fail(handle, "TimelineCutsceneCatalog is missing for timeline.play: " + normalizedCutsceneId);
            yield break;
        }

        TimelineCutsceneData cutscene = _catalog.FindById(normalizedCutsceneId);
        if (cutscene == null)
        {
            if (TrySkip(skipIfMissing, "timeline.play skipped because cutscene was not found: '" + normalizedCutsceneId + "'."))
            {
                yield break;
            }

            Fail(handle, "Timeline cutscene was not found: " + normalizedCutsceneId);
            yield break;
        }

        if (cutscene.TimelineAsset == null)
        {
            if (TrySkip(skipIfMissing, "timeline.play skipped because TimelineAsset is missing: '" + normalizedCutsceneId + "'."))
            {
                yield break;
            }

            Fail(handle, "TimelineAsset is missing for cutscene: " + normalizedCutsceneId);
            yield break;
        }

        GameStateManager stateManager = GameStateManager.Instance;
        GameState previousState = stateManager != null ? stateManager.CurrentState : GameState.Exploration;
        bool shouldRestoreState = lockInput && stateManager != null;
        if (lockInput && stateManager != null)
        {
            stateManager.ChangeState(GameState.Cutscene);
        }

        PlayableDirector director = null;
        GameObject directorObject = null;
        TimelineCutscenePlaybackLifetime lifetime = null;
        bool cleanupCompleted = false;
        bool handedOffToLifetime = false;

        try
        {
            directorObject = new GameObject("TimelineCutsceneDirector_" + normalizedCutsceneId);
            directorObject.hideFlags = HideFlags.HideAndDontSave;
            director = directorObject.AddComponent<PlayableDirector>();
            lifetime = directorObject.AddComponent<TimelineCutscenePlaybackLifetime>();
            lifetime.Initialize(
                director,
                normalizedCutsceneId,
                handle,
                stateManager,
                previousState,
                shouldRestoreState,
                restoreCamera,
                () => cleanupCompleted = true);

            ScenarioTimelineSignalReceiver signalReceiver = directorObject.AddComponent<ScenarioTimelineSignalReceiver>();
            signalReceiver.Initialize(_bindingSource, _battleCinematicRunner, _battleTweenCinematicService);
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playableAsset = cutscene.TimelineAsset;

            string bindingError;
            if (!ApplyBindings(cutscene, director, out bindingError))
            {
                if (TrySkip(skipIfMissing, bindingError))
                {
                    yield break;
                }

                Fail(handle, bindingError);
                yield break;
            }

            director.RebuildGraph();
            director.Play();

            if (!waitForComplete)
            {
                handedOffToLifetime = true;
                yield break;
            }

            while (!cleanupCompleted)
            {
                if (handle != null && handle.IsCancellationRequested)
                {
                    lifetime.RequestStop("[TimelineCutsceneRunner] timeline.play canceled: " + normalizedCutsceneId);
                }

                if (!cleanupCompleted && director != null && director.state != PlayState.Playing)
                {
                    lifetime.CleanupNow();
                }

                if (!cleanupCompleted)
                {
                    yield return null;
                }
            }
        }
        finally
        {
            if (!handedOffToLifetime && !cleanupCompleted)
            {
                if (lifetime != null)
                {
                    lifetime.CleanupNow();
                }
                else if (directorObject != null)
                {
                    if (restoreCamera)
                    {
                        CameraController.Instance?.ResetCamera(0.25f);
                    }

                    if (director != null)
                    {
                        director.Stop();
                        director.playableAsset = null;
                    }

                    DestroyObjectSafe(directorObject);

                    if (shouldRestoreState)
                    {
                        stateManager.ChangeState(previousState);
                    }
                }
            }
        }
    }

    internal static void DestroyObjectSafe(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
            return;
        }

        UnityEngine.Object.DestroyImmediate(target);
    }

    private bool ApplyBindings(TimelineCutsceneData cutscene, PlayableDirector director, out string error)
    {
        error = string.Empty;
        if (cutscene == null || director == null)
        {
            error = "Timeline cutscene or PlayableDirector is missing.";
            return false;
        }

        if (!ApplyOutputBindings(cutscene, director, out error))
        {
            return false;
        }

        if (!ApplyReferenceBindings(cutscene, director, out error))
        {
            return false;
        }

        return true;
    }

    private bool ApplyOutputBindings(TimelineCutsceneData cutscene, PlayableDirector director, out string error)
    {
        error = string.Empty;
        if (cutscene.OutputBindings == null || cutscene.OutputBindings.Count == 0)
        {
            return true;
        }

        foreach (TimelineCutsceneBindingEntry entry in cutscene.OutputBindings)
        {
            if (entry == null)
            {
                continue;
            }

            bool matched = false;
            foreach (PlayableBinding output in cutscene.TimelineAsset.outputs)
            {
                if (!string.Equals(Normalize(output.streamName), Normalize(entry.BindingName), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matched = true;
                Type expectedType = ResolveExpectedType(entry, output.outputTargetType);
                UnityEngine.Object value;
                if (!TryResolveBinding(cutscene, entry, expectedType, out value, out error))
                {
                    return false;
                }

                if (value != null)
                {
                    director.SetGenericBinding(output.sourceObject, value);
                }

                break;
            }

            if (!matched)
            {
                error = BuildBindingError(
                    cutscene.CutsceneId,
                    entry,
                    "Timeline output streamName was not found: '" + Normalize(entry.BindingName) + "'.");
                return false;
            }
        }

        return true;
    }

    private bool ApplyReferenceBindings(TimelineCutsceneData cutscene, PlayableDirector director, out string error)
    {
        error = string.Empty;
        if (cutscene.ReferenceBindings == null || cutscene.ReferenceBindings.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < cutscene.ReferenceBindings.Count; i++)
        {
            TimelineCutsceneBindingEntry entry = cutscene.ReferenceBindings[i];
            if (entry == null)
            {
                continue;
            }

            Type expectedType = ResolveExpectedType(entry, typeof(UnityEngine.Object));
            UnityEngine.Object value;
            if (!TryResolveBinding(cutscene, entry, expectedType, out value, out error))
            {
                return false;
            }

            director.SetReferenceValue(new PropertyName(Normalize(entry.BindingName)), value);
        }

        return true;
    }

    private bool TryResolveBinding(
        TimelineCutsceneData cutscene,
        TimelineCutsceneBindingEntry entry,
        Type expectedType,
        out UnityEngine.Object value,
        out string error)
    {
        value = null;
        error = string.Empty;

        UnityEngine.Object resolved;
        if (_bindingSource != null
            && _bindingSource.TryResolveBinding(entry.KeyKind, Normalize(entry.Key), expectedType, out resolved, out error)
            && resolved != null)
        {
            value = ConvertBindingTarget(resolved, expectedType);
        }
        else if (TryResolveSceneObjectFallback(Normalize(entry.Key), expectedType, out resolved))
        {
            value = ConvertBindingTarget(resolved, expectedType);
        }

        if (value != null)
        {
            return true;
        }

        if (!entry.Required)
        {
            error = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            error = BuildBindingError(
                cutscene.CutsceneId,
                entry,
                "Binding target was not found or could not be converted to '" + SafeTypeName(expectedType) + "'.");
        }

        return false;
    }

    private static bool TryResolveSceneObjectFallback(string key, Type expectedType, out UnityEngine.Object value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        GameObject sceneObject = GameObject.Find(key.Trim());
        if (sceneObject == null)
        {
            return false;
        }

        value = ConvertBindingTarget(sceneObject, expectedType);
        return value != null;
    }

    private static UnityEngine.Object ConvertBindingTarget(UnityEngine.Object resolved, Type expectedType)
    {
        if (resolved == null)
        {
            return null;
        }

        if (expectedType == null || expectedType == typeof(UnityEngine.Object))
        {
            return resolved;
        }

        if (expectedType.IsInstanceOfType(resolved))
        {
            return resolved;
        }

        if (resolved is GameObject gameObject)
        {
            return ConvertFromGameObject(gameObject, expectedType);
        }

        if (resolved is Component component)
        {
            if (expectedType == typeof(GameObject))
            {
                return component.gameObject;
            }

            if (expectedType == typeof(Transform))
            {
                return component.transform;
            }

            Component nestedComponent = component.GetComponent(expectedType);
            if (nestedComponent != null)
            {
                return nestedComponent;
            }

            return ConvertFromGameObject(component.gameObject, expectedType);
        }

        return null;
    }

    private static UnityEngine.Object ConvertFromGameObject(GameObject gameObject, Type expectedType)
    {
        if (gameObject == null)
        {
            return null;
        }

        if (expectedType == typeof(GameObject))
        {
            return gameObject;
        }

        if (expectedType == typeof(Transform))
        {
            return gameObject.transform;
        }

        if (typeof(Component).IsAssignableFrom(expectedType))
        {
            return gameObject.GetComponent(expectedType);
        }

        return null;
    }

    private static Type ResolveExpectedType(TimelineCutsceneBindingEntry entry, Type runtimeExpectedType)
    {
        if (entry == null || entry.ValueType == TimelineCutsceneBindingValueType.Auto)
        {
            return runtimeExpectedType;
        }

        switch (entry.ValueType)
        {
            case TimelineCutsceneBindingValueType.GameObject:
                return typeof(GameObject);
            case TimelineCutsceneBindingValueType.Transform:
                return typeof(Transform);
            case TimelineCutsceneBindingValueType.Animator:
                return typeof(Animator);
            case TimelineCutsceneBindingValueType.AudioSource:
                return typeof(AudioSource);
            case TimelineCutsceneBindingValueType.CameraController:
                return typeof(CameraController);
            case TimelineCutsceneBindingValueType.CinemachineCamera:
                return typeof(CinemachineCamera);
            case TimelineCutsceneBindingValueType.PlayableDirector:
                return typeof(PlayableDirector);
            default:
                return runtimeExpectedType;
        }
    }

    private static bool TrySkip(bool skipIfMissing, string message)
    {
        if (!skipIfMissing)
        {
            return false;
        }

        Debug.LogWarning("[TimelineCutsceneRunner] " + message);
        return true;
    }

    private static void Fail(ActionExecutionHandle handle, string message)
    {
        Debug.LogError("[TimelineCutsceneRunner] " + message);
        if (handle != null)
        {
            handle.Fail(message);
        }
    }

    private static string BuildBindingError(string cutsceneId, TimelineCutsceneBindingEntry entry, string reason)
    {
        return "Timeline cutscene binding failed. cutscene='"
            + Normalize(cutsceneId)
            + "', binding='"
            + Normalize(entry != null ? entry.BindingName : string.Empty)
            + "', key='"
            + Normalize(entry != null ? entry.Key : string.Empty)
            + "', kind='"
            + (entry != null ? entry.KeyKind.ToString() : "Unknown")
            + "'. "
            + reason;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string SafeTypeName(Type type)
    {
        return type != null ? type.Name : "Object";
    }
}