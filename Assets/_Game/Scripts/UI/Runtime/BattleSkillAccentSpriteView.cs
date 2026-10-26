using UnityEngine;

/// <summary>활성 실루엣 한 개만 실제 배우의 현재 프레임에 맞춥니다. 휴면 시 GameObject 자체를 끕니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BattleSkillAccentSpriteView : MonoBehaviour
{
    private SpriteRenderer _source;
    private SpriteRenderer _renderer;
    private Color _tint = Color.clear;
    public SpriteRenderer Renderer => _renderer;
    public bool IsSourceAvailable => _source != null && _source.gameObject.activeInHierarchy;

    public void Bind(SpriteRenderer source, Material material)
    {
        if (source == null)
        {
            Unbind(transform.parent);
            return;
        }
        _source = source;
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        _renderer.sharedMaterial = material;
        transform.SetParent(source.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        SyncNow();
    }

    public void SetTint(Color tint)
    {
        _tint = tint;
        if (_renderer != null)
            _renderer.color = new Color(tint.r, tint.g, tint.b, tint.a * (_source != null ? _source.color.a : 0f));
    }

    private void LateUpdate() => SyncNow();

    public void SyncNow()
    {
        if (_source == null || _renderer == null)
        {
            if (_renderer != null) _renderer.enabled = false;
            return;
        }
        _renderer.sprite = _source.sprite;
        _renderer.flipX = _source.flipX;
        _renderer.flipY = _source.flipY;
        _renderer.drawMode = _source.drawMode;
        _renderer.size = _source.size;
        _renderer.tileMode = _source.tileMode;
        _renderer.adaptiveModeThreshold = _source.adaptiveModeThreshold;
        _renderer.spriteSortPoint = _source.spriteSortPoint;
        _renderer.maskInteraction = _source.maskInteraction;
        _renderer.sortingLayerID = _source.sortingLayerID;
        _renderer.sortingOrder = Mathf.Min(32767, _source.sortingOrder + 1);
        _renderer.renderingLayerMask = _source.renderingLayerMask;
        _renderer.forceRenderingOff = _source.forceRenderingOff;
        _renderer.enabled = _source.enabled && _source.sprite != null && _source.gameObject.activeInHierarchy;
        gameObject.layer = _source.gameObject.layer;
        SetTint(_tint);
    }

    public void Unbind(Transform parkingParent)
    {
        _source = null;
        if (_renderer != null)
        {
            _renderer.enabled = false;
            _renderer.sprite = null;
            _renderer.sharedMaterial = null;
        }
        // 배우의 OnDisable/OnDestroy 안에서는 자식의 부모를 바꿀 수 없습니다.
        // 정확한 원본 pose를 유지한 채 휴면시키고, 다음 정상 Bind에서만 재부모합니다.
        // 배우와 함께 삭제된 경우 표시 소유자의 Unity null 검사로 새 뷰를 만듭니다.
        gameObject.SetActive(false);
    }
}
