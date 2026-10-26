using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>턴 큐 왼쪽의 작은 보스 HP 표시. HP 이벤트만 관찰하며 전투를 제어하지 않습니다.</summary>
public sealed class BattleBossProgressPresenter : MonoBehaviour
{
    private readonly List<EnemyCharacter> _actors = new List<EnemyCharacter>();
    private readonly List<Image> _markers = new List<Image>(8);
    private RectTransform _root;
    private TMP_Text _name;
    private TMP_Text _value;
    private Image _fill;
    private EnemyCharacter _actor;
    private Tween _tween;
    private float _displayed;

    public void Bind(RectTransform host, IReadOnlyList<EnemyCharacter> enemies, TMP_FontAsset font)
    {
        Clear();
        if (host == null) return;
        EnsureView(host, font);
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyCharacter actor = enemies[i];
            if (!Eligible(actor) || _actors.Contains(actor)) continue;
            _actors.Add(actor);
            actor.OnHPChanged += HandleHp;
        }
        LocalizationManager.LanguageChanged += HandleLanguage;
        SelectFirst();
    }

    public void Prefer(EnemyCharacter actor)
    {
        if (actor == _actor || !Eligible(actor) || !_actors.Contains(actor)) return;
        Select(actor);
    }

    private static bool Eligible(EnemyCharacter actor) => actor != null && actor.Data != null
        && actor.Data.UsesHealthProgress && actor.IsAlive && actor.isActiveAndEnabled;

    private void SelectFirst()
    {
        for (int i = 0; i < _actors.Count; i++)
            if (Eligible(_actors[i])) { Select(_actors[i]); return; }
        _actor = null;
        _tween?.Kill(false);
        _tween = null;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private void Select(EnemyCharacter actor)
    {
        _tween?.Kill(false);
        _tween = null;
        _actor = actor;
        float[] markers = BattleBossProgressPolicy.NormalizeMarkers(actor.Data.HealthProgressMarkers);
        while (_markers.Count < markers.Length)
            _markers.Add(Image("Health interval", _root, new Vector2(1f, 7f), Vector2.zero, Color.white));
        for (int i = 0; i < _markers.Count; i++)
        {
            Image marker = _markers[i];
            if (marker == null) continue;
            marker.gameObject.SetActive(i < markers.Length);
            if (i < markers.Length) marker.rectTransform.anchoredPosition = new Vector2(5f + 160f * markers[i], -22f);
        }
        _root.gameObject.SetActive(true);
        Refresh(false);
    }

    private void HandleHp(CharacterBase actor, int current, int maximum)
    {
        if (!Eligible(_actor)) { SelectFirst(); return; }
        if (actor == _actor) Refresh(true);
    }

    private void HandleLanguage(LanguageType language) => Refresh(false);

    private void Refresh(bool animate)
    {
        if (!Eligible(_actor) || _root == null) return;
        float ratio = BattleBossProgressPolicy.Ratio(_actor.CurrentHP, _actor.MaxHP);
        _name.text = _actor.Data.LocalizedName;
        _value.SetText("{0}/{1}  ({2}%)", _actor.CurrentHP, _actor.MaxHP, Mathf.CeilToInt(ratio * 100f));
        _tween?.Kill(false);
        _tween = null;
        if (!animate || !Application.isPlaying) { SetRatio(ratio); return; }
        _tween = DOTween.To(() => _displayed, SetRatio, ratio, .14f)
            .SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(false)
            .SetLink(_root.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void SetRatio(float value)
    {
        _displayed = value;
        if (_fill != null) _fill.rectTransform.sizeDelta = new Vector2(160f * value, 3f);
    }

    private void LateUpdate()
    {
        // 이벤트 연출이 HP 변경 없이 적을 퇴장시켜도 이전 보스 표시를 남기지 않습니다.
        if (_root != null && _root.gameObject.activeSelf && !Eligible(_actor)) SelectFirst();
    }

    public void Clear()
    {
        _tween?.Kill(false);
        _tween = null;
        for (int i = 0; i < _actors.Count; i++)
            if (_actors[i] != null) _actors[i].OnHPChanged -= HandleHp;
        _actors.Clear();
        LocalizationManager.LanguageChanged -= HandleLanguage;
        _actor = null;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private void EnsureView(RectTransform host, TMP_FontAsset font)
    {
        if (_root != null) return;
        _root = Rect("Boss health progress", host, new Vector2(170f, 32f), new Vector2(8f, -8f));
        Image("Background", _root, new Vector2(170f, 32f), Vector2.zero, new Color(0f, 0f, 0f, .65f));
        _name = Text("Name", font, new Vector2(160f, 12f), new Vector2(5f, -2f), 10f);
        _value = Text("Health", font, new Vector2(160f, 10f), new Vector2(5f, -13f), 8f);
        Image("Empty", _root, new Vector2(160f, 3f), new Vector2(5f, -25f), new Color(.25f, .23f, .3f));
        _fill = Image("Remaining", _root, new Vector2(160f, 3f), new Vector2(5f, -25f), new Color(1f, .85f, .35f));
    }

    private TMP_Text Text(string name, TMP_FontAsset font, Vector2 size, Vector2 position, float pixels)
    {
        RectTransform rect = Rect(name, _root, size, position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = pixels;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Image Image(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
    {
        Image image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, RectTransform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        Clear();
        if (_root == null) return;
        if (Application.isPlaying) Destroy(_root.gameObject);
        else DestroyImmediate(_root.gameObject);
    }
}
