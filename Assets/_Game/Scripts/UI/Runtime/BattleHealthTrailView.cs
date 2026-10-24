using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 HP 뒤의 감소 구간만 표시합니다. 연타는 첫 감소의 종료 시각을 연장하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleHealthTrailView : MonoBehaviour
{
    private Image _source;
    private Image _trail;
    private Tween _tween;
    private bool _solid;
    private float _fromRatio;
    private float _targetRatio;

    public void ShowLoss(Image source, bool solid, float previousRatio, float targetRatio, Color color,
        float delay, float duration)
    {
        if (source == null || source.transform.parent == null) { Clear(); return; }
        if (_source != source || _solid != solid)
        {
            Clear();
            DestroyTrail();
            _source = source;
            _solid = solid;
        }
        EnsureTrail();
        _targetRatio = Mathf.Clamp01(targetRatio);
        if (_trail == null) return;
        _trail.color = color;
        if (_tween != null && _tween.IsActive()) return;
        _fromRatio = Mathf.Clamp01(previousRatio);
        if (_fromRatio <= _targetRatio) { Clear(); return; }
        CopyLayout();
        _trail.gameObject.SetActive(true);
        SetRatio(_fromRatio);
        float hold = SanitizeDuration(delay, 0.12f, 0f, 0.5f);
        float fall = SanitizeDuration(duration, 0.28f, 0.01f, 1f);
        _tween = DOTween.To(() => 0f, elapsed =>
            {
                if (_source == null || _trail == null) { Clear(); return; }
                float progress = Mathf.Clamp01((elapsed - hold) / fall);
                float eased = 1f - (1f - progress) * (1f - progress);
                SetRatio(Mathf.Lerp(_fromRatio, _targetRatio, eased));
            }, hold + fall, hold + fall)
            .SetEase(Ease.Linear).SetUpdate(true).SetTarget(this).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(Clear);
    }

    private void EnsureTrail()
    {
        if (_trail != null) return;
        var root = new GameObject("HP 감소 잔여 표시", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement));
        root.layer = _source.gameObject.layer;
        root.transform.SetParent(_source.transform.parent, false);
        root.GetComponent<LayoutElement>().ignoreLayout = true;
        _trail = root.GetComponent<Image>();
        _trail.raycastTarget = false;
        _trail.maskable = _source.maskable;
        _trail.material = _source.material;
        _trail.sprite = _source.sprite;
        _trail.overrideSprite = _source.overrideSprite;
        _trail.type = _source.type;
        _trail.fillMethod = _source.fillMethod;
        _trail.fillOrigin = _source.fillOrigin;
        _trail.fillClockwise = _source.fillClockwise;
        _trail.fillCenter = _source.fillCenter;
        _trail.preserveAspect = _source.preserveAspect;
        _trail.pixelsPerUnitMultiplier = _source.pixelsPerUnitMultiplier;
        // 기존 형제의 상대 순서는 유지하고 실제 HP의 바로 뒤에만 추가합니다.
        root.transform.SetSiblingIndex(_source.transform.GetSiblingIndex());
        root.SetActive(false);
    }

    private void CopyLayout()
    {
        RectTransform source = _source.rectTransform;
        RectTransform trail = _trail.rectTransform;
        trail.anchorMin = source.anchorMin;
        trail.anchorMax = source.anchorMax;
        trail.pivot = source.pivot;
        trail.sizeDelta = source.sizeDelta;
        trail.anchoredPosition3D = source.anchoredPosition3D;
        trail.localRotation = source.localRotation;
        Vector3 scale = source.localScale;
        trail.localScale = _solid ? new Vector3(1f, scale.y, scale.z) : scale;
    }

    private void SetRatio(float ratio)
    {
        if (_trail == null) return;
        _trail.fillAmount = ratio;
        if (_solid)
        {
            Vector3 scale = _trail.rectTransform.localScale;
            scale.x = ratio;
            _trail.rectTransform.localScale = scale;
        }
    }

    private static float SanitizeDuration(float value, float fallback, float minimum, float maximum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);

    public void Clear()
    {
        Tween owned = _tween;
        _tween = null;
        if (owned != null && owned.IsActive()) owned.Kill(false);
        if (_trail != null) _trail.gameObject.SetActive(false);
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        Clear();
        DestroyTrail();
    }

    private void DestroyTrail()
    {
        if (_trail == null) return;
        GameObject owned = _trail.gameObject;
        _trail = null;
        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }
}
