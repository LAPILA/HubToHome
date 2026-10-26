using UnityEngine;

/// <summary>반복 Z가 막혔을 때만 쓰는 작은 월드 게이지. 소유 PlayerController가 갱신/정리합니다.</summary>
public sealed class BattleParryRearmIndicator
{
    private GameObject _root;
    private SpriteRenderer _background, _fill;
    private Sprite _pixel;
    private float _readyUntil;
    private bool _wasRecovering;

    public void Refresh(Transform owner, SpriteRenderer actor, float remaining, float interval)
    {
        if (owner == null || actor == null) { Hide(); return; }
        if (remaining > 0f) { _wasRecovering = true; _readyUntil = 0f; }
        else if (_wasRecovering) { _wasRecovering = false; _readyUntil = Time.unscaledTime + 0.12f; }
        if (remaining <= 0f && Time.unscaledTime >= _readyUntil) { Hide(); return; }
        Ensure(owner);
        _root.SetActive(true);
        float ppu = actor.sprite != null ? Mathf.Max(1f, actor.sprite.pixelsPerUnit) : 32f;
        _root.transform.position = new Vector3(actor.bounds.min.x - 9f / ppu, actor.bounds.center.y, owner.position.z);
        _root.transform.rotation = Quaternion.identity;
        Vector3 parentScale = owner.lossyScale;
        _root.transform.localScale = new Vector3(SafeInverse(parentScale.x), SafeInverse(parentScale.y), 1f);
        _background.sortingLayerID = _fill.sortingLayerID = actor.sortingLayerID;
        _background.sortingOrder = Mathf.Min(32765, actor.sortingOrder + 2);
        _fill.sortingOrder = _background.sortingOrder + 1;
        _background.transform.localScale = new Vector3(7f / ppu, 3f / ppu, 1f);
        float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, interval));
        _fill.transform.localPosition = new Vector3(1f / ppu, 0f, 0f);
        _fill.transform.localScale = new Vector3(Mathf.Round(5f * progress) / ppu, 1f / ppu, 1f);
        _fill.color = remaining > 0f ? new Color(0.65f, 0.68f, 0.75f) : new Color(1f, 0.88f, 0.25f);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
        _wasRecovering = false;
        _readyUntil = 0f;
    }

    public void Dispose()
    {
        if (_root != null) Object.Destroy(_root);
        if (_pixel != null) Object.Destroy(_pixel);
        _root = null;
        _pixel = null;
    }

    private void Ensure(Transform owner)
    {
        if (_root != null) return;
        _root = new GameObject("Parry Rearm");
        _root.transform.SetParent(owner, false);
        _pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        _pixel.name = "Parry Rearm Pixel (Owned)";
        _pixel.hideFlags = HideFlags.DontSave;
        _background = Create("Background");
        _background.color = Color.black;
        _fill = Create("Ready");
    }

    private SpriteRenderer Create(string name)
    {
        var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        renderer.transform.SetParent(_root.transform, false);
        renderer.sprite = _pixel;
        return renderer;
    }

    private static float SafeInverse(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;
}
