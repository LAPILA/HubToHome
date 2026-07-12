using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public interface ISceneRevealGate
{
    bool IsReadyToReveal { get; }
}

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    /// <summary>
    /// 씬 전환용 검은 페이드가 완전히 사라진 직후 호출됩니다.
    /// 씬 안의 시작 연출은 이 시점부터 실행해 첫 프레임을 안전하게 준비할 수 있습니다.
    /// </summary>
    public event System.Action<string> SceneRevealCompleted;

    [Header("Fade UI")]
    [SerializeField] private CanvasGroup _fadeCanvas;
    private bool _isLoading;
    [SerializeField] private UnityEngine.UI.Image _fadeImage; // Flash 연출 시 색상 변경용
    [SerializeField] private float _sceneRevealGateTimeout = 5f;

    private void Awake()
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
        StartCoroutine(FadeAndLoad(sceneName, fadeDuration, Color.black));
    }

    public void LoadBattleScene(string sceneName)
    {
        // 스타일: 전투 진입 시 하얗게 번쩍임!
        StartCoroutine(FadeAndLoad(sceneName, 0.1f, Color.white, isFlash: true));
    }

    private IEnumerator FadeAndLoad(string sceneName, float duration, Color fadeColor, bool isFlash = false)
    {
        if (_isLoading) yield break;
        _isLoading = true;

        if (_fadeCanvas == null)
        {
            SceneManager.LoadScene(sceneName);
            _isLoading = false;
            yield break;
        }

        _fadeCanvas.blocksRaycasts = true;
        
        if (_fadeImage != null) _fadeImage.color = fadeColor;
        _fadeCanvas.DOKill(); // 진행 중인 페이드 취소

        // SetUpdate(true)를 통해 게임이 일시정지 상태라도 화면이 넘어가게 보장
        yield return _fadeCanvas.DOFade(1f, duration).SetUpdate(true).WaitForCompletion();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;

        op.allowSceneActivation = true;
        yield return null;
        yield return StartCoroutine(WaitForSceneRevealGate(sceneName));

        // Flash 연출이면 페이드 인을 살짝 더 길게 가져감
        float inDuration = isFlash ? 0.3f : duration;
        yield return _fadeCanvas.DOFade(0f, inDuration).SetUpdate(true).WaitForCompletion();

        _fadeCanvas.blocksRaycasts = false;
        SceneRevealCompleted?.Invoke(sceneName);
        _isLoading = false;
    }

    private IEnumerator WaitForSceneRevealGate(string sceneName)
    {
        float startedAt = Time.unscaledTime;
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);

        while ((!loadedScene.IsValid() || !loadedScene.isLoaded || SceneManager.GetActiveScene().handle != loadedScene.handle)
            && !IsRevealGateTimedOut(startedAt))
        {
            loadedScene = SceneManager.GetSceneByName(sceneName);
            yield return null;
        }

        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            yield break;

        List<ISceneRevealGate> gates = FindRevealGates(loadedScene);
        if (gates.Count == 0)
            yield break;

        while (!AreAllRevealGatesReady(gates))
        {
            if (IsRevealGateTimedOut(startedAt))
            {
                Debug.LogWarning($"[SceneLoader] Scene reveal gate timed out. Scene={sceneName}");
                yield break;
            }

            yield return null;
        }
    }

    private bool IsRevealGateTimedOut(float startedAt)
    {
        return _sceneRevealGateTimeout > 0f && Time.unscaledTime - startedAt >= _sceneRevealGateTimeout;
    }

    private static List<ISceneRevealGate> FindRevealGates(Scene scene)
    {
        List<ISceneRevealGate> gates = new List<ISceneRevealGate>();
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
            if (gates[i] is Object unityObject && unityObject == null)
                continue;

            if (!gates[i].IsReadyToReveal)
                return false;
        }

        return true;
    }
}
