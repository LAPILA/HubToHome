using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>직접 피격 대상만 잠시 멈추고, 공유 원본을 수정하지 않는 개별 백색 재질을 재사용합니다.</summary>
[DisallowMultipleComponent]
public sealed class CharacterHitFeedback : MonoBehaviour
{
    // Camera cuts carry the stop-and-release rhythm. Target animation stays responsive by default.
    public const float PauseDuration = 0f;
    public const float StrongPauseDuration = 0f;
    public const float RapidPauseDuration = 0f;
    private const string MaterialPath = "HubToHome/CharacterHitSilhouette";

    [FoldoutGroup("명중 피드백"), LabelText("기본 정지 (초)"), SerializeField, Range(0f, 0.1f)]
    private float _normalPause = PauseDuration;
    [FoldoutGroup("명중 피드백"), LabelText("강한 타격 정지 (초)"), SerializeField, Range(0f, 0.12f)]
    private float _strongPause = StrongPauseDuration;
    [FoldoutGroup("명중 피드백"), LabelText("강한 타격 기준 (최대 HP 비율)"), SerializeField, Range(0.01f, 1f)]
    private float _strongDamageRatio = 0.1f;
    [FoldoutGroup("명중 피드백"), LabelText("연타 구분 간격 (초)"), SerializeField, Range(0.05f, 0.3f)]
    private float _repeatInterval = 0.12f;
    [FoldoutGroup("명중 피드백"), LabelText("연타 정지 (초)"), SerializeField, Range(0f, 0.04f)]
    private float _rapidPause = RapidPauseDuration;
    [FoldoutGroup("명중 피드백"), LabelText("연타 점멸 상한 (초)"), SerializeField, Range(0.01f, 0.1f)]
    private float _rapidFlashDuration = 0.045f;
    private readonly Tween[] _reactionTweens = new Tween[3];
    private int _tweenCount;
    private SpriteRenderer _source;
    private Material _whiteMaterial;
    private Material _originalMaterial;
    private bool _ownsMaterial;
    private Animator _animator;
    private float _restoreSpeed;
    private bool _ownsSpeed;
    private float _pauseRemaining;
    private float _flashRemaining;
    private float _flashDuration;
    private float _flashScale;
    private bool _impactPending;
    private bool _tweensPaused;
    private float _impactAge = float.PositiveInfinity;
    private bool _contactSinceUpdate;
    private Action _onImpact;

    public bool IsHolding => _impactPending;
    public bool IsActive => _impactPending || _flashRemaining > 0f || HasActiveTweens();

    public void ShowDefeat(SpriteRenderer source, float duration, float flashScale)
    {
        Cancel();
        _source = source;
        _originalMaterial = source != null ? source.sharedMaterial : null;
        _flashDuration = _flashRemaining = Mathf.Clamp(duration, 0.01f, 0.15f);
        _flashScale = VisualAccessibilityPolicy.NormalizeScale(flashScale);
        EnsureMaterial();
        SyncMaterial();
        enabled = true;
    }

    public float Begin(SpriteRenderer source, Animator animator, float flashDuration, float flashScale,
        Action onImpact, float damageRatio = 0f, Action onContact = null)
    {
        bool rapid = _impactAge < Mathf.Max(0.05f, _repeatInterval);
        float pause = Mathf.Clamp(rapid ? _rapidPause
            : damageRatio >= _strongDamageRatio ? _strongPause : _normalPause, 0f, 0.12f);
        // Catch-up hits and AoE must not repeatedly push an already-owned stop into the future.
        if (_impactPending) pause = Mathf.Min(pause, Mathf.Max(0f, _pauseRemaining));
        bool showContact = !_contactSinceUpdate;
        ClearReaction();
        _source = source;
        _originalMaterial = source != null ? source.sharedMaterial : null;
        _animator = animator;
        _onImpact = onImpact;
        _pauseRemaining = pause;
        _flashDuration = Mathf.Max(0.01f, flashDuration);
        if (rapid) _flashDuration = Mathf.Min(_flashDuration, Mathf.Max(0.01f, _rapidFlashDuration));
        _flashRemaining = _flashDuration;
        _flashScale = VisualAccessibilityPolicy.NormalizeScale(flashScale);
        _impactPending = pause > 0f;
        _impactAge = 0f;
        _contactSinceUpdate = true;
        if (_impactPending && _animator != null && _animator.isActiveAndEnabled)
        {
            _restoreSpeed = _animator.speed;
            _animator.speed = 0f;
            _ownsSpeed = true;
        }
        EnsureMaterial();
        // The contact frame is visible before the brief pose hold, not one reaction later.
        SyncMaterial();
        enabled = true;
        if (showContact) onContact?.Invoke();
        if (!_impactPending)
        {
            Action impact = _onImpact;
            _onImpact = null;
            impact?.Invoke();
        }
        return pause;
    }

