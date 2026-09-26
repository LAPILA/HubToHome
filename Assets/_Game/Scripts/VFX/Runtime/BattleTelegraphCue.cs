using DG.Tweening;
using UnityEngine;

/// <summary>공격 직전 한 번 보여주는 전조. 생성/반환은 해당 공격의 방어창이 소유합니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class BattleTelegraphCue : MonoBehaviour
{
    [Header("전조 애니메이션")]
    [Tooltip("연결된 Animator Controller에서 재생할 클립 이름입니다. 현재 Telegraph는 START입니다.")]
    [InspectorName("재생 클립")]
    [SerializeField] private string _animationState = "START";
    // Animator가 없는 구형 프리팹의 직렬화 호환용. 현재 전조의 편집 항목으로 노출하지 않습니다.
    [SerializeField, HideInInspector] private float _startScale = 0.25f;
    [SerializeField, HideInInspector] private float _peakScale = 1.2f;
    [SerializeField, HideInInspector] private float _growDuration = 0.08f;
    [SerializeField, HideInInspector] private float _settleDuration = 0.06f;
    [SerializeField, HideInInspector] private float _flashDuration = 0.08f;
    [Header("위치 · 크기는 프리팹 Transform의 Scale로 조절")]
    [SerializeField, InspectorName("중심 위치 보정")] private Vector3 _worldOffset = Vector3.zero;
    [SerializeField] private int _sortingOrderOffset = -1;
    [Header("핑 효과음 · 미지정이면 무음")]
    [SerializeField] private AudioClip _pingClip;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

    private SpriteRenderer _sprite;
    private Sprite _originalSprite;
    private Animator _animator;
    private AnimationClip _authoredClip;
    private bool _originalAnimatorEnabled;
    private bool _ownsAnimator;
    private float _authoredDuration;
    private bool _usesAuthoredAnimation;
    private bool _emphasized;
    private float _secondsUntilImpactAtStart;
    private float _playbackDuration;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Color _cueColor;
    private int _originalSortingLayer;
    private int _originalSortingOrder;
    private Transform _followTarget;
    private ObjectPoolManager _ownerPool;
    private Sequence _animation;
    private bool _leased;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _originalSprite = _sprite.sprite;
        _animator = GetComponent<Animator>();
        _originalAnimatorEnabled = _animator != null && _animator.enabled;
        // 독립 프리팹 확인 시에는 Animator를 멈추지 않습니다. 전투에서 대여할 때만 제어합니다.
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            string clipName = _animationState ?? string.Empty;
            int separator = clipName.LastIndexOf('.');
            if (separator >= 0) clipName = clipName.Substring(separator + 1);
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName)
                {
                    _authoredClip = clip;
                    break;
                }
            }
        }
        _originalScale = transform.localScale;
        _originalColor = _sprite.color;
        _originalSortingLayer = _sprite.sortingLayerID;
        _originalSortingOrder = _sprite.sortingOrder;
    }

    public static BattleTelegraphCue Spawn(GameObject prefab, Transform target, float secondsUntilImpact,
        bool prepareOnly = false, bool counterable = false)
    {
        if (prefab == null || target == null) return null;
        ObjectPoolManager pool = ObjectPoolManager.Instance;
        GameObject instance = pool != null
            ? pool.Spawn(prefab, target.position, Quaternion.identity)
            : Instantiate(prefab, target.position, Quaternion.identity);
        if (instance == null) return null;
        if (!instance.TryGetComponent(out BattleTelegraphCue cue))
        {
            Debug.LogError("[BattleTelegraphCue] 전조 프리팹에 BattleTelegraphCue가 필요합니다.", prefab);
            if (pool != null) pool.Despawn(instance);
            else Destroy(instance);
            return null;
        }

        cue._ownerPool = pool;
        cue._leased = true;
        cue.Play(target, secondsUntilImpact, prepareOnly, counterable);
        return cue;
    }

    private void Play(Transform target, float secondsUntilImpact, bool prepareOnly, bool counterable)
    {
        StopAnimation();
        _followTarget = target;
        _emphasized = false;
        _usesAuthoredAnimation = ResetAuthoredAnimation();
        // 적에게 parent하지 않아 늘어남/뒤집기 모션이 전조 크기와 방향을 바꾸지 않습니다.
        transform.SetPositionAndRotation(target.position + _worldOffset, Quaternion.identity);
        transform.localScale = _usesAuthoredAnimation ? _originalScale : _originalScale * _startScale;
        _sprite.enabled = true;
        _sprite.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0f);
        SpriteRenderer ownerSprite = target.GetComponentInParent<SpriteRenderer>();
        if (ownerSprite != null)
        {
            _sprite.sortingLayerID = ownerSprite.sortingLayerID;
            _sprite.sortingOrder = ownerSprite.sortingOrder + _sortingOrderOffset;
        }

        _cueColor = counterable ? new Color(0.35f, 0.9f, 1f, _originalColor.a) : _originalColor;
        // 새 아트의 모양/크기는 그대로 두고, C 반격의 청록색 구분만 유지합니다.
        transform.rotation = counterable && !_usesAuthoredAnimation
            ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
        if (prepareOnly)
        {
            if (!_usesAuthoredAnimation) transform.localScale = _originalScale * 0.65f;
            _sprite.color = new Color(_cueColor.r, _cueColor.g, _cueColor.b, _cueColor.a * 0.35f);
            return;
        }
        Emphasize(secondsUntilImpact);
    }

    public void Emphasize(float secondsUntilImpact)
    {
        if (!_leased || _emphasized) return;
        StopAnimation();
        _emphasized = true;
        _sprite.color = _cueColor;
        float available = float.IsNaN(secondsUntilImpact) || float.IsInfinity(secondsUntilImpact)
            ? 0.3f : Mathf.Max(0.01f, secondsUntilImpact);
        _secondsUntilImpactAtStart = available;
        if (_usesAuthoredAnimation)
        {
            transform.localScale = _originalScale;
            _playbackDuration = Mathf.Min(_authoredDuration, available);
            SampleAuthoredAnimation(0f);
        }
        else
        {
            PlayLegacyAnimation(available);
        }

        // 풀 예열/OnEnable에는 소리를 내지 않고, 실제 전조 시작에만 한 번 재생합니다.
        if (_pingClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_pingClip, _volume);
    }

    private bool ResetAuthoredAnimation()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return false;
        // Animator.Play + speed 0 + Update(0)에 의존하지 않고 실제 클립을 직접 평가합니다.
        _animator.enabled = false;
        _ownsAnimator = true;
        if (_authoredClip == null || _authoredClip.length <= 0f)
        {
            Debug.LogWarning($"[BattleTelegraphCue] 재생 가능한 '{_animationState}' 클립이 없어 기존 전조를 사용합니다.", this);
            return false;
        }
        _authoredDuration = _authoredClip.length;
        SampleAuthoredAnimation(0f);
        return true;
    }

    private void SampleAuthoredAnimation(float progress)
    {
        if (_authoredClip == null) return;
        // 마지막 프레임에서 유지합니다. 가져온 클립이 loop여도 처음으로 되감기지 않습니다.
        _authoredClip.SampleAnimation(gameObject, _authoredDuration * Mathf.Clamp(progress, 0f, 0.999999f));
    }

    private void PlayLegacyAnimation(float available)
    {
        transform.localScale = _originalScale * _startScale;
        float grow = Mathf.Max(0.01f, _growDuration);
        float settle = Mathf.Max(0.01f, _settleDuration);
        float flash = Mathf.Max(0.01f, _flashDuration);
        float speed = Mathf.Min(1f, available / (grow + settle + flash));
        _animation = DOTween.Sequence().SetRecyclable(false).SetAutoKill(false).Pause();
        _animation.Append(transform.DOScale(_originalScale * _peakScale, grow * speed).SetEase(Ease.OutCubic));
        _animation.Join(_sprite.DOFade(_originalColor.a, grow * speed).SetEase(Ease.OutQuad));
        _animation.Append(transform.DOScale(_originalScale, settle * speed).SetEase(Ease.OutQuad));
        _animation.Append(_sprite.DOFade(_originalColor.a * 0.25f, flash * speed * 0.35f));
        _animation.Append(_sprite.DOFade(_originalColor.a, flash * speed * 0.65f));
    }

    /// <summary>방어 판정과 같은 남은 시간으로 재생합니다. 호출이 없으면 정지 상태를 유지합니다.</summary>
    public void SynchronizeToImpact(float secondsUntilImpact)
    {
        if (!_leased || !_emphasized || _followTarget == null
            || float.IsNaN(secondsUntilImpact) || float.IsInfinity(secondsUntilImpact)) return;
        float elapsed = Mathf.Max(0f, _secondsUntilImpactAtStart - secondsUntilImpact);
        if (_usesAuthoredAnimation)
            SampleAuthoredAnimation(elapsed / _playbackDuration);
        else if (_animation != null && _animation.IsActive())
            _animation.Goto(Mathf.Min(elapsed, _animation.Duration()), false);
    }

    private void LateUpdate()
    {
        if (!_leased || _sprite == null) return;
        if (_followTarget == null)
        {
            _sprite.enabled = false;
            return;
        }
        transform.position = _followTarget.position + _worldOffset;
    }

    public void Release()
    {
        if (!_leased) return;
        _leased = false;
        ObjectPoolManager pool = _ownerPool;
        _ownerPool = null;
        if (pool != null) pool.Despawn(gameObject);
        else Destroy(gameObject);
    }

    private void StopAnimation()
    {
        Sequence owned = _animation;
        _animation = null;
        owned?.Kill(false);
    }

    private void OnDisable()
    {
        StopAnimation();
        if (_ownsAnimator && _animator != null) _animator.enabled = _originalAnimatorEnabled;
        _ownsAnimator = false;
        _leased = false;
        _ownerPool = null;
        _emphasized = false;
        _usesAuthoredAnimation = false;
        _followTarget = null;
        if (_sprite == null) return;
        transform.localScale = _originalScale;
        transform.localRotation = Quaternion.identity;
        _sprite.sprite = _originalSprite;
        _sprite.color = _originalColor;
        _sprite.enabled = true;
        _sprite.sortingLayerID = _originalSortingLayer;
        _sprite.sortingOrder = _originalSortingOrder;
    }

    private void OnDestroy() => StopAnimation();
}
