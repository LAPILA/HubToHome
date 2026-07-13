using System;
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
        if (!TryResolveTargetAlpha(mode, out float targetAlpha))
        {
            handle.Fail("Unsupported screen.fade mode: " + mode);
            yield break;
        }

        if (!TryResolveColor(color, out Color fadeColor))
        {
            handle.Fail("Unsupported screen.fade color: " + color);
            yield break;
        }

        ScreenTransitionOverlay overlay = _overlay != null ? _overlay : ScreenTransitionOverlay.GetOrCreate();
        yield return overlay.FadeTo(fadeColor, targetAlpha, duration, handle);
    }

    internal static bool TryResolveTargetAlpha(string mode, out float targetAlpha)
    {
        targetAlpha = 0f;
        if (string.IsNullOrWhiteSpace(mode))
            return false;

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

    internal static bool TryResolveColor(string color, out Color fadeColor)
    {
        fadeColor = Color.black;
        if (string.IsNullOrWhiteSpace(color))
            return true;

        switch (color.Trim().ToLowerInvariant())
        {
            case "black": fadeColor = Color.black; return true;
            case "white": fadeColor = Color.white; return true;
            case "clear": fadeColor = Color.clear; return true;
        }

        string html = color.Trim();
        if (!html.StartsWith("#"))
            html = "#" + html;

        return ColorUtility.TryParseHtmlString(html, out fadeColor);
    }
}

public sealed class ScreenTransitionOverlay : MonoBehaviour
{
    private const string OverlayName = "ScenarioScreenTransitionOverlay";
    private static ScreenTransitionOverlay _instance;

    private CanvasGroup _canvasGroup;
    private Image _image;
    private int _requestGeneration;

    public static ScreenTransitionOverlay GetOrCreate()
    {
        if (_instance != null)
            return _instance;

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

    public IEnumerator FadeTo(
        Color color,
        float targetAlpha,
        float duration,
        ActionExecutionHandle handle)
    {
        EnsureInitialized();

        int generation = ++_requestGeneration;
        var prior = new OverlayState(
            _canvasGroup.alpha,
            _canvasGroup.blocksRaycasts,
            _image.color,
            gameObject.activeSelf);

        Action<ActionExecutionHandle> cancellation = null;
        cancellation = _ =>
        {
            handle.CancellationRequested -= cancellation;
            if (generation == _requestGeneration)
                Restore(prior);
        };
        handle.CancellationRequested += cancellation;

        color.a = 1f;
        gameObject.SetActive(true);
        _image.color = color;
        _canvasGroup.blocksRaycasts = targetAlpha > 0.001f;

        float clampedDuration = Mathf.Max(0f, duration);
        float startAlpha = _canvasGroup.alpha;
        if (clampedDuration <= 0f)
        {
            handle.CancellationRequested -= cancellation;
            if (generation == _requestGeneration && !handle.IsCancellationRequested)
                ApplyAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            if (generation != _requestGeneration)
            {
                handle.CancellationRequested -= cancellation;
                yield break;
            }

            if (handle.IsCancellationRequested)
            {
                handle.CancellationRequested -= cancellation;
                Restore(prior);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            ApplyAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        handle.CancellationRequested -= cancellation;
        if (generation == _requestGeneration)
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

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void EnsureInitialized()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
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
    }

    private void ApplyAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        _canvasGroup.alpha = clampedAlpha;
        _canvasGroup.blocksRaycasts = clampedAlpha > 0.001f;
        if (clampedAlpha <= 0.001f)
            gameObject.SetActive(false);
        else if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void Restore(OverlayState state)
    {
        _canvasGroup.alpha = state.Alpha;
        _canvasGroup.blocksRaycasts = state.BlocksRaycasts;
        _image.color = state.Color;
        gameObject.SetActive(state.Active);
    }

    private readonly struct OverlayState
    {
        public OverlayState(float alpha, bool blocksRaycasts, Color color, bool active)
        {
            Alpha = alpha;
            BlocksRaycasts = blocksRaycasts;
            Color = color;
            Active = active;
        }

        public float Alpha { get; }
        public bool BlocksRaycasts { get; }
        public Color Color { get; }
        public bool Active { get; }
    }
}
