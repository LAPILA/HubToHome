using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds runtime UI text after display changes so TMP SDF scale data matches the current GameView resolution.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class UIResolutionRefreshService : MonoBehaviour
{
    private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
    private static UIResolutionRefreshService _instance;

    private Coroutine _refreshRoutine;
    private bool _refreshRequested;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private FullScreenMode _lastFullScreenMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void RequestRefresh()
    {
        EnsureInstance()?.ScheduleRefresh();
    }

    private static UIResolutionRefreshService EnsureInstance()
    {
        if (_instance != null) return _instance;

        UIResolutionRefreshService existing = FindFirstObjectByType<UIResolutionRefreshService>();
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var go = new GameObject("[UIResolutionRefreshService]");
        _instance = go.AddComponent<UIResolutionRefreshService>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        RememberCurrentDisplayState();
    }

    private void OnEnable()
    {
        GameConfigManager.DisplaySettingsChanged += RequestRefresh;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GameConfigManager.DisplaySettingsChanged -= RequestRefresh;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_refreshRoutine != null)
        {
            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }

        _refreshRequested = false;
    }

    private void Update()
    {
        if (!HasDisplayStateChanged()) return;

        RememberCurrentDisplayState();
        ScheduleRefresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleRefresh();
    }

    private bool HasDisplayStateChanged()
    {
        return _lastScreenWidth != Screen.width
            || _lastScreenHeight != Screen.height
            || _lastFullScreenMode != Screen.fullScreenMode;
    }

    private void RememberCurrentDisplayState()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastFullScreenMode = Screen.fullScreenMode;
    }

    private void ScheduleRefresh()
    {
        if (!isActiveAndEnabled) return;

        _refreshRequested = true;
        if (_refreshRoutine != null) return;

        _refreshRoutine = StartCoroutine(CoRefreshAfterDisplaySettles());
    }

    private IEnumerator CoRefreshAfterDisplaySettles()
    {
        while (_refreshRequested)
        {
            _refreshRequested = false;
            yield return null;
            yield return EndOfFrame;

            Canvas.ForceUpdateCanvases();

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null) continue;

                text.UpdateMeshPadding();
                text.SetVerticesDirty();
                text.SetMaterialDirty();
                text.ForceMeshUpdate(true, true);
            }

            Canvas.ForceUpdateCanvases();
        }

        _refreshRoutine = null;
    }
}
