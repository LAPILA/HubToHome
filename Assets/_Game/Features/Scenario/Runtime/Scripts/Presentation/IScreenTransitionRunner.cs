using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public interface IScreenTransitionRunner
{
    IEnumerator Fade(string mode, string color, float duration, ActionExecutionHandle handle);
}

public sealed class ScreenTransitionRunner : IScreenTransitionRunner
{
    private readonly ScreenTransitionOverlay _overlay;

    public ScreenTransitionRunner(ScreenTransitionOverlay overlay = null)
    {
        _overlay = overlay;
    }

    public IEnumerator Fade(string mode, string color, float duration, ActionExecutionHandle handle)
    {
        float targetAlpha;
        if (!TryResolveTargetAlpha(mode, out targetAlpha))
        {
            handle.Fail("Unsupported screen.fade mode: " + mode);
            yield break;
        }

        Color fadeColor;
        if (!TryResolveColor(color, out fadeColor))
        {
            handle.Fail("Unsupported screen.fade color: " + color);
            yield break;
        }

        ScreenTransitionOverlay overlay = _overlay != null ? _overlay : ScreenTransitionOverlay.GetOrCreate();
        yield return overlay.FadeTo(fadeColor, targetAlpha, duration, handle);
    }

    private static bool TryResolveTargetAlpha(string mode, out float targetAlpha)
    {
        targetAlpha = 0f;
        if (string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        switch (mode.Trim().ToLowerInvariant())
        {
            case "out":
            case "to":
            case "cover":
                targetAlpha = 1f;
                return true;
            case "in":
            case "from":
            case "reveal":
                targetAlpha = 0f;
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveColor(string color, out Color fadeColor)
    {
        fadeColor = Color.black;
        if (string.IsNullOrWhiteSpace(color))
        {
            return true;
        }

        switch (color.Trim().ToLowerInvariant())
        {
            case "black":
                fadeColor = Color.black;
                return true;
            case "white":
                fadeColor = Color.white;
                return true;
            case "clear":
                fadeColor = Color.clear;
                return true;
        }

        string html = color.Trim();
        if (!html.StartsWith("#"))
        {
            html = "#" + html;
        }

        return ColorUtility.TryParseHtmlString(html, out fadeColor);
    }
}

public sealed class ScreenTransitionOverlay : MonoBehaviour
{
    private const string OverlayName = "ScenarioScreenTransitionOverlay";
    private static ScreenTransitionOverlay _instance;

    private CanvasGroup _canvasGroup;
    private Image _image;

    public static ScreenTransitionOverlay GetOrCreate()
    {
        if (_instance != null)
        {
            return _instance;
        }

        var root = new GameObject(OverlayName);
        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        _instance = root.AddComponent<ScreenTransitionOverlay>();
        _instance.EnsureInitialized();
        _instance.ApplyAlpha(0f);
        return _instance;
    }

    public IEnumerator FadeTo(Color color, float targetAlpha, float duration, ActionExecutionHandle handle)
    {
        EnsureInitialized();

        color.a = 1f;
        _image.color = color;
        _canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        gameObject.SetActive(true);

        float clampedDuration = Mathf.Max(0f, duration);
        float startAlpha = _canvasGroup.alpha;
        if (clampedDuration <= 0f)
        {
            ApplyAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            if (handle.IsCancellationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            ApplyAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        ApplyAlpha(targetAlpha);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (_image == null)
        {
            _image = GetComponentInChildren<Image>(true);
            if (_image == null)
            {
                var imageObject = new GameObject("FadeImage");
                imageObject.transform.SetParent(transform, false);
                _image = imageObject.AddComponent<Image>();

                RectTransform rect = imageObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        _image.raycastTarget = true;
        ApplyAlpha(_canvasGroup.alpha);
    }

    private void ApplyAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        _canvasGroup.alpha = clampedAlpha;
        _canvasGroup.blocksRaycasts = clampedAlpha > 0.001f;
        if (clampedAlpha <= 0.001f)
        {
            gameObject.SetActive(false);
        }
    }
}
