using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Fade UI")]
    [SerializeField] private CanvasGroup _fadeCanvas;
    private bool _isLoading;
    [SerializeField] private UnityEngine.UI.Image _fadeImage; // Flash 연출 시 색상 변경용

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

        // Flash 연출이면 페이드 인을 살짝 더 길게 가져감
        float inDuration = isFlash ? 0.3f : duration;
        yield return _fadeCanvas.DOFade(0f, inDuration).SetUpdate(true).WaitForCompletion();

        _fadeCanvas.blocksRaycasts = false;
        _isLoading = false;
    }
}
