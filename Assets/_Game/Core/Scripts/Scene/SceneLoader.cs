using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// 비동기 씬 전환을 담당하는 싱글톤.
/// Fade / Flash 두 가지 전환 연출을 지원합니다.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    public static SceneLoader Instance { get; private set; }

    [Header("Fade Canvas")]
    [SerializeField] private CanvasGroup _fadeCanvas;

    // 캐싱
    private WaitForEndOfFrame _waitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_fadeCanvas != null)
            _fadeCanvas.alpha = 0f;
    }

    // ── 일반 Fade 전환 (기본 0.5초) ──────────────────────────
    public void LoadScene(string sceneName, float fadeDuration = 0.5f)
    {
        StartCoroutine(FadeAndLoad(sceneName, fadeDuration));
    }

    private IEnumerator FadeAndLoad(string sceneName, float duration)
    {
        // Fade Out
        yield return _fadeCanvas.DOFade(1f, duration).WaitForCompletion();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return _waitEOF;

        op.allowSceneActivation = true;
        yield return _waitEOF;

        // Fade In
        yield return _fadeCanvas.DOFade(0f, duration).WaitForCompletion();
    }

    // ── 전투 진입 Flash 전환 (빠른 연출) ─────────────────────
    public void LoadBattleScene(string sceneName)
    {
        StartCoroutine(FlashAndLoad(sceneName));
    }

    private IEnumerator FlashAndLoad(string sceneName)
    {
        // 빠른 Flash (0.1초 → 즉시 흰 화면)
        yield return _fadeCanvas.DOFade(1f, 0.1f).WaitForCompletion();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return _waitEOF;

        op.allowSceneActivation = true;
        yield return _waitEOF;

        // Fade In (0.25초)
        yield return _fadeCanvas.DOFade(0f, 0.25f).WaitForCompletion();
    }
}