    public void Track(Tween tween)
    {
        if (tween == null || !_impactPending && !enabled || _tweenCount >= _reactionTweens.Length) return;
        _reactionTweens[_tweenCount++] = tween;
        if (_tweensPaused) tween.Pause();
    }

    private void Update() => Advance(Time.unscaledDeltaTime, BattleDelay.IsPaused);

    // Explicit time input keeps the lifetime contract verifiable without running a scene.
    private void Advance(float deltaTime, bool paused)
    {
        if (_ownsSpeed && _animator != null && !Mathf.Approximately(_animator.speed, 0f))
        {
            // An external motion owner replaced our speed. Never restore over it or issue stale Hurt.
            _ownsSpeed = false;
            Cancel();
            return;
        }
        SetTweensPaused(paused);
        if (paused) return;
        float elapsed = Mathf.Max(0f, deltaTime);
        _impactAge += elapsed;
        _contactSinceUpdate = false;
        if (_impactPending)
        {
            _pauseRemaining -= elapsed;
            if (_pauseRemaining > 0f) return;
            _impactPending = false;
            ReleaseSpeed();
            Action impact = _onImpact;
            _onImpact = null;
            impact?.Invoke();
            // Do not drop the release pose/flash completely on a long frame.
        }
        else _flashRemaining = Mathf.Max(0f, _flashRemaining - elapsed);
        SyncMaterial();
        // A rapid flash ends before the next hit. Retain only its cadence clock through
        // the repeat window, so fading out cannot accidentally turn every other hit heavy.
        if (_flashRemaining <= 0f && !HasActiveTweens()
            && _impactAge >= Mathf.Max(0.05f, _repeatInterval))
        {
            _source = null;
            enabled = false;
        }
    }

    private void LateUpdate() => SyncMaterial();

    private void EnsureMaterial()
    {
        if (_source == null || _whiteMaterial != null || _flashScale <= 0f) return;
        Material material = Resources.Load<Material>(MaterialPath);
        if (material == null) return;
        _whiteMaterial = new Material(material) { hideFlags = HideFlags.DontSave };
    }

    private void SyncMaterial()
    {
        if (_whiteMaterial == null) return;
        if (_source != null && _source.sharedMaterial != (_ownsMaterial ? _whiteMaterial : _originalMaterial))
        {
            // A newer material owner wins, including changes during the brief hold.
            _ownsMaterial = false;
            _originalMaterial = null;
            _flashRemaining = 0f;
            return;
        }
        bool visible = _flashRemaining > 0f && _flashScale > 0f
            && _source != null && _source.enabled && _source.gameObject.activeInHierarchy;
        if (!visible) { ReleaseMaterial(); return; }
        if (!_ownsMaterial)
        {
            _source.sharedMaterial = _whiteMaterial;
            _ownsMaterial = true;
        }
        float fade = Mathf.Clamp01(_flashRemaining / Mathf.Max(0.01f, _flashDuration * 0.4f));
        _whiteMaterial.SetFloat("_WhiteAmount", _flashScale * fade);
    }

    private void ReleaseMaterial()
    {
        if (_ownsMaterial && _source != null && _source.sharedMaterial == _whiteMaterial)
            _source.sharedMaterial = _originalMaterial;
        _ownsMaterial = false;
    }

    private bool HasActiveTweens()
    {
        for (int i = 0; i < _tweenCount; i++)
            if (_reactionTweens[i] != null && _reactionTweens[i].IsActive()) return true;
        return false;
    }

    private void SetTweensPaused(bool paused)
    {
        if (_tweensPaused == paused) return;
        _tweensPaused = paused;
        for (int i = 0; i < _tweenCount; i++)
        {
            Tween tween = _reactionTweens[i];
            if (tween == null || !tween.IsActive()) continue;
            if (paused) tween.Pause();
            else tween.Play();
        }
    }

    private void ReleaseSpeed()
    {
        if (_ownsSpeed && _animator != null && Mathf.Approximately(_animator.speed, 0f))
            _animator.speed = _restoreSpeed;
        _ownsSpeed = false;
        _animator = null;
    }

    private void ClearReaction()
    {
        _impactPending = false;
        _onImpact = null;
        _flashRemaining = 0f;
        ReleaseSpeed();
        for (int i = 0; i < _tweenCount; i++)
        {
            Tween tween = _reactionTweens[i];
            _reactionTweens[i] = null;
            if (tween != null && tween.IsActive()) tween.Kill(false);
        }
        _tweenCount = 0;
        _tweensPaused = false;
        ReleaseMaterial();
        _source = null;
        _originalMaterial = null;
    }

    public void Cancel()
    {
        ClearReaction();
        _impactAge = float.PositiveInfinity;
        _contactSinceUpdate = false;
        enabled = false;
    }

    private void OnDisable() => Cancel();
    private void OnDestroy()
    {
        Cancel();
        if (_whiteMaterial == null) return;
        if (Application.isPlaying) Destroy(_whiteMaterial);
        else DestroyImmediate(_whiteMaterial);
    }
}
