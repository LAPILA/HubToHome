using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface ISceneRevealGate
{
    bool IsReadyToReveal { get; }
}

public interface ISceneRevealGateFailureSource : ISceneRevealGate
{
    bool HasFailed { get; }
    string FailureReason { get; }
}

public enum SceneLoadResult
{
    None,
    RejectedBusy,
    InvalidScene,
    LoadFailed,
    CancelledBeforeActivation,
    DestinationPreparationFailed,
    DestinationPreparationTimedOut,
    Succeeded
}

public static class SceneLoadResultUtility
{
    public static bool WasDestinationActivated(SceneLoadResult result)
    {
        return result == SceneLoadResult.Succeeded
            || result == SceneLoadResult.DestinationPreparationFailed
            || result == SceneLoadResult.DestinationPreparationTimedOut;
    }
}

public sealed class SceneLoadOperation
{
    private readonly Action<SceneLoadResult> _onCompleted;

    internal SceneLoadOperation(Action<SceneLoadResult> onCompleted = null)
    {
        _onCompleted = onCompleted;
    }

    public bool IsDone { get; private set; }
    public bool IsCancellationRequested { get; private set; }
    public SceneLoadResult Result { get; private set; } = SceneLoadResult.None;

    public bool Cancel()
    {
        if (IsDone)
            return false;

        IsCancellationRequested = true;
        return true;
    }

