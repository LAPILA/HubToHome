using DG.Tweening;
using UnityEngine;

/// <summary>공격 직전 한 번 보여주는 전조. 생성/반환은 해당 공격의 방어창이 소유합니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class BattleTelegraphCue : MonoBehaviour
{
    [Header("등장 · 원래 프리팹 크기 기준")]
    [SerializeField, Range(0.01f, 1f)] private float _startScale = 0.25f;
    [SerializeField, Range(1f, 2f)] private float _peakScale = 1.2f;
    [SerializeField, Min(0.01f)] private float _growDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _settleDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float _flashDuration = 0.08f;
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private int _sortingOrderOffset = 10;
    [Header("핑 효과음 · 미지정이면 무음")]
    [SerializeField] private AudioClip _pingClip;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

    private SpriteRenderer _sprite;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Color _cueColor;
    private int _originalSortingLayer;
    private int _originalSortingOrder;
    private Transform _followTarget;
    private ObjectPoolManager _ownerPool;
    private Sequence _animation;
    private float _elapsed;
    private bool _leased;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
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
        _elapsed = 0f;
        // 적에게 parent하지 않아 늘어남/뒤집기 모션이 전조 크기와 방향을 바꾸지 않습니다.
        transform.SetPositionAndRotation(target.position + _worldOffset, Quaternion.identity);
        transform.localScale = _originalScale * _startScale;
        _sprite.enabled = true;
        _sprite.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0f);
        SpriteRenderer ownerSprite = target.GetComponentInParent<SpriteRenderer>();
        if (ownerSprite != null)
        {
            _sprite.sortingLayerID = ownerSprite.sortingLayerID;
            _sprite.sortingOrder = ownerSprite.sortingOrder + _sortingOrderOffset;
        }

        _cueColor = counterable ? new Color(0.35f, 0.9f, 1f, _originalColor.a) : _originalColor;
        // 특수반격은 청록색과 회전으로 구별합니다. 일반 신호의 원색은 보존합니다.
        transform.rotation = counterable ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
        if (prepareOnly)
        {
            transform.localScale = _originalScale * 0.65f;
            _sprite.color = new Color(_cueColor.r, _cueColor.g, _cueColor.b, _cueColor.a * 0.35f);
            return;
        }
        Emphasize(secondsUntilImpact);
    }

    public void Emphasize(float secondsUntilImpact)
    {
        if (!_leased) return;
        StopAnimation();
        _elapsed = 0f;
        transform.localScale = _originalScale * _startScale;
        _sprite.color = _cueColor;
        float grow = Mathf.Max(0.01f, _growDuration);
        float settle = Mathf.Max(0.01f, _settleDuration);
        float flash = Mathf.Max(0.01f, _flashDuration);
        float available = float.IsNaN(secondsUntilImpact) || float.IsInfinity(secondsUntilImpact)
            ? 0.3f : Mathf.Max(0.01f, secondsUntilImpact);
        float speed = Mathf.Min(1f, available / (grow + settle + flash));
        _animation = DOTween.Sequence().SetRecyclable(false).SetAutoKill(false).Pause();
        _animation.Append(transform.DOScale(_originalScale * _peakScale, grow * speed).SetEase(Ease.OutCubic));
        _animation.Join(_sprite.DOFade(_originalColor.a, grow * speed).SetEase(Ease.OutQuad));
        _animation.Append(transform.DOScale(_originalScale, settle * speed).SetEase(Ease.OutQuad));
        _animation.Append(_sprite.DOFade(_originalColor.a * 0.25f, flash * speed * 0.35f));
        _animation.Append(_sprite.DOFade(_originalColor.a, flash * speed * 0.65f));

        // 풀 예열/OnEnable에는 소리를 내지 않고, 실제 전조 시작에만 한 번 재생합니다.
        if (_pingClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_pingClip, _volume);
    }

    private void LateUpdate()
    {
        if (!_leased) return;
        if (_followTarget == null)
        {
            _sprite.enabled = false;
            return;
        }
        transform.position = _followTarget.position + _worldOffset;
        // 방어 판정과 동일하게 설정/일시정지 중에는 멈추고, 그 외에는 실시간으로 진행합니다.
        if (GameInput.IsDefenseInputBlocked || _animation == null || !_animation.IsActive()) return;
        _elapsed += Time.unscaledDeltaTime;
        _animation.Goto(Mathf.Min(_elapsed, _animation.Duration()), false);
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
        _followTarget = null;
        if (_sprite == null) return;
        transform.localScale = _originalScale;
        _sprite.color = _originalColor;
        _sprite.enabled = true;
        _sprite.sortingLayerID = _originalSortingLayer;
        _sprite.sortingOrder = _originalSortingOrder;
    }

    private void OnDestroy() => StopAnimation();
}
