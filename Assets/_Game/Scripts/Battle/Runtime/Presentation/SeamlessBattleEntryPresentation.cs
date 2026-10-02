using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Host당 하나 재사용하는 경량 화면 전환. 게임 상태/참가자/전투 시작은 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class SeamlessBattleEntryPresentation : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _root, _cut, _top, _bottom, _warning;
    private Image _cutLine, _warningBorder, _warningStem, _warningDot;
    private Tween _tween;
    private BattleHudEntryMotion _hudMotion;
    private CameraController _camera;
    private CameraCommandToken _cameraToken;
    private Camera _output;
    private Transform _subject;
    private Transform _warningAnchor;
    private SpriteRenderer _subjectRenderer;
    private BattleEntrySettings _settings;
    private int _version;
    private bool _playing;
    private float _coveredAt;
    public bool IsPlaying => _playing;
    public bool IsCovered { get; private set; }
    public bool WasCompleted { get; private set; }

    public void Begin(BattleEntrySettings settings, PlayerController player)
    {
        Cancel();
        _settings = settings ?? new BattleEntrySettings();
        EnsureView();
        _subject = player != null ? player.transform : null;
        if (player != null && player.TryGetComponent(out CharacterBase character))
            character.TryGetPivot(CharacterPivotId.Top, out _warningAnchor);
        _subjectRenderer = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        _output = Camera.main;
        _camera = CameraController.Instance;
        _playing = true;
        _canvas.enabled = true;
        _cut.gameObject.SetActive(false);
        _top.gameObject.SetActive(false);
        _bottom.gameObject.SetActive(false);
        _warning.gameObject.SetActive(_subject != null);
        _warningBorder.color = _warningStem.color = _warningDot.color = _settings.WarningColor;
        _cutLine.color = _settings.SlashColor;
        _warning.localScale = Vector3.one * 0.7f;
        UpdateWarningPosition();
        GameInput.ConsumeBattleUIInput();
    }

    public IEnumerator Cover()
    {
        int version = _version;
        if (!IsCurrent(version)) yield break;
        if (_settings.WarningSfx != null) AudioManager.Instance?.PlaySFX(_settings.WarningSfx);
        yield return Animate(BattleEntrySettings.SafeDuration(_settings.WarningDuration), value =>
        {
            if (_warning == null) return;
            _warning.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Min(1f, value * 2.5f));
            UpdateWarningPosition();
        }, Ease.OutCubic);
        if (!IsCurrent(version)) yield break;
        if (_camera != null && _subject != null)
            _camera.TryFocusEncounter(_subject,
                BattleEntrySettings.FiniteClamp(_settings.ZoomRatio, 0.8f, 1f, 0.9f),
                BattleEntrySettings.SafeDuration(_settings.ZoomDuration), out _cameraToken, out _);
        yield return Animate(BattleEntrySettings.SafeDuration(_settings.ZoomDuration), _ => UpdateWarningPosition());
        if (!IsCurrent(version)) yield break;
        _warning.gameObject.SetActive(false);
        _cut.gameObject.SetActive(true);
        if (_settings.SlashSfx != null) AudioManager.Instance?.PlaySFX(_settings.SlashSfx);
        yield return Animate(BattleEntrySettings.SafeDuration(_settings.SlashDuration), DrawCut, Ease.InCubic);
        if (!IsCurrent(version)) yield break;
        _top.gameObject.SetActive(true);
        _bottom.gameObject.SetActive(true);
        DrawOpening(0f);
        _cut.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        _coveredAt = Time.unscaledTime;
        // 검정 화면을 실제로 한 프레임 표시한 뒤에만 전투 준비를 허용합니다.
        yield return null;
        if (!IsCurrent(version)) yield break;
        IsCovered = true;
        ReleaseCamera();
        // 준비는 즉시 시작합니다. 최소 암전 시간의 나머지는 Reveal에서 기다립니다.
    }

    public IEnumerator Reveal(BattleUIController ui)
    {
        int version = _version;
        if (!IsCurrent(version) || !IsCovered) yield break;
        float remainingBlack = Mathf.Max(0f,
            BattleEntrySettings.SafeDuration(_settings.BlackDuration) - (Time.unscaledTime - _coveredAt));
        yield return Animate(remainingBlack, null);
        if (!IsCurrent(version)) yield break;
        _hudMotion = ui != null ? ui.BeginEntryPresentation(
            BattleEntrySettings.FiniteClamp(_settings.HudDistance, 0f, 80f, 24f)) : null;
        float opening = BattleEntrySettings.SafeDuration(_settings.RevealDuration);
        float delay = BattleEntrySettings.SafeDuration(_settings.HudDelay);
        float hud = BattleEntrySettings.SafeDuration(_settings.HudDuration);
        float total = Mathf.Max(opening, delay + hud);
        IsCovered = false;
        yield return Animate(total, progress =>
        {
            float elapsed = total * progress;
            DrawOpening(EaseOutCubic(opening <= 0f ? 1f : elapsed / opening));
            float uiProgress = elapsed < delay ? 0f : hud <= 0f ? 1f : (elapsed - delay) / hud;
            _hudMotion?.SetProgress(EaseOutCubic(uiProgress));
        });
        if (!IsCurrent(version)) yield break;
        Cancel();
        GameInput.ConsumeBattleUIInput();
        WasCompleted = true;
    }

    public void Cancel()
    {
        WasCompleted = false;
        IsCovered = false;
        _version++;
        _playing = false;
        _tween?.Kill(false);
        _tween = null;
        _hudMotion?.Dispose();
        _hudMotion = null;
        ReleaseCamera();
        if (_canvas != null) _canvas.enabled = false;
        _subject = null;
        _warningAnchor = null;
        _subjectRenderer = null;
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
    private bool IsCurrent(int version) => this != null && isActiveAndEnabled && _playing && version == _version;

    private IEnumerator Animate(float duration, System.Action<float> apply, Ease ease = Ease.Linear)
    {
        int version = _version;
        if (!IsCurrent(version)) yield break;
        apply?.Invoke(0f);
        if (duration <= 0f) { apply?.Invoke(1f); yield break; }
        Tween own = DOTween.To(() => 0f, value =>
        {
            if (IsCurrent(version)) apply?.Invoke(value);
        }, 1f, duration).SetEase(ease).SetUpdate(true).SetRecyclable(false);
        _tween = own;
        yield return own.WaitForCompletion();
        if (_tween == own) _tween = null;
    }

    private void ReleaseCamera()
    {
        if (_camera != null && _camera.IsCurrent(_cameraToken)) _camera.Cancel(_cameraToken, true);
        _cameraToken = default;
        _camera = null;
    }

    private void UpdateWarningPosition()
    {
        if (_warning == null || _subject == null || _output == null) return;
        Vector3 world = _subject.position + Vector3.up;
        if (_warningAnchor != null)
            world = _warningAnchor.position;
        else if (_subjectRenderer != null)
            world = new Vector3(_subjectRenderer.bounds.center.x, _subjectRenderer.bounds.max.y, _subject.position.z);
        Vector3 screen = _output.WorldToScreenPoint(world);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local))
        {
            Rect bounds = _root.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + 16f, bounds.xMax - 16f);
            local.y = Mathf.Clamp(local.y + 16f, bounds.yMin + 16f, bounds.yMax - 16f);
            _warning.anchoredPosition = local;
        }
    }

    private void DrawCut(float progress)
    {
        if (_root == null || _cut == null) return;
        Vector2 size = _root.rect.size;
        float extent = size.x + size.y + 16f;
        _cut.sizeDelta = new Vector2(extent * 2f, extent * 2f);
        float edge = size.x * 0.5f + size.y * 0.5f * Mathf.Tan(22f * Mathf.Deg2Rad) + 8f;
        _cut.anchoredPosition = new Vector2(Mathf.Lerp(-edge, edge, progress), 0f);
    }

    private void DrawOpening(float progress)
    {
        if (_root == null || _top == null || _bottom == null) return;
        Vector2 size = _root.rect.size;
        float half = size.y * 0.5f + 1f;
        _top.sizeDelta = _bottom.sizeDelta = new Vector2(size.x + 2f, half);
        _top.anchoredPosition = new Vector2(0f, half * Mathf.Clamp01(progress));
        _bottom.anchoredPosition = new Vector2(0f, -half * Mathf.Clamp01(progress));
    }

    private static float EaseOutCubic(float progress)
    {
        float remaining = 1f - Mathf.Clamp01(progress);
        return 1f - remaining * remaining * remaining;
    }

    private void EnsureView()
    {
        if (_canvas != null) return;
        var go = new GameObject("Battle Entry Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingLayerID = 0;
        _canvas.sortingOrder = 32764; // 전역 페이드·모달 대화·레터박스 우선권은 유지합니다.
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.matchWidthOrHeight = 0.5f;
        _root = go.GetComponent<RectTransform>();
        _cut = MakeImage("Diagonal Cover", _root, Color.black).rectTransform;
        _cut.pivot = new Vector2(1f, 0.5f);
        _cut.localRotation = Quaternion.Euler(0f, 0f, 22f);
        _cutLine = MakeImage("Cut Edge", _cut, Color.white);
        _cutLine.rectTransform.anchorMin = new Vector2(1f, 0f);
        _cutLine.rectTransform.anchorMax = Vector2.one;
        _cutLine.rectTransform.sizeDelta = new Vector2(4f, 0f);
        _top = MakeImage("Upper Cover", _root, Color.black).rectTransform;
        _top.anchorMin = _top.anchorMax = _top.pivot = new Vector2(0.5f, 1f);
        _bottom = MakeImage("Lower Cover", _root, Color.black).rectTransform;
        _bottom.anchorMin = _bottom.anchorMax = _bottom.pivot = new Vector2(0.5f, 0f);
        _warning = new GameObject("Encounter Warning", typeof(RectTransform)).GetComponent<RectTransform>();
        _warning.SetParent(_root, false);
        _warning.sizeDelta = new Vector2(24f, 28f);
        _warningBorder = MakeImage("Diamond Border", _warning, Color.white);
        _warningBorder.rectTransform.sizeDelta = new Vector2(19f, 19f);
        _warningBorder.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image inner = MakeImage("Diamond Fill", _warningBorder.rectTransform, Color.black);
        inner.rectTransform.sizeDelta = new Vector2(17f, 17f);
        _warningStem = MakeImage("Mark Stem", _warning, Color.white);
        _warningStem.rectTransform.sizeDelta = new Vector2(3f, 10f);
        _warningStem.rectTransform.anchoredPosition = new Vector2(0f, 3f);
        _warningDot = MakeImage("Mark Dot", _warning, Color.white);
        _warningDot.rectTransform.sizeDelta = new Vector2(3f, 3f);
        _warningDot.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        _canvas.enabled = false;
    }

    private static Image MakeImage(string name, RectTransform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