    internal void Complete(SceneLoadResult result)
    {
        if (IsDone)
            return;

        Result = result;
        IsDone = true;
        try
        {
            _onCompleted?.Invoke(result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public event Action<string> SceneRevealCompleted;

    [Header("Fade UI")]
    [SerializeField] private CanvasGroup _fadeCanvas;
    [SerializeField] private UnityEngine.UI.Image _fadeImage;
    [SerializeField] private float _sceneRevealGateTimeout = 5f;

    private bool _isLoading;
    private SceneLoadOperation _activeOperation;
    private Tween _fadeTween;
    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();

    public bool IsLoading => _isLoading;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_fadeCanvas != null)
        {
            _fadeCanvas.alpha = 0f;
            _fadeCanvas.blocksRaycasts = false;
        }
    }

    public void LoadScene(string sceneName, float fadeDuration = 0.5f)
    {
        LoadSceneWithResult(sceneName, fadeDuration);
    }

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    public void LoadBattleScene(string sceneName)
    {
        StartLoad(sceneName, 0.1f, ResolveBattleTransitionColor(), true, null);
    }

    protected virtual Color ResolveBattleTransitionColor()
    {
        float scale = VisualAccessibilityPolicy.NormalizeScale(
            _screenFlashScaleProvider?.Scale
            ?? GameConfigManager.DefaultFlashIntensity);
        Color color = VisualAccessibilityPolicy.ScaleFlashColor(
            Color.black,
            Color.white,
            scale);
        color.a = 1f;
        return color;
    }

    public SceneLoadOperation LoadSceneWithResult(
        string sceneName,
        float fadeDuration = 0.5f,
        Action<SceneLoadResult> onCompleted = null)
    {
        return StartLoad(sceneName, fadeDuration, Color.black, false, onCompleted);
    }

    private SceneLoadOperation StartLoad(
        string sceneName,
        float duration,
        Color fadeColor,
        bool isFlash,
        Action<SceneLoadResult> onCompleted)
    {
        var operation = new SceneLoadOperation(onCompleted);
        if (_isLoading)
        {
            operation.Complete(SceneLoadResult.RejectedBusy);
            return operation;
        }

        if (!IsSceneLoadable(sceneName))
        {
            Debug.Log($"[SceneLoader] Build Settings에서 씬을 찾을 수 없습니다. Scene={sceneName}", this);
            operation.Complete(SceneLoadResult.InvalidScene);
            return operation;
        }

        _isLoading = true;
        _activeOperation = operation;
        StartCoroutine(FadeAndLoad(sceneName, Mathf.Max(0f, duration), fadeColor, isFlash, operation));
        return operation;
    }

    public bool CanLoadScene(string sceneName)
    {
        return IsSceneLoadable(sceneName);
    }

    protected virtual bool IsSceneLoadable(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName)
            && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    protected virtual AsyncOperation BeginLoadSceneAsync(string sceneName)
    {
        return SceneManager.LoadSceneAsync(sceneName);
    }

    private IEnumerator FadeAndLoad(
        string sceneName,
        float duration,
        Color fadeColor,
        bool isFlash,
        SceneLoadOperation operation)
    {
        float previousAlpha = _fadeCanvas != null ? _fadeCanvas.alpha : 0f;
        bool previousBlocksRaycasts = _fadeCanvas != null && _fadeCanvas.blocksRaycasts;
        Color previousColor = _fadeImage != null ? _fadeImage.color : Color.black;
        bool sceneActivated = false;

        if (_fadeCanvas != null)
        {
            _fadeCanvas.blocksRaycasts = true;
            if (_fadeImage != null)
                _fadeImage.color = fadeColor;

            if (duration <= 0f)
                _fadeCanvas.alpha = 1f;
            else
                yield return FadeCanvasTo(1f, duration);
        }

        if (operation.IsCancellationRequested)
        {
            RestoreFade(previousAlpha, previousBlocksRaycasts, previousColor);
            Finish(operation, SceneLoadResult.CancelledBeforeActivation);
            yield break;
        }

        AsyncOperation loadOperation = null;
        try
        {
            loadOperation = BeginLoadSceneAsync(sceneName);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (loadOperation == null)
        {
            RestoreFade(previousAlpha, previousBlocksRaycasts, previousColor);
            Finish(operation, SceneLoadResult.LoadFailed);
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
        sceneActivated = true;
        yield return null;

        var gateWait = new SceneRevealGateWait();
        yield return StartCoroutine(WaitForSceneRevealGate(sceneName, gateWait));

        float revealDuration = isFlash ? 0.3f : duration;
        if (_fadeCanvas != null)
        {
            yield return FadeCanvasTo(0f, revealDuration);
            _fadeCanvas.blocksRaycasts = false;
        }

        SceneLoadResult result = ResolveSceneLoadResult(sceneActivated, gateWait.State);
        // Release the transition before destination reveal handlers acquire their own state.
        Finish(operation, result);

        if (result == SceneLoadResult.Succeeded)
        {
            try
            {
                SceneRevealCompleted?.Invoke(sceneName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private IEnumerator FadeCanvasTo(float targetAlpha, float duration)
    {
        if (_fadeCanvas == null)
            yield break;

        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            _fadeCanvas.alpha = targetAlpha;
            yield break;
        }

        _fadeTween?.Kill(false);
        _fadeCanvas.DOKill(false);
        Tween tween = _fadeTween = _fadeCanvas.DOFade(targetAlpha, clampedDuration).SetUpdate(true);
        if (tween != null && tween.active)
        {
            while (tween.active && !tween.IsComplete())
                yield return null;

            if (_fadeCanvas != null && tween.active)
                _fadeCanvas.alpha = targetAlpha;
            if (ReferenceEquals(_fadeTween, tween))
                _fadeTween = null;

            yield break;
        }

        float startAlpha = _fadeCanvas.alpha;
        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadeCanvas.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / clampedDuration));
            yield return null;
        }

        _fadeCanvas.alpha = targetAlpha;
    }

    private void Finish(SceneLoadOperation operation, SceneLoadResult result)
    {
        if (ReferenceEquals(_activeOperation, operation))
        {
            _activeOperation = null;
            _isLoading = false;
        }

        operation?.Complete(result);
    }

    private void RestoreFade(float alpha, bool blocksRaycasts, Color color)
    {
        if (_fadeCanvas == null)
            return;

        _fadeTween?.Kill(false);
        _fadeTween = null;
        _fadeCanvas.DOKill(false);
        _fadeCanvas.alpha = Mathf.Clamp01(alpha);
        _fadeCanvas.blocksRaycasts = blocksRaycasts;
        if (_fadeImage != null)
            _fadeImage.color = color;
    }

    private IEnumerator WaitForSceneRevealGate(
        string sceneName,
        SceneRevealGateWait wait)
    {
        float startedAt = Time.unscaledTime;
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);

        while (!IsSceneActiveAndLoaded(loadedScene))
        {
            if (IsRevealGateTimedOut(startedAt))
            {
                wait.Set(SceneRevealGateState.TimedOut, "목적 Scene 활성화를 기다리는 중 시간이 초과됐습니다.");
                Debug.LogWarning(
                    $"[SceneLoader] Scene activation timed out. Scene={sceneName}",
                    this);
                yield break;
            }

            loadedScene = SceneManager.GetSceneByName(sceneName);
            yield return null;
        }

        List<ISceneRevealGate> gates = FindRevealGates(loadedScene);
        while (true)
        {
            if (TryGetRevealGateFailure(gates, out string failureReason))
            {
                wait.Set(SceneRevealGateState.Failed, failureReason);
                Debug.LogError(
                    $"[SceneLoader] Scene reveal gate failed. Scene={sceneName}, Error={failureReason}",
                    this);
                yield break;
            }

            if (AreAllRevealGatesReady(gates))
            {
                wait.Set(SceneRevealGateState.Ready, string.Empty);
                yield break;
            }

            if (IsRevealGateTimedOut(startedAt))
            {
                wait.Set(SceneRevealGateState.TimedOut, "Scene reveal gate 대기 시간이 초과됐습니다.");
                Debug.LogWarning(
                    $"[SceneLoader] Scene reveal gate timed out. Scene={sceneName}",
                    this);
                yield break;
            }

            yield return null;
        }
    }

    private static bool IsSceneActiveAndLoaded(Scene scene)
    {
        return scene.IsValid()
            && scene.isLoaded
            && SceneManager.GetActiveScene().handle == scene.handle;
    }

    private static bool TryGetRevealGateFailure(
        List<ISceneRevealGate> gates,
        out string failureReason)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            ISceneRevealGate gate = gates[i];
            if (gate is UnityEngine.Object unityObject && unityObject == null)
                continue;

            if (gate is not ISceneRevealGateFailureSource failureSource
                || !failureSource.HasFailed)
            {
                continue;
            }

            failureReason = string.IsNullOrWhiteSpace(failureSource.FailureReason)
                ? "목적 Scene 준비에 실패했습니다."
                : failureSource.FailureReason.Trim();
            return true;
        }

        failureReason = string.Empty;
        return false;
    }

