using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>실제 크기 월드 실루엣과 HUD 뒤 명중 점멸. 요청한 실행만 자기 토큰을 정리합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleSkillAccentPresentation : MonoBehaviour
{
    public const int CanvasOrder = -30000;
    private static BattleSkillAccentPresentation s_instance;
    private static readonly IScreenFlashScaleProvider FlashScale = new GameConfigScreenFlashScaleProvider();
    private Canvas _canvas;
    private CanvasGroup _group;
    private FixedCanvasViewport _viewport;
    private UIViewportService _viewportService;
    private RectTransform _stage;
    private Image _backdrop;
    private BattleSkillAccentSpriteView _spriteView;
    private Material _silhouette;
    private Tween _progressTween;
    private Coroutine _playback;
    private uint _token;
    private float _progress;
    private float _strength;
    private bool _isShowing;
    private Color _accentColor;
    private Action_PresentationAccent.AccentMode _mode;

    public readonly struct Lease : IDisposable
    {
        private readonly BattleSkillAccentPresentation _owner;
        private readonly uint _token;
        internal Lease(BattleSkillAccentPresentation owner, uint token) { _owner = owner; _token = token; }
        public bool IsCurrent => _owner != null && _owner.Owns(_token);
        public void Seek(float elapsed)
        {
            if (IsCurrent) _owner._progressTween?.Goto(elapsed, false);
        }
        public void Dispose()
        {
            if (IsCurrent) _owner.HideImmediate();
        }
    }

    public static Lease Begin(CharacterBase actor, Action_PresentationAccent settings, Func<bool> isActive = null)
    {
        if (actor == null || !actor.isActiveAndEnabled || !actor.IsAlive || settings == null
            || settings.Mode == Action_PresentationAccent.AccentMode.CutIn || !settings.HasValidSettings) return default;
        if (settings.Sfx != null) AudioManager.Instance?.PlaySFX(settings.Sfx, settings.SfxVolume);
        float strength = settings.Strength * FlashScale.Scale;
        if (strength <= 0f || settings.AccentColor.a <= 0f) return default;
        if (s_instance != null && s_instance.gameObject.scene != SceneManager.GetActiveScene())
        {
            BattleSkillAccentPresentation previous = s_instance;
            s_instance = null;
            previous.HideImmediate();
            if (Application.isPlaying) Destroy(previous.gameObject);
            else DestroyImmediate(previous.gameObject);
        }
        if (s_instance == null)
        {
            var root = new GameObject("[Battle Skill Accent]", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup));
            s_instance = root.AddComponent<BattleSkillAccentPresentation>();
        }
        if (!s_instance.isActiveAndEnabled) return default;
        return s_instance.Show(actor, settings, strength, isActive);
    }

    private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        HideImmediate();
    }

    private void OnDestroy()
    {
        HideImmediate();
        // 휴면 뷰는 배우의 자식에 남습니다. 표시 소유자가 먼저 삭제돼도 뷰를 남기지 않습니다.
        if (_spriteView != null)
        {
            GameObject ownedView = _spriteView.gameObject;
            _spriteView = null;
            if (Application.isPlaying) Destroy(ownedView);
            else DestroyImmediate(ownedView);
        }
        if (_silhouette != null)
        {
            if (Application.isPlaying) Destroy(_silhouette);
            else DestroyImmediate(_silhouette);
        }
        if (s_instance == this) s_instance = null;
    }

    private void HandleSceneChanged(Scene previous, Scene next) => HideImmediate();

    private Lease Show(CharacterBase actor, Action_PresentationAccent settings, float strength, Func<bool> isActive)
    {
        HideImmediate();
        _mode = settings.Mode;
        _strength = Mathf.Clamp01(strength);
        _accentColor = settings.AccentColor;
        _progress = 0f;
        if (_mode == Action_PresentationAccent.AccentMode.Silhouette)
        {
            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = actor.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null) return default;
            Material material = ResolveSilhouette();
            if (material == null) return default;
            if (_spriteView == null)
            {
                var clone = new GameObject("Skill Accent Silhouette", typeof(SpriteRenderer));
                clone.transform.SetParent(transform, false);
                _spriteView = clone.AddComponent<BattleSkillAccentSpriteView>();
            }
            _spriteView.Bind(renderer, material);
        }
        else
        {
            EnsureView();
            _canvas.enabled = true;
            _stage.gameObject.SetActive(true);
            _group.alpha = 1f;
            _viewport.enabled = true;
            _viewportService.RegisterFixedViewport(this);
        }
        _isShowing = true;

        uint token = _token;
        _progressTween = DOTween.To(() => _progress, value =>
            {
                if (!Owns(token)) return;
                _progress = value;
                ApplyFrame(value);
            }, 1f, settings.Duration)
            .SetEase(Ease.Linear).SetAutoKill(false).SetRecyclable(false).Pause();
        ApplyFrame(0f);
        _playback = StartCoroutine(WatchPlayback(actor, token, settings.Duration, isActive));
        return new Lease(this, token);
    }

    private IEnumerator WatchPlayback(CharacterBase actor, uint token, float duration, Func<bool> isActive)
    {
        // 외부가 상위 코루틴을 Dispose 없이 멈춰도 이 표시만 정해진 시간 안에 닫힙니다.
        yield return null;
        float elapsed = 0f;
        try
        {
            while (Owns(token) && actor != null && actor.isActiveAndEnabled && actor.IsAlive
                && (_mode != Action_PresentationAccent.AccentMode.Silhouette || _spriteView != null && _spriteView.IsSourceAvailable)
                && (isActive == null || isActive()))
            {
                if (!BattleDelay.IsPaused)
                {
                    elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                    _progressTween?.Goto(elapsed, false);
                    if (elapsed >= duration) break;
                }
                yield return null;
            }
        }
        finally
        {
            if (Owns(token))
            {
                _playback = null;
                HideImmediate();
            }
        }
    }

    private bool Owns(uint token) => isActiveAndEnabled && token == _token && _isShowing;

    private void EnsureView()
    {
        if (_canvas != null) return;
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingLayerID = 0;
        _canvas.sortingOrder = CanvasOrder;
        _canvas.pixelPerfect = true;
        _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _viewportService = UIViewportService.GetOrCreate();
        _viewportService.RegisterFixedViewport(this);
        _viewport = GetComponent<FixedCanvasViewport>();
        _stage = new GameObject("Playfield Accent", typeof(RectTransform), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        RectTransform stage = _stage;
        stage.SetParent(_viewport.ContentRect, false);
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        // HUD 아래162+8, 턴 큐 위48+8을 피한 기준 픽셀 영역입니다.
        stage.sizeDelta = new Vector2(640f, 250f);
        stage.anchoredPosition = new Vector2(0f, 55f);
        _backdrop = CreateImage("Backdrop", stage);
        _backdrop.rectTransform.sizeDelta = stage.sizeDelta;
        _canvas.enabled = false;
        _group.alpha = 0f;
        _viewport.enabled = false;
        _viewportService.Unregister(this);
        stage.gameObject.SetActive(false);
    }

    private static Image CreateImage(string name, RectTransform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.raycastTarget = false;
        return image;
    }

    private Material ResolveSilhouette()
    {
        if (_silhouette != null) return _silhouette;
        Shader shader = Resources.Load<Shader>("HubToHome/SkillAccentSilhouette");
        if (shader != null) _silhouette = new Material(shader) { name = "Skill Accent Silhouette (Owned)", hideFlags = HideFlags.DontSave };
        return _silhouette;
    }

    private void ApplyFrame(float progress)
    {
        float alpha = EvaluateOpacity(_mode, progress, _strength, _accentColor.a);
        if (_mode == Action_PresentationAccent.AccentMode.Flash)
        {
            _backdrop.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha * 0.45f);
            return;
        }
        if (_spriteView != null)
            _spriteView.SetTint(new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha));
    }

    public static float EvaluateEnvelope(Action_PresentationAccent.AccentMode mode, float progress)
    {
        if (mode == Action_PresentationAccent.AccentMode.CutIn || mode < Action_PresentationAccent.AccentMode.CutIn
            || mode > Action_PresentationAccent.AccentMode.Flash || float.IsNaN(progress) || float.IsInfinity(progress)) return 0f;
        float t = Mathf.Clamp01(progress);
        if (mode == Action_PresentationAccent.AccentMode.Flash) return (1f - t) * (1f - t);
        float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
        float exit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.65f) / 0.35f));
        return enter * exit;
    }

    public static float EvaluateOpacity(Action_PresentationAccent.AccentMode mode, float progress, float strength, float colorAlpha)
    {
        if (float.IsNaN(strength) || float.IsInfinity(strength)
            || float.IsNaN(colorAlpha) || float.IsInfinity(colorAlpha)) return 0f;
        return EvaluateEnvelope(mode, progress) * Mathf.Clamp01(strength) * Mathf.Clamp01(colorAlpha);
    }

    private void HideImmediate()
    {
        unchecked { _token++; }
        _isShowing = false;
        Tween tween = _progressTween;
        _progressTween = null;
        tween?.Kill(false);
        Coroutine playback = _playback;
        _playback = null;
        if (playback != null) StopCoroutine(playback);
        if (_canvas != null) _canvas.enabled = false;
        if (_stage != null) _stage.gameObject.SetActive(false);
        if (_group != null) _group.alpha = 0f;
        if (_spriteView != null) _spriteView.Unbind(transform);
        if (_viewport != null) _viewport.enabled = false;
        if (_viewportService != null) _viewportService.Unregister(this);
    }
}