    private static SceneLoadResult ResolveSceneLoadResult(
        bool sceneActivated,
        SceneRevealGateState gateState)
    {
        if (!sceneActivated)
            return SceneLoadResult.LoadFailed;

        return gateState switch
        {
            SceneRevealGateState.Failed => SceneLoadResult.DestinationPreparationFailed,
            SceneRevealGateState.TimedOut => SceneLoadResult.DestinationPreparationTimedOut,
            _ => SceneLoadResult.Succeeded
        };
    }
    private bool IsRevealGateTimedOut(float startedAt)
    {
        return _sceneRevealGateTimeout > 0f
            && Time.unscaledTime - startedAt >= _sceneRevealGateTimeout;
    }

    private static List<ISceneRevealGate> FindRevealGates(Scene scene)
    {
        var gates = new List<ISceneRevealGate>();
        if (!scene.IsValid() || !scene.isLoaded)
            return gates;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                if (behaviours[j] is ISceneRevealGate gate)
                    gates.Add(gate);
            }
        }

        return gates;
    }

    private static bool AreAllRevealGatesReady(List<ISceneRevealGate> gates)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i] is UnityEngine.Object unityObject && unityObject == null)
                continue;

            if (!gates[i].IsReadyToReveal)
                return false;
        }

        return true;
    }

    private enum SceneRevealGateState
    {
        Ready,
        Failed,
        TimedOut
    }

    private sealed class SceneRevealGateWait
    {
        public SceneRevealGateState State { get; private set; } = SceneRevealGateState.Ready;
        public string FailureReason { get; private set; } = string.Empty;

        public void Set(SceneRevealGateState state, string failureReason)
        {
            State = state;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    protected virtual void OnDestroy()
    {
        _fadeTween?.Kill(false);
        _fadeTween = null;

        if (Instance == this)
            Instance = null;

        if (_fadeCanvas != null)
            _fadeCanvas.DOKill(false);

        if (_activeOperation != null && !_activeOperation.IsDone)
            Finish(_activeOperation, SceneLoadResult.LoadFailed);
    }
}
